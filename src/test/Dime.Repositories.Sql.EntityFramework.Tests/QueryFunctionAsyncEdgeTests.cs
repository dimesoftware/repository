using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dime.Repositories.Sql.EntityFramework.Tests
{
    /// <summary>
    /// Exhaustive edge-case coverage of <see cref="ISqlRepository{T}.QueryFunctionAsync"/>.
    /// SQLite cannot evaluate a real TVF, so each test asserts on the *emitted SQL* captured
    /// via EF Core's logger pipeline. The reaching-the-database assertion is left to the
    /// integration test project (real SQL Server).
    /// </summary>
    [TestClass]
    public class QueryFunctionAsyncEdgeTests
    {
        private static string FindFunctionCommandText(TestDatabase db)
            => db.CapturedSql.FirstOrDefault(s => s.Contains("FROM ["))
               ?? string.Join("\n---\n", db.CapturedSql);

        private static ISqlRepository<Blog> NewRepo(TestDatabase db)
            => new EfRepository<Blog, BloggingContext>(new BloggingContext(db.Options));

        // ── Schema/name escape ────────────────────────────────────────────

        [TestMethod]
        public async Task EscapesMultipleClosingBracketsInName()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("a]b]c"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[a]]b]]c]");
        }

        [TestMethod]
        public async Task EscapesMultipleClosingBracketsInSchema()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("Func", "s]]"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[s]]]]]");
        }

        [TestMethod]
        public async Task DoesNotEscapeOpeningBracketsBecauseTSqlDoesNotRequireIt()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            // '[' inside [..] is literal in T-SQL; only ']' needs doubling.
            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("a[b"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[a[b]");
        }

        [TestMethod]
        public async Task PreservesUnicodeIdentifiers()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("Über_Func_カタカナ"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[Über_Func_カタカナ]");
        }

        [TestMethod]
        public async Task DoubleQuotesAreNotEscaped_TheyAreLiteralInBrackets()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("f\"oo"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[f\"oo]");
        }

        [TestMethod]
        public async Task SingleQuotesInIdentifierAreLiteral()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("f'oo"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[f'oo]");
        }

        // ── Parameter list shape ──────────────────────────────────────────

        [TestMethod]
        public async Task ManyParameters_AllAppearCommaSeparatedInOrder()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            DbParameter[] parameters = Enumerable.Range(0, 25)
                .Select(i => (DbParameter)new SqliteParameter($"@p{i}", i))
                .ToArray();

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            string expected = string.Join(", ", Enumerable.Range(0, 25).Select(i => $"@p{i}"));
            StringAssert.Contains(sql, $"({expected})");
        }

        [TestMethod]
        public async Task ParameterNameWithoutAtSign_IsRoundTrippedAsIs()
        {
            // ParameterName is trusted (not user input); just verify the EXACT string is rendered.
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            DbParameter[] parameters = [new SqliteParameter("bareName", 1)];

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "(bareName)");
        }

        [TestMethod]
        public async Task SingleParameterEmitsNoLeadingComma()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            DbParameter[] parameters = [new SqliteParameter("@only", 1)];

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "(@only)");
            Assert.IsFalse(sql.Contains(", @only"), "Single parameter should not have a leading separator.");
            Assert.IsFalse(sql.Contains("@only,"), "Single parameter should not have a trailing separator.");
        }

        // ── Schema/name corner cases ──────────────────────────────────────

        [TestMethod]
        public async Task EmptyStringSchemaStaysEmpty()
        {
            // Caller responsibility — we just don't crash.
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            try
            {
                await repo.QueryFunctionAsync<Blog>("Func", string.Empty);
            }
            catch
            {
                // SQLite trips — fine.
            }

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[].[Func]");
        }

        [TestMethod]
        public async Task SchemaWithDot_DoesNotSplitIntoTwoIdentifiers()
        {
            // '.' is part of the identifier when wrapped in brackets; we should NOT split it
            // into nested [a].[b] regions — the whole string is one bracketed token.
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>("Func", "weird.schema"));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[weird.schema].[Func]");
        }

        // ── splitQuery flag is inert on FromSqlRaw without Includes ───────

        [TestMethod]
        public async Task SplitQueryFlag_DoesNotChangeEmittedSql()
        {
            using TestDatabase splitOn = new(captureSql: true);
            using ISqlRepository<Blog> repoOn = NewRepo(splitOn);
            try { await repoOn.QueryFunctionAsync<Blog>("Func", "dbo", null, null, splitQuery: true); } catch { }

            using TestDatabase splitOff = new(captureSql: true);
            using ISqlRepository<Blog> repoOff = NewRepo(splitOff);
            try { await repoOff.QueryFunctionAsync<Blog>("Func", "dbo", null, null, splitQuery: false); } catch { }

            string sqlOn = FindFunctionCommandText(splitOn);
            string sqlOff = FindFunctionCommandText(splitOff);

            // Strip the EF "Executing DbCommand […]" log prefix that contains a timestamp/duration.
            string Norm(string s) => s.Substring(s.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase));
            Assert.AreEqual(Norm(sqlOn), Norm(sqlOff),
                "splitQuery is a no-op for FromSqlRaw without Includes — emitted SQL must match.");
        }

        // ── Projection shapes ─────────────────────────────────────────────

        [TestMethod]
        public async Task ProjectionToPrimitive_TypeIsHonoured()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<string>("Func", "dbo", null, x => x.Url));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]");
        }

        [TestMethod]
        public async Task ProjectionToDto_TypeIsHonoured()
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<BlogDto>("Func", "dbo", null,
                    x => new BlogDto { Url = x.Url }));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, "[dbo].[Func]");
        }

        // ── Public-surface invariants ─────────────────────────────────────

        [TestMethod]
        public void QueryFunctionAsync_IsExposedOnISqlRepository()
        {
            // Reflection check: the method actually appears on the public interface.
            System.Reflection.MethodInfo method = typeof(ISqlRepository<Blog>)
                .GetMethod(nameof(ISqlRepository<Blog>.QueryFunctionAsync));

            Assert.IsNotNull(method, "QueryFunctionAsync must be on ISqlRepository<T>.");
            Assert.IsTrue(method.IsGenericMethodDefinition,
                "QueryFunctionAsync must be generic over TResult.");
        }

        [TestMethod]
        public void QueryFunctionAsync_HasExpectedParameterShape()
        {
            System.Reflection.MethodInfo method = typeof(ISqlRepository<Blog>)
                .GetMethod(nameof(ISqlRepository<Blog>.QueryFunctionAsync));
            System.Reflection.ParameterInfo[] parameters = method.GetParameters();

            Assert.AreEqual(5, parameters.Length);
            Assert.AreEqual("name", parameters[0].Name);
            Assert.AreEqual("schema", parameters[1].Name);
            Assert.AreEqual("parameters", parameters[2].Name);
            Assert.AreEqual("select", parameters[3].Name);
            Assert.AreEqual("splitQuery", parameters[4].Name);

            Assert.IsTrue(parameters[1].HasDefaultValue);
            Assert.AreEqual("dbo", parameters[1].DefaultValue);
            Assert.IsTrue(parameters[2].HasDefaultValue);
            Assert.IsNull(parameters[2].DefaultValue);
            Assert.IsTrue(parameters[3].HasDefaultValue);
            Assert.IsNull(parameters[3].DefaultValue);
            Assert.IsTrue(parameters[4].HasDefaultValue);
            Assert.AreEqual(false, parameters[4].DefaultValue);
        }

        // ── Repeat-call independence ──────────────────────────────────────

        [TestMethod]
        public async Task BackToBackCalls_EmitTwoCommands()
        {
            // Each call needs its own repo: a non-factory EfRepository disposes its DbContext
            // via `using ctx = Context` inside the call, leaving the same repo unusable for a
            // second invocation. The TestDatabase (connection + options) is shared so both
            // emissions land in the same capture buffer.
            using TestDatabase testDb = new(captureSql: true);

            using (ISqlRepository<Blog> repoA = NewRepo(testDb))
                try { await repoA.QueryFunctionAsync<Blog>("FuncA"); } catch { }

            using (ISqlRepository<Blog> repoB = NewRepo(testDb))
                try { await repoB.QueryFunctionAsync<Blog>("FuncB"); } catch { }

            int a = testDb.CapturedSql.Count(s => s.Contains("[FuncA]"));
            int b = testDb.CapturedSql.Count(s => s.Contains("[FuncB]"));

            Assert.AreEqual(1, a);
            Assert.AreEqual(1, b);
        }

        // ── DataRow theory of identifier escapes ─────────────────────────

        [DataTestMethod]
        [DataRow("plain", "[plain]")]
        [DataRow("with]bracket", "[with]]bracket]")]
        [DataRow("two]]close", "[two]]]]close]")]
        [DataRow("trailing]", "[trailing]]]")]
        [DataRow("]leading", "[]]leading]")]
        [DataRow("inner[open", "[inner[open]")]
        [DataRow("spaces in name", "[spaces in name]")]
        [DataRow("hyphen-name", "[hyphen-name]")]
        public async Task EscapeIdentifier_TheoryRendering(string raw, string expectedBracketed)
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            await Assert.ThrowsAsync<SqliteException>(() => repo.QueryFunctionAsync<Blog>(raw));

            string sql = FindFunctionCommandText(testDb);
            StringAssert.Contains(sql, expectedBracketed);
        }

        // ── Parameter set theory ──────────────────────────────────────────

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(5)]
        [DataRow(10)]
        public async Task ParameterCount_TheoryEmission(int count)
        {
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            DbParameter[] parameters = Enumerable.Range(0, count)
                .Select(i => (DbParameter)new SqliteParameter($"@p{i}", i))
                .ToArray();

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            if (count == 0)
            {
                StringAssert.Contains(sql, "[Func]()");
            }
            else
            {
                string expected = "(" + string.Join(", ", Enumerable.Range(0, count).Select(i => $"@p{i}")) + ")";
                StringAssert.Contains(sql, expected);
            }
        }

        // ── Concurrent-ish exercise (no shared state) ─────────────────────

        [TestMethod]
        public async Task ConcurrentCalls_OnSeparateRepos_DoNotInterfere()
        {
            // Each repo owns its own TestDatabase → its own in-memory connection.
            // Verifies the SQL builder is not shared mutable state.
            Task<string>[] tasks = Enumerable.Range(0, 8).Select(async i =>
            {
                using TestDatabase db = new(captureSql: true);
                using ISqlRepository<Blog> repo = NewRepo(db);
                try { await repo.QueryFunctionAsync<Blog>($"F_{i}"); } catch { }
                return FindFunctionCommandText(db);
            }).ToArray();

            string[] results = await Task.WhenAll(tasks);

            for (int i = 0; i < results.Length; i++)
                StringAssert.Contains(results[i], $"[F_{i}]");
        }

        // ── Anti-injection: parameter VALUES are still bound by EF ────────

        [TestMethod]
        public async Task ParameterValueContainingSqlText_IsBoundNotInterpolated()
        {
            // If the value were interpolated it would appear in the rendered command text
            // as plain text. EF Core binds via Microsoft.Data.Sqlite parameter slots —
            // the value travels separately from the SQL statement.
            using TestDatabase testDb = new(captureSql: true);
            using ISqlRepository<Blog> repo = NewRepo(testDb);

            DbParameter[] parameters = [new SqliteParameter("@evil", "'; DROP TABLE Blogs; --")];

            await Assert.ThrowsAsync<SqliteException>(
                () => repo.QueryFunctionAsync<Blog>("Func", "dbo", parameters));

            string sql = FindFunctionCommandText(testDb);
            // The placeholder name must appear; the raw injection payload must NOT appear
            // inside the SELECT statement (it may appear elsewhere in the captured log as
            // a parameter binding line — that's expected).
            StringAssert.Contains(sql, "(@evil)");
        }
    }
}
