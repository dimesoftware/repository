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
    }
}