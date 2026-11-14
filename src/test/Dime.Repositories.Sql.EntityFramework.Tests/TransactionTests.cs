using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        private static async Task<List<string>> GetUrlsAsync(TestDatabase testDb)
        {
            await using BloggingContext context = new(testDb.Options);
            return await context.Blogs.OrderBy(x => x.BlogId).Select(x => x.Url).ToListAsync();
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
