using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class FindAllPagedAsyncEntitySplitQueryTests
    {
        private static EfRepository<Blog, BloggingContext> NewRepo(TestDatabase db)
            => new(new BloggingContext(db.Options));

        private static IEnumerable<IOrder<Blog>> OrderByUrl(bool ascending = true)
            => new[] { new Order<Blog>(nameof(Blog.Url), ascending) };

        [TestMethod]
        public async Task SplitQueryTrue_ReturnsExpectedTotalAndPageData()
        {
            using TestDatabase testDb = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cat"),
                orderBy: OrderByUrl(),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(2, result.Total);
            CollectionAssert.AreEquivalent(
                new[] { "http://sample.com/catfish", "http://sample.com/cats" },
                result.Data.Select(b => b.Url).ToList());
        }

        [TestMethod]
        public async Task SplitQueryTrue_PaginatesInSql()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            _ = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl(),
                page: 2,
                pageSize: 1,
                splitQuery: true);

            string dataSql = testDb.CapturedSql.LastOrDefault(s =>
                s.Contains("FROM \"Blogs\"") && !s.Contains("COUNT(*)") && !s.Contains("count(*)")) ?? string.Empty;
            Assert.IsTrue(dataSql.Contains("LIMIT") || dataSql.Contains("OFFSET") || dataSql.Contains("FETCH"),
                $"Expected paging clause in data SQL. Got: {dataSql}");
        }

        [TestMethod]
        public async Task SplitQueryTrue_WithCollectionInclude_LoadsRelated()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cats"),
                orderBy: OrderByUrl(),
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
        public async Task SplitQueryTrue_WithIncludes_EmitsMoreDataCommandsThanSingleMode()
        {
            int split = await CountDataCommands(splitQuery: true);
            int single = await CountDataCommands(splitQuery: false);

            Assert.IsTrue(split > single,
                $"Split should issue more data commands than single mode; split={split} single={single}");
        }

        [TestMethod]
        public async Task SplitQueryFalse_WithIncludes_EmitsSingleJoinedDataCommand()
        {
            int single = await CountDataCommands(splitQuery: false);
            Assert.AreEqual(1, single, "Single-query mode should emit exactly one data command for the join");
        }

        [TestMethod]
        public async Task SplitQuery_AlwaysEmitsSeparateCountQuery()
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            int before = testDb.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl(),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            List<string> after = testDb.CapturedSql.Skip(before).ToList();
            int countCommands = after.Count(s => s.Contains("COUNT(*)") || s.Contains("count(*)"));
            Assert.IsTrue(countCommands >= 1, "Expected a SELECT COUNT(*) command for the page total");
        }

        [TestMethod]
        public async Task SplitQueryTrue_EmptyResult_HasZeroTotal()
        {
            using TestDatabase testDb = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url == "no-such-url",
                orderBy: OrderByUrl(),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(0, result.Total);
            Assert.AreEqual(0, result.Data.Count());
        }

        [TestMethod]
        public async Task SplitQueryTrue_NullOrderBy_DoesNotThrow()
        {
            using TestDatabase testDb = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: null,
                page: 1,
                pageSize: 2,
                splitQuery: true);

            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Total);
        }

        private static async Task<int> CountDataCommands(bool splitQuery)
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(testDb);

            int before = testDb.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: OrderByUrl(),
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
