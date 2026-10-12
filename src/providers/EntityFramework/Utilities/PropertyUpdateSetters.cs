using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace Dime.Repositories
{
    /// <summary>
    /// Translates a <see cref="PropertyUpdate{TEntity}"/> into the setters of a single EF Core ExecuteUpdate call.
    /// </summary>
    internal sealed class PropertyUpdateSetters<TEntity>(UpdateSettersBuilder<TEntity> setters) : IPropertyUpdateVisitor<TEntity>
        where TEntity : class
    {
        public void Visit<TValue>(Expression<Func<TEntity, TValue>> propertySelector, TValue value)
            => setters.SetProperty(propertySelector, value);

        public void Visit<TValue>(Expression<Func<TEntity, TValue>> propertySelector, Expression<Func<TEntity, TValue>> valueExpression)
            => setters.SetProperty(propertySelector, valueExpression);

        /// <summary>
        /// Refuses a missing or empty update before any statement reaches the data store.
        /// </summary>
        internal static void EnsureNotEmpty(PropertyUpdate<TEntity> update)
        {
            ArgumentNullException.ThrowIfNull(update);
            if (update.IsEmpty)
                throw new ArgumentException("The update must contain at least one property assignment.", nameof(update));
        }
    }
}
