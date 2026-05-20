using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Pins the four behavioural bugs surfaced during code review so they cannot regress:
    ///   1. QueryFunctionAsync without a projection and TResult != TEntity throws a *clear* exception
    ///      instead of the latent InvalidCastException from <c>(IEnumerable&lt;TResult&gt;)</c>.
    ///   2. FindAllPagedAsync split-query overload with <c>count = null</c> falls back to <c>where</c>
    ///      rather than counting the unfiltered set.
    ///   3. FindAllPagedAsync split-query overload with <c>where = null</c> does not throw an
    ///      ArgumentNullException from <c>Queryable.Count(predicate)</c>.
    ///   4. Factory-backed repositories do not mint an extra DbContext when including navigations
    ///      (old code referenced the <c>Context</c> property inside <c>Include(...)</c> instead of
    ///      the locally captured <c>ctx</c>).
    /// </summary>
    [TestClass]
    public class RegressionTests
    {
        // ──────────────────────────────────────────────────────────────────
        // Fix 1 — QueryFunctionAsync: clear exception when TResult != TEntity and select is null.
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task QueryFunctionAsync_NoProjection_TResultMismatch_ThrowsInvalidOperation()
        {
            using TestDatabase testDb = new();
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repo.QueryFunctionAsync<BlogDto>("Func"));

            Assert.Contains(nameof(BlogDto), ex.Message);
            Assert.Contains(nameof(Blog), ex.Message);
            Assert.Contains("select", ex.Message);
        }

        [TestMethod]
        public async Task QueryFunctionAsync_NoProjection_TResultMismatch_DoesNotThrowInvalidCast()
        {
            using TestDatabase testDb = new();
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            Exception thrown = null;
            try
            {
                await repo.QueryFunctionAsync<BlogDto>("Func");
            }
            catch (Exception ex)
            {
                thrown = ex;
            }

            Assert.IsNotNull(thrown);
            Assert.IsNotInstanceOfType(thrown, typeof(InvalidCastException),
                "Should fail validation before issuing SQL, not at the cast.");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_NoProjection_TResultMatchesTEntity_IsAllowed()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            // SQLite cannot run a TVF, but if we got past validation we should fail at the DB,
            // not at the type guard. Anything that is not InvalidOperationException is acceptable.
            try
            {
                await repo.QueryFunctionAsync<Blog>("Func");
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail($"Type guard fired incorrectly for matching TResult: {ex.Message}");
            }
            catch
            {
                // expected — SQLite trips
            }
        }

        [TestMethod]
        public async Task QueryFunctionAsync_WithProjection_TResultMismatch_IsAllowed()
        {
            // With an explicit projection, the type guard must NOT fire — the projection handles
            // the conversion. We still expect a SQLite error because the TVF does not exist.
            using TestDatabase testDb = new();
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            try
            {
                await repo.QueryFunctionAsync<BlogDto>("Func", "dbo", null, x => new BlogDto { Url = x.Url });
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail($"Type guard fired even though a projection was supplied: {ex.Message}");
            }
            catch
            {
                // expected — SQLite trips
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // Fix 2 — count parameter falls back to where when null.
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQuery_NullCount_UsesWhereForTotal()
        {
            // Seeded data has 3 blogs, 2 of which contain "cat".
            // With count=null the total should fall back to applying the WHERE predicate,
            // yielding 2, NOT the unfiltered 3.
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                count: null,
                select: x => x.Url,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                groupBy: null,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(2, page.Total, "Total should respect the WHERE filter when count is null.");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQuery_ExplicitCount_OverridesWhere()
        {
            // When the caller provides an explicit count predicate it should be used verbatim.
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: x => x.Url.Contains("cat"),
                count: x => true,
                select: x => x.Url,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                groupBy: null,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, page.Total, "Explicit count predicate must override the WHERE-based fallback.");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQuery_NullCountAndNullWhere_CountsAll()
        {
            // count=null && where=null → no predicate at all → count all rows.
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: null,
                count: null,
                select: x => x.Url,
                orderBy: (IEnumerable<IOrder<Blog>>)null,
                groupBy: null,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, page.Total);
        }

        // ──────────────────────────────────────────────────────────────────
        // Fix 3 — split-query overload tolerates null where.
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQuery_NullWhere_DoesNotThrow()
        {
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: null,
                select: x => x.Url,
                orderBy: (Expression<Func<Blog, dynamic>>)null,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, page.Total, "Total with null WHERE should equal the unfiltered seed count (3).");
            Assert.AreEqual(3, page.Data.Count());
        }

        [TestMethod]
        public async Task FindAllPagedAsync_SplitQuery_NullWhere_NullOrderBy_DoesNotThrow()
        {
            // Bare-bones invocation: null where, null orderBy. Must not NRE on Count(null) or
            // on a null orderBy lambda.
            using TestDatabase testDb = new();
            using EfRepository<Blog, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            IPage<string> page = await repo.FindAllPagedAsync<string>(
                where: null,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            Assert.AreEqual(3, page.Total);
        }

        // ──────────────────────────────────────────────────────────────────
        // Fix 4 — factory-backed repository: paged async mints two contexts (data + count run
        // in parallel via Task.WhenAll), and the Include path does NOT mint a third.
        // The sync paged path still mints exactly one (no parallelism possible synchronously).
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task FindAllPagedAsync_FactoryBacked_MintsTwoContexts_OneForDataOneForCount()
        {
            using TestDatabase testDb = new(seedPosts: true);
            CountingContextFactory factory = new(testDb.Options);
            using EfRepository<Blog, BloggingContext> repo = new(factory, new RepositoryConfiguration());

            IPage<Blog> page = await repo.FindAllPagedAsync<Blog>(
                where: x => true,
                select: x => x,
                orderBy: (Expression<Func<Blog, dynamic>>)(x => x.BlogId),
                ascending: true,
                page: 1,
                pageSize: 10,
                includes: nameof(Blog.Posts));

            Assert.IsNotNull(page);
            Assert.AreEqual(2, factory.CreatedCount,
                $"Factory mode runs data + count in parallel → exactly two contexts (data, count). " +
                $"Crucially, Include must reuse the data context — anything more than 2 means the " +
                $"Include path is minting a third context again. Factory minted {factory.CreatedCount}.");
        }

        [TestMethod]
        public void FindAllPaged_FactoryBacked_Sync_DoesNotMintExtraContextForInclude()
        {
            using TestDatabase testDb = new(seedPosts: true);
            CountingContextFactory factory = new(testDb.Options);
            using EfRepository<Blog, BloggingContext> repo = new(factory, new RepositoryConfiguration());

            Page<Blog> page = repo.FindAllPaged<Blog>(
                where: x => true,
                select: x => x,
                orderBy: (Expression<Func<Blog, dynamic>>)(x => x.BlogId),
                ascending: true,
                page: 1,
                pageSize: 10,
                includes: nameof(Blog.Posts));

            Assert.IsNotNull(page);
            Assert.AreEqual(1, factory.CreatedCount,
                $"Sync overload runs sequentially on one context (parallelism is async-only). " +
                $"Factory minted {factory.CreatedCount}.");
        }

        [TestMethod]
        public async Task FindAllPagedAsync_NonFactory_RunsSequentiallyOnSharedContext()
        {
            // Without a factory, there is only one DbContext; EF Core forbids concurrent ops
            // on it, so the helper must fall back to running data + count sequentially.
            using TestDatabase testDb = new();
            using BloggingContext ctx = new(testDb.Options);
            using EfRepository<Blog, BloggingContext> repo = new(ctx);

            IPage<Blog> page = await repo.FindAllPagedAsync<Blog>(
                where: x => true,
                select: x => x,
                orderBy: (Expression<Func<Blog, dynamic>>)(x => x.BlogId),
                ascending: true,
                page: 1,
                pageSize: 10);

            Assert.AreEqual(3, page.Total);
            // The shared context must still be usable after the call — the helper does not own
            // its lifetime, so it must not be disposed inside the paged method.
            Assert.IsNotNull(ctx.ChangeTracker,
                "Non-factory: the constructor-injected ctx must survive a paged call.");
        }

        // ──────────────────────────────────────────────────────────────────
        // Concurrency safety — factory-backed repos must tolerate the
        // "fire several tasks, await later via Task.WhenAll" pattern without
        // tripping EF's "second operation was started on this context" error.
        // ──────────────────────────────────────────────────────────────────

        [TestMethod]
        public async Task ManyConcurrentFindAllPagedAsync_FactoryBacked_NoSecondOperationError()
        {
            // 16 concurrent paged calls on a single factory-backed repo. The factory mints a
            // fresh context per Context-property access, so each call (and each parallel
            // data+count pair inside it) operates on isolated contexts. EF's
            // "A second operation was started on this context" would surface as an
            // InvalidOperationException; this test would crash if the isolation slipped.
            using TestDatabase db = new();
            CountingContextFactory factory = new(db.Options);
            using EfRepository<Blog, BloggingContext> repo = new(factory, new RepositoryConfiguration());

            Task<IPage<Blog>>[] tasks = [.. Enumerable.Range(0, 16).Select(_ =>
                repo.FindAllPagedAsync(
                    where: x => true,
                    orderBy: (Expression<Func<Blog, dynamic>>)(x => x.BlogId),
                    ascending: true,
                    page: 1,
                    pageSize: 10))];

            IPage<Blog>[] results = await Task.WhenAll(tasks);

            Assert.IsTrue(results.All(p => p.Total == 3),
                "Every concurrent paged call must return the full seed (3) without EF concurrency errors.");
            Assert.AreEqual(16 * 2, factory.CreatedCount,
                "Each paged call mints 2 contexts (data + count). 16 calls → 32 contexts total. " +
                $"Saw {factory.CreatedCount} — anything else means contexts are being shared across calls.");
        }

        [TestMethod]
        public async Task FireAndAwaitLater_FactoryBacked_IsSafe()
        {
            // The canonical "kick off, do other work, await later" pattern the user worried about:
            //     var t1 = repo.X(); var t2 = repo.Y(); await Task.WhenAll(t1, t2);
            // In factory mode each repo call mints a fresh context, so the pending tasks can
            // legitimately overlap.
            using TestDatabase db = new(seedPosts: true);
            CountingContextFactory factory = new(db.Options);
            using EfRepository<Blog, BloggingContext> repo = new(factory, new RepositoryConfiguration());

            Task<IEnumerable<string>> t1 = repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: null,
                pageSize: null);

            Task<IPage<Blog>> t2 = repo.FindAllPagedAsync(
                where: x => true,
                orderBy: (Expression<Func<Blog, dynamic>>)(x => x.BlogId),
                ascending: true,
                page: 1,
                pageSize: 10);

            await Task.WhenAll(t1, t2);

            Assert.AreEqual(3, t1.Result.Count(),
                "FindAllAsync result must materialise correctly even when awaited late.");
            Assert.AreEqual(3, t2.Result.Total,
                "FindAllPagedAsync result must materialise correctly even when awaited late.");
        }

        [TestMethod]
        public async Task MixedConcurrentOperations_FactoryBacked_DoNotCollide()
        {
            // FindAllAsync (single context per call) interleaved with FindAllPagedAsync
            // (two contexts per call) — every combination must coexist on one repo instance.
            using TestDatabase db = new();
            CountingContextFactory factory = new(db.Options);
            using EfRepository<Blog, BloggingContext> repo = new(factory, new RepositoryConfiguration());

            Task[] tasks =
            [
                repo.FindAllAsync<string>(x => true, x => x.Url, x => x.Url, true, null, null),
                repo.FindAllPagedAsync(where: x => true, orderBy: (Expression<Func<Blog, dynamic>>)(x => x.BlogId), ascending: true, page: 1, pageSize: 10),
                repo.CountAsync(),
                repo.FindAllAsync<string>(x => x.Url.Contains("cat"), x => x.Url, x => x.Url, true, null, null),
                repo.FindAllPagedAsync<string>(where: x => true, select: x => x.Url, orderBy: x => x.Url, ascending: true, page: 1, pageSize: 5),
            ];

            await Task.WhenAll(tasks);

            // We don't pin the exact CreatedCount — different overloads mint a different number
            // of contexts. The point of the test is that NONE of these calls collide.
            Assert.IsGreaterThan(0, factory.CreatedCount);
        }

        // Counting factory — records how many DbContext instances were minted during a test.
        private sealed class CountingContextFactory : IDbContextFactory<BloggingContext>
        {
            private readonly DbContextOptions<BloggingContext> _options;
            private int _count;

            public CountingContextFactory(DbContextOptions<BloggingContext> options) => _options = options;

            public int CreatedCount => _count;

            public BloggingContext CreateDbContext()
            {
                Interlocked.Increment(ref _count);
                return new BloggingContext(_options);
            }
        }
    }

    /// <summary>
    /// Lightweight DTO used by regression tests to deliberately differ from <see cref="Blog"/>.
    /// </summary>
    public class BlogDto
    {
        public string Url { get; set; }
    }
}
