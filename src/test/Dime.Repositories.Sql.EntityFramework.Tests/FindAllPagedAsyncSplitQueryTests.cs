using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class FindAllPagedAsyncSplitQueryTests
    {
        private static EfRepository<Blog, BloggingContext> NewRepo(TestDatabase db)
            => new(new BloggingContext(db.Options));

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_ReturnsExpectedShape()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            IPage<string> split = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(2, split.Total);
            CollectionAssert.AreEquivalent(
                new[] { "http://sample.com/catfish", "http://sample.com/cats" },
                split.Data.ToList());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_RespectsTotalCount()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            IPage<string> result = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 1,
                splitQuery: true);

            Assert.AreEqual(2, result.Total);
            Assert.AreEqual(1, result.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_OrdersAscending()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            IPage<string> result = await repo.FindAllPagedAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            List<string> list = result.Data.ToList();
            CollectionAssert.AreEqual(list.OrderBy(s => s).ToList(), list);
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_OrdersDescending()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            IPage<string> result = await repo.FindAllPagedAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: false,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            List<string> list = result.Data.ToList();
            CollectionAssert.AreEqual(list.OrderByDescending(s => s).ToList(), list);
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_PaginatesInSql()
        {
            using TestDatabase testDb = new(captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            _ = await repo.FindAllPagedAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 2,
                pageSize: 1,
                splitQuery: true);

            string dataSql = testDb.CapturedSql.LastOrDefault(s =>
                s.Contains("FROM \"Blogs\"") && !s.Contains("COUNT(*)") && !s.Contains("count(*)")) ?? string.Empty;
            Assert.IsTrue(dataSql.Contains("LIMIT") || dataSql.Contains("OFFSET") || dataSql.Contains("FETCH"),
                $"Expected paging clause in data SQL. Got: {dataSql}");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_WithIncludeCollection_LoadsRelated()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            IPage<Blog> result = await repo.FindAllPagedAsync<Blog>(
                where: x => x.Url.Contains("cats"),
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            Blog cats = result.Data.FirstOrDefault(b => b.Url == "http://sample.com/cats");
            Assert.IsNotNull(cats);
            Assert.IsNotNull(cats.Posts);
            Assert.AreEqual(2, cats.Posts.Count);
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_WithIncludes_EmitsMoreDataCommandsThanSingleMode()
        {
            int split = await CountDataCommands(splitQuery: true);
            int single = await CountDataCommands(splitQuery: false);

            Assert.IsTrue(split > single,
                $"Split should issue more data commands than single mode; split={split} single={single}");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryFalse_WithIncludes_EmitsSingleJoinedDataCommand()
        {
            int single = await CountDataCommands(splitQuery: false);
            Assert.AreEqual(1, single, "Single-query mode should emit exactly one data command for the join");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQuery_AlwaysEmitsSeparateCountQuery()
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            int before = testDb.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync<Blog>(
                where: x => true,
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            List<string> after = testDb.CapturedSql.Skip(before).ToList();
            int countCommands = after.Count(s => s.Contains("COUNT(*)") || s.Contains("count(*)"));
            Assert.IsTrue(countCommands >= 1, "Expected a SELECT COUNT(*) command for the page total");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_EmptyResult_HasZeroTotal()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            IPage<string> result = await repo.FindAllPagedAsync<string>(
                where: x => x.Url == "no-such-url",
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(0, result.Total);
            Assert.AreEqual(0, result.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_NullOrderBy_DoesNotThrow()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            IPage<string> result = await repo.FindAllPagedAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: 1,
                pageSize: 2,
                splitQuery: true);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Total);
        }

        private static async Task<int> CountDataCommands(bool splitQuery)
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = NewRepo(testDb);

            int before = testDb.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync<Blog>(
                where: x => true,
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: splitQuery,
                includes: nameof(Blog.Posts));

            return testDb.CapturedSql.Skip(before)
                .Count(s => s.Contains("Executed DbCommand") &&
                            (s.Contains("FROM \"Blogs\"") || s.Contains("FROM \"Posts\"")) &&
                            !s.Contains("COUNT(*)") && !s.Contains("count(*)"));
        }
    }
}