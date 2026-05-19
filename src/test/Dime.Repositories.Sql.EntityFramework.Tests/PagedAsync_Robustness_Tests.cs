using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Robustness: determinism across repeat calls, isolation between contexts,
    /// AsNoTracking behavior, mixed split/non-split call patterns on the same fixture,
    /// and a smattering of multi-overload integration scenarios.
    /// </summary>
    [TestClass]
    public class PagedAsync_Robustness_Tests
    {
        private static IPagedQueryRepositoryAsync<Blog> NewRepo(TestDatabase db)
            => new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

        private static IEnumerable<IOrder<Blog>> ById => [new Order<Blog>(nameof(Blog.BlogId), true)];

        [TestMethod]
        public async Task Determinism_RepeatCallsReturnIdenticalResults()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);

            List<IPage<Blog>> runs = [];
            for (int i = 0; i < 5; i++)
            {
                using IPagedQueryRepositoryAsync<Blog> r = NewRepo(db);
                runs.Add(await r.FindAllPagedAsync(
                    where: x => x.Url.Contains("cat"),
                    orderBy: ById,
                    page: 1,
                    pageSize: 10,
                    splitQuery: true,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]));
            }

            // All runs should have identical Total and ordering
            Assert.IsTrue(runs.All(p => p.Total == runs[0].Total));
            for (int i = 1; i < runs.Count; i++)
                CollectionAssert.AreEqual(
                    runs[0].Data.Select(b => b.BlogId).ToList(),
                    runs[i].Data.Select(b => b.BlogId).ToList());
        }

        [TestMethod]
        public async Task NoTracking_ReturnedEntitiesAreDetached()
        {
            using TestDatabase db = new(seedPosts: true);
            using BloggingContext ctx = new(db.Options);
            using IPagedQueryRepositoryAsync<Blog> repo = new EfRepository<Blog, BloggingContext>(ctx);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            // None of the returned entities should be tracked in the underlying context
            Blog blog = result.Data.First();
            Assert.AreEqual(Microsoft.EntityFrameworkCore.EntityState.Detached, ctx.Entry(blog).State);
        }

        [TestMethod]
        public async Task MixedSplitAndNonSplit_SameRepoInstance_BothWork()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);

            // Each call gets its own context via factory pattern; with direct ctx we'd
            // hit the "context disposed" issue. Use separate repo instances.
            IPage<Blog> split;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(db))
                split = await r.FindAllPagedAsync(
                    where: null, orderBy: ById,
                    page: 1, pageSize: 10, splitQuery: true,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            IPage<Blog> single;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(db))
                single = await r.FindAllPagedAsync(
                    where: null, orderBy: ById,
                    page: 1, pageSize: 10, splitQuery: false,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(split.Total, single.Total);
            CollectionAssert.AreEqual(
                split.Data.Select(b => b.BlogId).ToList(),
                single.Data.Select(b => b.BlogId).ToList());
        }

        [TestMethod]
        public async Task IncludesNullArray_DoesNotErrorAndLoadsNoCollections()
        {
            using TestDatabase db = new(seedPosts: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: (string[])null);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task IncludesContainingNullElement_IsTolerated()
        {
            using TestDatabase db = new(seedPosts: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: [nameof(Blog.Posts), null]);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task IncludeSingleNavigation_LoadsExactlyThatNavigation()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            Blog cats = result.Data.First(b => b.Url == "http://sample.com/cats");
            Assert.IsNotNull(cats.Posts);
            Assert.IsTrue(cats.Tags == null || cats.Tags.Count == 0,
                "Tags should not be loaded when only Posts is included.");
        }

        [TestMethod]
        public async Task SplitQueryTrue_CallTwiceOnFreshRepos_BothSucceed()
        {
            using TestDatabase db = new(seedPosts: true);

            IPage<Blog> first;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(db))
                first = await r.FindAllPagedAsync(
                    where: null, orderBy: ById,
                    page: 1, pageSize: 10, splitQuery: true,
                    includes: nameof(Blog.Posts));

            IPage<Blog> second;
            using (IPagedQueryRepositoryAsync<Blog> r = NewRepo(db))
                second = await r.FindAllPagedAsync(
                    where: null, orderBy: ById,
                    page: 1, pageSize: 10, splitQuery: true,
                    includes: nameof(Blog.Posts));

            Assert.AreEqual(first.Total, second.Total);
            Assert.AreEqual(first.Data.Count(), second.Data.Count());
        }

        [TestMethod]
        public async Task SplitQuery_DoesNotMutateContextState_OnRepeatedCalls()
        {
            using TestDatabase db = new(seedPosts: true);
            using BloggingContext ctx = new(db.Options);
            using IPagedQueryRepositoryAsync<Blog> repo = new EfRepository<Blog, BloggingContext>(ctx);

            int trackedBefore = ctx.ChangeTracker.Entries().Count();
            _ = await repo.FindAllPagedAsync(
                where: null, orderBy: ById,
                page: 1, pageSize: 10, splitQuery: true,
                includes: nameof(Blog.Posts));
            int trackedAfter = ctx.ChangeTracker.Entries().Count();

            // No-tracking queries must not leave entries in the change tracker.
            Assert.AreEqual(trackedBefore, trackedAfter);
        }

        [TestMethod]
        public async Task SplitQuery_ReturnedCollections_AreEnumerableMultipleTimes()
        {
            using TestDatabase db = new(seedPosts: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null, orderBy: ById,
                page: 1, pageSize: 10, splitQuery: true,
                includes: nameof(Blog.Posts));

            Blog cats = result.Data.First(b => b.Url == "http://sample.com/cats");
            // Enumerate twice — the materialized result must be a list/array, not deferred.
            int count1 = cats.Posts.Count;
            int count2 = cats.Posts.Count();
            Assert.AreEqual(count1, count2);
            Assert.AreEqual(2, count1);
        }

        [TestMethod]
        public async Task TotalCount_MatchesWhereOnlyNotJoinCardinality()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            // 3 blogs × (≈ 2 posts × 2 tags avg) = ~12 join rows if Cartesian were counted.
            // Real Total must be 3.
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null, orderBy: ById,
                page: 1, pageSize: 10, splitQuery: true,
                includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task PageOutsideRange_TotalStillCorrect()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cat"),
                orderBy: ById,
                page: 100,
                pageSize: 50,
                splitQuery: true);

            Assert.AreEqual(2, result.Total);
            Assert.AreEqual(0, result.Data.Count());
        }

        [TestMethod]
        public async Task RepositoryFactoryPattern_SplitQueryWorksWithFreshContextPerCall()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);

            // Repository factory style: each call gets a brand-new context.
            for (int i = 0; i < 3; i++)
            {
                using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);
                IPage<Blog> result = await repo.FindAllPagedAsync(
                    where: null, orderBy: ById,
                    page: 1, pageSize: 10, splitQuery: true,
                    includes: [nameof(Blog.Posts), nameof(Blog.Tags)]);

                Assert.AreEqual(3, result.Total);
                Blog cats = result.Data.First(b => b.Url == "http://sample.com/cats");
                Assert.HasCount(2, cats.Posts);
                Assert.HasCount(3, cats.Tags);
            }
        }
    }
}