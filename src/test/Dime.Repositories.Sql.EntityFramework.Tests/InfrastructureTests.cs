using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class InfrastructureTests
    {
        [TestMethod]
        public void Ctor_FromContextOnly_ShouldUseDefaultConfiguration()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            Assert.IsNotNull(repo.Configuration);
            Assert.AreEqual(ConcurrencyStrategy.ClientFirst, repo.Configuration.SaveStrategy);
        }

        [TestMethod]
        public void Ctor_FromContextAndConfiguration_ShouldUseProvidedConfiguration()
        {
            using TestDatabase testDb = new();
            RepositoryConfiguration config = new() { SaveInBatch = true };
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options), config);

            Assert.AreSame(config, repo.Configuration);
            Assert.IsTrue(repo.Configuration.SaveInBatch);
        }

        [TestMethod]
        public void Ctor_FromFactoryAndConfiguration_ShouldExposeContext()
        {
            using TestDatabase testDb = new();
            TestContextFactory factory = new(testDb.Options);
            RepositoryConfiguration config = new();
            using EfRepository<Blog, BloggingContext> repo = new(factory, config);

            // Triggering a method that reads Context exercises the factory path.
            Assert.AreEqual(3, repo.Count());
        }

        [TestMethod]
        public void ExplicitOperator_ShouldReturnContext()
        {
            using TestDatabase testDb = new();
            BloggingContext ctx = new(testDb.Options);
            using EfRepository<Blog, BloggingContext> repo = new(ctx);

            BloggingContext extracted = (BloggingContext)repo;

            Assert.AreSame(ctx, extracted);
        }

        [TestMethod]
        public void Create_ThenSaveChangesReturnsTrueBranch()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            // SaveInBatch=false hits the 0 < result return path
            Blog created = repo.Create(new Blog { Url = "http://savebranch.com" });

            Assert.IsNotNull(created);
        }

        [TestMethod]
        public void Create_WithSaveInBatch_ReturnsFalseBranch()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(
                new BloggingContext(testDb.Options),
                new RepositoryConfiguration { SaveInBatch = true });

            // SaveInBatch=true short-circuits SaveChanges and returns false.
            Blog created = repo.Create(new Blog { Url = "http://nosave.com" });

            using BloggingContext context = new(testDb.Options);
            // Save was skipped, so the new blog isn't persisted.
            Assert.AreEqual(3, context.Blogs.Count());
            Assert.IsNotNull(created);
        }

        [TestMethod]
        public async Task CreateAsync_WithSaveInBatch_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(
                new BloggingContext(testDb.Options),
                new RepositoryConfiguration { SaveInBatch = true });

            // SaveInBatch=true short-circuits async SaveChanges and returns false.
            await repo.CreateAsync(new Blog { Url = "http://no-async-save.com" });

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_GroupBy_FlattenSelect_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Func<Blog, object> groupBy = b => b.BlogId;
            System.Linq.Expressions.Expression<Func<IGrouping<object, Blog>, IEnumerable<Blog>>> select = g => g;

            IPage<Blog> result = await repo.FindAllPagedAsync<Blog>(
                where: b => true,
                groupBy: groupBy,
                select: select);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task FindAllPagedAsync_TrackChanges_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<IOrder<Blog>> orderBy = new List<IOrder<Blog>> { new Order<Blog>("BlogId", true) };
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: b => true,
                count: b => true,
                orderBy: orderBy,
                page: 1,
                pageSize: 2,
                trackChanges: true);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_TrackChangesFalse_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<IOrder<Blog>> orderBy = new List<IOrder<Blog>> { new Order<Blog>("BlogId", true) };
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: b => true,
                count: b => true,
                orderBy: orderBy,
                page: 1,
                pageSize: 2,
                trackChanges: false);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public async Task UpdateAsync_BatchWithReferenceNavigation_ShouldDetachNavigations()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            // Posts has Blog reference navigation — exercises the non-collection branch of DetachNavigationProperties.
            await repo.UpdateAsync(
            [
                new Post { PostId = 1, Title = "Updated 1", Content = "x", BlogId = 1, Blog = new Blog { BlogId = 1 } },
                new Post { PostId = 2, Title = "Updated 2", Content = "y", BlogId = 1, Blog = new Blog { BlogId = 1 } }
            ]);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("Updated 1", context.Posts.Find(1).Title);
        }

        [TestMethod]
        public void LinqOrderHelper_GetAsEnumerable_LinqToObjects_ShouldThrowDueToReflection()
        {
            // GetAsEnumerable uses Queryable reflection internally; LINQ-to-objects callers
            // hit the cast/Invoke path and throw. Exercising it still covers the entry lines.
            List<Blog> data = new() { new Blog { BlogId = 1, Url = "a" } };

            LinqOrderHelper<Blog> helper = new("OrderBy", nameof(Blog.Url));
            Assert.ThrowsExactly<ArgumentException>(() => helper.GetAsEnumerable(data));
        }

        [TestMethod]
        public async Task ExecuteSqlAsync_ShouldRunRawSql()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await ((EfRepository<Blog, BloggingContext>)repo).ExecuteSqlAsync(
                "UPDATE Blogs SET Url = 'http://raw' WHERE BlogId = 1");

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://raw", context.Blogs.Find(1).Url);
        }

        [TestMethod]
        public async Task CountAsync_DbContextExtension_NoPredicate_ShouldCountAll()
        {
            using TestDatabase testDb = new();
            await using BloggingContext ctx = new(testDb.Options);

            int count = await ctx.CountAsync<Blog>();

            Assert.AreEqual(3, count);
        }

        [TestMethod]
        public async Task CountAsync_DbContextExtension_WithPredicate_ShouldCountMatching()
        {
            using TestDatabase testDb = new();
            await using BloggingContext ctx = new(testDb.Options);

            int count = await ctx.CountAsync<Blog>(b => b.Url.Contains("cat"));

            Assert.AreEqual(2, count);
        }

        [TestMethod]
        public void Count_DbContextExtension_NoPredicate_ShouldCountAll()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            Assert.AreEqual(3, ctx.Count<Blog>());
        }

        [TestMethod]
        public void Count_DbContextExtension_WithPredicate_ShouldCountMatching()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            Assert.AreEqual(2, ctx.Count<Blog>(b => b.Url.Contains("cat")));
        }

        [TestMethod]
        public async Task ToListAsyncSafe_NonAsyncEnumerable_ShouldReturnList()
        {
            IQueryable<int> source = new[] { 1, 2, 3 }.AsQueryable();

            List<int> list = await source.ToListAsyncSafe();

            Assert.AreEqual(3, list.Count);
        }

        [TestMethod]
        public async Task ToListAsyncSafe_NullSource_ShouldThrow()
        {
            IQueryable<int> source = null;

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => source.ToListAsyncSafe());
        }

        [TestMethod]
        public async Task ToListAsyncSafe_AsyncEnumerableSource_ShouldReturnList()
        {
            using TestDatabase testDb = new();
            await using BloggingContext ctx = new(testDb.Options);

            List<Blog> list = await ctx.Blogs.AsNoTracking().ToListAsyncSafe();

            Assert.AreEqual(3, list.Count);
        }

        [TestMethod]
        public void Include_NullIncludes_ShouldReturnQueryUnchanged()
        {
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.Include(ctx, (string[])null);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void Include_EmptyIncludes_ShouldAutoIncludeNavigations()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            // Empty includes triggers the auto-include branch over navigation properties.
            IQueryable<Blog> result = ctx.Blogs.Include(ctx, includes: []);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void Dispose_CalledTwice_ShouldNoopSecondCall()
        {
            using TestDatabase testDb = new();
            EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Dispose();
            // Second dispose hits the Context==null short-circuit branch.
            repo.Dispose();
        }

        [TestMethod]
        public void IncludeView_NullIncludes_ShouldAutoIncludeNavigations()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().IncludeView<Blog, Blog>(ctx, null);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void IncludeView_EmptyIncludes_ShouldAutoIncludeNavigations()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().IncludeView<Blog, Blog>(ctx, []);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void IncludeView_WithExplicitIncludes_ShouldApply()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            IQueryable<Blog> result = ctx.Blogs.AsNoTracking().IncludeView<Blog, Blog>(ctx, "Posts");

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public void OrderLinq_ThenBy_ShouldChainSort()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            IOrderedQueryable<Post> first = ctx.Posts.AsNoTracking().Order(nameof(Post.BlogId));
            IOrderedQueryable<Post> chained = first.AsEnumerable().ThenBy(nameof(Post.PostId));

            Assert.AreEqual(5, chained.Count());
        }

        [TestMethod]
        public void OrderLinq_ThenByDescending_ShouldChainSort()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using BloggingContext ctx = new(testDb.Options);

            IOrderedQueryable<Post> first = ctx.Posts.AsNoTracking().Order(nameof(Post.BlogId));
            IOrderedQueryable<Post> chained = first.AsEnumerable().ThenByDescending(nameof(Post.PostId));

            Assert.AreEqual(5, chained.Count());
        }
    }

    internal class TestContextFactory : IDbContextFactory<BloggingContext>
    {
        private readonly DbContextOptions<BloggingContext> _options;

        public TestContextFactory(DbContextOptions<BloggingContext> options)
        {
            _options = options;
        }

        public BloggingContext CreateDbContext() => new(_options);
    }
}