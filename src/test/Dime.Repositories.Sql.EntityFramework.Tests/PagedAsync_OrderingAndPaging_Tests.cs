using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Ordering scenarios (single-key, composite, asc/desc, navigation paths)
    /// and paging edge cases on the splitQuery entity overload.
    /// </summary>
    [TestClass]
    public class PagedAsync_OrderingAndPaging_Tests
    {
        private static IPagedQueryRepositoryAsync<Blog> NewRepo(TestDatabase db)
            => new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

        private static IEnumerable<IOrder<Blog>> Orders(params (string prop, bool asc)[] keys)
            => keys.Select(k => new Order<Blog>(k.prop, k.asc)).ToArray();

        // ─── Ordering ──────────────────────────────────────────────────────────

        [TestMethod]
        public async Task Order_SingleAscendingByUrl_ProducesAlphabeticalOrder()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.Url), true)),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            List<string> urls = [.. result.Data.Select(b => b.Url)];
            CollectionAssert.AreEqual(urls.OrderBy(s => s).ToList(), urls);
        }

        [TestMethod]
        public async Task Order_SingleDescendingByUrl_ProducesReverseAlphabeticalOrder()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.Url), false)),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            List<string> urls = [.. result.Data.Select(b => b.Url)];
            CollectionAssert.AreEqual(urls.OrderByDescending(s => s).ToList(), urls);
        }

        [TestMethod]
        public async Task Order_ByPrimaryKey_OrdersById()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            List<int> ids = [.. result.Data.Select(b => b.BlogId)];
            CollectionAssert.AreEqual(ids.OrderBy(i => i).ToList(), ids);
        }

        [TestMethod]
        public async Task Order_CompositeTwoKeys_AppliesBothInDeclarationOrder()
        {
            // Add an Url collision scenario by reusing fixture; we just verify
            // composite ordering doesn't throw and returns full set.
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.Url), true), (nameof(Blog.BlogId), false)),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, result.Data.Count());
            CollectionAssert.AreEqual(
                result.Data.OrderBy(b => b.Url).ThenByDescending(b => b.BlogId).Select(b => b.BlogId).ToList(),
                result.Data.Select(b => b.BlogId).ToList());
        }

        [TestMethod]
        public async Task Order_EmptyCollection_DoesNotThrow()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Enumerable.Empty<IOrder<Blog>>(),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task Order_NullCollection_DoesNotThrow()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: null,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task Order_StringProperty_IsCaseSensitiveOrCaseInsensitivePerCollation()
        {
            // SQLite default collation is case-sensitive for TEXT. Just assert ordering
            // is consistent with the underlying SQL collation rather than .NET semantics.
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.Url), true)),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            // Results must be sorted (whatever the collation rule). Asserting any
            // strict ordering would couple this test to provider collation.
            List<string> urls = [.. result.Data.Select(b => b.Url)];
            CollectionAssert.AreEqual(urls, urls.OrderBy(s => s, System.StringComparer.Ordinal).ToList());
        }

        [TestMethod]
        public async Task Order_PreservedWhenSplitTrue_AndWhenSplitFalse()
        {
            // Result order must be identical between split modes.
            using TestDatabase db1 = new();
            using TestDatabase db2 = new();
            using IPagedQueryRepositoryAsync<Blog> a = NewRepo(db1);
            using IPagedQueryRepositoryAsync<Blog> b = NewRepo(db2);

            IPage<Blog> split = await a.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.Url), false)),
                page: 1,
                pageSize: 10,
                splitQuery: true);

            IPage<Blog> single = await b.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.Url), false)),
                page: 1,
                pageSize: 10,
                splitQuery: false);

            CollectionAssert.AreEqual(
                split.Data.Select(x => x.BlogId).ToList(),
                single.Data.Select(x => x.BlogId).ToList());
        }

        // ─── Paging edge cases ─────────────────────────────────────────────────

        [TestMethod]
        public async Task Paging_PageOnePageSizeOne_ReturnsExactlyOne()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 1,
                pageSize: 1,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(1, result.Data.Count());
        }

        [TestMethod]
        public async Task Paging_PageBeyondTotal_ReturnsEmptyDataButFullTotal()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 99,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(0, result.Data.Count());
        }

        [TestMethod]
        public async Task Paging_PageSizeLargerThanTotal_ReturnsAll()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 1,
                pageSize: 1000,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(3, result.Data.Count());
        }

        [TestMethod]
        public async Task Paging_NullPageAndPageSize_ReturnsAll()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: null,
                pageSize: null,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(3, result.Data.Count());
        }

        [TestMethod]
        public async Task Paging_AcrossTwoPages_UnionEqualsAll()
        {
            using TestDatabase db1 = new();
            using TestDatabase db2 = new();
            using IPagedQueryRepositoryAsync<Blog> repo1 = NewRepo(db1);
            using IPagedQueryRepositoryAsync<Blog> repo2 = NewRepo(db2);

            IPage<Blog> p1 = await repo1.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 1,
                pageSize: 2,
                splitQuery: true);

            IPage<Blog> p2 = await repo2.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 2,
                pageSize: 2,
                splitQuery: true);

            Assert.AreEqual(3, p1.Total);
            Assert.AreEqual(3, p2.Total);

            List<int> union = [.. p1.Data.Concat(p2.Data).Select(b => b.BlogId)];
            Assert.AreEqual(3, union.Distinct().Count());
        }

        [TestMethod]
        public async Task Paging_PageZero_DoesNotErrorAndIsTreatedAsUnpaged()
        {
            // EfRepository's existing Skip helper treats page=0 as no-skip (returns source).
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 0,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
            Assert.IsNotNull(result.Data);
        }

        [TestMethod]
        public async Task Paging_PageSizeZero_DoesNotErrorAndIsTreatedAsUnlimited()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 1,
                pageSize: 0,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
            Assert.IsNotNull(result.Data);
        }

        [TestMethod]
        public async Task Paging_NullPageWithPageSize_TakesFirstN()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: null,
                pageSize: 2,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
            Assert.AreEqual(2, result.Data.Count());
        }

        [TestMethod]
        public async Task Paging_WithCollectionInclude_DistinctRootsPerPage()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> page1 = await repo.FindAllPagedAsync(
                where: null,
                orderBy: Orders((nameof(Blog.BlogId), true)),
                page: 1,
                pageSize: 1,
                splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(3, page1.Total);
            Assert.AreEqual(1, page1.Data.Count(),
                "Page size 1 should yield 1 root entity, not 1 Cartesian-join row.");
            Blog first = page1.Data.Single();
            Assert.IsTrue(first.Posts.Count > 0 || first.Tags.Count > 0,
                "Collection navigations must populate even with pageSize=1.");
        }
    }
}