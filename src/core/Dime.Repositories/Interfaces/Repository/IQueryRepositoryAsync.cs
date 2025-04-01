using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public partial interface IQueryRepositoryAsync<TEntity> : IDisposable where TEntity : class
    {
        /// <summary>
        /// Gets the record by its identifier
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <returns>The record of type <typeparamref name="TEntity"/> that matches the id</returns>
        TEntity FindById(object? id);

        /// <summary>
        /// Gets the record by its identifier
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <returns>The record of type <typeparamref name="TEntity"/> that matches the id</returns>
        Task<TEntity> FindByIdAsync(object? id);

        /// <summary>
        /// Gets the record by its identifier
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>The record of type <typeparamref name="TEntity"/> that matches the id</returns>
        Task<TEntity> FindByIdAsync(object? id, params string[] includes);

        /// <summary>
        /// Checks if the record exists
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <returns>The first record of type <typeparamref name="TEntity"/> that matches the query</returns>
        bool Exists(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Checks if the record exists
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <returns>The first record of type <typeparamref name="TEntity"/> that matches the query</returns>
        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <returns>The first record of type <typeparamref name="TEntity"/> that matches the query</returns>
        TEntity FindOne(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <returns>The first record of type <typeparamref name="TEntity"/> that matches the query</returns>
        Task<TEntity> FindOneAsync(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>The first record of type <typeparamref name="TEntity"/> that matches the query</returns>
        TEntity FindOne(Expression<Func<TEntity, bool>> where, params string[] includes);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>The first record of type <typeparamref name="TEntity"/> that matches the query</returns>
        Task<TEntity> FindOneAsync(Expression<Func<TEntity, bool>> where, params string[] includes);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="ascending">Indicates whether the sorting is ascending (true) or descending (false)</param>
        /// <param name="page">The page number which is multiplied by the pagesize to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <returns>An instance of <typeparamref name="TResult"/> with the mapped data from the record that matched all filters.</returns>
        Task<TResult> FindOneAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null)
            where TResult : class;

        /// <summary>
        /// Finds entities based on provided criteria.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        IEnumerable<TEntity> FindAll(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Finds entities based on provided criteria.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="includes"></param>
        /// <param name="page"></param>
        /// <param name="pageSize"></param>
        IEnumerable<TEntity> FindAll(Expression<Func<TEntity, bool>> where, int? page, int? pageSize, string[] includes);

        /// <summary>
        ///
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="includeAll"></param>
        /// <param name="includes"></param>
        /// <returns></returns>
        IEnumerable<TEntity> FindAll(Expression<Func<TEntity, bool>> where, bool includeAll, params string[] includes);

        /// <summary>
        /// Retrieves a collection of projected,sorted, paged and filtered items in a flat list
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the pagesize to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <returns>An collection of <typeparamref name="TResult"/> with the mapped data from the records that matched all filters.</returns>
        IEnumerable<TResult> FindAll<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null);

        /// <summary>
        /// Finds entities based on provided criteria.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>An collection of <typeparamref name="TEntity"/> that matched all filters.</returns>
        Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> where, params string[] includes);

        /// <summary>
        /// Retrieves a collection of projected,sorted, paged and filtered items in a flat list
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the pagesize to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <returns>An collection of <typeparamref name="TResult"/> with the mapped data from the records that matched all filters.</returns>
        Task<IEnumerable<TResult>> FindAllAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TResult, bool>> selectWhere = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null);

        /// <summary>
        /// Retrieves a collection of paged, sorted and filtered items in a flat list
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The order by.</param>
        /// <param name="page">The page number which is multiplied by the pagesize to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>An collection of <typeparamref name="TResult"/> with the mapped data from the records that matched all filters.</returns>
        Task<IEnumerable<TEntity>> FindAllAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Counts the amount of records in the data store for the table that corresponds to the entity type <typeparamref name="TEntity"/>.
        /// </summary>
        /// <returns>A number of the amount of records</returns>
        Task<long> CountAsync();

        /// <summary>
        /// Counts the amount of records in the data store for the table that corresponds to the entity type <typeparamref name="TEntity"/>.
        /// </summary>
        /// <returns>A number of the amount of records</returns>
        long Count();

        /// <summary>
        /// Counts the amount of records in the data store for the table that corresponds to the entity type <typeparamref name="TEntity"/>.
        /// </summary>
        /// <returns>A number of the amount of records</returns>
        /// <param name="where">The expression to execute against the data store</param>
        /// <history>
        Task<long> CountAsync(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Counts the amount of records in the data store for the table that corresponds to the entity type <typeparamref name="TEntity"/>.
        /// </summary>
        /// <returns>A number of the amount of records</returns>
        /// <param name="where">The expression to execute against the data store</param>
        /// <history>
        long Count(Expression<Func<TEntity, bool>> where);
    }
}