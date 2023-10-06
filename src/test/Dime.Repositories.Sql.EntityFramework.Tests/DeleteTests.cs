using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    public partial class RepositoryTests
    {
        [TestMethod]
        public void Delete_ByEntity_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            repo.Delete(new Blog { BlogId = 1 });

            // Use a separate instance of the context to verify correct data was saved to database
            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(2, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_ByEntity_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            await repo.DeleteAsync(new Blog { BlogId = 1 });

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(2, context.Blogs.Count());
        }

        [TestMethod]
        public async Task DeleteAsync_ByIds_ShouldRemoveList()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            List<int> ids = new() { 1, 2 };
            await repo.DeleteAsync(ids);

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual(1, context.Blogs.Count());
        }
    }
}