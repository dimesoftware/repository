using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.IntegrationTests
{
    [TestClass]
    public class IntegrationTests
    {
        private static EfRepository<Blog, BloggingContext> NewRepo(List<string> capturedSql = null)
            => new(SqlServerFixture.CreateContext(capturedSql));

        // 1. TVF with a parameter — the headline test SQLite cannot prove.
        [TestMethod]
        public async Task QueryFunctionAsync_AgainstRealTvf_ReturnsExpectedRows()
        {
            using ISqlRepository<Blog> repo = NewRepo();

            // Search for the literal deterministic URL — guaranteed unique under the seed
            // (Bogus URLs are namespaced under https://bogus.example/...).
            DbParameter[] parameters = [new SqlParameter("@needle", "sample.com/catfish")];
            IEnumerable<BlogDto> result = await repo.QueryFunctionAsync(
                "fGetBlogsWithUrlContaining",
                "dbo",
                parameters,
                select: x => new BlogDto { Url = x.Url });

            List<BlogDto> list = [.. result];
            Assert.HasCount(1, list, "Expected exactly one row matching 'sample.com/catfish'.");
            Assert.AreEqual("http://sample.com/catfish", list[0].Url);
        }

        // 2. TVF that uses OPENJSON over an NVARCHAR(MAX) parameter.
        [TestMethod]
        public async Task QueryFunctionAsync_WithOpenJsonParameter_PassesParameterCorrectly()
        {
            using ISqlRepository<Blog> repo = NewRepo();

            string json = "[\"http://sample.com/cats\",\"http://sample.com/dogs\"]";
            DbParameter[] parameters = [new SqlParameter("@urls", System.Data.SqlDbType.NVarChar, -1) { Value = json }];

            IEnumerable<BlogDto> result = await repo.QueryFunctionAsync(
                "fGetBlogsByJsonUrls",
                "dbo",
                parameters,
                select: x => new BlogDto { Url = x.Url });

            List<string> urls = [.. result.Select(r => r.Url).OrderBy(u => u)];
            CollectionAssert.AreEqual(
                new[] { "http://sample.com/cats", "http://sample.com/dogs" },
                urls);
        }

        // 3. Paging emits SQL Server OFFSET/FETCH, not LIMIT.
        [TestMethod]
        public async Task FindAllPagedAsync_OnSqlServer_EmitsOffsetFetch()
        {
            List<string> captured = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(captured);

            _ = await repo.FindAllPagedAsync(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 2,
                pageSize: 2,
                splitQuery: false);

            string dataSql = captured.LastOrDefault(s => s.Contains("FROM [Blogs]") && !s.Contains("COUNT(*)"))
                             ?? string.Join("\n---\n", captured);

            Assert.Contains("OFFSET", dataSql);
            Assert.Contains("FETCH NEXT", dataSql);
            Assert.Contains("ROWS ONLY", dataSql);
        }

        // 4. splitQuery: true with two collection includes emits multiple SELECTs.
        [TestMethod]
        public async Task FindAllPagedAsync_SplitQueryTrue_OnSqlServer_EmitsMultipleSelects()
        {
            List<string> captured = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(captured);

            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => x.Url == "http://sample.com/cats",
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            // Sanity: the result must contain the loaded collections (proves split actually fanned out).
            Blog cats = page.Data.Single();
            Assert.HasCount(3, cats.Posts);
            Assert.HasCount(4, cats.Tags);

            // Split mode must issue MULTIPLE executed-command lines. EF Core's split-query feature
            // emits one query per root + collection nav. Compare against the captured executed
            // command lines (rather than scraping specific table names, which depend on EF's
            // alias formatting).
            int executedCommands = captured.Count(s => s.Contains("Executed DbCommand"));
            Assert.IsGreaterThanOrEqualTo(3,
executedCommands, $"Expected at least 3 executed commands in split mode (root + 2 collections, plus count). Got {executedCommands}.\nCaptured:\n{string.Join("\n", captured)}");
        }

        // 5. String ordering follows SQL Server collation (case-insensitive), not .NET ordinal.
        [TestMethod]
        public async Task WithOrder_StringOrdering_FollowsSqlServerCollation()
        {
            using IRepository<Blog> repo = NewRepo();

            // Filter to the mixed-case seed: "Cats", "cats", "CATS", "Dogs".
            IEnumerable<string> result = await repo.FindAllAsync(
                where: x => x.Url == "Cats" || x.Url == "cats" || x.Url == "CATS" || x.Url == "Dogs",
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true);

            List<string> list = [.. result];
            Assert.HasCount(4, list);

            // Under SQL Server's default case-insensitive collation, all "cats" variants tie
            // and sort BEFORE "Dogs". Under .NET ordinal sort, lowercase "cats" (c=99) would
            // come AFTER "Dogs" (D=68). This is the regression we care about.
            Assert.AreEqual("Dogs", list[3], "SQL Server collation must place 'Dogs' last; ordinal sort would not.");
            CollectionAssert.AreEquivalent(new[] { "Cats", "cats", "CATS" }, list.Take(3).ToList());
        }

        // 6. NULL ordering follows SQL Server rules (NULLs first for ASC).
        [TestMethod]
        public async Task FindAllPagedAsync_NullOrdering_FollowsSqlServerRules()
        {
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo();

            // Restrict to the deterministic seed only (exclude the Bogus bulk corpus).
            IPage<string> page = await repo.FindAllPagedAsync(
                where: x => x.Url.StartsWith("http://sample.com/"),
                select: x => x.Description,
                orderBy: x => x.Description,
                ascending: true,
                page: 1,
                pageSize: 100,
                splitQuery: false);

            List<string> list = [.. page.Data];
            // 3 blogs have URLs starting with "http://sample.com/". catfish has NULL description, cats and dogs have non-null.
            Assert.HasCount(3, list, $"Expected 3 rows. Got: [{string.Join(",", list.Select(s => s ?? "<null>"))}]");
            // SQL Server default for ASC: NULLs sort first.
            Assert.IsNull(list[0], $"Expected NULL first under SQL Server ASC ordering. Got: [{string.Join(",", list.Select(s => s ?? "<null>"))}]");
            Assert.IsNotNull(list[1]);
            Assert.IsNotNull(list[2]);
        }

        // 7. AsNoTracking is honored — returned entities are detached and the change tracker
        // does not gain entries from the repository read.
        [TestMethod]
        public async Task AsNoTracking_OnSqlServer_DoesNotPopulateChangeTracker()
        {
            // Use a factory so the repo's Context property creates a fresh DbContext per call.
            // That lets us hand the same factory's context to the assertion without it being
            // disposed by the repository operation.
            DbContextFactory factory = new();
            using IPagedQueryRepositoryAsync<Blog> repo = new EfRepository<Blog, BloggingContext>(
                factory, new RepositoryConfiguration());

            IPage<Blog> page = await repo.FindAllPagedAsync(
                where: x => true,
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: false);

            Assert.IsTrue(page.Data.Any(), "Expected at least one blog.");

            // Sanity: a fresh ctx is clean. Attaching one of the returned (detached) entities
            // would succeed without a duplicate-key tracker error, proving AsNoTracking returned
            // a detached entity.
            using BloggingContext verifyCtx = SqlServerFixture.CreateContext();
            Blog first = page.Data.First();
            verifyCtx.Blogs.Attach(first);
            Assert.AreEqual(Microsoft.EntityFrameworkCore.EntityState.Unchanged,
                verifyCtx.Entry(first).State,
                "Entity returned by repository should be detached and attachable cleanly.");
        }

        // Inline IDbContextFactory used solely to drive Test 7's fresh-ctx-per-call pattern.
        private sealed class DbContextFactory : IDbContextFactory<BloggingContext>
        {
            public BloggingContext CreateDbContext() => SqlServerFixture.CreateContext();
        }

        // 8. Split-query correctly loads BOTH collections (the actual reason split exists).
        [TestMethod]
        public async Task SplitQuery_OnSqlServer_LoadsCollectionsCorrectlyAcrossSeparateQueries()
        {
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo();

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url == "http://sample.com/cats",
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Blog cats = result.Data.Single();
            Assert.IsNotNull(cats.Posts);
            Assert.IsNotNull(cats.Tags);
            Assert.HasCount(3, cats.Posts, "Expected 3 Posts seeded for the cats blog.");
            Assert.HasCount(4, cats.Tags, "Expected 4 Tags seeded for the cats blog.");
        }

        // 9. TVF with no parameters.
        [TestMethod]
        public async Task QueryFunctionAsync_TvfWithNoParameters_Works()
        {
            using ISqlRepository<Blog> repo = NewRepo();

            IEnumerable<BlogDto> result = await repo.QueryFunctionAsync(
                "fGetAllBlogs",
                "dbo",
                parameters: null,
                select: x => new BlogDto { Url = x.Url });

            List<BlogDto> list = [.. result];
            // Deterministic seed + Bogus bulk corpus.
            Assert.HasCount(SqlServerFixture.TotalBlogCount, list,
                $"Expected {SqlServerFixture.TotalBlogCount} blogs from parameterless TVF, got {list.Count}.");
        }

        // ─── Bulk-corpus tests (exercise the Bogus-seeded volume) ────────────

        // 11. Paging through hundreds of bulk-seeded blogs in SQL.
        [TestMethod]
        public async Task Bulk_Paging_DistinctRootsAcrossPages_NoOverlap()
        {
            IPage<Blog> page1;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo())
                page1 = await r.FindAllPagedAsync(
                    where: x => x.Url.StartsWith("https://bogus.example/"),
                    select: x => x,
                    orderBy: x => x.BlogId,
                    ascending: true,
                    page: 1,
                    pageSize: 50,
                    splitQuery: false);

            IPage<Blog> page5;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo())
                page5 = await r.FindAllPagedAsync(
                    where: x => x.Url.StartsWith("https://bogus.example/"),
                    select: x => x,
                    orderBy: x => x.BlogId,
                    ascending: true,
                    page: 5,
                    pageSize: 50,
                    splitQuery: false);

            Assert.AreEqual(SqlServerFixture.BulkSummary.BlogCount, page1.Total);
            Assert.HasCount(50, page1.Data);
            Assert.HasCount(50, page5.Data);

            HashSet<int> page1Ids = [.. page1.Data.Select(b => b.BlogId)];
            HashSet<int> page5Ids = [.. page5.Data.Select(b => b.BlogId)];
            Assert.IsFalse(page1Ids.Overlaps(page5Ids),
                "Pages 1 and 5 must contain disjoint sets of blog IDs.");
        }

        // 12. Bulk split-vs-no-split: data parity on a realistic-volume query.
        [TestMethod]
        public async Task Bulk_SplitTrue_Equivalent_ToNoSplit_OnRealisticVolume()
        {
            using IPagedQueryRepositoryAsync<Blog> repoSingle = NewRepo();
            IPage<Blog> single = await repoSingle.FindAllPagedAsync(
                where: x => x.Url.StartsWith("https://bogus.example/"),
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 100,
                splitQuery: false,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            using IPagedQueryRepositoryAsync<Blog> repoSplit = NewRepo();
            IPage<Blog> split = await repoSplit.FindAllPagedAsync(
                where: x => x.Url.StartsWith("https://bogus.example/"),
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 100,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(single.Total, split.Total);
            Assert.HasCount(single.Data.Count(), split.Data);

            List<Blog> singleList = [.. single.Data];
            List<Blog> splitList = [.. split.Data];
            for (int i = 0; i < singleList.Count; i++)
            {
                Assert.AreEqual(singleList[i].BlogId, splitList[i].BlogId);
                Assert.AreEqual(singleList[i].Posts?.Count ?? 0, splitList[i].Posts?.Count ?? 0,
                    $"Posts count mismatch for blog {singleList[i].BlogId} (URL: {singleList[i].Url})");
                Assert.AreEqual(singleList[i].Tags?.Count ?? 0, splitList[i].Tags?.Count ?? 0,
                    $"Tags count mismatch for blog {singleList[i].BlogId} (URL: {singleList[i].Url})");
            }
        }

        // 13. Bulk split mode actually issues separate data SQL commands.
        [TestMethod]
        public async Task Bulk_SplitTrue_EmitsMoreDataCommandsThanSingle()
        {
            List<string> splitSql = new();
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(splitSql))
                _ = await r.FindAllPagedAsync(
                    where: x => x.Url.StartsWith("https://bogus.example/"),
                    select: x => x,
                    orderBy: x => x.BlogId,
                    ascending: true,
                    page: 1,
                    pageSize: 100,
                    splitQuery: true,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            List<string> singleSql = new();
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(singleSql))
                _ = await r.FindAllPagedAsync(
                    where: x => x.Url.StartsWith("https://bogus.example/"),
                    select: x => x,
                    orderBy: x => x.BlogId,
                    ascending: true,
                    page: 1,
                    pageSize: 100,
                    splitQuery: false,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            int splitDataCommands = splitSql.Count(s => s.Contains("Executed DbCommand") && !s.Contains("COUNT(*)"));
            int singleDataCommands = singleSql.Count(s => s.Contains("Executed DbCommand") && !s.Contains("COUNT(*)"));

            Assert.IsGreaterThan(singleDataCommands, splitDataCommands,
                $"Split mode should issue more data commands than single mode on a bulk query. Split={splitDataCommands}, Single={singleDataCommands}");
        }

        // 14. Bulk corpus: TVF returns the full universe (deterministic + bulk).
        [TestMethod]
        public async Task Bulk_QueryFunctionAsync_ReturnsAllBlogs()
        {
            using ISqlRepository<Blog> repo = NewRepo();

            IEnumerable<BlogDto> result = await repo.QueryFunctionAsync(
                "fGetAllBlogs",
                "dbo",
                parameters: null,
                select: x => new BlogDto { Url = x.Url });

            Assert.HasCount(SqlServerFixture.TotalBlogCount, result.ToList());
        }

        // 15. Bulk corpus: page with collection nav filter (Posts.Any) still distinguishes
        // distinct roots and counts correctly under SQL Server.
        [TestMethod]
        public async Task Bulk_WhereOnNavCollection_FiltersDistinctRoots()
        {
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo();

            // Blogs (from the bulk corpus) that have at least one Post.
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.StartsWith("https://bogus.example/") && x.Posts.Any(),
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 100,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            // Total is the count of distinct roots matching the predicate, not the JOIN cardinality.
            Assert.IsGreaterThan(0, result.Total);
            Assert.IsLessThanOrEqualTo(SqlServerFixture.BulkSummary.BlogCount, result.Total);
            Assert.IsTrue(result.Data.All(b => b.Posts != null && b.Posts.Count > 0),
                "Every returned blog should have at least one Post (split-query must populate the included collection).");
        }

        // 10. splitQuery true vs false produce equivalent shape/data.
        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithMultipleIncludes_OnSqlServer_Equivalent()
        {
            using IRepository<Blog> repoSingle = NewRepo();
            IEnumerable<Blog> single = await repoSingle.FindAllAsync(
                where: x => x.Url == "http://sample.com/cats",
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: false,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            using IRepository<Blog> repoSplit = NewRepo();
            IEnumerable<Blog> split = await repoSplit.FindAllAsync(
                where: x => x.Url == "http://sample.com/cats",
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            List<Blog> singleList = [.. single];
            List<Blog> splitList = [.. split];

            Assert.HasCount(singleList.Count, splitList);
            for (int i = 0; i < singleList.Count; i++)
            {
                Assert.AreEqual(singleList[i].BlogId, splitList[i].BlogId);
                Assert.AreEqual(singleList[i].Url, splitList[i].Url);
                Assert.AreEqual(singleList[i].Posts?.Count ?? 0, splitList[i].Posts?.Count ?? 0,
                    $"Posts count mismatch for blog {singleList[i].BlogId}");
                Assert.AreEqual(singleList[i].Tags?.Count ?? 0, splitList[i].Tags?.Count ?? 0,
                    $"Tags count mismatch for blog {singleList[i].BlogId}");
            }
        }
    }
}
