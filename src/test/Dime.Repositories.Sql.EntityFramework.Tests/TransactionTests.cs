using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class TransactionTests
    {
        [TestMethod]
        public async Task CommitAsync_ShouldCommitEveryWrite()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
            {
                IRepository<Blog> repo = transaction.Create<Blog>();
                await repo.CreateAsync(new Blog { Url = "http://sample.com/zebras" });
                await repo.UpdateAsync(x => x.BlogId == 1, new PropertyUpdate<Blog>().Set(x => x.Url, "http://sample.com/lions"));
                await repo.DeleteAsync(x => x.BlogId == 3);
                await transaction.CommitAsync();
            }

            CollectionAssert.AreEqual(new[] { "http://sample.com/lions", "http://sample.com/catfish", "http://sample.com/zebras" }, await GetUrlsAsync(testDb));
        }

        [TestMethod]
        public async Task DisposeAsync_WithoutCommit_ShouldRollBackEveryWrite()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
            {
                IRepository<Blog> repo = transaction.Create<Blog>();
                await repo.CreateAsync(new Blog { Url = "http://sample.com/zebras" });
                await repo.UpdateAsync(x => x.BlogId == 1, new PropertyUpdate<Blog>().Set(x => x.Url, "http://sample.com/lions"));
                await repo.DeleteAsync(x => x.BlogId == 3);
            }

            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs" }, await GetUrlsAsync(testDb));
        }

        [TestMethod]
        public async Task RollbackAsync_ShouldDiscardEveryWriteAndCompleteTheTransaction()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            IRepository<Blog> repo = transaction.Create<Blog>();
            await repo.DeleteAsync(x => x.BlogId == 3);
            await transaction.RollbackAsync();

            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs" }, await GetUrlsAsync(testDb));
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => transaction.CommitAsync());
        }

        [TestMethod]
        public async Task Create_SecondRepository_ShouldSeeUncommittedWritesOfTheFirst()
        {
            using TestDatabase testDb = new(seedPosts: true);
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            IRepository<Post> postRepo = transaction.Create<Post>();
            await postRepo.UpdateAsync(x => x.BlogId == 2, new PropertyUpdate<Post>().Set(x => x.BlogId, 1));

            IRepository<Post> readRepo = transaction.Create<Post>();
            Assert.AreEqual(3, await readRepo.CountAsync(x => x.BlogId == 1));
        }

        [TestMethod]
        public async Task Create_ShouldUseTheOverriddenRepositoryType()
        {
            using TestDatabase testDb = new();
            CustomRepositoryFactory factory = new(new TestContextFactory(testDb.Options));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            Assert.IsInstanceOfType<CustomRepository<Blog>>(transaction.Create<Blog>());
        }

        [TestMethod]
        public async Task AcquireLockAsync_NotSqlServer_ShouldThrowNotSupported()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            await Assert.ThrowsExactlyAsync<NotSupportedException>(() => transaction.AcquireLockAsync("blog:1"));
        }

        [TestMethod]
        public async Task Repository_UsedAfterTheTransaction_ShouldThrowButDisposeQuietly()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            IRepository<Blog> leaked;
            await using (IRepositoryTransaction transaction = await factory.BeginTransactionAsync())
            {
                leaked = transaction.Create<Blog>();
                await transaction.CommitAsync();
            }

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => leaked.FindAllAsync(x => true));
            leaked.Dispose();
        }

        [TestMethod]
        public async Task CreateAsync_UnclassifiedSaveErrorOutsideATransaction_ShouldThrowDatabaseAccessException()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(new DbContextOptionsBuilder<BloggingContext>()
                .UseSqlite(testDb.Connection)
                .AddInterceptors(new FailingSaveInterceptor(failures: 1))
                .Options));

            using IRepository<Blog> repo = factory.Create<Blog>();
            await Assert.ThrowsExactlyAsync<DatabaseAccessException>(() => repo.CreateAsync(new Blog { Url = "http://sample.com/zebras" }));
        }

        [TestMethod]
        public async Task CreateAsync_UnclassifiedSaveErrorInBeginTransaction_ShouldThrowDatabaseAccessException()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(new DbContextOptionsBuilder<BloggingContext>()
                .UseSqlite(testDb.Connection)
                .AddInterceptors(new FailingSaveInterceptor(failures: 1))
                .Options));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            await Assert.ThrowsExactlyAsync<DatabaseAccessException>(() => transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" }));
        }

        [TestMethod]
        public async Task BeginTransactionAsync_RetryingExecutionStrategy_ShouldThrow()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(RetryingOptions(testDb)));

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => factory.BeginTransactionAsync());
            StringAssert.Contains(exception.Message, "without retries");
        }

        [TestMethod]
        public async Task Create_AfterCommit_ShouldThrow()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            await transaction.CommitAsync();

            InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => transaction.Create<Blog>());
            StringAssert.Contains(exception.Message, "already completed");
        }

        [TestMethod]
        public async Task AcquireLockAsync_AfterCommit_ShouldThrowInvalidOperation()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            await transaction.CommitAsync();

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => transaction.AcquireLockAsync("blog:1"));
        }

        [TestMethod]
        public async Task FindAllPagedAsync_InATransaction_ShouldRunBothQueriesOnOneContext()
        {
            using TestDatabase testDb = new();
            TrackingContextFactory contextFactory = new(testDb.Options);
            EfRepositoryFactory<BloggingContext> factory = new(contextFactory);

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            int before = contextFactory.Contexts.Count;
            IPage<Blog> page = await transaction.Create<Blog>().FindAllPagedAsync(null, null, 1, 2);

            Assert.AreEqual(1, contextFactory.Contexts.Count - before);
            Assert.AreEqual(3, page.Total);
            Assert.AreEqual(2, page.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_InATransactionWithAWrappedContextFactory_ShouldRunBothQueriesOnOneContext()
        {
            using TestDatabase testDb = new();
            TrackingContextFactory contextFactory = new(testDb.Options);
            WrappingRepositoryFactory factory = new(contextFactory);

            await using IRepositoryTransaction transaction = await factory.BeginTransactionAsync();
            int before = contextFactory.Contexts.Count;
            await transaction.Create<Blog>().FindAllPagedAsync(null, null, 1, 2);

            Assert.AreEqual(1, contextFactory.Contexts.Count - before);
        }

        private static DbContextOptions<BloggingContext> RetryingOptions(TestDatabase testDb)
            => new DbContextOptionsBuilder<BloggingContext>()
                .UseSqlite(testDb.Connection, options => options.ExecutionStrategy(dependencies => new RetryingExecutionStrategy(dependencies)))
                .Options;

        private static async Task<List<string>> GetUrlsAsync(TestDatabase testDb)
        {
            await using BloggingContext context = new(testDb.Options);
            return await context.Blogs.OrderBy(x => x.BlogId).Select(x => x.Url).ToListAsync();
        }

        private sealed class TransientException(Exception innerException = null) : Exception("A simulated transient failure.", innerException);

        /// <summary>
        /// Fails the first saves the way a provider reports an error it wraps, which the repository cannot classify.
        /// </summary>
        private sealed class FailingSaveInterceptor(int failures) : SaveChangesInterceptor
        {
            private int _failures = failures;

            public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
            {
                ThrowWhileFailing();
                return result;
            }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                ThrowWhileFailing();
                return ValueTask.FromResult(result);
            }

            private void ThrowWhileFailing()
            {
                if (_failures-- > 0)
                    throw new DbUpdateException("A simulated transient failure.", new TransientException(new IOException()));
            }
        }

        private sealed class RetryingExecutionStrategy(ExecutionStrategyDependencies dependencies)
            : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
        {
            protected override bool ShouldRetryOn(Exception exception) => exception is TransientException;
        }

        private sealed class TrackingContextFactory(DbContextOptions<BloggingContext> options) : IDbContextFactory<BloggingContext>
        {
            public List<BloggingContext> Contexts { get; } = [];

            public BloggingContext CreateDbContext()
            {
                BloggingContext context = new(options);
                Contexts.Add(context);
                return context;
            }
        }

        private sealed class CustomRepository<TEntity>(IDbContextFactory<BloggingContext> contextFactory, RepositoryConfiguration configuration)
            : EfRepository<TEntity, BloggingContext>(contextFactory, configuration)
            where TEntity : class, new();

        private sealed class CustomRepositoryFactory(IDbContextFactory<BloggingContext> contextFactory) : EfRepositoryFactory<BloggingContext>(contextFactory)
        {
            protected override IRepository<TEntity> CreateRepository<TEntity>(IDbContextFactory<BloggingContext> contextFactory, RepositoryConfiguration opts)
                => new CustomRepository<TEntity>(contextFactory, opts);
        }

        /// <summary>
        /// Wraps the context factory it is handed, the way a factory that adds logging or tenant filters does.
        /// </summary>
        private sealed class WrappingRepositoryFactory(IDbContextFactory<BloggingContext> contextFactory) : EfRepositoryFactory<BloggingContext>(contextFactory)
        {
            protected override IRepository<TEntity> CreateRepository<TEntity>(IDbContextFactory<BloggingContext> contextFactory, RepositoryConfiguration opts)
                => new EfRepository<TEntity, BloggingContext>(new DelegatingContextFactory(contextFactory), opts);
        }

        private sealed class DelegatingContextFactory(IDbContextFactory<BloggingContext> inner) : IDbContextFactory<BloggingContext>
        {
            public BloggingContext CreateDbContext() => inner.CreateDbContext();
        }
    }
}
