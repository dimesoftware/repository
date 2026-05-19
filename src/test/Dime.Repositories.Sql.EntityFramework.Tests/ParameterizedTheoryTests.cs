using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Parameterized "theory-style" coverage. Each test enumerates a representative grid of
    /// inputs via <see cref="DataRowAttribute"/> so the same logic is exercised across:
    /// page/pageSize boundaries, ordering directions, schema/name combinations, splitQuery
    /// toggles, identifier-escape pathologies, and predicate fall-back permutations.
    /// </summary>
    [TestClass]
    public class ParameterizedTheoryTests
    {
        private static EfRepository<Blog, BloggingContext> NewRepo(TestDatabase db)
            => new(new BloggingContext(db.Options));

        // ── splitQuery × where × count fall-back matrix ───────────────────

        // (where  , count  , expectedTotal)
        [DataTestMethod]
        [DataRow(null, null, 3, DisplayName = "null/null → all")]
        [DataRow("cat", null, 2, DisplayName = "where=cat, count=null → 2 (falls back to where)")]
        [DataRow(null, "cat", 2, DisplayName = "where=null, count=cat → 2 (count wins)")]
        [DataRow("cat", "dog", 1, DisplayName = "explicit count overrides where")]
        [DataRow("zzz", null, 0, DisplayName = "no match → 0")]
        public async Task PagedSplit10_CountFallBackMatrix(string whereContains, string countContains, int expectedTotal)
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            Expression<Func<Blog, bool>> where = whereContains is null ? null : x => x.Url.Contains(whereContains);
            Expression<Func<Blog, bool>> count = countContains is null ? null : x => x.Url.Contains(countContains);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: where,
                count: count,
                select: x => x.Url,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                groupBy: null,
                ascending: true, page: 1, pageSize: 10, splitQuery: true);

            Assert.AreEqual(expectedTotal, page.Total);
        }

        // ── Paging boundaries × splitQuery flag ───────────────────────────

        [DataTestMethod]
        [DataRow(1, 1, true)]
        [DataRow(1, 1, false)]
        [DataRow(1, 2, true)]
        [DataRow(1, 2, false)]
        [DataRow(1, 3, true)]
        [DataRow(1, 3, false)]
        [DataRow(2, 2, true)]
        [DataRow(2, 2, false)]
        [DataRow(3, 1, true)]
        [DataRow(3, 1, false)]
        public async Task FindAllAsync_Split_PageAndSize(int page, int pageSize, bool splitFlag)
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
                splitQuery: splitFlag);

            // Expected count: clamp(seed - (page-1)*pageSize, 0..pageSize)
            int skipped = (page - 1) * pageSize;
            int expected = Math.Max(0, Math.Min(pageSize, 3 - skipped));

            Assert.AreEqual(expected, result.Count(),
                $"page={page} pageSize={pageSize} splitFlag={splitFlag}");
        }

        // ── Ordering matrix: ascending toggle × splitQuery flag ───────────

        [DataTestMethod]
        [DataRow(true, true)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(false, false)]
        public async Task FindAllAsync_Split_OrderingMatrix(bool ascending, bool splitFlag)
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            List<string> result = (await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: ascending,
                page: null,
                pageSize: null,
                splitQuery: splitFlag)).ToList();

            List<string> expected = ascending
                ? result.OrderBy(s => s).ToList()
                : result.OrderByDescending(s => s).ToList();

            CollectionAssert.AreEqual(expected, result);
        }

        // ── Schema/name combinations on QueryFunctionAsync ────────────────

        [DataTestMethod]
        [DataRow("dbo", "Func", "[dbo].[Func]")]
        [DataRow("reports", "Func", "[reports].[Func]")]
        [DataRow("dbo", "f_lookup_v2", "[dbo].[f_lookup_v2]")]
        [DataRow("Telemetry", "fGetLast24h", "[Telemetry].[fGetLast24h]")]
        [DataRow("dbo", "F1", "[dbo].[F1]")]
        [DataRow("DBO", "FUNC", "[DBO].[FUNC]")] // case is preserved
        public async Task QueryFunctionAsync_SchemaNameTheory(string schema, string name, string expectedFragment)
        {
            using TestDatabase db = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>(name, schema));

            string sql = db.CapturedSql.FirstOrDefault(s => s.Contains("FROM ["))
                         ?? string.Join("\n---\n", db.CapturedSql);
            StringAssert.Contains(sql, expectedFragment);
        }

        // ── Parameter placeholder rendering ──────────────────────────────

        [DataTestMethod]
        [DataRow(new[] { "@a" }, "(@a)")]
        [DataRow(new[] { "@a", "@b" }, "(@a, @b)")]
        [DataRow(new[] { "@b", "@a" }, "(@b, @a)")] // order preserved
        [DataRow(new[] { "@first", "@second", "@third" }, "(@first, @second, @third)")]
        public async Task QueryFunctionAsync_PlaceholderRenderingTheory(string[] paramNames, string expectedFragment)
        {
            using TestDatabase db = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

            DbParameter[] parameters = paramNames
                .Select(n => (DbParameter)new SqliteParameter(n, 0))
                .ToArray();

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = db.CapturedSql.FirstOrDefault(s => s.Contains("FROM ["))
                         ?? string.Join("\n---\n", db.CapturedSql);
            StringAssert.Contains(sql, expectedFragment);
        }

        // ── Predicate variant × paged-overload variant ────────────────────

        [DataTestMethod]
        [DataRow("cat", 2)]
        [DataRow("dog", 1)]
        [DataRow("sample", 3)]
        [DataRow("nope", 0)]
        public async Task FindAllPagedAsync_Split7_TotalForPredicate(string contains, int expectedTotal)
        {
            using TestDatabase db = new();
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains(contains),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1, pageSize: 10, splitQuery: true);

            Assert.AreEqual(expectedTotal, page.Total);
            Assert.AreEqual(expectedTotal, page.Data.Count());
        }

        // ── Page-data subset is stable across runs ────────────────────────

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task FindAllPagedAsync_Split7_Idempotent(bool splitFlag)
        {
            // Non-factory EfRepository is single-use, so each invocation owns its own repo.
            using TestDatabase db = new();

            IPage<string> first;
            using (EfRepository<Blog, BloggingContext> repoFirst = NewRepo(db))
                first = await repoFirst.FindAllPagedAsync<string>(
                    where: x => true, select: x => x.Url, orderBy: x => x.Url,
                    ascending: true, page: 1, pageSize: 10, splitQuery: splitFlag);

            IPage<string> second;
            using (EfRepository<Blog, BloggingContext> repoSecond = NewRepo(db))
                second = await repoSecond.FindAllPagedAsync<string>(
                    where: x => true, select: x => x.Url, orderBy: x => x.Url,
                    ascending: true, page: 1, pageSize: 10, splitQuery: splitFlag);

            Assert.AreEqual(first.Total, second.Total);
            CollectionAssert.AreEqual(first.Data.ToList(), second.Data.ToList());
        }

        // ── Includes resolve when entity has navigations ──────────────────

        [TestMethod]
        public async Task FindAllPagedAsync_Split7_WithExplicitInclude_HasNavigationsPopulated()
        {
            using TestDatabase db = new(seedPosts: true);
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<Blog> page = await repo.FindAllPagedAsync<Blog>(
                where: x => true,
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1, pageSize: 10, splitQuery: true,
                includes: nameof(Blog.Posts));

            Assert.IsTrue(page.Data.Any(b => b.Posts != null && b.Posts.Any()),
                "At least one returned blog should have its Posts navigation populated.");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_Split7_NoExplicitIncludes_AutoWalksAllNavigations()
        {
            // Documented behaviour of EFExtensions.Include: when `includes` is an empty array
            // (the params default), the helper walks every navigation on the entity and includes
            // each one. This test pins the contract — flipping it would be a behaviour change
            // and must be reflected here first.
            using TestDatabase db = new(seedPosts: true);
            using EfRepository<Blog, BloggingContext> repo = NewRepo(db);

            IPage<Blog> page = await repo.FindAllPagedAsync<Blog>(
                where: x => true,
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1, pageSize: 10, splitQuery: true);

            Assert.IsTrue(page.Data.Any(b => b.Posts != null && b.Posts.Any()),
                "When no includes are supplied, EFExtensions.Include auto-walks navigations — " +
                "Posts should be populated.");
        }
    }
}
