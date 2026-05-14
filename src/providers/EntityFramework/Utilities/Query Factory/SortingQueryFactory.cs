using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    internal static partial class QueryFactory
    {
        internal static IQueryable<TSource> WithOrder<TSource>(this IQueryable<TSource> source, IEnumerable<IOrder<TSource>> orderByExpression)
        {
            if (orderByExpression != null && orderByExpression.Count() > 1)
            {
                IEnumerable<TSource> orderBy = null;
                for (int i = 0; i < orderByExpression.Count(); i++)
                {
                    (string property, bool isAscending) = orderByExpression.ElementAt(i);
                    orderBy = i == 0
                        ? isAscending ? source.Order(property) : source.OrderDescending(property)
                        : isAscending ? orderBy.ThenBy(property) : orderBy.ThenByDescending(property);
                }

                return orderBy.AsQueryable();
            }

            if (orderByExpression != null && orderByExpression.Count() == 1)
                return orderByExpression.ElementAt(0).IsAscending ?
                    source.Order(orderByExpression.ElementAt(0).Property).AsQueryable() :
                    source.OrderDescending(orderByExpression.ElementAt(0).Property).AsQueryable();

            return source.OrderBy(x => true);
        }

        internal static IQueryable<TSource> WithOrder<TSource>(this IQueryable<TSource> source, IEnumerable<Expression<Func<TSource, object>>> orderByExpression, bool ascending)
        {
            if (orderByExpression == null)
                return source;
            if (orderByExpression.Count() > 1)
            {
                Expression<Func<TSource, object>> first = orderByExpression.ElementAt(0);
                Expression<Func<TSource, object>> second = orderByExpression.ElementAt(1);

                IOrderedQueryable<TSource> primary = ascending
                    ? source.OrderBy(first)
                    : source.OrderByDescending(first);

                return ascending
                    ? primary.ThenBy(second)
                    : primary.ThenByDescending(second);
            }

            Expression<Func<TSource, object>> only = orderByExpression.ElementAt(0);
            return ascending
                ? source.OrderBy(only)
                : source.OrderByDescending(only);
        }

        internal static IQueryable<TSource> WithOrder<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, object>> orderByExpression, bool ascending)
        {
            if (orderByExpression == null)
                return source;
            return ascending
                ? source.OrderBy(orderByExpression)
                : source.OrderByDescending(orderByExpression);
        }

        internal static IQueryable<TSource> WithOrder<TSource>(this IQueryable<TSource> source, Func<TSource, object> orderByExpression, bool ascending)
        {
            if (orderByExpression == null)
            {
                Func<TSource, object> defaultSorting = x => true;
                return ascending ? source.OrderBy(defaultSorting).AsQueryable() :
                    source.OrderByDescending(defaultSorting).AsQueryable();
            }

            return ascending ? source.OrderBy(orderByExpression).AsQueryable() :
                source.OrderByDescending(orderByExpression).AsQueryable();
        }
    }
}