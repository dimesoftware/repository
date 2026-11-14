using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    /// <summary>
    /// Describes a set of property assignments to apply to every entity matching a filter in one statement.
    /// Built fluently: <c>new PropertyUpdate&lt;Job&gt;().Set(x =&gt; x.Status, value).Set(x =&gt; x.IsOpen, false)</c>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to update</typeparam>
    public sealed class PropertyUpdate<TEntity> where TEntity : class
    {
        private readonly List<PropertyAssignment<TEntity>> _assignments = [];

        /// <summary>
        /// The assignments in the order they were added.
        /// </summary>
        public IReadOnlyList<PropertyAssignment<TEntity>> Assignments => _assignments;

        /// <summary>
        /// The number of assignments.
        /// </summary>
        public int Count => _assignments.Count;

        /// <summary>
        /// Indicates whether no assignment has been added yet.
        /// </summary>
        public bool IsEmpty => _assignments.Count == 0;

        /// <summary>
        /// Sets the property to a constant value.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value</typeparam>
        /// <param name="propertySelector">Expression selecting the property to update</param>
        /// <param name="value">The new value to set</param>
        /// <returns>This instance, for chaining</returns>
        public PropertyUpdate<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> propertySelector, TValue value)
        {
            ArgumentNullException.ThrowIfNull(propertySelector);
            _assignments.Add(new ConstantPropertyAssignment<TEntity, TValue>(propertySelector, value));
            return this;
        }

        /// <summary>
        /// Sets the property using an expression that can reference the existing entity.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value</typeparam>
        /// <param name="propertySelector">Expression selecting the property to update</param>
        /// <param name="valueExpression">Expression that calculates the new value based on the existing entity</param>
        /// <returns>This instance, for chaining</returns>
        public PropertyUpdate<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> propertySelector, Expression<Func<TEntity, TValue>> valueExpression)
        {
            ArgumentNullException.ThrowIfNull(propertySelector);
            ArgumentNullException.ThrowIfNull(valueExpression);
            _assignments.Add(new ExpressionPropertyAssignment<TEntity, TValue>(propertySelector, valueExpression));
            return this;
        }

        /// <summary>
        /// Dispatches every assignment, in order, to the visitor.
        /// </summary>
        /// <param name="visitor">The visitor that translates the assignments for a data store</param>
        public void Accept(IPropertyUpdateVisitor<TEntity> visitor)
        {
            ArgumentNullException.ThrowIfNull(visitor);
            foreach (PropertyAssignment<TEntity> assignment in _assignments)
                assignment.Accept(visitor);
        }
    }
}
