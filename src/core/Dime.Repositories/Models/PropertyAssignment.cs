using System.Linq.Expressions;

namespace Dime.Repositories
{
    /// <summary>
    /// A single property assignment in a <see cref="PropertyUpdate{TEntity}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to update</typeparam>
    public abstract class PropertyAssignment<TEntity> where TEntity : class
    {
        private protected PropertyAssignment()
        {
        }

        /// <summary>
        /// The expression selecting the property to update.
        /// </summary>
        public abstract LambdaExpression PropertySelector { get; }

        /// <summary>
        /// Dispatches this assignment to the visitor with its value type preserved.
        /// </summary>
        /// <param name="visitor">The visitor</param>
        public abstract void Accept(IPropertyUpdateVisitor<TEntity> visitor);
    }
}
