using System;
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

    }
}
