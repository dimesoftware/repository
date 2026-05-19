using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class UpdateTests
    {
        [TestMethod]
        public void Update_DefaultCommit_ShouldPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog returned = repo.Update(new Blog { BlogId = 2, Url = "http://sample.com/updated" });

            Assert.IsNotNull(returned);
            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://sample.com/updated", context.Blogs.Find(2).Url);
        }

        [TestMethod]
        public void Update_Collection_ShouldUpdateAll()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            List<Blog> blogs =
            [
                new() { BlogId = 1, Url = "http://x.com" },
                new() { BlogId = 2, Url = "http://y.com" }
            ];
            repo.Update(blogs);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://x.com", context.Blogs.Find(1).Url);
            Assert.AreEqual("http://y.com", context.Blogs.Find(2).Url);
        }

        [TestMethod]
        public void Update_Collection_Empty_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            repo.Update([]);

            using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://sample.com/cats", context.Blogs.Find(1).Url);
        }

        [TestMethod]
        public async Task UpdateAsync_Entity_NoCommit_ShouldNotPersist()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.UpdateAsync(new Blog { BlogId = 1, Url = "http://no-commit.com" }, commitChanges: false);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://sample.com/cats", context.Blogs.Find(1).Url);
        }

        [TestMethod]
        public async Task UpdateAsync_Collection_Empty_ShouldNoop()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.UpdateAsync([]);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://sample.com/cats", context.Blogs.Find(1).Url);
        }

        [TestMethod]
        public async Task UpdateAsync_Entity_WithPropertyNames_ShouldUpdate()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await repo.UpdateAsync(
                new Blog { BlogId = 1, Url = "http://sample.com/property-name" },
                nameof(Blog.Url));

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://sample.com/property-name", context.Blogs.Find(1).Url);
        }

        [TestMethod]
        public async Task UpdateAsync_Entity_WithPropertyExpressions_ShouldUpdate()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Expression<Func<Blog, object>> urlProp = b => b.Url;
            await repo.UpdateAsync(
                new Blog { BlogId = 1, Url = "http://sample.com/property-expr" },
                urlProp);

            await using BloggingContext context = new(testDb.Options);
            Assert.AreEqual("http://sample.com/property-expr", context.Blogs.Find(1).Url);
        }

        [TestMethod]
        public void Update_ByEntity_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            repo.Update(new Blog { BlogId = 1, Url = "http://sample.com/zebras" });

            // Use a separate instance of the context to verify correct data was saved to database
            using BloggingContext context = new(testDb.Options);
            Blog blog = context.Blogs.Find(1);
            Assert.AreEqual("http://sample.com/zebras", blog.Url);
        }

        [TestMethod]
        public void Update_ByEntity_Commit_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            repo.Update(new Blog { BlogId = 1, Url = "http://sample.com/zebras" }, true);

            // Use a separate instance of the context to verify correct data was saved to database
            using BloggingContext context = new(testDb.Options);
            Blog blog = context.Blogs.Find(1);
            Assert.AreEqual("http://sample.com/zebras", blog.Url);
        }

        [TestMethod]
        public void Update_ByEntity_DoNotCommit_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            repo.Update(new Blog { BlogId = 1, Url = "http://sample.com/zebras" }, false);

            // Use a separate instance of the context to verify correct data was saved to database
            using BloggingContext context = new(testDb.Options);
            Blog blog = context.Blogs.Find(1);
            Assert.AreEqual("http://sample.com/cats", blog.Url);
        }

        [TestMethod]
        public void Update_WithExecuteUpdate_ConstantValue_ShouldUpdateMatchingEntities()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            // Update blogs with BlogId 1 and 2 (the ones with "cat" in URL)
            int updated = repo.Update(
                b => b.BlogId == 1 || b.BlogId == 2,
                b => b.Url,
                "http://sample.com/updated");

            // Verify the number of entities updated
            Assert.AreEqual(2, updated);

            // Use a separate instance of the context to verify correct data was saved to database
            using BloggingContext context = new(testDb.Options);
            Blog blog1 = context.Blogs.Find(1);
            Blog blog2 = context.Blogs.Find(2);
            Blog blog3 = context.Blogs.Find(3);

            Assert.AreEqual("http://sample.com/updated", blog1.Url);
            Assert.AreEqual("http://sample.com/updated", blog2.Url);
            Assert.AreEqual("http://sample.com/dogs", blog3.Url); // Should not be updated
        }

        [TestMethod]
        public void Update_WithExecuteUpdate_ExpressionValue_ShouldUpdateMatchingEntities()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            // Update all blogs to have a modified URL using an expression
            int updated = repo.Update(
                b => b.BlogId <= 2,
                b => b.Url,
                b => b.Url + "/modified");

            // Verify the number of entities updated
            Assert.AreEqual(2, updated);

            // Use a separate instance of the context to verify correct data was saved to database
            using BloggingContext context = new(testDb.Options);
            Blog blog1 = context.Blogs.Find(1);
            Blog blog2 = context.Blogs.Find(2);
            Blog blog3 = context.Blogs.Find(3);

            Assert.EndsWith("/modified", blog1.Url);
            Assert.EndsWith("/modified", blog2.Url);
            Assert.AreEqual("http://sample.com/dogs", blog3.Url); // Should not be updated
        }

        [TestMethod]
        public void Update_WithExecuteUpdate_NoMatchingEntities_ShouldReturnZero()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            // Try to update blogs that don't exist
            int updated = repo.Update(
                b => b.BlogId > 100,
                b => b.Url,
                "http://sample.com/updated");

            // Verify no entities were updated
            Assert.AreEqual(0, updated);

            // Verify no data was changed
            using BloggingContext context = new(testDb.Options);
            Blog blog1 = context.Blogs.Find(1);
            Assert.AreEqual("http://sample.com/cats", blog1.Url);
        }
    }
}