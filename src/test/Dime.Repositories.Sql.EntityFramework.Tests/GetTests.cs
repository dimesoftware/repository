using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class GetTests
    {
        [TestMethod]
        public void FindAll_Contains_ShouldFindMatches()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            IEnumerable<Blog> result = repo.FindAll(x => x.Url.Contains("cat"));
            Assert.AreEqual(2, result.Count());
        }

        [TestMethod]
        public void Exists_True_WhenMatch()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Assert.IsTrue(repo.Exists(b => b.Url == "http://sample.com/cats"));
        }

        [TestMethod]
        public void Exists_False_WhenNoMatch()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Assert.IsFalse(repo.Exists(b => b.Url == "http://nothere.com"));
        }

        [TestMethod]
        public void FindById_ShouldReturnEntity()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog blog = repo.FindById(1);

            Assert.IsNotNull(blog);
            Assert.AreEqual("http://sample.com/cats", blog.Url);
        }

        [TestMethod]
        public void FindById_NotFound_ShouldReturnNull()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Assert.IsNull(repo.FindById(999));
        }

        [TestMethod]
        public void FindById_WithIncludes_ShouldReturnEntity()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            Blog blog = repo.FindById(1, "Posts");

            Assert.IsNotNull(blog);
        }

        [TestMethod]
        public void FindOne_Predicate_ShouldReturnEntity()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog blog = repo.FindOne(b => b.Url == "http://sample.com/dogs");

            Assert.IsNotNull(blog);
            Assert.AreEqual(3, blog.BlogId);
        }

        [TestMethod]
        public void FindOne_PredicateWithIncludes_ShouldReturnEntity()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog blog = repo.FindOne(b => b.Url == "http://sample.com/dogs", "Posts");

            Assert.IsNotNull(blog);
        }

        [TestMethod]
        public void FindOne_Projection_ShouldReturnProjectedType()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            BlogProjection projection = repo.FindOne<BlogProjection>(
                where: b => b.BlogId == 1,
                select: b => new BlogProjection { Url = b.Url });

            Assert.IsNotNull(projection);
            Assert.AreEqual("http://sample.com/cats", projection.Url);
        }

        [TestMethod]
        public void FindAll_WithIncludes_ShouldReturnEntities()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<Blog> blogs = repo.FindAll(b => b.Url.Contains("cat"), "Posts");

            Assert.AreEqual(2, blogs.Count());
        }

        [TestMethod]
        public void FindAll_WithIncludeAllFlag_ShouldReturnEntities()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<Blog> blogs = repo.FindAll(b => b.Url.Contains("cat"), true, "Posts");

            Assert.AreEqual(2, blogs.Count());
        }

        [TestMethod]
        public void FindAll_WithPagingAndIncludes_ShouldReturnEntities()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<Blog> blogs = repo.FindAll(b => true, page: 1, pageSize: 2, includes: []);

            Assert.IsNotNull(blogs);
        }

        [TestMethod]
        public void FindAll_Projection_ShouldReturnProjectedItems()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<BlogProjection> items = repo.FindAll<BlogProjection>(
                where: b => true,
                select: b => new BlogProjection { Url = b.Url });

            Assert.AreEqual(3, items.Count());
        }

        [TestMethod]
        public void FindAll_OrderedAscending_ShouldReturnSorted()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<Blog> blogs = repo.FindAll(
                where: b => true,
                orderBy: b => b.Url,
                ascending: true);

            Assert.AreEqual("http://sample.com/catfish", blogs.First().Url);
        }

        [TestMethod]
        public void FindAll_OrderedDescending_ShouldReturnSorted()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IEnumerable<Blog> blogs = repo.FindAll(
                where: b => true,
                orderBy: b => b.Url,
                ascending: false);

            Assert.AreEqual("http://sample.com/dogs", blogs.First().Url);
        }

        [TestMethod]
        public async Task FindByIdAsync_ShouldReturnEntity()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog blog = await repo.FindByIdAsync(1);
            Assert.IsNotNull(blog);
        }

        [TestMethod]
        public async Task FindByIdAsync_WithIncludes_ShouldReturnEntity()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog blog = await repo.FindByIdAsync(1, "Posts");
            Assert.IsNotNull(blog);
        }

        [TestMethod]
        public async Task ExistsAsync_True_WhenMatch()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Assert.IsTrue(await repo.ExistsAsync(b => b.Url == "http://sample.com/cats"));
        }

        [TestMethod]
        public async Task ExistsAsync_False_WhenNoMatch()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Assert.IsFalse(await repo.ExistsAsync(b => b.Url == "http://nothere.com"));
        }

        [TestMethod]
        public async Task FindOneAsync_Predicate_ShouldReturnEntity()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog blog = await repo.FindOneAsync(b => b.Url == "http://sample.com/dogs");
            Assert.IsNotNull(blog);
        }

        [TestMethod]
        public async Task FindOneAsync_PredicateWithIncludes_ShouldReturnEntity()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Blog blog = await repo.FindOneAsync(b => b.Url == "http://sample.com/dogs", "Posts");
            Assert.IsNotNull(blog);
        }

        [TestMethod]
        public async Task FindOneAsync_Projection_ShouldReturnProjectedType()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            BlogProjection projection = await repo.FindOneAsync<BlogProjection>(
                where: b => b.BlogId == 1,
                select: b => new BlogProjection { Url = b.Url });

            Assert.IsNotNull(projection);
            Assert.AreEqual("http://sample.com/cats", projection.Url);
        }

        [TestMethod]
        public async Task FindAllAsync_WithIncludes_ShouldReturnEntities()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<Blog> blogs = await repo.FindAllAsync(b => b.Url.Contains("cat"), "Posts");
            Assert.AreEqual(2, blogs.Count());
        }

        [TestMethod]
        public async Task FindAllAsync_Projection_ShouldReturnProjectedItems()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<BlogProjection> items = await repo.FindAllAsync<BlogProjection>(
                where: b => true,
                select: b => new BlogProjection { Url = b.Url });

            Assert.AreEqual(3, items.Count());
        }
    }

    public class BlogProjection
    {
        public string Url { get; set; }
    }
}