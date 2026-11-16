using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.IntegrationTests
{
    [TestClass]
    public class TransactionTests
    {
        // A user error number that stands in for an error the repository cannot classify.
        private const int UnclassifiedErrorNumber = 50001;

        private string _prefix;

        [TestInitialize]
        public async Task SeedAsync()
        {
            _prefix = $"tx-{Guid.NewGuid():N}-";
            await using BloggingContext ctx = SqlServerFixture.CreateContext();
            ctx.Blogs.AddRange(new Blog { Url = _prefix + "a" }, new Blog { Url = _prefix + "b" });
            await ctx.SaveChangesAsync();
        }

        [TestCleanup]
        public async Task CleanupAsync()
        {
            await using BloggingContext ctx = SqlServerFixture.CreateContext();
            await ctx.Blogs.Where(x => x.Url.StartsWith(_prefix)).ExecuteDeleteAsync();
        }

        [TestMethod]
        public async Task CommitAsync_ShouldCommitExecuteUpdateAndExecuteDelete()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory());

            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
            {
                await WriteAsync(transaction);
                await transaction.CommitAsync();
            }

            CollectionAssert.AreEqual(new[] { _prefix + "a-moved", _prefix + "c" }, await GetUrlsAsync());
        }

        [TestMethod]
        public async Task DisposeAsync_WithoutCommit_ShouldRollBackExecuteUpdateAndExecuteDelete()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory());

            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
                await WriteAsync(transaction);

            CollectionAssert.AreEqual(new[] { _prefix + "a", _prefix + "b" }, await GetUrlsAsync());
        }

        [TestMethod]
        public async Task BeginTransactionAsync_EveryRepository_ShouldRunOnOneConnectionAndTransaction()
        {
            CommandRecorder recorder = new();
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory(recorder));

            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
            {
                await transaction.AcquireLockAsync(_prefix);
                await WriteAsync(transaction);
                await transaction.CommitAsync();
            }

            Assert.IsGreaterThanOrEqualTo(4, recorder.Commands.Count);
            Assert.HasCount(1, recorder.Commands.Select(x => x.Connection).Distinct());
            Assert.HasCount(1, recorder.Commands.Select(x => x.Transaction).Distinct());
            Assert.IsNotNull(recorder.Commands.First().Transaction);
        }

        [TestMethod]
        public async Task BeginTransactionAsync_RetryingExecutionStrategy_ShouldThrow()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory(retryOnFailure: true));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => factory.BeginTransactionAsync());
        }

        [TestMethod]
        public async Task AcquireLockAsync_SameKey_ShouldRunTheSecondTransactionAfterTheFirstEnds()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory());
            ConcurrentQueue<string> events = new();
            TaskCompletionSource firstHoldsLock = new(TaskCreationOptions.RunContinuationsAsynchronously);

            async Task FirstAsync()
            {
                await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
                await transaction.AcquireLockAsync(_prefix);
                firstHoldsLock.SetResult();
                await Task.Delay(TimeSpan.FromSeconds(1));
                events.Enqueue("first done");
                await transaction.CommitAsync();
            }

            async Task SecondAsync()
            {
                await firstHoldsLock.Task;
                await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
                await transaction.AcquireLockAsync(_prefix);
                events.Enqueue("second has lock");
                await transaction.CommitAsync();
            }

            await Task.WhenAll(FirstAsync(), SecondAsync());

            CollectionAssert.AreEqual(new[] { "first done", "second has lock" }, events.ToArray());
        }

        [TestMethod]
        public async Task AcquireLockAsync_HeldElsewhere_ShouldThrowAfterTheTimeout()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory());

            await using IRepositoryTransaction first = await factory.BeginTransactionAsync();
            await first.AcquireLockAsync(_prefix);

            await using IRepositoryTransaction second = await factory.BeginTransactionAsync();
            await Assert.ThrowsExactlyAsync<ConcurrencyException>(() => second.AcquireLockAsync(_prefix, TimeSpan.FromMilliseconds(200)));
        }

        [TestMethod]
        public async Task DisposeAsync_ShouldReturnTheConnection()
        {
            ConnectionCounter connections = new();
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory(connections: connections));

            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
                await WriteAsync(transaction);

            Assert.IsGreaterThan(0, connections.Opened);
            Assert.AreEqual(connections.Opened, connections.Closed);
        }

        [TestMethod]
        public async Task CreateAsync_ForeignKeyViolation_ShouldThrowConstraintViolationInsideAndOutsideATransaction()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory());

            using (IRepository<Post> repo = factory.Create<Post>())
                await Assert.ThrowsExactlyAsync<ConstraintViolationException>(() => repo.CreateAsync(new Post { BlogId = int.MaxValue, Title = _prefix }));

            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
            {
                await WriteAsync(transaction);
                await Assert.ThrowsExactlyAsync<ConstraintViolationException>(() => transaction.Create<Post>().CreateAsync(new Post { BlogId = int.MaxValue, Title = _prefix }));
            }

            CollectionAssert.AreEqual(new[] { _prefix + "a", _prefix + "b" }, await GetUrlsAsync());
        }

        [TestMethod]
        public async Task UpdateAsync_SetBasedForeignKeyViolation_ShouldThrowConstraintViolationInsideAndOutsideATransaction()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory());
            await using (BloggingContext ctx = SqlServerFixture.CreateContext())
            {
                ctx.Posts.Add(new Post { BlogId = (await ctx.Blogs.FirstAsync(x => x.Url == _prefix + "a")).BlogId, Title = _prefix });
                await ctx.SaveChangesAsync();
            }

            using (IRepository<Post> repo = factory.Create<Post>())
                await Assert.ThrowsExactlyAsync<ConstraintViolationException>(() => repo.UpdateAsync(x => x.Title == _prefix, x => x.BlogId, int.MaxValue));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            await Assert.ThrowsExactlyAsync<ConstraintViolationException>(() => transaction.Create<Post>().UpdateAsync(x => x.Title == _prefix, x => x.BlogId, int.MaxValue));
        }

        [TestMethod]
        public async Task CreateAsync_UniqueIndexViolationInATransaction_ShouldThrowConstraintViolation()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory());
            await using BloggingContext ctx = SqlServerFixture.CreateContext();
            int blogId = (await ctx.Blogs.FirstAsync(x => x.Url == _prefix + "a")).BlogId;

            // Allows one post for this blog only, so the second insert violates the index; DDL takes no parameters.
            string createIndex = $"CREATE UNIQUE INDEX [IX_Posts_OnePostPerTestBlog] ON [Posts] ([BlogId]) WHERE [BlogId] = {blogId}";
            await ctx.Database.ExecuteSqlRawAsync(createIndex);

            try
            {
                await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
                IRepository<Post> posts = transaction.Create<Post>();
                await posts.CreateAsync(new Post { BlogId = blogId, Title = _prefix });
                await Assert.ThrowsExactlyAsync<ConstraintViolationException>(() => posts.CreateAsync(new Post { BlogId = blogId, Title = _prefix }));
            }
            finally
            {
                await ctx.Database.ExecuteSqlRawAsync("DROP INDEX [IX_Posts_OnePostPerTestBlog] ON [Posts]");
            }

            Assert.AreEqual(0, await CountPostsAsync());
        }

        [TestMethod]
        public async Task CreateAsync_UnclassifiedSqlError_ShouldThrowDatabaseAccessException()
        {
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory(failingInsert: new FailingInsert()));

            using IRepository<Blog> repo = factory.Create<Blog>();
            await Assert.ThrowsExactlyAsync<DatabaseAccessException>(() => repo.CreateAsync(new Blog { Url = _prefix + "c" }));
        }

        [TestMethod]
        public async Task FindAllPagedAsync_InATransaction_ShouldRunOnTheSharedConnection()
        {
            CommandRecorder recorder = new();
            EfRepositoryFactory<BloggingContext> factory = new(new ContextFactory(recorder));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            IPage<Blog> page = await transaction.Create<Blog>().FindAllPagedAsync(x => x.Url.StartsWith(_prefix), null, 1, 1);

            Assert.AreEqual(2, page.Total);
            Assert.AreEqual(1, page.Data.Count());
            Assert.HasCount(1, recorder.Commands.Select(x => x.Connection).Distinct());
        }

        private async Task WriteAsync(IRepositoryTransaction transaction)
        {
            IRepository<Blog> repo = transaction.Create<Blog>();
            await repo.CreateAsync(new Blog { Url = _prefix + "c" });
            await repo.UpdateAsync(x => x.Url == _prefix + "a", new PropertyUpdate<Blog>().Set(x => x.Url, _prefix + "a-moved"));
            await repo.DeleteAsync(x => x.Url == _prefix + "b");
        }

        private async Task<int> CountPostsAsync()
        {
            await using BloggingContext ctx = SqlServerFixture.CreateContext();
            return await ctx.Posts.CountAsync(x => x.Title == _prefix);
        }

        private async Task<List<string>> GetUrlsAsync()
        {
            await using BloggingContext ctx = SqlServerFixture.CreateContext();
            return await ctx.Blogs.Where(x => x.Url.StartsWith(_prefix)).OrderBy(x => x.Url).Select(x => x.Url).ToListAsync();
        }

        private sealed class ContextFactory(CommandRecorder recorder = null, bool retryOnFailure = false, ConnectionCounter connections = null, FailingInsert failingInsert = null) : IDbContextFactory<BloggingContext>
        {
            public BloggingContext CreateDbContext()
            {
                DbContextOptionsBuilder<BloggingContext> builder = new DbContextOptionsBuilder<BloggingContext>()
                    .UseSqlServer(SqlServerFixture.ConnectionString, options =>
                    {
                        if (retryOnFailure)
                            options.EnableRetryOnFailure();
                    });

                if (recorder != null)
                    builder.AddInterceptors(recorder);

                if (connections != null)
                    builder.AddInterceptors(connections);

                if (failingInsert != null)
                    builder.AddInterceptors(failingInsert);

                return new BloggingContext(builder.Options);
            }
        }

        private sealed class ConnectionCounter : DbConnectionInterceptor
        {
            private int _opened;
            private int _closed;

            public int Opened => _opened;

            public int Closed => _closed;

            public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _opened);
                return Task.CompletedTask;
            }

            public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
                => Interlocked.Increment(ref _opened);

            public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
            {
                Interlocked.Increment(ref _closed);
                return Task.CompletedTask;
            }

            public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
                => Interlocked.Increment(ref _closed);
        }

        /// <summary>
        /// Makes SQL Server fail the first INSERT with an error the repository cannot classify.
        /// </summary>
        private sealed class FailingInsert : DbCommandInterceptor
        {
            private int _failures = 1;

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                if (command.CommandText.Contains("INSERT", StringComparison.OrdinalIgnoreCase) && Interlocked.Decrement(ref _failures) >= 0)
                    command.CommandText = $"THROW {UnclassifiedErrorNumber}, 'A simulated failure.', 1;";

                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }

        private sealed class CommandRecorder : DbCommandInterceptor
        {
            public ConcurrentQueue<(DbConnection Connection, DbTransaction Transaction)> Commands { get; } = new();

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
            {
                Commands.Enqueue((command.Connection, command.Transaction));
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }

            public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Commands.Enqueue((command.Connection, command.Transaction));
                return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
            }
        }
    }
}
