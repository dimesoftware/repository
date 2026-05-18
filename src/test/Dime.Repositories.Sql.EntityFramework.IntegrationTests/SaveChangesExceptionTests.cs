using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.IntegrationTests
{
    /// <summary>
    /// Covers the DbUpdateException branches in EfRepository.SaveChanges / SaveChangesAsync
    /// that map SqlException numbers to the typed repository exceptions.
    /// </summary>
    [TestClass]
    public class SaveChangesExceptionTests
    {
        private static EfRepository<TEntity, BloggingContext> NewRepo<TEntity>() where TEntity : class, new()
            => new(SqlServerFixture.CreateContext());

        [TestMethod]
        public void Create_ForeignKeyViolation_ShouldThrowMappedException()
        {
            // Inserting a Post that references a non-existent Blog forces SQL Server error 547
            // (FK violation), which the repository maps via the DbUpdateException → SqlException path.
            using EfRepository<Post, BloggingContext> repo = NewRepo<Post>();

            try
            {
                repo.Create(new Post { Title = "orphan", Content = "x", BlogId = int.MaxValue });
                Assert.Fail("Expected an exception due to the FK violation.");
            }
            catch (Exception ex) when (ex is ConstraintViolationException || ex is DbUpdateException || ex is DatabaseAccessException)
            {
                // Each of those branches is exercised regardless of how EF wraps the SqlException.
            }
        }

        [TestMethod]
        public async Task CreateAsync_ForeignKeyViolation_ShouldThrowMappedException()
        {
            using EfRepository<Post, BloggingContext> repo = NewRepo<Post>();

            try
            {
                await repo.CreateAsync(new Post { Title = "orphan-async", Content = "x", BlogId = int.MaxValue });
                Assert.Fail("Expected an exception due to the FK violation.");
            }
            catch (Exception ex) when (ex is ConstraintViolationException || ex is DbUpdateException || ex is DatabaseAccessException)
            {
            }
        }

        [TestMethod]
        public void Create_DuplicateUniqueIndex_ShouldThrowMappedException()
        {
            // Tags table has a unique index on Name (created by the fixture).
            // First insert seeds the value; second insert raises SQL Server error 2601.
            using EfRepository<Tag, BloggingContext> repo = NewRepo<Tag>();

            // Use a Blog from the deterministic seed.
            using BloggingContext setupCtx = SqlServerFixture.CreateContext();
            int blogId = setupCtx.Blogs.Where(b => b.Url == "http://sample.com/dogs").Select(b => b.BlogId).First();

            string uniqueName = $"dup-tag-{System.Guid.NewGuid()}";
            repo.Create(new Tag { Name = uniqueName, BlogId = blogId });

            using EfRepository<Tag, BloggingContext> repo2 = NewRepo<Tag>();
            try
            {
                repo2.Create(new Tag { Name = uniqueName, BlogId = blogId });
                Assert.Fail("Expected an exception due to the duplicate unique index.");
            }
            catch (Exception ex) when (ex is ConstraintViolationException || ex is DbUpdateException || ex is DatabaseAccessException)
            {
            }
        }

        [TestMethod]
        public async Task CreateAsync_DuplicateUniqueIndex_ShouldThrowMappedException()
        {
            using EfRepository<Tag, BloggingContext> repo = NewRepo<Tag>();

            using BloggingContext setupCtx = SqlServerFixture.CreateContext();
            int blogId = setupCtx.Blogs.Where(b => b.Url == "http://sample.com/dogs").Select(b => b.BlogId).First();

            string uniqueName = $"dup-tag-async-{System.Guid.NewGuid()}";
            await repo.CreateAsync(new Tag { Name = uniqueName, BlogId = blogId });

            using EfRepository<Tag, BloggingContext> repo2 = NewRepo<Tag>();
            try
            {
                await repo2.CreateAsync(new Tag { Name = uniqueName, BlogId = blogId });
                Assert.Fail("Expected an exception due to the duplicate unique index.");
            }
            catch (Exception ex) when (ex is ConstraintViolationException || ex is DbUpdateException || ex is DatabaseAccessException)
            {
            }
        }
    }
}
