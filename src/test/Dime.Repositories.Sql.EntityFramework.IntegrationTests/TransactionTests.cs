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
        public async Task AcquireLockAsync_SameKey_ShouldRunTheSecondUnitAfterTheFirstEnds()
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

        private async Task WriteAsync(IRepositoryTransaction transaction)
        {
            IRepository<Blog> repo = transaction.Create<Blog>();
            await repo.CreateAsync(new Blog { Url = _prefix + "c" });
            await repo.UpdateAsync(x => x.Url == _prefix + "a", new PropertyUpdate<Blog>().Set(x => x.Url, _prefix + "a-moved"));
            await repo.DeleteAsync(x => x.Url == _prefix + "b");
        }

        private async Task<List<string>> GetUrlsAsync()
        {
            await using BloggingContext ctx = SqlServerFixture.CreateContext();
            return await ctx.Blogs.Where(x => x.Url.StartsWith(_prefix)).OrderBy(x => x.Url).Select(x => x.Url).ToListAsync();
        }

        private sealed class ContextFactory(CommandRecorder recorder = null, bool retryOnFailure = false) : IDbContextFactory<BloggingContext>
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

                return new BloggingContext(builder.Options);
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
