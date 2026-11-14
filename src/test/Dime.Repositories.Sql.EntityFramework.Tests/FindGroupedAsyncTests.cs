using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class FindGroupedAsyncTests
    {
        [TestMethod]
        public async Task FindGroupedAsync_GroupWithCount_ShouldReturnOneRowPerGroup()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            var result = (await repo.FindGroupedAsync(
                null,
                p => p.BlogId,
                g => new { BlogId = g.Key, Count = g.Count() }))
                .OrderBy(x => x.BlogId)
                .ToList();

            Assert.HasCount(3, result);
            Assert.AreEqual(1, result[0].BlogId);
            Assert.AreEqual(2, result[0].Count);
            Assert.AreEqual(2, result[1].BlogId);
            Assert.AreEqual(1, result[1].Count);
            Assert.AreEqual(3, result[2].BlogId);
            Assert.AreEqual(2, result[2].Count);
        }

        [TestMethod]
        public async Task FindGroupedAsync_KeyThroughNavigation_ShouldGroupOnRelatedColumns()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            var result = (await repo.FindGroupedAsync(
                p => true,
                p => new { p.Blog.Url, p.BlogId },
                g => new { g.Key.Url, g.Key.BlogId, Count = g.Count(), LongestTitle = g.Max(p => p.Title.Length) }))
                .ToDictionary(x => x.Url);

            Assert.HasCount(3, result);
            Assert.AreEqual(2, result["http://sample.com/cats"].Count);
            Assert.AreEqual(1, result["http://sample.com/catfish"].Count);
            Assert.AreEqual(2, result["http://sample.com/dogs"].Count);
            Assert.AreEqual("Catfish post".Length, result["http://sample.com/catfish"].LongestTitle);
        }

        [TestMethod]
        public async Task FindGroupedAsync_Where_ShouldFilterBeforeGrouping()
        {
            using TestDatabase testDb = new(seedPosts: true);
            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            var result = (await repo.FindGroupedAsync(
                p => p.Title.EndsWith("2"),
                p => new { p.Blog.Url },
                g => new { g.Key.Url, Count = g.Count() }))
                .OrderBy(x => x.Url)
                .ToList();

            Assert.HasCount(2, result);
            Assert.AreEqual("http://sample.com/cats", result[0].Url);
            Assert.AreEqual(1, result[0].Count);
            Assert.AreEqual("http://sample.com/dogs", result[1].Url);
            Assert.AreEqual(1, result[1].Count);
        }

        [TestMethod]
        public async Task FindGroupedAsync_ShouldTranslateGroupingAndAggregatesToOneSqlQuery()
        {
            using TestDatabase testDb = new(seedPosts: true, captureSql: true);
            using IRepository<Post> repo = new EfRepository<Post, BloggingContext>(new BloggingContext(testDb.Options));

            int before = testDb.CapturedSql.Count;
            _ = await repo.FindGroupedAsync(
                p => p.PostId > 0,
                p => new { p.Blog.Url, p.BlogId },
                g => new { g.Key.Url, g.Key.BlogId, Count = g.Count() });

            List<string> commands = [.. testDb.CapturedSql.Skip(before)];
            Assert.HasCount(1, commands, "The grouped read must be a single SQL query.");
            string sql = commands[0];
            Assert.Contains("GROUP BY", sql);
            Assert.Contains("COUNT(*)", sql);
            Assert.Contains("JOIN \"Blogs\"", sql);
            Assert.Contains("WHERE", sql);
        }
    }
}
