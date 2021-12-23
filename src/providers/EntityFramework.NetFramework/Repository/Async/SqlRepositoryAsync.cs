﻿using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        /// <summary>
        /// Executes the SQL asynchronous.
        /// </summary>
        /// <param name="sql">The SQL.</param>
        /// <returns></returns>
        public async Task ExecuteSqlAsync(string sql)
        {
            
            await Context.Database.ExecuteSqlCommandAsync(sql).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the stored procedure asynchronous.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
        public async Task<int> ExecuteStoredProcedureAsync(string name, params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", y.Select(z => $"{z.ParameterName}={z.Value}"));
                return $"EXEC {x} {parameterString}";
            }

            string execQueryString = ExecQuery(name, parameters);
            
            return await Context.Database.ExecuteSqlCommandAsync(execQueryString, parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the stored procedure asynchronous.
        /// </summary>
        /// <param name="name">The name of the stored procedure.</param>
        /// <param name="schema"></param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
        public async Task<int> ExecuteStoredProcedureAsync(string name, string schema = "dbo", params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", y.Select(z => $"{z.ParameterName}={z.Value}"));
                return $"EXEC {schema}.{x} {parameterString}";
            }

            string execQueryString = ExecQuery(name, parameters);
            
            return await Context.Database.ExecuteSqlCommandAsync(execQueryString, parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the stored procedure asynchronous.
        /// </summary>
        /// <param name="name">The name of the stored procedure.</param>
        /// <param name="schema"></param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
        public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string name, string schema = "dbo", params DbParameter[] parameters)
        {
            using DbConnection connection = Context.Database.Connection;
            connection.Open();
            using DbCommand cmd = connection.CreateCommand();
            cmd.CommandText = name;
            cmd.CommandType = System.Data.CommandType.StoredProcedure;
            cmd.Parameters.AddRange(parameters);

            using IDataReader reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
            return reader.GetRecords<T>();
        }

        /// <summary>
        /// Executes the stored procedure asynchronous.
        /// </summary>
        /// <param name="name">The name of the stored procedure.</param>
        /// <param name="schema"></param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
        public async Task<int> ExecuteStoredProcedureAsync<T>(T name, string schema = "dbo", params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", y.Select(z => $"{z.ParameterName}={z.Value}"));
                return $"EXEC {schema}.{nameof(x)} {parameterString}";
            }

            string execQueryString = ExecQuery(nameof(name), parameters);
            
            return await Context.Database.ExecuteSqlCommandAsync(execQueryString, parameters).ConfigureAwait(false);
        }

        /// <summary>
        /// Executes the stored procedure asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="command">The name of the stored procedure.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns></returns>
        public async Task<IEnumerable<T>> ExecuteStoredProcedureAsync<T>(string command, params DbParameter[] parameters)
        {
            string ExecQuery(string x, DbParameter[] y)
            {
                string parameterString = string.Join(",", y.Select(z => $"@{z.ParameterName}={z.Value}"));
                return $"EXEC {x} {parameterString}";
            }

            return await Task.Run(() =>
            {
                
                return Context.Database.SqlQuery<T>(ExecQuery(command, parameters));
            }).ConfigureAwait(false);
        }
    }
}