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
            // No order requested: leave the query alone. Paging callers downstream
            // (SkipQueryFactory.With(page, pageSize, orderBy)) inject a stable fallback
            // ORDER BY when Skip is actually applied — emitting a constant sort here just
            // forces an extra "ORDER BY (constant)" on every unpaged call.
            if (orderByExpression == null)
                return source;

            int count = orderByExpression.Count();
            if (count == 0)
                return source;

            if (count > 1)
            {
                IEnumerable<TSource> orderBy = null;
                for (int i = 0; i < count; i++)
                {
                    (string property, bool isAscending) = orderByExpression.ElementAt(i);
                    orderBy = i == 0
                        ? isAscending ? source.Order(property) : source.OrderDescending(property)
                        : isAscending ? orderBy.ThenBy(property) : orderBy.ThenByDescending(property);
                }

                return orderBy.AsQueryable();
            }

            return orderByExpression.ElementAt(0).IsAscending
                ? source.Order(orderByExpression.ElementAt(0).Property).AsQueryable()
                : source.OrderDescending(orderByExpression.ElementAt(0).Property).AsQueryable();
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

        /// <summary>
        /// Orders by a compiled <see cref="Func{TSource, TResult}"/> via LINQ-to-objects.
        /// <para>
        /// This overload intentionally materializes the query into memory and orders client-side.
        /// Callers passing a <see cref="Func{T, TResult}"/> (rather than an
        /// <see cref="System.Linq.Expressions.Expression{TDelegate}"/>) are opting out of provider
        /// translation — there is no SQL-translatable equivalent to an arbitrary delegate.
        /// </para>
        /// <para>
        /// For database-backed queries, prefer the <see cref="Expression{T}"/>-based overloads;
        /// they translate ordering (and any downstream paging) into SQL.
        /// </para>
        /// </summary>
        internal static IQueryable<TSource> WithOrder<TSource>(this IQueryable<TSource> source, Func<TSource, object> orderByExpression, bool ascending)
        {
            // No-op when no order is requested. The paging extension injects a fallback ORDER BY
            // when Skip is applied, so unpaged callers no longer pay for a constant sort.
            if (orderByExpression == null)
                return source;

            return ascending
                ? source.OrderBy(orderByExpression).AsQueryable()
                : source.OrderByDescending(orderByExpression).AsQueryable();
        }
    }
}