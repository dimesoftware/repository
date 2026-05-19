using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    [TestClass]
    public class QueryFunctionAsyncTests
    {
        private static string FindFunctionCommandText(TestDatabase db)
        {
            return db.CapturedSql.FirstOrDefault(s => s.Contains("FROM ["))
                   ?? string.Join("\n---\n", db.CapturedSql);
        }

        [TestMethod]
        public async Task QueryFunctionAsync_DefaultSchema_EmitsDboBracketedSchema()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("MyFunc"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[MyFunc]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_CustomSchema_UsesProvidedSchema()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("MyFunc", "reports"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[reports].[MyFunc]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_FunctionNameAndSchemaAreBracketed()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("Func", "sch"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[sch].[Func]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_NoParameters_EmitsEmptyParens()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("Func"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]()");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_NullParameters_EmitsEmptyParens()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("Func", "dbo", null));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]()");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_EmptyParametersArray_EmitsEmptyParens()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", System.Array.Empty<DbParameter>()));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]()");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_SingleParameter_AppearsInParens()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            DbParameter[] parameters = [new SqliteParameter("@TenantId", 42)];

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func](@TenantId)");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_MultipleParameters_AppearInOrderCommaSeparated()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            DbParameter[] parameters =
            [
                new SqliteParameter("@A", 1),
                new SqliteParameter("@B", 2),
                new SqliteParameter("@C", 3),
            ];

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "(@A, @B, @C)");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_ParameterValuesAreSent()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            DbParameter[] parameters = [new SqliteParameter("@TenantId", 12345)];

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string concatenated = string.Join("\n", testDb.CapturedSql);
            StringAssert.Contains(concatenated, "12345");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_SqlStartsWithSelectStarFrom()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("Func"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "SELECT");
            StringAssert.Contains(sql, "FROM [dbo].[Func]()");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_WithSplitQueryTrue_StillEmitsFunctionSql()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", null, null, splitQuery: true));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_WithSplitQueryFalse_EmitsFunctionSql()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", null, null, splitQuery: false));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_WithSelectProjection_EmitsFunctionSql()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<string>("Func", "dbo", null, x => x.Url));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_DistinctSchemaAndName_AreBothBracketedSeparately()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("MyFunc", "mySchema"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[mySchema]");
            StringAssert.Contains(sql, "[MyFunc]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_ExecutionReachesDatabase_ProducesSqliteException()
        {
            using TestDatabase testDb = new();
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            SqliteException ex = await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("NonExistentTvf"));

            Assert.IsNotNull(ex);
        }

        [TestMethod]
        public async Task QueryFunctionAsync_FunctionNameWithUnderscores_RoundTripsToSql()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("my_func_v2"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[my_func_v2]");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_TwoParameters_OrderPreservedFromArray()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            DbParameter[] parameters =
            [
                new SqliteParameter("@Second", "b"),
                new SqliteParameter("@First", "a"),
            ];

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "(@Second, @First)");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_NameWithCloseBracket_IsEscapedByDoubling()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            // Injection attempt: a stray ']' would close the identifier and let
            // anything after run as SQL. The escape doubles the ']' so it stays
            // inside the bracketed identifier.
            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("foo]; DROP TABLE Blogs; --"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[foo]]; DROP TABLE Blogs; --]");
            Assert.IsFalse(sql.Contains("[foo]; DROP"),
                $"Unescaped ']' would have broken out of the identifier. SQL: {sql}");
        }

        [TestMethod]
        public async Task QueryFunctionAsync_SchemaWithCloseBracket_IsEscapedByDoubling()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = new EfRepository<Blog, BloggingContext>(new BloggingContext(testDb.Options));

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "sch]; DROP TABLE Blogs; --"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[sch]]; DROP TABLE Blogs; --]");
        }
    }
}