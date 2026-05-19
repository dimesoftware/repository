using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class FindAllAsyncSplitQueryTests
    {
        private static EfRepository<Blog, BloggingContext> NewRepo(TestDatabase db)
            => new(new BloggingContext(db.Options));

        [TestMethod]
        public async Task FindAllAsync_SplitQueryFalse_ReturnsSameCountAsExistingOverload()
        {
            using TestDatabase testDb = new();

            IEnumerable<string> existing;
            using (IRepository<Blog> repo = NewRepo(testDb))
                existing = await repo.FindAllAsync<string>(
                    where: x => x.Url.Contains("cat"),
                    select: x => x.Url,
                    orderBy: null,
                    ascending: null,
                    page: null,
                    pageSize: null);

            IEnumerable<string> nonSplit;
            using (IRepository<Blog> repo = NewRepo(testDb))
                nonSplit = await repo.FindAllAsync<string>(
                    where: x => x.Url.Contains("cat"),
                    select: x => x.Url,
                    orderBy: null,
                    ascending: null,
                    page: null,
                    pageSize: null,
                    splitQuery: false);

            Assert.AreEqual(existing.Count(), nonSplit.Count());
            CollectionAssert.AreEquivalent(existing.ToList(), nonSplit.ToList());
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_ReturnsSameResultsAsNonSplit()
        {
            using TestDatabase testDb = new();

            IEnumerable<string> nonSplit;
            using (IRepository<Blog> repo = NewRepo(testDb))
                nonSplit = await repo.FindAllAsync<string>(
                    where: x => x.Url.Contains("cat"),
                    select: x => x.Url,
                    orderBy: null,
                    ascending: null,
                    page: null,
                    pageSize: null,
                    splitQuery: false);

            IEnumerable<string> split;
            using (IRepository<Blog> repo = NewRepo(testDb))
                split = await repo.FindAllAsync<string>(
                    where: x => x.Url.Contains("cat"),
                    select: x => x.Url,
                    orderBy: null,
                    ascending: null,
                    page: null,
                    pageSize: null,
                    splitQuery: true);

            CollectionAssert.AreEquivalent(nonSplit.ToList(), split.ToList());
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithWhereFilter_FiltersResults()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => x.Url.Contains("cat"),
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            Assert.AreEqual(2, result.Count());
            Assert.IsTrue(result.All(u => u.Contains("cat")));
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithoutFilter_ReturnsAll()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            Assert.AreEqual(3, result.Count());
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithProjection_ProjectsToTResult()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            foreach (string s in result)
                StringAssert.StartsWith(s, "http");
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_OrderByAscending_OrdersAscending()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: null,
                pageSize: null,
                splitQuery: true);

            List<string> list = result.ToList();
            CollectionAssert.AreEqual(list.OrderBy(s => s).ToList(), list);
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_OrderByDescending_OrdersDescending()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: false,
                page: null,
                pageSize: null,
                splitQuery: true);

            List<string> list = result.ToList();
            CollectionAssert.AreEqual(list.OrderByDescending(s => s).ToList(), list);
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithPageSize_LimitsResults()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 2,
                splitQuery: true);

            Assert.AreEqual(2, result.Count());
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithPage2_SkipsFirstPage()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> page1 = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 1,
                pageSize: 2,
                splitQuery: true);

            using IRepository<Blog> repo2 = NewRepo(testDb);
            IEnumerable<string> page2 = await repo2.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: 2,
                pageSize: 2,
                splitQuery: true);

            Assert.AreEqual(1, page2.Count());
            CollectionAssert.AreNotEquivalent(page1.ToList(), page2.ToList());
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_EmptyResult_ReturnsEmpty()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => x.Url == "no-such-url",
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            Assert.AreEqual(0, result.Count());
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithIncludeCollectionNav_LoadsRelated()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<Blog> result = await repo.FindAllAsync<Blog>(
                where: x => x.Url.Contains("cats"),
                select: x => x,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true,
                includes: nameof(Blog.Posts));

            Blog cats = result.FirstOrDefault(b => b.Url == "http://sample.com/cats");
            Assert.IsNotNull(cats);
            Assert.IsNotNull(cats.Posts);
            Assert.AreEqual(2, cats.Posts.Count);
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_WithIncludes_EmitsMoreCommandsThanSingleMode()
        {
            (int splitCount, string splitDump) = await CountExecutedCommandsWithIncludesAsync(splitQuery: true);
            (int singleCount, string singleDump) = await CountExecutedCommandsWithIncludesAsync(splitQuery: false);

            Assert.IsTrue(splitCount > singleCount,
                $"split={splitCount} single={singleCount}\nSPLIT SQL:\n{splitDump}\n---\nSINGLE SQL:\n{singleDump}");
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryFalse_WithIncludes_EmitsSingleJoinCommand()
        {
            (int single, _) = await CountExecutedCommandsWithIncludesAsync(splitQuery: false);
            Assert.AreEqual(1, single, "Single-query mode should emit exactly one data command for the Blogs/Posts join");
        }

        private static async Task<(int count, string dump)> CountExecutedCommandsWithIncludesAsync(bool splitQuery)
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);
            using IRepository<Blog> repo = NewRepo(testDb);

            int beforeCount = testDb.CapturedSql.Count;

            _ = (await repo.FindAllAsync<Blog>(
                where: x => true,
                select: x => x,
                orderBy: x => x.BlogId,
                ascending: true,
                page: null,
                pageSize: null,
                splitQuery: splitQuery,
                includes: nameof(Blog.Posts))).ToList();

            List<string> after = testDb.CapturedSql.Skip(beforeCount).ToList();
            int count = after.Count(s =>
                s.Contains("Executed DbCommand") &&
                (s.Contains("FROM \"Blogs\"") || s.Contains("FROM \"Posts\"")));
            return (count, string.Join("\n===\n", after));
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_PreservesOrderAndCount()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => x.Url.Contains("sample"),
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: true,
                page: null,
                pageSize: null,
                splitQuery: true);

            List<string> list = result.ToList();
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("http://sample.com/catfish", list[0]);
            Assert.AreEqual("http://sample.com/cats", list[1]);
            Assert.AreEqual("http://sample.com/dogs", list[2]);
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_DefaultAscendingWhenNullPassed()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: x => x.Url,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            List<string> list = result.ToList();
            CollectionAssert.AreEqual(list.OrderBy(s => s).ToList(), list);
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_ReturnsIEnumerableThatIsNotNull()
        {
            using TestDatabase testDb = new();
            using IRepository<Blog> repo = NewRepo(testDb);

            IEnumerable<string> result = await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true);

            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task FindAllAsync_SplitQueryTrue_NoIncludes_EmitsSingleCommand()
        {
            using TestDatabase testDb = new(captureSql: true);
            using IRepository<Blog> repo = NewRepo(testDb);

            _ = (await repo.FindAllAsync<string>(
                where: x => true,
                select: x => x.Url,
                orderBy: null,
                ascending: null,
                page: null,
                pageSize: null,
                splitQuery: true)).ToList();

            int blogCommands = testDb.CapturedSql.Count(s => s.Contains("FROM \"Blogs\""));
            Assert.AreEqual(1, blogCommands);
        }
    }
}