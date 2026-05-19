using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// Defines a SQL repository interface with support for executing SQL commands and stored procedures.
    /// </summary>
    /// <typeparam name="T">The type of the entity.</typeparam>
    public interface ISqlRepository<T> : IRepository<T>, IStoredProcedureRepository where T : class
    {
        /// <summary>
        /// Executes the provided SQL command asynchronously.
        /// </summary>
        /// <param name="sql">The SQL command to execute.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ExecuteSqlAsync(string sql);

        /// <summary>
        /// Executes the specified stored procedure asynchronously.
        /// </summary>
        /// <param name="command">The name of the stored procedure to execute.</param>
        /// <param name="parameters">The parameters to pass to the stored procedure.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows affected.</returns>
        Task<int> ExecuteStoredProcedureAsync(string command, params DbParameter[] parameters);

        /// <summary>
        /// Executes the specified stored procedure asynchronously with a given schema.
        /// </summary>
        /// <param name="command">The name of the stored procedure to execute.</param>
        /// <param name="schema">The schema of the stored procedure. Defaults to "dbo".</param>
        /// <param name="parameters">The parameters to pass to the stored procedure.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the number of rows affected.</returns>
        Task<int> ExecuteStoredProcedureAsync(string command, string schema = "dbo", params DbParameter[] parameters);

        /// <summary>
        /// Executes the specified stored procedure asynchronously and returns a single result.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="command">The name of the stored procedure to execute.</param>
        /// <param name="schema">The schema of the stored procedure. Defaults to "dbo".</param>
        /// <param name="parameters">The parameters to pass to the stored procedure.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the result of the stored procedure.</returns>
        Task<int> ExecuteStoredProcedureAsync<TResult>(TResult command, string schema = "dbo", params DbParameter[] parameters);

        /// <summary>
        /// Executes the specified stored procedure asynchronously and returns a collection of results.
        /// </summary>
        /// <typeparam name="TResult">The type of the results.</typeparam>
        /// <param name="command">The name of the stored procedure to execute.</param>
        /// <param name="parameters">The parameters to pass to the stored procedure.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of results.</returns>
        Task<IEnumerable<TResult>> ExecuteStoredProcedureAsync<TResult>(string command, params DbParameter[] parameters);

        /// <summary>
        /// Executes the specified stored procedure asynchronously with a given schema and returns a collection of results.
        /// </summary>
        /// <typeparam name="TResult">The type of the results.</typeparam>
        /// <param name="name">The name of the stored procedure to execute.</param>
        /// <param name="schema">The schema of the stored procedure. Defaults to "dbo".</param>
        /// <param name="parameters">The parameters to pass to the stored procedure.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains a collection of results.</returns>
        Task<IEnumerable<TResult>> ExecuteStoredProcedureAsync<TResult>(string name, string schema = "dbo", params DbParameter[] parameters);

        /// <summary>
        /// Executes a SQL Server inline table-valued function and projects results.
        /// SQL emitted: SELECT * FROM [schema].[name](@p1, @p2, ...)
        /// </summary>
        /// <typeparam name="TResult">The projected result type.</typeparam>
        /// <param name="name">The name of the inline table-valued function.</param>
        /// <param name="schema">The schema of the function. Defaults to "dbo".</param>
        /// <param name="parameters">The parameters to pass to the function.</param>
        /// <param name="select">Optional projection expression. When null, the implementation requires <typeparamref name="TResult"/> to equal <typeparamref name="T"/>.</param>
        /// <param name="splitQuery">
        /// Calls <c>AsSplitQuery()</c> on the underlying <c>FromSqlRaw</c> query.
        /// <para>
        /// In practice this is a no-op for typical TVF usage: this overload does not expose
        /// <c>Include</c>, and <c>AsSplitQuery</c> only takes effect when EF Core has
        /// collection navigations to split. The parameter is kept for symmetry with
        /// <c>FindAllAsync</c> / <c>FindAllPagedAsync</c> and reserved for future use if
        /// include support is added here.
        /// </para>
        /// </param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the materialized rows.</returns>
        Task<IEnumerable<TResult>> QueryFunctionAsync<TResult>(string name, string schema = "dbo", DbParameter[] parameters = null, Expression<Func<T, TResult>> select = null, bool splitQuery = false) where TResult : class;
    }
}