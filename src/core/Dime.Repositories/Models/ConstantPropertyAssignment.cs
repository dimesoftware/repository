using System;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    internal sealed class ConstantPropertyAssignment<TEntity, TValue>(Expression<Func<TEntity, TValue>> propertySelector, TValue value)
        : PropertyAssignment<TEntity> where TEntity : class
    {
        public override LambdaExpression PropertySelector => propertySelector;

        public override void Accept(IPropertyUpdateVisitor<TEntity> visitor) => visitor.Visit<TValue>(propertySelector, value);
    }
}
