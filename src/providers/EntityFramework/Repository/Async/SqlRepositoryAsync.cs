using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public async Task ExecuteSqlAsync(string sql)
        {
            TContext ctx = Context;
            await ctx.Database.ExecuteSqlRawAsync(sql);
        }

        public async Task<int> ExecuteStoredProcedureAsync(string name, params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", parameters.Select(z => $"{z.ParameterName}={z.Value}"));
                return $"EXEC {name} {parameterString}";
            }

            string execQueryString = ExecQuery(name, parameters);
            TContext ctx = Context;
            return await ctx.Database.ExecuteSqlRawAsync(execQueryString, parameters);
        }

        public async Task<int> ExecuteStoredProcedureAsync(string name, string schema = "dbo", params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", parameters.Select(z => $"{z.ParameterName}={z.Value}"));
                return $"EXEC {schema}.{name} {parameterString}";
            }

            string execQueryString = ExecQuery(name, parameters);
            TContext ctx = Context;
            return await ctx.Database.ExecuteSqlRawAsync(execQueryString, parameters);
        }

        public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string name, string schema = "dbo", params DbParameter[] parameters)
        {
            string connectionString = Context.Database.GetDbConnection()?.ConnectionString;
            await using DbConnection connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = name;
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);

            using IDataReader reader = await cmd.ExecuteReaderAsync();
            return reader.GetRecords<T>();
        }

        public async Task<int> ExecuteStoredProcedureAsync<T>(T name, string schema = "dbo", params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", parameters.Select(z => $"{z.ParameterName}={z.Value}"));
                return $"EXEC {schema}.{nameof(name)} {parameterString}";
            }

            string execQueryString = ExecQuery(nameof(name), parameters);
            TContext ctx = Context;
            return 1;
        }

        public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string command, params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", parameters.Select(z => $"@{z.ParameterName}={z.Value}"));
                return $"EXEC {command} {parameterString}";
            }

            return await Task.Run(() =>
            {
                using TContext ctx = Context;
                return Task.FromResult(new List<T>());
            });
        }

        public async Task<IEnumerable<TResult>> QueryFunctionAsync<TResult>(
            string name,
            string schema = "dbo",
            DbParameter[] parameters = null,
            Expression<Func<TEntity, TResult>> select = null,
            bool splitQuery = false)
            where TResult : class
        {
            parameters ??= [];
            string placeholders = string.Join(", ", parameters.Select(p => p.ParameterName));
            // T-SQL identifier escape: a ']' inside [..] is escaped by doubling it.
            // Parameter VALUES are parameterized via DbParameter; the identifier-level
            // escaping here closes the schema/name interpolation as an injection vector.
            string sql = $"SELECT * FROM [{EscapeIdentifier(schema)}].[{EscapeIdentifier(name)}]({placeholders})";

            using TContext ctx = Context;
            IQueryable<TEntity> query = ctx.Set<TEntity>().FromSqlRaw(sql, parameters);

            if (splitQuery)
                query = query.AsSplitQuery();

            query = query.AsNoTracking();

            if (select != null)
                return await query.Select(select).ToListAsync();

            if (typeof(TResult) != typeof(TEntity))
                throw new InvalidOperationException(
                    $"QueryFunctionAsync<{typeof(TResult).Name}> was called without a 'select' projection, " +
                    $"but TResult ({typeof(TResult).Name}) does not match TEntity ({typeof(TEntity).Name}). " +
                    "Either pass a projection expression, or call the method with TResult equal to TEntity.");

            return (IEnumerable<TResult>)(await query.ToListAsync());
        }

        private static string EscapeIdentifier(string identifier)
            => identifier?.Replace("]", "]]");
    }
}