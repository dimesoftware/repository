using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.IntegrationTests
{
    [TestClass]
    public class StoredProcedureTests
    {
        private static EfRepository<Blog, BloggingContext> NewRepo()
            => new(SqlServerFixture.CreateContext());

        private static int CatsBlogId()
        {
            using BloggingContext ctx = SqlServerFixture.CreateContext();
            return ctx.Blogs.Where(b => b.Url == "http://sample.com/cats").Select(b => b.BlogId).First();
        }

        [TestMethod]
        public void ExecuteStoredProcedure_NoSchemaOverload_ShouldExecuteAndReturnRowCount()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            // INT param avoids the string-interpolation pitfall in the non-generic overload
            // (EXEC text is built by raw $"{name}={value}" formatting).
            DbParameter[] parameters = [new SqlParameter("@blogId", CatsBlogId())];

            int result = repo.ExecuteStoredProcedure("dbo.pBumpBlogDescription", parameters);

            Assert.AreEqual(1, result, "UPDATE through the SP should report one affected row.");
        }

        [TestMethod]
        public void ExecuteStoredProcedure_WithSchemaOverload_ShouldExecuteAndReturnRowCount()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters = [new SqlParameter("@blogId", CatsBlogId())];

            int result = repo.ExecuteStoredProcedure("pBumpBlogDescription", "dbo", parameters);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void ExecuteStoredProcedure_Generic_ShouldMapResultsToType()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters = [new SqlParameter("@prefix", "http://sample.com/")];
            IEnumerable<BlogDto> result = repo.ExecuteStoredProcedure<BlogDto>(
                "pGetBlogsByUrlPrefix", "dbo", parameters);

            List<BlogDto> list = [.. result];
            Assert.HasCount(3, list, "Three deterministic blogs match the http://sample.com/ prefix.");
            CollectionAssert.AreEquivalent(
                new[] { "http://sample.com/cats", "http://sample.com/catfish", "http://sample.com/dogs" },
                list.Select(b => b.Url).ToList());
        }

        [TestMethod]
        public async Task ExecuteStoredProcedureAsync_Generic_ShouldMapResultsToType()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters = [new SqlParameter("@prefix", "http://sample.com/")];
            IEnumerable<BlogDto> result = await repo.ExecuteStoredProcedureAsync<BlogDto>(
                "pGetBlogsByUrlPrefix", "dbo", parameters);

            List<BlogDto> list = [.. result];
            Assert.HasCount(3, list);
        }

        [TestMethod]
        public async Task ExecuteStoredProcedureAsync_NoSchemaOverload_ShouldExecuteAndReturnRowCount()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters = [new SqlParameter("@blogId", CatsBlogId())];

            int result = await repo.ExecuteStoredProcedureAsync("dbo.pBumpBlogDescription", parameters);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task ExecuteStoredProcedureAsync_WithSchemaOverload_ShouldExecuteAndReturnRowCount()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters = [new SqlParameter("@blogId", CatsBlogId())];

            int result = await repo.ExecuteStoredProcedureAsync("pBumpBlogDescription", "dbo", parameters);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void GetStoredProcedureSchema_ShouldReturnParameterMetadata()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            List<SqlParameter> parameters = [.. repo.GetStoredProcedureSchema("pBumpBlogDescription")];

            Assert.IsTrue(
                parameters.Any(p => p.ParameterName.Contains("blogId", System.StringComparison.OrdinalIgnoreCase)),
                $"Expected @blogId parameter. Got: [{string.Join(",", parameters.Select(p => p.ParameterName))}]");
        }
    }
}
