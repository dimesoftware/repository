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
        public async Task RollbackAsync_ShouldDiscardEveryWriteAndCompleteTheUnit()
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
        public async Task ExecuteInTransactionAsync_ShouldCommitTheWritesOfTwoRepositories()
        {
            using TestDatabase testDb = new(seedPosts: true);
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await factory.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
            {
                await transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" });
                await transaction.Create<Post>().DeleteAsync(x => x.BlogId == 3);
            });

            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs", "http://sample.com/zebras" }, await GetUrlsAsync(testDb));
            Assert.AreEqual(3, await CountPostsAsync(testDb));
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_ShouldReturnTheResultOfTheWork()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            Blog created = await factory.ExecuteInTransactionAsync((transaction, cancellationToken) => transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" }));

            Assert.AreEqual(4, created.BlogId);
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_WorkThrows_ShouldRollBackEveryWriteAndRethrow()
        {
            using TestDatabase testDb = new(seedPosts: true);
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => factory.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
            {
                await transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" });
                await transaction.Create<Post>().DeleteAsync(x => x.BlogId == 3);
                throw new InvalidOperationException();
            }));

            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs" }, await GetUrlsAsync(testDb));
            Assert.AreEqual(5, await CountPostsAsync(testDb));
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_TransientFailure_ShouldReplayTheUnitAndCommitOnce()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(RetryingOptions(testDb)));
            int attempts = 0;

            await factory.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
            {
                attempts++;
                await transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" });
                if (attempts == 1)
                    throw new TransientException();
            });

            Assert.AreEqual(2, attempts);
            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs", "http://sample.com/zebras" }, await GetUrlsAsync(testDb));
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_ReplayedUnit_ShouldDisposeEveryContext()
        {
            using TestDatabase testDb = new();
            TrackingContextFactory contextFactory = new(RetryingOptions(testDb));
            EfRepositoryFactory<BloggingContext> factory = new(contextFactory);
            int attempts = 0;

            await factory.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
            {
                attempts++;
                await transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" });

                // A context the work creates itself and never disposes.
                _ = ((IDbContextFactory<BloggingContext>)transaction).CreateDbContext();
                if (attempts == 1)
                    throw new TransientException();
            });

            Assert.AreEqual(2, attempts);
            Assert.HasCount(7, contextFactory.Contexts);
            Assert.IsTrue(contextFactory.Contexts.All(x => x.IsDisposed));
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_Cancelled_ShouldNotRunTheWork()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));
            bool ran = false;

            await Assert.ThrowsAsync<OperationCanceledException>(() => factory.ExecuteInTransactionAsync((transaction, cancellationToken) =>
            {
                ran = true;
                return Task.CompletedTask;
            }, new CancellationToken(canceled: true)));

            Assert.IsFalse(ran);
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_TransactionUsedAfterTheUnit_ShouldThrow()
        {
            using TestDatabase testDb = new();
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(testDb.Options));

            IRepositoryTransaction leaked = null;
            await factory.ExecuteInTransactionAsync((transaction, cancellationToken) =>
            {
                leaked = transaction;
                return Task.CompletedTask;
            });

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => leaked.Create<Blog>().FindAllAsync(x => true));
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_TransientSaveError_ShouldReplayTheUnitAndCommitOnce()
        {
            using TestDatabase testDb = new();
            FailingSaveInterceptor interceptor = new(failures: 1);
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(RetryingOptions(testDb, interceptor)));
            int attempts = 0;

            await factory.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
            {
                attempts++;
                await transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" });
            });

            Assert.AreEqual(2, attempts);
            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs", "http://sample.com/zebras" }, await GetUrlsAsync(testDb));
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_TransientErrorInSynchronousSave_ShouldReplayTheUnitAndCommitOnce()
        {
            using TestDatabase testDb = new();
            FailingSaveInterceptor interceptor = new(failures: 1);
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(RetryingOptions(testDb, interceptor)));
            int attempts = 0;

            await factory.ExecuteInTransactionAsync((transaction, cancellationToken) =>
            {
                attempts++;
                transaction.Create<Blog>().Create(new Blog { Url = "http://sample.com/zebras" });
                return Task.CompletedTask;
            });

            Assert.AreEqual(2, attempts);
            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs", "http://sample.com/zebras" }, await GetUrlsAsync(testDb));
        }

        [TestMethod]
        public async Task ExecuteInTransactionAsync_SaveKeepsFailing_ShouldThrowRetryLimitExceededAndRollBack()
        {
            using TestDatabase testDb = new();
            FailingSaveInterceptor interceptor = new(failures: int.MaxValue);
            EfRepositoryFactory<BloggingContext> factory = new(new TestContextFactory(RetryingOptions(testDb, interceptor)));

            await Assert.ThrowsExactlyAsync<RetryLimitExceededException>(() => factory.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
            {
                await transaction.Create<Blog>().DeleteAsync(x => x.BlogId == 3);
                await transaction.Create<Blog>().CreateAsync(new Blog { Url = "http://sample.com/zebras" });
            }));

            CollectionAssert.AreEqual(new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs" }, await GetUrlsAsync(testDb));
        }

        [TestMethod]
        public async Task CreateAsync_UnclassifiedSaveErrorOutsideAUnit_ShouldThrowDatabaseAccessException()
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

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => factory.BeginTransactionAsync());
        }

        private static DbContextOptions<BloggingContext> RetryingOptions(TestDatabase testDb, FailingSaveInterceptor interceptor = null)
        {
            DbContextOptionsBuilder<BloggingContext> builder = new DbContextOptionsBuilder<BloggingContext>()
                .UseSqlite(testDb.Connection, options => options.ExecutionStrategy(dependencies => new RetryingExecutionStrategy(dependencies)));

            if (interceptor != null)
                builder.AddInterceptors(interceptor);

            return builder.Options;
        }

        private static async Task<int> CountPostsAsync(TestDatabase testDb)
        {
            await using BloggingContext context = new(testDb.Options);
            return await context.Posts.CountAsync();
        }

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

        private sealed class TrackedBloggingContext(DbContextOptions<BloggingContext> options) : BloggingContext(options)
        {
            public bool IsDisposed { get; private set; }

            public override void Dispose()
            {
                IsDisposed = true;
                base.Dispose();
            }

            public override ValueTask DisposeAsync()
            {
                IsDisposed = true;
                return base.DisposeAsync();
            }
        }

        private sealed class TrackingContextFactory(DbContextOptions<BloggingContext> options) : IDbContextFactory<BloggingContext>
        {
            public List<TrackedBloggingContext> Contexts { get; } = [];

            public BloggingContext CreateDbContext()
            {
                TrackedBloggingContext context = new(options);
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
    }
}
