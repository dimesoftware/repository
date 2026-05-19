using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Edge-case coverage for the four split-query overloads added by this branch.
    /// Each overload is exercised against in-memory SQLite so we can assert behaviour
    /// (correct totals, correct ordering, predicate fall-back, includes resolution)
    /// without needing a real Cartesian-fan-out to observe. The Cartesian-vs-split
    /// SQL-shape distinction is asserted at the integration-test layer.
    /// </summary>
    [TestClass]
    public class SplitQueryEdgeTests
    {
        private static EfRepository<Blog, BloggingContext> NewRepo(TestDatabase db)
            => new(new BloggingContext(db.Options));

        // ── FindAllAsync<TResult>(... splitQuery, ...) ────────────────────

        [TestMethod]
        public async Task FindAllAsync_Split_NullWhere_ReturnsEverything()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: null,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public async Task FindAllAsync_Split_AscendingFalse_FlipsOrder()
        {
            // A non-factory EfRepository is single-use: each call ends with `using ctx = Context`
            // which disposes the underlying DbContext. Each invocation needs its own repo.
            using TestDatabase db = new();

            List<string> asc;
            using (EfRepository<Blog, BloggingContext> repoAsc = NewRepo(db))
                asc = (await repoAsc.FindAllAsync<string>(
                    where: x => true, select: x => x.Url, orderBy: x => x.Url,
                    ascending: true, page: null, pageSize: null, splitQuery: true)).ToList();

            List<string> desc;
            using (EfRepository<Blog, BloggingContext> repoDesc = NewRepo(db))
                desc = (await repoDesc.FindAllAsync<string>(
                    where: x => true, select: x => x.Url, orderBy: x => x.Url,
                    ascending: false, page: null, pageSize: null, splitQuery: true)).ToList();

            CollectionAssert.AreEqual(asc, desc.AsEnumerable().Reverse().ToList());
        }

        [TestMethod]
        public async Task FindAllAsync_Split_TogglingFlag_ProducesEqualResults()
        {
            using TestDatabase db = new(seedPosts: true);
            using EfRepository<Blog, BloggingContext> repoSplit = NewRepo(db);
            using EfRepository<Blog, BloggingContext> repoNoSplit = NewRepo(db);

            List<string> split = (await repoSplit.FindAllAsync<string>(
                where: x => true, select: x => x.Url, orderBy: x => x.Url,
                ascending: true, page: null, pageSize: null,
                splitQuery: true, includes: nameof(Blog.Posts))).ToList();

            List<string> noSplit = (await repoNoSplit.FindAllAsync<string>(
                where: x => true, select: x => x.Url, orderBy: x => x.Url,
                ascending: true, page: null, pageSize: null,
                splitQuery: false, includes: nameof(Blog.Posts))).ToList();

            CollectionAssert.AreEqual(noSplit, split,
                "splitQuery is a SQL-shape concern; result set must be identical.");
        }

        [TestMethod]
        public async Task FindAllAsync_Split_PageSizeOne_ReturnsOneItem()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 1,
                splitQuery: true);

            Assert.AreEqual(1, result.Count());
        }

        [DataTestMethod]
        [DataRow(1, 1, 1)]
        [DataRow(1, 2, 2)]
        [DataRow(1, 3, 3)]
        [DataRow(1, 10, 3)]
        [DataRow(2, 2, 1)]
        [DataRow(2, 3, 0)]
        [DataRow(3, 1, 1)]
        [DataRow(4, 1, 0)]
        public async Task FindAllAsync_Split_PagingBoundaries(int page, int pageSize, int expected)
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: page,
                pageSize: pageSize,
                splitQuery: true);

            Assert.AreEqual(expected, result.Count());
        }

        // ── FindAllPagedAsync<TResult>(... splitQuery 7-arg, ...) ─────────

        [TestMethod]
        public async Task FindAllPagedAsync_Split7_Total_RespectsWhere()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 1,
                splitQuery: true);

            Assert.AreEqual(2, page.Total);
            Assert.AreEqual(1, page.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_Split7_AscendingFalse_ReversesPage()
        {
            using TestDatabase db = new();

            IPage<string> asc;
            using (EfRepository<Blog, BloggingContext> repoAsc = NewRepo(db))
                asc = await repoAsc.FindAllPagedAsync<string>(
                    where: x => true, select: x => x.Url, orderBy: x => x.Url,
                    ascending: true, page: 1, pageSize: 10, splitQuery: true);

            IPage<string> desc;
            using (EfRepository<Blog, BloggingContext> repoDesc = NewRepo(db))
                desc = await repoDesc.FindAllPagedAsync<string>(
                    where: x => true, select: x => x.Url, orderBy: x => x.Url,
                    ascending: false, page: 1, pageSize: 10, splitQuery: true);

            CollectionAssert.AreEqual(asc.Data.ToList(), desc.Data.AsEnumerable().Reverse().ToList());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_Split7_NullWhere_TotalEqualsSeedCount()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: null, select: x => x.Url,
                orderBy: (Expression<Func<Blog, dynamic>>)null,
                ascending: null, page: null, pageSize: null,
                splitQuery: true);

            Assert.AreEqual(3, page.Total);
        }

        // ── FindAllPagedAsync(IOrder... splitQuery, entity, ...) ──────────

        [TestMethod]
        public async Task FindAllPagedAsync_SplitEntity_NullOrderBy_IsPermissive()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                page: 1, pageSize: 10, splitQuery: true);

            Assert.AreEqual(3, page.Total);
            Assert.AreEqual(3, page.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitEntity_IOrderAscending_OrdersByUrl()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IEnumerable<IOrder<Blog>> orderBy = [new Order<Blog>(nameof(Blog.Url), isAscending: true)];

            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true, orderBy: orderBy,
                page: 1, pageSize: 10, splitQuery: true);

            List<string> urls = page.Data.Select(b => b.Url).ToList();
            CollectionAssert.AreEqual(urls.OrderBy(s => s).ToList(), urls);
        }

        // ── FindAllPagedAsync<TResult>(where, count, select, IOrder, …, splitQuery, …) ──

        [TestMethod]
        public async Task FindAllPagedAsync_Split10_CountPredicateDifferentFromWhere()
        {
            // where filters the page rows; count predicate dictates the Total.
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("dog"),
                count: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                groupBy: null,
                ascending: true, page: 1, pageSize: 10, splitQuery: true);

            Assert.AreEqual(2, page.Total, "Total comes from the count predicate.");
            Assert.AreEqual(1, page.Data.Count(), "Page rows come from the where predicate.");
            Assert.AreEqual("http://sample.com/dogs", page.Data.Single());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_Split10_NullWhere_NullCount_CountsAll()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: null,
                count: null,
                select: x => x.Url,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                groupBy: null,
                ascending: true, page: 1, pageSize: 10, splitQuery: true);

            Assert.AreEqual(3, page.Total);
        }

        [TestMethod]
        public async Task FindAllPagedAsync_Split10_NonNullWhere_NullCount_FallsBackToWhere()
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                count: null,
                select: x => x.Url,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                groupBy: null,
                ascending: true, page: 1, pageSize: 10, splitQuery: true);

            Assert.AreEqual(2, page.Total);
        }

        // ── Cross-overload: split-on vs split-off equivalence ─────────────

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task FindAllAsync_SplitQuery_DataIsIndependentOfFlag(bool splitFlag)
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true, page: 1, pageSize: 10, splitQuery: splitFlag);

            CollectionAssert.AreEquivalent(
                new[] { "http://sample.com/cats", "http://sample.com/catfish" },
                result.ToList());
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task FindAllPagedAsync_Split7_DataIsIndependentOfFlag(bool splitFlag)
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true, page: 1, pageSize: 10, splitQuery: splitFlag);

            CollectionAssert.AreEquivalent(
                new[] { "http://sample.com/cats", "http://sample.com/catfish" },
                page.Data.ToList());
            Assert.AreEqual(2, page.Total);
        }

        // ── SQL emission: AsSplitQuery emits the marker comment on SQL Server only ──
        // (SQLite never emits the AsSplitQuery hint; we just verify the query *runs*
        // and produces correct results, which is what the in-memory layer can prove.)

        [TestMethod]
        public async Task FindAllPagedAsync_Split7_WithCapturedSql_QueryReachesProvider()
        {
            using TestDatabase db = new(captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            _ = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true, page: 1, pageSize: 10, splitQuery: true);

            Assert.IsTrue(db.CapturedSql.Any(s => s.Contains("FROM \"Blogs\"") || s.Contains("FROM [Blogs]")),
                "Expected at least one captured command targeting the Blogs table.");
        }

        // ── Public-surface invariants ─────────────────────────────────────

        [TestMethod]
        public void Interface_HasSplitQueryOverload_OnFindAllAsync()
        {
            System.Reflection.MethodInfo[] methods = typeof(IQueryRepositoryAsync<Blog>)
                .GetMethods()
                .Where(m => m.Name == nameof(IQueryRepositoryAsync<Blog>.FindAllAsync))
                .ToArray();

            Assert.IsTrue(methods.Any(m => m.GetParameters().Any(p => p.Name == "splitQuery" && p.ParameterType == typeof(bool))),
                "IQueryRepositoryAsync<T>.FindAllAsync must expose a splitQuery boolean.");
        }

        [TestMethod]
        public void Interface_HasSplitQueryOverloads_OnFindAllPagedAsync()
        {
            int splitOverloads = typeof(IPagedQueryRepositoryAsync<Blog>)
                .GetMethods()
                .Count(m => m.Name == nameof(IPagedQueryRepositoryAsync<Blog>.FindAllPagedAsync)
                            && m.GetParameters().Any(p => p.Name == "splitQuery" && p.ParameterType == typeof(bool)));

            Assert.IsTrue(splitOverloads >= 3,
                $"Expected at least three FindAllPagedAsync overloads accepting splitQuery; found {splitOverloads}.");
        }

    }
}
