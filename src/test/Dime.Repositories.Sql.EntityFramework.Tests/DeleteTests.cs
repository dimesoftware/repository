using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class DeleteTests
    {
        [TestMethod]
        public void Delete_ByEntity_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            repo.Delete(new Blog { BlogId = 1 });

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(2, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_ById_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            repo.Delete(id: 1);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(2, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_ById_NotFound_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            repo.Delete(id: 999);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_ById_CommitFalse_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Delete(id: 1, commit: false);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_ById_CommitTrue_ShouldPersist()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Delete(id: 1, commit: true);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(2, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_ById_NotFound_CommitFalse_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Delete(id: 999, commit: false);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_Entity_CommitFalse_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Delete(new Blog { BlogId = 1 }, commit: false);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_Collection_ShouldRemoveAll()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            List<Blog> blogs = new() { new() { BlogId = 1 }, new() { BlogId = 2 } };
            repo.Delete(blogs);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(1, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_Collection_Empty_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Delete(new List<Blog>());

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public void Delete_Where_ShouldRemoveMatches()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            repo.Delete(b => b.Url.Contains("cat"));

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(1, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_ById_NotFound_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.DeleteAsync(id: 999);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_ById_CommitFalse_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.DeleteAsync(id: 1, commit: false);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_ById_CommitTrue_ShouldPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.DeleteAsync(id: 1, commit: true);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(2, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_Entity_CommitFalse_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.DeleteAsync(new Blog { BlogId = 1 }, commit: false);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_Collection_ShouldRemoveAll()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            List<Blog> blogs = new() { new() { BlogId = 1 }, new() { BlogId = 2 } };
            await repo.DeleteAsync(blogs);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(1, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_Collection_Empty_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.DeleteAsync(new List<Blog>());

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(3, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_Where_ShouldRemoveMatches()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.DeleteAsync(b => b.Url.Contains("cat"));

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(1, context.Blogs.Count());
        }
    }
}