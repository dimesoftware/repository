using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class CreateTests
    {
        [TestMethod]
        public void Create_ShouldAddOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            repo.Create(new Blog { Url = "http://sample.com" });

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(4, context.Blogs.Count());
            Assert.AreEqual("http://sample.com", context.Blogs.OrderByDescending(x => x.BlogId).First().Url);
        }

        [TestMethod]
        public async Task CreateAsync_ShouldAddOne()
        {
            using TestDatabase testDb = new();

            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));
            await repo.CreateAsync(new Blog { Url = "http://sample.com" });

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(4, context.Blogs.Count());
            Assert.AreEqual("http://sample.com", context.Blogs.OrderByDescending(x => x.BlogId).First().Url);
        }

        [TestMethod]
        public void Create_WithCondition_ShouldAddWhenNoMatch()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Create(new Blog { Url = "http://new.com" }, b => b.Url == "http://new.com");

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(4, context.Blogs.Count());
        }

        [TestMethod]
        public void Create_WithCondition_ShouldSkipWhenMatch()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Create(new Blog { Url = "http://sample.com/cats" }, b => b.Url == "http://sample.com/cats");

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public void Create_WithCondition_NullCondition_ShouldAdd()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Create(new Blog { Url = "http://x.com" }, condition: null);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(4, context.Blogs.Count());
        }

        [TestMethod]
        public void Create_WithBeforeSaveAction_ShouldRunActionBeforeSave()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            bool actionInvoked = false;
            repo.Create(new Blog { Url = "http://before.com" }, (entity, ctx) =>
            {
                actionInvoked = true;
                entity.Url = "http://mutated.com";
                return Task.CompletedTask;
            });

            using BloggingContext context = new(testDb.Options);
            Assert.IsTrue(actionInvoked);
            Assert.AreEqual("http://mutated.com", context.Blogs.OrderByDescending(x => x.BlogId).First().Url);
        }

        [TestMethod]
        public void Create_WithCommitTrue_ShouldPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            repo.Create(new Blog { Url = "http://commit.com" }, commit: true);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(4, context.Blogs.Count());
        }

        [TestMethod]
        public void Create_WithCommitFalse_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            repo.Create(new Blog { Url = "http://no-commit.com" }, commit: false);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public void Create_Queryable_ShouldAddAll()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            var newBlogs = new[]
            {
                new Blog { Url = "http://a.com" },
                new Blog { Url = "http://b.com" }
            }.AsQueryable();

            repo.Create(newBlogs);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(5, context.Blogs.Count());
        }

        [TestMethod]
        public void Create_Queryable_Empty_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Create(System.Linq.Enumerable.Empty<Blog>().AsQueryable());

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task CreateAsync_WithPredicate_ShouldSkipWhenMatch()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.CreateAsync(new Blog { Url = "http://sample.com/cats" }, b => b.Url == "http://sample.com/cats");

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task CreateAsync_WithPredicate_ShouldAddWhenNoMatch()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.CreateAsync(new Blog { Url = "http://new.com" }, b => b.Url == "http://missing.com");

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(4, context.Blogs.Count());
        }

        [TestMethod]
        public async Task CreateAsync_WithBeforeSaveAction_ShouldRunActionBeforeSave()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            bool actionInvoked = false;
            await repo.CreateAsync(new Blog { Url = "http://before.com" }, (entity, ctx) =>
            {
                actionInvoked = true;
                entity.Url = "http://mutated.com";
                return Task.CompletedTask;
            });

            await using BloggingContext context = new(testDb.Options);
            Assert.IsTrue(actionInvoked);
            Assert.AreEqual("http://mutated.com", context.Blogs.OrderByDescending(x => x.BlogId).First().Url);
        }

        [TestMethod]
        public async Task CreateAsync_WithCommitFalse_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.CreateAsync(new Blog { Url = "http://no-commit.com" }, commit: false);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task CreateAsync_Queryable_ShouldAddAll()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            var newBlogs = new[]
            {
                new Blog { Url = "http://x.com" },
                new Blog { Url = "http://y.com" }
            }.AsQueryable();

            await repo.CreateAsync(newBlogs);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(5, context.Blogs.Count());
        }

        [TestMethod]
        public async Task CreateAsync_Queryable_Empty_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.CreateAsync(System.Linq.Enumerable.Empty<Blog>().AsQueryable());

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }
    }
}