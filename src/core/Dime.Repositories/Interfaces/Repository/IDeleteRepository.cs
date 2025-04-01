using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// Definition for a repository which supports the capability to delete items from the data store
    /// </summary>
    /// <typeparam name="TEntity">The collection type</typeparam>
    public interface IDeleteRepository<TEntity> : IDisposable where TEntity : class
    {
        /// <summary>
        /// Removes the record from the data store by its identifier
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <returns>Void</returns>
        void Delete(object? id);

        /// <summary>
        /// Removes the record from the data store by its identifier
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <returns>Void</returns>
        Task DeleteAsync(object? id);

        /// <summary>
        /// Removes all records
        /// </summary>
        /// <returns>Task</returns>
        Task DeleteAsync();

        /// <summary>
        /// Removes the record from the data store by its identifier
        /// </summary>
        /// <param name="id">The identifier of the entity</param>
        /// <param name="commit">Indicates whether or not SaveChangesAsync should be called during this call</param>
        /// <returns>Void</returns>
        Task DeleteAsync(object? id, bool commit);

        /// <summary>
        /// Removes the record from the data store by its identifiers
        /// </summary>
        /// <param name="ids">The identifiers of the entity</param>
        /// <returns>Void</returns>
        Task DeleteAsync(IEnumerable<object?> ids);

        /// <summary>
        /// Removes the record
        /// </summary>
        /// <param name="entity">The entity to remove</param>
        /// <returns>Task</returns>
        void Delete(TEntity entity);

        /// <summary>
        /// Removes the record
        /// </summary>
        /// <param name="entity">The entity to remove</param>
        /// <returns>Task</returns>
        Task DeleteAsync(TEntity entity);

        /// <summary>
        /// Deletes the existing entity.
        /// </summary>
        void Delete(Expression<Func<TEntity, bool>> where);

        /// <summary>
        /// Removes the records
        /// </summary>
        /// <param name="entities">The disconnected entities to remove</param>
        /// <returns>Void</returns>
        Task DeleteAsync(IEnumerable<TEntity> entities);

        /// <summary>
        /// Removes the record from the data store
        /// </summary>
        /// <param name="entity">The disconnected entity to remove</param>
        /// <param name="commit">Indicates whether or not SaveChangesAsync should be called during this call</param>
        /// <returns>Task</returns>
        Task DeleteAsync(TEntity entity, bool commit);

        /// <summary>
        /// Removes the record from the data store
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <returns>Void</returns>
        Task DeleteAsync(Expression<Func<TEntity, bool>> where);
    }
}