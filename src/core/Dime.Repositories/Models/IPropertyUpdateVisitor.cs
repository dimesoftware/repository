using System;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    /// <summary>
    /// Receives the assignments of a <see cref="PropertyUpdate{TEntity}"/> with their value types preserved,
    /// so a provider can translate them without reflection.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to update</typeparam>
    public interface IPropertyUpdateVisitor<TEntity> where TEntity : class
    {
        /// <summary>
        /// Visits an assignment of a constant value.
        /// </summary>
        void Visit<TValue>(Expression<Func<TEntity, TValue>> propertySelector, TValue value);

        /// <summary>
        /// Visits an assignment of a value expression that can reference the existing entity.
        /// </summary>
        void Visit<TValue>(Expression<Func<TEntity, TValue>> propertySelector, Expression<Func<TEntity, TValue>> valueExpression);
    }
}
