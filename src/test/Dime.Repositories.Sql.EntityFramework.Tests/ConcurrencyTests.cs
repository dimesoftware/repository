using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class ConcurrencyTests
    {
        private static ConcurrentItem Seed(TestDatabase testDb)
        {
            using BloggingContext ctx = new(testDb.Options);
            ConcurrentItem item = new() { Value = "initial", Token = "v1" };
            ctx.ConcurrentItems.Add(item);
            ctx.SaveChanges();
            return item;
        }

        private static void MutateInOtherContext(TestDatabase testDb, int id, string newValue, string newToken)
        {
            using BloggingContext other = new(testDb.Options);
            ConcurrentItem item = other.ConcurrentItems.First(c => c.Id == id);
            item.Value = newValue;
            item.Token = newToken;
            other.SaveChanges();
        }

        [TestMethod]
        public void SaveChanges_ConcurrencyConflict_ClientFirst_ShouldOverwriteDbValues()
        {
            using TestDatabase testDb = new();
            ConcurrentItem seeded = Seed(testDb);
            MutateInOtherContext(testDb, seeded.Id, "from-other-ctx", "v2");

            using BloggingContext ctx = new(testDb.Options);
            using EfRepository<ConcurrentItem, BloggingContext> repo = new(
                ctx,
                new RepositoryConfiguration { SaveStrategy = ConcurrencyStrategy.ClientFirst });

            // Stale token (still "v1") — concurrency check fails, recovery path runs.
            ConcurrentItem stale = new() { Id = seeded.Id, Value = "client-wins", Token = "v1" };
            repo.Update(stale);

            using BloggingContext verify = new(testDb.Options);
            ConcurrentItem final = verify.ConcurrentItems.First(c => c.Id == seeded.Id);
            Assert.AreEqual("client-wins", final.Value);
        }

        [TestMethod]
        public void SaveChanges_ConcurrencyConflict_DatabaseFirst_ShouldKeepDbValues()
        {
            using TestDatabase testDb = new();
            ConcurrentItem seeded = Seed(testDb);
            MutateInOtherContext(testDb, seeded.Id, "from-other-ctx", "v2");

            using BloggingContext ctx = new(testDb.Options);
            using EfRepository<ConcurrentItem, BloggingContext> repo = new(
                ctx,
                new RepositoryConfiguration { SaveStrategy = ConcurrencyStrategy.DatabaseFirst });

            ConcurrentItem stale = new() { Id = seeded.Id, Value = "client-attempt", Token = "v1" };
            repo.Update(stale);

            // DatabaseFirst discards client changes; persisted row keeps the other context's write.
            using BloggingContext verify = new(testDb.Options);
            ConcurrentItem final = verify.ConcurrentItems.First(c => c.Id == seeded.Id);
            Assert.AreEqual("from-other-ctx", final.Value);
        }

        [TestMethod]
        public async Task SaveChangesAsync_ConcurrencyConflict_ClientFirst_ShouldOverwriteDbValues()
        {
            using TestDatabase testDb = new();
            ConcurrentItem seeded = Seed(testDb);
            MutateInOtherContext(testDb, seeded.Id, "from-other-ctx", "v2");

            await using BloggingContext ctx = new(testDb.Options);
            using EfRepository<ConcurrentItem, BloggingContext> repo = new(
                ctx,
                new RepositoryConfiguration { SaveStrategy = ConcurrencyStrategy.ClientFirst });

            ConcurrentItem stale = new() { Id = seeded.Id, Value = "async-client-wins", Token = "v1" };
            await repo.UpdateAsync(stale);

            await using BloggingContext verify = new(testDb.Options);
            ConcurrentItem final = verify.ConcurrentItems.First(c => c.Id == seeded.Id);
            Assert.AreEqual("async-client-wins", final.Value);
        }

        [TestMethod]
        public void SaveChanges_DbUpdateException_NoNestedInner_ShouldRethrow()
        {
            using TestDatabase testDb = new();
            using (BloggingContext seed = new(testDb.Options))
            {
                seed.ConcurrentItems.Add(new ConcurrentItem { Id = 100, Value = "a", Token = "t" });
                seed.SaveChanges();
            }

            using EfRepository<ConcurrentItem, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            // Same PK — SQLite throws UNIQUE constraint via SqliteException with no further inner.
            // The repo's DbUpdateException handler hits the rethrow branch when the doubly-nested inner is null.
            Assert.ThrowsExactly<DbUpdateException>(
                () => repo.Create(new ConcurrentItem { Id = 100, Value = "b", Token = "t" }));
        }

        [TestMethod]
        public async Task SaveChangesAsync_DbUpdateException_NoNestedInner_ShouldRethrow()
        {
            using TestDatabase testDb = new();
            await using (BloggingContext seed = new(testDb.Options))
            {
                seed.ConcurrentItems.Add(new ConcurrentItem { Id = 200, Value = "a", Token = "t" });
                await seed.SaveChangesAsync();
            }

            using EfRepository<ConcurrentItem, BloggingContext> repo = new(new BloggingContext(testDb.Options));

            await Assert.ThrowsExactlyAsync<DbUpdateException>(
                () => repo.CreateAsync(new ConcurrentItem { Id = 200, Value = "b", Token = "t" }));
        }

        [TestMethod]
        public async Task SaveChangesAsync_ConcurrencyConflict_DatabaseFirst_ShouldKeepDbValues()
        {
            using TestDatabase testDb = new();
            ConcurrentItem seeded = Seed(testDb);
            MutateInOtherContext(testDb, seeded.Id, "from-other-ctx", "v2");

            await using BloggingContext ctx = new(testDb.Options);
            using EfRepository<ConcurrentItem, BloggingContext> repo = new(
                ctx,
                new RepositoryConfiguration { SaveStrategy = ConcurrencyStrategy.DatabaseFirst });

            ConcurrentItem stale = new() { Id = seeded.Id, Value = "async-client-attempt", Token = "v1" };
            await repo.UpdateAsync(stale);

            await using BloggingContext verify = new(testDb.Options);
            ConcurrentItem final = verify.ConcurrentItems.First(c => c.Id == seeded.Id);
            Assert.AreEqual("from-other-ctx", final.Value);
        }
    }
}
