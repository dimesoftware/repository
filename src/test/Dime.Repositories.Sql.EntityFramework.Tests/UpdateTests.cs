using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class UpdateTests
    {
        [TestMethod]
        public void Update_ByEntity_ShouldRemoveOne()
        {
            using TestDatabase testDb = new();

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            repo.Update(new Blog { BlogId = 1, Url = "http://sample.com/zebras" });

            // Use a separate instance of the context to verify correct data was saved to database
            using BloggingContext context = new(testDb.Options);
            Blog blog = context.Blogs.Find(1);
            Assert.IsTrue(blog.Url == "http://sample.com/zebras");
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
            Assert.IsTrue(blog.Url == "http://sample.com/zebras");
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
            Assert.IsTrue(blog.Url == "http://sample.com/cats");
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

            Assert.IsTrue(blog1.Url.EndsWith("/modified"));
            Assert.IsTrue(blog2.Url.EndsWith("/modified"));
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