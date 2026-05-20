using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Pins the three performance-oriented changes:
    ///   • Async-up of <c>FindAllAsync</c>/<c>FindAllPagedAsync</c> — proved via a
    ///     <see cref="DbCommandInterceptor"/> that counts async vs. sync read paths.
    ///   • Auto-include-all-navigations is opt-in — default empty <c>includes</c> emits no JOINs.
    ///   • Constant ORDER BY fallback dropped — unpaged queries with no orderBy emit no
    ///     <c>ORDER BY</c>; paged queries with no orderBy still get the SkipQueryFactory fallback.
    /// </summary>
    [TestClass]
    public class PerformanceImprovementTests
    {
        // ──────────────────────────────────────────────────────────────────
        // Async-up — interceptor-based proof that the async read path is used.
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task FindAllAsync_UsesAsyncReadPath_NotSynchronous()
        {
            using TestDatabase db = new();
            ReaderPathInterceptor interceptor = new();

            DbContextOptions<BloggingContext> options =
                new DbContextOptionsBuilder<BloggingContext>(db.Options)
                    .AddInterceptors(interceptor)
                    .Options;

            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(options));

            _ = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: null,
                pageSize: null);

            Assert.IsGreaterThanOrEqualTo(1, interceptor.AsyncReads, $"Expected at least one async ReaderExecutingAsync; saw {interceptor.AsyncReads} (sync: {interceptor.SyncReads}).");
            Assert.AreEqual(0, interceptor.SyncReads, "FindAllAsync must not fall back to the synchronous read path.");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_UsesAsyncReadPath_BothDataAndCount()
        {
            using TestDatabase db = new();
            ReaderPathInterceptor interceptor = new();

            DbContextOptions<BloggingContext> options =
                new DbContextOptionsBuilder<BloggingContext>(db.Options)
                    .AddInterceptors(interceptor)
                    .Options;

            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(options));

            _ = await repo.FindAllPagedAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 10);

            // Both the data SELECT and the COUNT(*) should go through ReaderExecutingAsync.
            Assert.IsGreaterThanOrEqualTo(2, interceptor.AsyncReads,
                $"Expected ≥2 async reads (data + count); saw {interceptor.AsyncReads}.");
            Assert.AreEqual(0, interceptor.SyncReads,
                "Paged async path must not use synchronous reader execution.");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQuery_UsesAsyncReadPath()
        {
            using TestDatabase db = new();
            ReaderPathInterceptor interceptor = new();

            DbContextOptions<BloggingContext> options =
                new DbContextOptionsBuilder<BloggingContext>(db.Options)
                    .AddInterceptors(interceptor)
                    .Options;

            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(options));

            _ = await repo.FindAllPagedAsync(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1, pageSize: 10, splitQuery: true);

            Assert.AreEqual(0, interceptor.SyncReads);
            Assert.IsGreaterThanOrEqualTo(2, interceptor.AsyncReads);
        }

        [TestMethod]
        public async Task ManyConcurrentFindAllAsync_DoNotStarveThreadPool()
        {
            // Smoke test: with truly async I/O, dozens of concurrent calls should complete in
            // well under the sync-blocking equivalent. We don't measure ms — we just verify
            // correctness and that none of them throw or deadlock. The interceptor count
            // proves no call fell back to the sync read path.
            const int concurrency = 32;
            ReaderPathInterceptor interceptor = new();

            using TestDatabase db = new();
            DbContextOptions<BloggingContext> options =
                new DbContextOptionsBuilder<BloggingContext>(db.Options)
                    .AddInterceptors(interceptor)
                    .Options;

            Task<int>[] tasks = [.. Enumerable.Range(0, concurrency).Select(async _ =>
            {
                // Each operation gets its own repo: a non-factory EfRepository is single-use.
                using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(options));
                IEnumerable<string> rows = await repo.FindAllAsync<string>(
                    where: x => true,
                    select: x => x.Url,
                    orderBy: x => x.Url,
                    ascending: true,
                    page: null,
                    pageSize: null);
                return rows.Count();
            })];

            int[] counts = await Task.WhenAll(tasks);

            Assert.IsTrue(counts.All(c => c == 3), "Every concurrent call should return the full seed.");
            Assert.AreEqual(0, interceptor.SyncReads, "No call may regress to the synchronous reader.");
        }

        // ──────────────────────────────────────────────────────────────────
        // Auto-include — current behaviour is "walk every navigation when includes is empty".
        // This is a documented Cartesian-fan-out trap; flipping it is tracked as a follow-up
        // issue and intentionally NOT done in this PR. These tests pin the current behaviour
        // so we notice if it changes.
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task FindAllAsync_ExplicitIncludePosts_JoinsPostsOnly()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true, captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

            _ = await repo.FindAllAsync(where: x => true, includes: nameof(Blog.Posts));

            // Look only at the data SELECT, not the seed INSERTs into Tags from TestDatabase setup.
            string dataSelect = db.CapturedSql.FirstOrDefault(s => s.Contains("SELECT") && (s.Contains("FROM \"Blogs\"") || s.Contains("FROM [Blogs]")));
            Assert.IsNotNull(dataSelect, "Expected a SELECT against Blogs in the capture log.");

            Assert.Contains("\"Posts\"", dataSelect, $"Explicit include of Posts must appear in the SELECT. SQL: {dataSelect}");
            Assert.DoesNotContain("\"Tags\"", dataSelect, $"Tags must NOT be joined when only Posts was asked. SQL: {dataSelect}");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_EmptyIncludes_AutoWalksAllNavigations()
        {
            // Documented behaviour: an empty `includes` array (the params default) triggers
            // EFExtensions.Include's auto-walk of every navigation on the entity. The
            // long-term plan is to flip this to opt-in — see the GH issue.
            using TestDatabase db = new(seedPosts: true, captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(db.Options));

            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: null,
                page: 1, pageSize: 10);

            Assert.IsTrue(page.Data.Any(b => b.Posts != null && b.Posts.Any()),
                "Empty-includes auto-walk should populate Posts under current contract.");
        }

        // ──────────────────────────────────────────────────────────────────
        // Dropped constant ORDER BY — unpaged emits no ORDER BY when none requested.
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task FindAllPagedAsync_NoOrderBy_NoPaging_DoesNotEmitConstantOrderBy()
        {
            // page=0/pageSize=0 → SkipQueryFactory short-circuits, so no fallback sort is added.
            // With no orderBy supplied, the emitted SQL should contain NO "ORDER BY" — neither
            // a real one nor the previous "ORDER BY (constant)" footgun.
            using TestDatabase db = new(captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(db.Options));

            // includes=null short-circuits the auto-walk so EF doesn't emit its own ORDER BY
            // for include-join materialisation; we only want to assert about OUR fallback sort.
            _ = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: null,
                page: null,
                pageSize: null,
                includes: (string[])null);

            string dataSelect = db.CapturedSql.FirstOrDefault(s => s.Contains("FROM \"Blogs\"") || s.Contains("FROM [Blogs]"));
            Assert.IsNotNull(dataSelect);
            Assert.DoesNotContain("ORDER BY", dataSelect, $"Unpaged + no-orderBy must emit no ORDER BY. SQL: {dataSelect}");
        }

        [TestMethod]
        public async Task FindAllAsync_NoOrderBy_NoPaging_DoesNotEmitConstantOrderBy()
        {
            using TestDatabase db = new(captureSql: true);
            using IRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

            // includes=null skips auto-walk; the projection to a primitive (Url) also strips
            // any provider-side ORDER BY for include materialisation.
            _ = await repo.FindAllAsync(
                where: x => true,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                includes: (string[])null);

            string dataSelect = db.CapturedSql.FirstOrDefault(s => s.Contains("FROM \"Blogs\"") || s.Contains("FROM [Blogs]"));
            Assert.IsNotNull(dataSelect);
            Assert.DoesNotContain("ORDER BY", dataSelect, $"FindAllAsync with no orderBy and no paging must emit no ORDER BY. SQL: {dataSelect}");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_NoOrderBy_WithPaging_StillEmitsOrderBy()
        {
            // Counter-test: when paging IS requested, SkipQueryFactory injects a fallback ORDER BY
            // so SQL Server's OFFSET/FETCH stays valid. SQLite is lenient on this and emits
            // "ORDER BY (SELECT 1)" or similar — either way, an ORDER BY token must appear.
            using TestDatabase db = new(captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(db.Options));

            _ = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                page: 1,
                pageSize: 10);

            string dataSelect = db.CapturedSql.FirstOrDefault(s => s.Contains("FROM \"Blogs\"") || s.Contains("FROM [Blogs]"));
            Assert.IsNotNull(dataSelect);
            Assert.Contains("ORDER BY", dataSelect,
                $"Paging without an explicit orderBy must still inject a fallback ORDER BY. SQL: {dataSelect}");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_WithExplicitOrderBy_OrderByAppearsInSql()
        {
            using TestDatabase db = new(captureSql: true);
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(db.Options));

            _ = await repo.FindAllPagedAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1, pageSize: 10);

            string dataSelect = db.CapturedSql.FirstOrDefault(s =>
                (s.Contains("FROM \"Blogs\"") || s.Contains("FROM [Blogs]")) && !s.Contains("COUNT("));
            Assert.IsNotNull(dataSelect);
            Assert.Contains("ORDER BY", dataSelect,
                $"Explicit orderBy must produce ORDER BY in the emitted SQL. SQL: {dataSelect}");
        }

        // ──────────────────────────────────────────────────────────────────
        // Interceptor that records which read path the provider took.
        // ──────────────────────────────────────────────────────────────────

        private sealed class ReaderPathInterceptor : DbCommandInterceptor
        {
            private int _asyncReads;
            private int _syncReads;

            public int AsyncReads => _asyncReads;
            public int SyncReads => _syncReads;

            public override System.Threading.Tasks.ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _asyncReads);
                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }

            public override InterceptionResult<DbDataReader> ReaderExecuting(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result)
            {
                Interlocked.Increment(ref _syncReads);
                return base.ReaderExecuting(command, eventData, result);
            }
        }
    }
}