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

        [TestMethod]
        public void ExecuteStoredProcedure_NoSchemaOverload_ShouldExecuteAndReturnRowCount()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters =
            [
                new SqlParameter("@url", $"http://sp-test-noschema/{System.Guid.NewGuid()}"),
                new SqlParameter("@description", "sp insert no-schema")
            ];

            int result = repo.ExecuteStoredProcedure("dbo.pInsertBlog", parameters);

            Assert.AreEqual(1, result, "INSERT should report one affected row.");
        }

        [TestMethod]
        public void ExecuteStoredProcedure_WithSchemaOverload_ShouldExecuteAndReturnRowCount()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters =
            [
                new SqlParameter("@url", $"http://sp-test-schema/{System.Guid.NewGuid()}"),
                new SqlParameter("@description", "sp insert schema")
            ];

            int result = repo.ExecuteStoredProcedure("pInsertBlog", "dbo", parameters);

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

            DbParameter[] parameters =
            [
                new SqlParameter("@url", $"http://sp-async-noschema/{System.Guid.NewGuid()}"),
                new SqlParameter("@description", "async no-schema")
            ];

            int result = await repo.ExecuteStoredProcedureAsync("dbo.pInsertBlog", parameters);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public async Task ExecuteStoredProcedureAsync_WithSchemaOverload_ShouldExecuteAndReturnRowCount()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            DbParameter[] parameters =
            [
                new SqlParameter("@url", $"http://sp-async-schema/{System.Guid.NewGuid()}"),
                new SqlParameter("@description", "async schema")
            ];

            int result = await repo.ExecuteStoredProcedureAsync("pInsertBlog", "dbo", parameters);

            Assert.AreEqual(1, result);
        }

        [TestMethod]
        public void GetStoredProcedureSchema_ShouldReturnParameterMetadata()
        {
            using EfRepository<Blog, BloggingContext> repo = NewRepo();

            List<SqlParameter> parameters = [.. repo.GetStoredProcedureSchema("pInsertBlog")];

            // pInsertBlog declares @url and @description (DeriveParameters also adds @RETURN_VALUE).
            Assert.IsTrue(parameters.Any(p => p.ParameterName.Contains("url", System.StringComparison.OrdinalIgnoreCase)),
                $"Expected @url parameter. Got: [{string.Join(",", parameters.Select(p => p.ParameterName))}]");
            Assert.IsTrue(parameters.Any(p => p.ParameterName.Contains("description", System.StringComparison.OrdinalIgnoreCase)),
                $"Expected @description parameter. Got: [{string.Join(",", parameters.Select(p => p.ParameterName))}]");
        }
    }
}
