using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Real-world where-predicate shapes against the splitQuery entity overload.
    /// </summary>
    [TestClass]
    public class PagedAsync_WhereScenarios_Tests
    {
        private static IPagedQueryRepositoryAsync<Blog> NewRepo(TestDatabase db)
            => new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

        private static IEnumerable<IOrder<Blog>> ById => new[] { new Order<Blog>(nameof(Blog.BlogId), true) };

        [TestMethod]
        public async Task Where_Equality_OnUrl_FiltersToSingleMatch()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url == "http://sample.com/cats",
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(1, result.Total);
            Assert.AreEqual("http://sample.com/cats", result.Data.Single().Url);
        }

        [TestMethod]
        public async Task Where_StringContains_FiltersToSubstringMatches()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cat"),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(2, result.Total);
            Assert.IsTrue(result.Data.All(b => b.Url.Contains("cat")));
        }

        [TestMethod]
        public async Task Where_StringStartsWith_FiltersByPrefix()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.StartsWith("http://sample.com/cat"),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(2, result.Total);
        }

        [TestMethod]
        public async Task Where_StringEndsWith_FiltersBySuffix()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.EndsWith("dogs"),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(1, result.Total);
        }

        [TestMethod]
        public async Task Where_ComposedAnd_AppliesBothPredicates()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cat") && x.Url.EndsWith("s"),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            // matches "cats", not "catfish"
            Assert.AreEqual(1, result.Total);
            Assert.AreEqual("http://sample.com/cats", result.Data.Single().Url);
        }

        [TestMethod]
        public async Task Where_ComposedOr_AppliesEitherPredicate()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("dogs") || x.Url.Contains("fish"),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(2, result.Total);
        }

        [TestMethod]
        public async Task Where_NavCollectionAny_FiltersByChildExistence()
        {
            using TestDatabase db = new(seedPosts: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Posts.Any(p => p.Title.Contains("Catfish")),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            Assert.AreEqual(1, result.Total);
            Assert.AreEqual("http://sample.com/catfish", result.Data.Single().Url);
        }

        [TestMethod]
        public async Task Where_NavCollectionCount_FiltersByCount()
        {
            using TestDatabase db = new(seedPosts: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Posts.Count >= 2,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            // cats has 2 posts, dogs has 2 posts → 2 matches
            Assert.AreEqual(2, result.Total);
        }

        [TestMethod]
        public async Task Where_Null_ReturnsAllRows()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: null,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task Where_AlwaysTrue_ReturnsAllRows()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => true,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(3, result.Total);
        }

        [TestMethod]
        public async Task Where_AlwaysFalse_ReturnsEmpty()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => false,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(0, result.Total);
            Assert.AreEqual(0, result.Data.Count());
        }

        [TestMethod]
        public async Task Where_ClosedOverLocalVariable_Translates()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            string needle = "dogs";
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains(needle),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(1, result.Total);
        }

        [TestMethod]
        public async Task Where_OnPrimaryKey_FiltersById()
        {
            using TestDatabase db = new();
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> all = await repo.FindAllPagedAsync(
                where: null,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);
            int firstId = all.Data.First().BlogId;

            using TestDatabase db2 = new();
            using IPagedQueryRepositoryAsync<Blog> repo2 = NewRepo(db2);

            IPage<Blog> result = await repo2.FindAllPagedAsync(
                where: x => x.BlogId == firstId,
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true);

            Assert.AreEqual(1, result.Total);
            Assert.AreEqual(firstId, result.Data.Single().BlogId);
        }

        [TestMethod]
        public async Task Where_WithCollectionIncludes_FiltersDistinctRoots()
        {
            using TestDatabase db = new(seedPosts: true, seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Url.Contains("cat"),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: new[] { nameof(Blog.Posts), nameof(Blog.Tags) });

            Assert.AreEqual(2, result.Total,
                "Total counts distinct Blogs matching where, not Cartesian rows over Posts/Tags.");
        }

        [TestMethod]
        public async Task Where_OnIncludedCollectionMember_FiltersAtRootLevel()
        {
            using TestDatabase db = new(seedTags: true);
            using IPagedQueryRepositoryAsync<Blog> repo = NewRepo(db);

            // Blogs that have a tag named "feline" → only cats
            IPage<Blog> result = await repo.FindAllPagedAsync(
                where: x => x.Tags.Any(t => t.Name == "feline"),
                orderBy: ById,
                page: 1,
                pageSize: 10,
                splitQuery: true,
                includes: nameof(Blog.Tags));

            Assert.AreEqual(1, result.Total);
            Assert.AreEqual("http://sample.com/cats", result.Data.Single().Url);
        }
    }
}
