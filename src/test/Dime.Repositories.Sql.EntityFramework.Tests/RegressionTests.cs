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
