using System;
using System.Collections.Generic;
using System.Linq;
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
            Assert.AreEqual("http://sample.com/zebras", blog.Url);
        }

        [TestMethod]
        public async Task UpdateAsync_Collection_ShouldUpdateAll()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Post post = new() { PostId = 1 };
            await repo.UpdateAsync(new Blog { BlogId = 1, Url = "http://sample.com/zebras", Posts = [post] });
            await repo.UpdateAsync(new Blog { BlogId = 2, Url = "http://sample.com/lions", Posts = [post] });

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Assert.AreEqual("http://sample.com/zebras", blog1.Url);

            Blog blog2 = await context.Blogs.FindAsync(2);
            Assert.AreEqual("http://sample.com/lions", blog2.Url);
        }

        [TestMethod]
        public async Task UpdateAsyncBatch_Collection_ShouldUpdateAll()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Post post = new() { PostId = 1 };
            await repo.UpdateAsync([new Blog { BlogId = 1, Url = "http://sample.com/zebras", Posts = [post] }, new Blog { BlogId = 2, Url = "http://sample.com/lions", Posts = [post] }]);

            // Use a separate instance of the context to verify correct data was saved to database
            await using BloggingContext context = new(testDb.Options);
            Blog blog1 = await context.Blogs.FindAsync(1);
            Assert.AreEqual("http://sample.com/zebras", blog1.Url);

            Blog blog2 = await context.Blogs.FindAsync(2);
            Assert.AreEqual("http://sample.com/lions", blog2.Url);
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

            Assert.EndsWith("/modified", blog1.Url);
            Assert.EndsWith("/modified", blog2.Url);
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

        [TestMethod]
        public async Task UpdateAsync_WithPropertyUpdate_TwoProperties_ShouldUpdateBothInOneStatement()
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);

            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            int before = testDb.CapturedSql.Count;
            int updated = await repo.UpdateAsync(
                p => p.BlogId == 1,
                new PropertyUpdate<Post>()
                    .Set(p => p.Title, "Archived")
                    .Set(p => p.Content, "Gone"));

            Assert.AreEqual(2, updated);
            Assert.AreEqual(1, testDb.CapturedSql.Skip(before).Count(s => s.Contains("UPDATE")), "Both assignments must go out in a single UPDATE statement.");

            await using BloggingContext context = new(testDb.Options);
            List<Post> posts = [.. context.Posts.OrderBy(p => p.PostId)];
            Assert.IsTrue(posts.Where(p => p.BlogId == 1).All(p => p.Title == "Archived" && p.Content == "Gone"));
            Assert.IsTrue(posts.Where(p => p.BlogId != 1).All(p => p.Title != "Archived" && p.Content != "Gone")); // Should not be updated
        }

        [TestMethod]
        public async Task UpdateAsync_WithPropertyUpdate_ExpressionValue_ShouldUpdateMatchingEntities()
        {
            using TestDatabase testDb = new(seedPosts: true);

            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            int updated = await repo.UpdateAsync(
                p => p.BlogId == 3,
                new PropertyUpdate<Post>()
                    .Set(p => p.Title, p => p.Title + " (edited)")
                    .Set(p => p.Content, p => p.Content + p.Content));

            Assert.AreEqual(2, updated);

            await using BloggingContext context = new(testDb.Options);
            List<Post> dogPosts = [.. context.Posts.Where(p => p.BlogId == 3).OrderBy(p => p.PostId)];
            Assert.AreEqual("Dog post 1 (edited)", dogPosts[0].Title);
            Assert.AreEqual("WoofWoof", dogPosts[0].Content);
            Assert.AreEqual("Dog post 2 (edited)", dogPosts[1].Title);
            Assert.AreEqual("BarkBark", dogPosts[1].Content);
            Assert.AreEqual(0, context.Posts.Count(p => p.BlogId != 3 && p.Title.EndsWith("(edited)"))); // Should not be updated
        }

        [TestMethod]
        public async Task UpdateAsync_WithPropertyUpdate_NoMatchingEntities_ShouldReturnZero()
        {
            using TestDatabase testDb = new(seedPosts: true);

            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            int updated = await repo.UpdateAsync(
                p => p.PostId > 100,
                new PropertyUpdate<Post>().Set(p => p.Title, "x").Set(p => p.Content, "y"));

            Assert.AreEqual(0, updated);
        }

        [TestMethod]
        public async Task UpdateAsync_WithPropertyUpdate_Empty_ShouldThrowArgumentException()
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);

            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            int before = testDb.CapturedSql.Count;
            await Assert.ThrowsExactlyAsync<ArgumentException>(() => repo.UpdateAsync(p => true, new PropertyUpdate<Post>()));
            Assert.AreEqual(before, testDb.CapturedSql.Count, "An empty update must not reach the database.");
        }

        [TestMethod]
        public async Task UpdateAsync_WithPropertyUpdate_Null_ShouldThrowArgumentNullException()
        {
            using TestDatabase testDb = new(seedPosts: true);

            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => repo.UpdateAsync(p => true, (PropertyUpdate<Post>)null));
        }
    }
}