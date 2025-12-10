using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class UpdateAsyncTests
    {
        [TestMethod]
        public async Task UpdateAsync_ByEntity_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            await repo.UpdateAsync(new Blog { BlogId = 1, Url = "http://sample.com/zebras" });

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog = await context.Blogs.FindAsync(1);
            Assert.IsTrue(blog.Url == "http://sample.com/zebras");
        }

        [TestMethod]
        public async Task UpdateAsync_Collection_ShouldUpdateAll()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            var post = new Post() { PostId = 1 };
            await repo.UpdateAsync(new Blog { BlogId = 1, Url = "http://sample.com/zebras", Posts = [post] });
            await repo.UpdateAsync(new Blog { BlogId = 2, Url = "http://sample.com/lions", Posts = [post] });

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Assert.IsTrue(blog1.Url == "http://sample.com/zebras");

            Blog blog2 = await context.Blogs.FindAsync(2);
            Assert.IsTrue(blog2.Url == "http://sample.com/lions");
        }
        [TestMethod]
        public async Task UpdateAsyncBatch_Collection_ShouldUpdateAll()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            var post = new Post() { PostId = 1 };
            await repo.UpdateAsync([new Blog { BlogId = 1, Url = "http://sample.com/zebras", Posts = [post] }, new Blog { BlogId = 2, Url = "http://sample.com/lions", Posts = [post] }]);
            
            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Assert.IsTrue(blog1.Url == "http://sample.com/zebras");

            Blog blog2 = await context.Blogs.FindAsync(2);
            Assert.IsTrue(blog2.Url == "http://sample.com/lions");
        }

        [TestMethod]
        public async Task UpdateAsync_WithExecuteUpdate_ConstantValue_ShouldUpdateMatchingEntities()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            
            // Update blogs with BlogId 1 and 2 (the ones with "cat" in URL)
            int updated = await repo.UpdateAsync(
                b => b.BlogId == 1 || b.BlogId == 2,
                b => b.Url,
                "http://sample.com/updated");

            // Verify the number of entities updated
            Assert.AreEqual(2, updated);

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Blog blog2 = await context.Blogs.FindAsync(2);
            Blog blog3 = await context.Blogs.FindAsync(3);

            Assert.AreEqual("http://sample.com/updated", blog1.Url);
            Assert.AreEqual("http://sample.com/updated", blog2.Url);
            Assert.AreEqual("http://sample.com/dogs", blog3.Url); // Should not be updated
        }

        [TestMethod]
        public async Task UpdateAsync_WithExecuteUpdate_ExpressionValue_ShouldUpdateMatchingEntities()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            
            // Update all blogs to have a modified URL using an expression
            int updated = await repo.UpdateAsync(
                b => b.BlogId <= 2,
                b => b.Url,
                b => b.Url + "/modified");

            // Verify the number of entities updated
            Assert.AreEqual(2, updated);

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Blog blog2 = await context.Blogs.FindAsync(2);
            Blog blog3 = await context.Blogs.FindAsync(3);

            Assert.IsTrue(blog1.Url.EndsWith("/modified"));
            Assert.IsTrue(blog2.Url.EndsWith("/modified"));
            Assert.AreEqual("http://sample.com/dogs", blog3.Url); // Should not be updated
        }

        [TestMethod]
        public async Task UpdateAsync_WithExecuteUpdate_NoMatchingEntities_ShouldReturnZero()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            
            // Try to update blogs that don't exist
            int updated = await repo.UpdateAsync(
                b => b.BlogId > 100,
                b => b.Url,
                "http://sample.com/updated");

            // Verify no entities were updated
            Assert.AreEqual(0, updated);

            // Verify no data was changed
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Assert.AreEqual("http://sample.com/cats", blog1.Url);
        }

        [TestMethod]
        public async Task UpdateAsync_WithExecuteUpdate_AllEntities_ShouldUpdateAll()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            
            // Update all blogs
            int updated = await repo.UpdateAsync(
                b => true,
                b => b.Url,
                "http://sample.com/all-updated");

            // Verify all entities were updated
            Assert.AreEqual(3, updated);

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Blog blog2 = await context.Blogs.FindAsync(2);
            Blog blog3 = await context.Blogs.FindAsync(3);

            Assert.AreEqual("http://sample.com/all-updated", blog1.Url);
            Assert.AreEqual("http://sample.com/all-updated", blog2.Url);
            Assert.AreEqual("http://sample.com/all-updated", blog3.Url);
        }
    }
}