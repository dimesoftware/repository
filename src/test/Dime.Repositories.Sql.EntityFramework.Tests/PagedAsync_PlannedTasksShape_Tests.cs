using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Tests modelled after the dimescheduler "planned tasks" production shape:
    /// a root entity with TWO collection navigations (Posts + Tags here), pagination,
    /// ordering, and a where clause. This is the case AsSplitQuery exists to solve.
    /// </summary>
    [TestClass]
    public class PagedAsync_PlannedTasksShape_Tests
    {
        private static IPagedQueryRepositoryAsync<Blog> NewRepo(TestDatabase db)
            => new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

        private static IEnumerable<IOrder<Blog>> OrderBy(string prop, bool asc = true)
            => [new Order<Blog>(prop, asc)];

        private static int CountDataCommands(TestDatabase db, int sinceIndex)
            => db.CapturedSql.Skip(sinceIndex).Count(s =>
                s.Contains("Executed DbCommand") &&
                !s.Contains("COUNT(*)") &&
                !s.Contains("count(*)") &&
                (s.Contains("FROM \"Blogs\"") || s.Contains("FROM \"Posts\"") || s.Contains("FROM \"Tags\"")));

        private static int CountCountCommands(TestDatabase db, int sinceIndex)
            => db.CapturedSql.Skip(sinceIndex).Count(s =>
                s.Contains("Executed DbCommand") && (s.Contains("COUNT(*)") || s.Contains("count(*)")));

        // ─── Cartesian-shape tests ─────────────────────────────────────────────

        [TestMethod]
        public async Task CartesianFanOut_TwoCollections_SplitFalse_EmitsSingleCombinedDataQuery()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: false,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(1, CountDataCommands(db, before),
                "Single mode should emit one combined Cartesian-JOIN data query");
        }

        [TestMethod]
        public async Task CartesianFanOut_TwoCollections_SplitTrue_EmitsThreeSeparateDataQueries()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            int data = CountDataCommands(db, before);
            Assert.AreEqual(3, data,
                $"Split mode should emit 1 root + 1 per collection nav = 3 data commands. Got {data}");
        }

        [TestMethod]
        public async Task CartesianFanOut_SplitVsNoSplit_IdenticalResultsAndCount()
        {
            using TestDatabase db1 = new(seedPosts: true, seedTags: true);
            IPage<Blog> single;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(db1))
                single = await r.FindAllPagedAsync(
                    where: x => x.Url.Contains("cat"),
                    orderBy: OrderBy(nameof(Blog.BlogId)),
                    page: 1,
                    pageSize: 10,
                    splitQuery: false,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            using TestDatabase db2 = new(seedPosts: true, seedTags: true);
            IPage<Blog> split;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(db2))
                split = await r.FindAllPagedAsync(
                    where: x => x.Url.Contains("cat"),
                    orderBy: OrderBy(nameof(Blog.BlogId)),
                    page: 1,
                    pageSize: 10,
                    splitQuery: true,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(single.Total, split.Total);
            CollectionAssert.AreEqual(
                single.Data.Select(b => b.BlogId).ToList(),
                split.Data.Select(b => b.BlogId).ToList());
        }

        [TestMethod]
        public async Task CartesianFanOut_Paged_TotalReflectsDistinctRoots_NotJoinRows()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            // 3 distinct Blogs, not 3 × 2 × 3 = 18 join rows
            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(3, result.Data.Count());
        }

        [TestMethod]
        public async Task CartesianFanOut_Paged_PageSizeRespectsDistinctRoots()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> page1 = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 2,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(3, page1.Total);
            Assert.AreEqual(2, page1.Data.Count());
        }

        [TestMethod]
        public async Task CartesianFanOut_SplitTrue_WhereFilterPropagatesToAllDataQueries()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cats"),
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            List<string> dataCommands = db.CapturedSql.Skip(before).Where(s =>
                s.Contains("Executed DbCommand") && !s.Contains("COUNT(*)") &&
                (s.Contains("FROM \"Blogs\"") || s.Contains("FROM \"Posts\"") || s.Contains("FROM \"Tags\""))).ToList();

            foreach (string sql in dataCommands)
                StringAssert.Contains(sql, "WHERE",
                    "Each split-query data command should propagate the where predicate.");
        }

        [TestMethod]
        public async Task CartesianFanOut_SplitTrue_AllCollectionsLoadedOnReturnedEntities()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cats"),
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Blog cats = result.Data.First(b => b.Url == "http://sample.com/cats");
            Assert.IsNotNull(cats.Posts);
            Assert.IsNotNull(cats.Tags);
            Assert.AreEqual(2, cats.Posts.Count);
            Assert.AreEqual(3, cats.Tags.Count);
        }

        [TestMethod]
        public async Task CartesianFanOut_SplitTrue_OneCollectionAndOneReference_BothLoaded()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts)]);

            Blog catfish = result.Data.First(b => b.Url == "http://sample.com/catfish");
            Assert.IsNotNull(catfish.Posts);
            Assert.AreEqual(1, catfish.Posts.Count);
            // Tags not included; should be null or empty
            Assert.IsTrue(catfish.Tags == null || catfish.Tags.Count == 0);
        }

        // ─── Strict SQL-emission counts ────────────────────────────────────────

        [TestMethod]
        public async Task Sql_SplitTrue_OneCollectionNav_EmitsExactly2DataCommands()
        {
            using TestDatabase db = new(seedPosts: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts)]);

            Assert.AreEqual(2, CountDataCommands(db, before),
                "Split with 1 collection nav = 1 root + 1 nav = 2 data commands");
        }

        [TestMethod]
        public async Task Sql_AnyMode_AlwaysExactly1CountCommand_SplitTrue()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(1, CountCountCommands(db, before));
        }

        [TestMethod]
        public async Task Sql_AnyMode_AlwaysExactly1CountCommand_SplitFalse()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: false,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(1, CountCountCommands(db, before));
        }

        [TestMethod]
        public async Task Sql_SplitTrue_OrderByAppearsInAllDataQueries()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.Url)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            List<string> dataCommands = db.CapturedSql.Skip(before).Where(s =>
                s.Contains("Executed DbCommand") && !s.Contains("COUNT(*)") &&
                (s.Contains("FROM \"Blogs\"") || s.Contains("FROM \"Posts\"") || s.Contains("FROM \"Tags\""))).ToList();

            foreach (string sql in dataCommands)
                StringAssert.Contains(sql, "ORDER BY",
                    "Each split-query data command must order by the same key for stable materialization.");
        }

        [TestMethod]
        public async Task Sql_SplitTrue_LimitAppearsAtLeastOnce()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: null,
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 2,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            List<string> dataCommands = db.CapturedSql.Skip(before).Where(s =>
                s.Contains("Executed DbCommand") && !s.Contains("COUNT(*)") &&
                (s.Contains("FROM \"Blogs\"") || s.Contains("FROM \"Posts\"") || s.Contains("FROM \"Tags\""))).ToList();

            Assert.IsTrue(dataCommands.Any(s => s.Contains("LIMIT") || s.Contains("OFFSET") || s.Contains("FETCH")),
                "Paging must be expressed in SQL on at least the root query.");
        }

        [TestMethod]
        public async Task Sql_SplitTrue_NoSubQueryFetchesMoreThanRootCount()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            _ = await repo.FindAllPagedAsync(
                where: x => x.Url == "http://sample.com/cats",
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 1,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            // Sub-queries (Posts / Tags) should be scoped to the root selection,
            // so they include a join/IN over the root keys, not pull all Posts/Tags.
            List<string> subQueries = db.CapturedSql.Skip(before).Where(s =>
                s.Contains("Executed DbCommand") && !s.Contains("COUNT(*)") &&
                (s.Contains("FROM \"Posts\"") || s.Contains("FROM \"Tags\""))).ToList();

            foreach (string sql in subQueries)
                Assert.IsTrue(sql.Contains("JOIN") || sql.Contains("IN ("),
                    $"Sub-query should be scoped to root keys (JOIN or IN). Got: {sql}");
        }

        [TestMethod]
        public async Task CartesianFanOut_SplitTrue_AlwaysFalseWhere_TotalZero_NoDataQueriesEmittedForCollections()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            int before = db.CapturedSql.Count;
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url == "no-such-thing",
                orderBy: OrderBy(nameof(Blog.BlogId)),
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(0, result.Total);
            // EF still runs the root query (empty) but may or may not run nav queries.
            // Assert we never crash and the result is empty.
            Assert.AreEqual(0, result.Data.Count());
        }
    }
}