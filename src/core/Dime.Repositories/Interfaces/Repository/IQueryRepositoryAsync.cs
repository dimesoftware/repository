using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// Asynchronous read-side surface of a repository. The blocking counterparts live in
    /// <see cref="IQueryRepository{TEntity}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
    public partial interface IQueryRepositoryAsync<TEntity> : IDisposable where TEntity : class
    {
        /// <summary>
        /// Gets the record by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <returns>The record of type <typeparamref name="TEntity"/> that matches the id</returns>
        Task<TEntity> FindByIdAsync(object? id);

        /// <summary>
        /// Gets the record by its identifier, eagerly loading the supplied navigation paths.
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>The record of type <typeparamref name="TEntity"/> that matches the id</returns>
        Task<TEntity> FindByIdAsync(object? id, params string[] includes);

        /// <summary>
        /// Checks if a record matching the predicate exists.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        Task<TEntity> FindOneAsync(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter,
        /// eagerly loading the supplied navigation paths.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        Task<TEntity> FindOneAsync(Expression<Func<TEntity, bool>> where, params string[] includes);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> parameter.
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        Task<TResult> FindOneAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
            where TResult : class;

        /// <summary>
        /// Finds entities based on provided criteria.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>An collection of <typeparamref name="TEntity"/> that matched all filters.</returns>
        Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> where, params string[] includes);

        /// <summary>
        /// Retrieves a collection of projected, sorted, paged and filtered items in a flat list.
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the pagesize to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>An collection of <typeparamref name="TResult"/> with the mapped data from the records that matched all filters.</returns>
        Task<IEnumerable<TResult>> FindAllAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Same as the seven-arg FindAllAsync but with EF Core split-query opt-in.
        /// When <paramref name="splitQuery"/> is true, the EF provider runs separate
        /// SQL statements for each collection navigation in the projection — avoiding
        /// the Cartesian fan-out that explodes logical reads on multi-collection projections.
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="ascending">Indicates whether the sorting is ascending (true) or descending (false)</param>
        /// <param name="page">The page number which is multiplied by the pagesize to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="splitQuery">When true, opts into EF Core's AsSplitQuery() to avoid Cartesian fan-out on multi-collection projections.</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        Task<IEnumerable<TResult>> FindAllAsync<TResult>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, TResult>> select,
            Expression<Func<TEntity, object>> orderBy,
            bool? ascending,
            int? page,
            int? pageSize,
            bool splitQuery,
            params string[] includes)
            where TResult : class;

        /// <summary>
        /// Retrieves a collection of paged, sorted and filtered items in a flat list.
        /// </summary>
        Task<IEnumerable<TEntity>> FindAllAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Counts every record in the table that corresponds to <typeparamref name="TEntity"/>.
        /// </summary>
        Task<long> CountAsync();

        /// <summary>
        /// Counts every record in the table that corresponds to <typeparamref name="TEntity"/>
        /// matching the supplied predicate.
        /// </summary>
        Task<long> CountAsync(Expression<Func<TEntity, bool>> where);
    }
}
