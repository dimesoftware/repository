using System;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    internal sealed class ExpressionPropertyAssignment<TEntity, TValue>(Expression<Func<TEntity, TValue>> propertySelector, Expression<Func<TEntity, TValue>> valueExpression)
        : PropertyAssignment<TEntity> where TEntity : class
    {
        public override LambdaExpression PropertySelector => propertySelector;

        public override void Accept(IPropertyUpdateVisitor<TEntity> visitor) => visitor.Visit<TValue>(propertySelector, valueExpression);
    }
}
