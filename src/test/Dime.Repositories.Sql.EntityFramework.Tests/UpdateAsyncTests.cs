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
    }
}