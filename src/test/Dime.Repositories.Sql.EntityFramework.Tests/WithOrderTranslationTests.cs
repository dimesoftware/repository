using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class WithOrderTranslationTests
    {
        private static string FindDataCommandText(TestDatabase db)
            => db.CapturedSql.LastOrDefault(s => s.Contains("FROM \"Blogs\"")) ?? string.Empty;

        [TestMethod]
        public async Task FindAllAsync_ProjectedOrderBy_EmitsSqlOrderBy()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            _ = (await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: true)).ToList();

            string sql = FindDataCommandText(testDb);
            Assert.Contains("ORDER BY", sql);
        }

        [TestMethod]
        public async Task FindAllAsync_ProjectedOrderByDescending_EmitsSqlDescOrder()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            _ = (await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: false)).ToList();

            string sql = FindDataCommandText(testDb);
            Assert.Contains("DESC", sql);
        }

        [TestMethod]
        public async Task FindAllAsync_ProjectedOrderByWithPaging_EmitsSqlLimitOffset()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            _ = (await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: true, page: 2, pageSize: 1)).ToList();

            string sql = FindDataCommandText(testDb);
            Assert.Contains("ORDER BY", sql);
            Assert.IsTrue(sql.Contains("LIMIT") || sql.Contains("OFFSET"), $"Expected LIMIT/OFFSET in SQL: {sql}");
        }

        [TestMethod]
        public async Task FindAllAsync_OrderByAscending_OrdersAscending()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<string> result = await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: true);

            List<string> list = [.. result];
            CollectionAssert.AreEqual(list.OrderBy(s => s).ToList(), list);
        }

        [TestMethod]
        public async Task FindAllAsync_OrderByDescending_OrdersDescending()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<string> result = await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: false);
            List<string> list = [.. result];
            CollectionAssert.AreEqual(list.OrderByDescending(s => s).ToList(), list);
        }

        [TestMethod]
        public async Task FindAllAsync_OrderByWithPaging_PagesInDatabase()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<string> page1 = await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: true, page: 1, pageSize: 2);

            using IRepository<Blog> repo2 = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));
            IEnumerable<string> page2 = await repo2.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: true, page: 2, pageSize: 2);

            Assert.AreEqual(2, page1.Count());
            Assert.AreEqual(1, page2.Count());
            CollectionAssert.AreNotEquivalent(page1.ToList(), page2.ToList());
        }

        [TestMethod]
        public async Task FindAllAsync_OrderBy_DoesNotPullAllRowsIntoMemory()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            _ = (await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: true, page: 1, pageSize: 1)).ToList();

            string sql = FindDataCommandText(testDb);
            Assert.IsTrue(sql.Contains("LIMIT") || sql.Contains("OFFSET") || sql.Contains("FETCH"),
                $"Paging must execute in SQL, not in memory. Captured: {sql}");
        }

        [TestMethod]
        public async Task FindAllAsync_NoOrderBy_DoesNotErrorOnPaging()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            IEnumerable<string> result = await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: null, ascending: null, page: 1, pageSize: 2);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task FindAllAsync_NullOrderBy_DoesNotEmitOrderByClause()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            _ = (await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: null, ascending: null)).ToList();

            string sql = FindDataCommandText(testDb);
            Assert.DoesNotContain("ORDER BY", sql, $"No ORDER BY expected when orderBy is null. Got: {sql}");
        }

        [TestMethod]
        public async Task FindAllAsync_OrderByOnDifferentProperty_TranslatesToSql()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            _ = (await repo.FindAllAsync(where: x => true, select: x => x.Url, orderBy: x => x.BlogId, ascending: true)).ToList();

            string sql = FindDataCommandText(testDb);
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("BlogId", sql);
        }
    }
}