using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    internal static partial class QueryFactory
    {
        internal static IQueryable<TResult> WithSelect<TSource, TResult, TKey>(
            this IQueryable<IGrouping<TKey, TSource>> source,
            Expression<Func<IGrouping<TKey, TSource>, IEnumerable<TResult>>> selector)
            => selector == null ? default : source.SelectMany(selector);

        internal static IQueryable<TResult> WithSelect<TSource, TResult>(
            this IQueryable<IGrouping<object, TSource>> source,
            Expression<Func<IGrouping<object, TSource>, int, TResult>> selector)
            => selector == null ? default : source.Select(selector);

        internal static IQueryable<TResult> WithSelect<TSource, TResult>(this IOrderedEnumerable<TSource> source,
            Func<TSource, TResult> selector)
            where TSource : class
            where TResult : class
            => selector == null ? default : source.Select(selector).AsQueryable();

        internal static IQueryable<TResult> WithSelect<TSource, TResult>(this IQueryable<TSource> source,
            Expression<Func<TSource, TResult>> selector)
            where TSource : class
            => selector == null ? default : source.Select(selector).AsQueryable();

        internal static IQueryable<TResult> WithSelect<TSource, TResult>(this IQueryable<TSource> source,
            Expression<Func<TSource, TResult>> selector,
            Expression<Func<TResult, bool>> where)
            where TSource : class
            => selector == null ? default : where == null ? source.Select(selector).AsQueryable() : source.Select(selector).Where(where).AsQueryable();

        internal static IQueryable<TResult> WithSelect<TSource, TResult>(this IQueryable<TSource> source,
            Func<TSource, TResult> selector)
            where TSource : class
            where TResult : class
            => selector == null ? default : source.Select(selector).AsQueryable();

        internal static TResult WithFirstSelect<TSource, TResult>(this TSource source,
            Expression<Func<TSource, TResult>> selector)
            where TSource : class
            where TResult : class
            => selector == null
                ? default
                : new List<TSource> { source }.AsQueryable().Select(selector).FirstOrDefault();
    }
}