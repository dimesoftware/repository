using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public partial class PagedAsyncTests
    {
        [TestMethod]
        public async Task FindAllPagedAsync_All_ShouldFindMatches()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            IPage<Blog> result = await repo.FindAllPagedAsync(null, null, 1, 2);
            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_Contains_ShouldFindMatches()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            IPage<Blog> result = await repo.FindAllPagedAsync(x => x.Url.Contains("cat"), null, 1, 1);
            Assert.AreEqual(2, result.Total);
            Assert.AreEqual(1, result.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_Projection_ExpressionOrderBy_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IPage<BlogProjection> result = await repo.FindAllPagedAsync<BlogProjection>(
                where: b => true,
                select: b => new BlogProjection { Url = b.Url },
                orderBy: b => b.BlogId,
                ascending: true,
                page: 1,
                pageSize: 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_EntityOnly_ExpressionOrderBy_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            System.Linq.Expressions.Expression<System.Func<Blog, object>> orderBy = b => b.BlogId;
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: b => true,
                orderBy: orderBy,
                ascending: true,
                page: 1,
                pageSize: 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_EntityOnly_ExpressionOrderByWithGroup_ShouldReturnPage()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            System.Linq.Expressions.Expression<System.Func<Blog, object>> orderBy = b => b.BlogId;
            System.Linq.Expressions.Expression<System.Func<Blog, object>> groupBy = null;
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: b => true,
                orderBy: orderBy,
                groupBy: groupBy,
                ascending: true,
                page: 1,
                pageSize: 2);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }
    }
}