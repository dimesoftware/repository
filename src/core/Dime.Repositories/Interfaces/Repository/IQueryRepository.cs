using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    /// <summary>
    /// Synchronous read-side surface of a repository. The asynchronous counterparts
    /// live in <see cref="IQueryRepositoryAsync{TEntity}"/>; this interface intentionally
    /// holds only the blocking variants so callers can be explicit about what they use.
    /// </summary>
    /// <typeparam name="TEntity">The entity type managed by the repository.</typeparam>
    public partial interface IQueryRepository<TEntity> : IDisposable where TEntity : class
    {
        /// <summary>
        /// Gets the record by its identifier.
        /// </summary>
        /// <param name="id">The identifier of the entity.</param>
        /// <returns>The record of type <typeparamref name="TEntity"/> that matches the id.</returns>
        TEntity FindById(object? id);

        /// <summary>
        /// Checks if a record matching the predicate exists.
        /// </summary>
        /// <param name="where">The expression to execute against the data store.</param>
        bool Exists(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> predicate.
        /// </summary>
        TEntity FindOne(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Gets the first record from the data store that matches the <paramref name="where"/> predicate,
        /// eagerly loading the supplied navigation property paths.
        /// </summary>
        TEntity FindOne(Expression<Func<TEntity, bool>> where, params string[] includes);

        /// <summary>
        /// Finds entities based on the provided predicate.
        /// </summary>
        IEnumerable<TEntity> FindAll(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Finds entities based on the provided predicate with paging and includes.
        /// </summary>
        IEnumerable<TEntity> FindAll(Expression<Func<TEntity, bool>> where, int? page, int? pageSize, string[] includes);

        /// <summary>
        /// Finds entities based on the provided predicate, optionally eager-loading every navigation
        /// or just the supplied ones.
        /// </summary>
        IEnumerable<TEntity> FindAll(Expression<Func<TEntity, bool>> where, bool includeAll, params string[] includes);

        /// <summary>
        /// Retrieves a collection of projected, sorted, paged and filtered items in a flat list.
        /// </summary>
        /// <typeparam name="TResult">The projected class.</typeparam>
        IEnumerable<TResult> FindAll<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Counts every record in the table that corresponds to <typeparamref name="TEntity"/>.
        /// </summary>
        long Count();

        /// <summary>
        /// Counts every record in the table that corresponds to <typeparamref name="TEntity"/>
        /// matching the supplied predicate.
        /// </summary>
        long Count(Expression<Func<TEntity, bool>> where);
    }
}