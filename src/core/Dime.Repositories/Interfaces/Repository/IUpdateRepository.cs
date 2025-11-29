using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// Definition for a repository which supports the capability to update items in the data store
    /// </summary>
    /// <typeparam name="TEntity">The collection type</typeparam>
    public interface IUpdateRepository<TEntity> : IDisposable where TEntity : class
    {
        /// <summary>
        /// Updates the existing entity.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <returns>The updated entity</returns>
        TEntity Update(TEntity entity);

        /// <summary>
        /// Updates the existing entity.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="commitChanges">Indication whether or not the SaveChangesAsync should be called during this call</param>
        /// <returns>The updated entity</returns>
        TEntity Update(TEntity entity, bool commitChanges = true);

        /// <summary>
        /// Updates the existing entity.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="commitChanges">Indication whether or not the SaveChangesAsync should be called during this call</param>
        /// <returns>The updated entity</returns>
        Task<TEntity> UpdateAsync(TEntity entity, bool commitChanges = true);

        /// <summary>
        /// Updates the entities
        /// </summary>
        /// <param name="entities">The entities to update</param>
        /// <param name="commitChanges">Indication whether or not the SaveChangesAsync should be called during this call</param>
        /// <returns>Void</returns>
        Task UpdateAsync(IEnumerable<TEntity> entities, bool commitChanges = true);

        /// <summary>
        /// Updates the existing entity.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="properties">The properties of the entity to update</param>
        /// <returns>The updated entity</returns>
        Task<TEntity> UpdateAsync(TEntity entity, params Expression<Func<TEntity, object>>[] properties);

        /// <summary>
        /// Updates the existing entity.
        /// </summary>
        /// <param name="entity">The entity to update</param>
        /// <param name="properties">The properties of the entity to update</param>
        /// <returns>The updated entity</returns>
        Task<TEntity> UpdateAsync(TEntity entity, params string[] properties);

        /// <summary>
        /// Updates entities matching the where clause by setting a property to a new value.
        /// Uses ExecuteUpdate for efficient bulk updates without loading entities.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value</typeparam>
        /// <param name="where">Expression to filter entities to update</param>
        /// <param name="propertySelector">Expression selecting the property to update</param>
        /// <param name="newValue">The new value to set</param>
        /// <returns>The number of entities updated</returns>
        Task<int> UpdateAsync<TValue>(Expression<Func<TEntity, bool>> where, Expression<Func<TEntity, TValue>> propertySelector, TValue newValue);

        /// <summary>
        /// Updates entities matching the where clause by setting a property using an expression that references the existing value.
        /// Uses ExecuteUpdate for efficient bulk updates without loading entities.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value</typeparam>
        /// <param name="where">Expression to filter entities to update</param>
        /// <param name="propertySelector">Expression selecting the property to update</param>
        /// <param name="valueExpression">Expression that calculates the new value based on the existing entity</param>
        /// <returns>The number of entities updated</returns>
        Task<int> UpdateAsync<TValue>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, TValue>> propertySelector,
            Expression<Func<TEntity, TValue>> valueExpression);

        /// <summary>
        /// Updates entities matching the where clause (synchronous version).
        /// Uses ExecuteUpdate for efficient bulk updates without loading entities.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value</typeparam>
        /// <param name="where">Expression to filter entities to update</param>
        /// <param name="propertySelector">Expression selecting the property to update</param>
        /// <param name="newValue">The new value to set</param>
        /// <returns>The number of entities updated</returns>
        int Update<TValue>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, TValue>> propertySelector,
            TValue newValue);

        /// <summary>
        /// Updates entities matching the where clause by setting a property using an expression that references the existing value (synchronous version).
        /// Uses ExecuteUpdate for efficient bulk updates without loading entities.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value</typeparam>
        /// <param name="where">Expression to filter entities to update</param>
        /// <param name="propertySelector">Expression selecting the property to update</param>
        /// <param name="valueExpression">Expression that calculates the new value based on the existing entity</param>
        /// <returns>The number of entities updated</returns>
        int Update<TValue>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, TValue>> propertySelector,
            Expression<Func<TEntity, TValue>> valueExpression);
    }
}