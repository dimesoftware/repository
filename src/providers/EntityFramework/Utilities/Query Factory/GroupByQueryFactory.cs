using System;
using System.Linq;
using System.Linq.Expressions;

namespace Dime.Repositories
{
    internal static partial class QueryFactory
    {
        public static IQueryable<IGrouping<TKey, TSource>> WithGroup<TSource, TKey>(this IQueryable<TSource> source, Expression<Func<TSource, TKey>> predicate)
            => source.GroupBy(predicate);

        public static IQueryable<IGrouping<TKey, TSource>> WithGroup<TSource, TKey>(this IQueryable<TSource> source, Func<TSource, TKey> predicate)
            => source.GroupBy(predicate).AsQueryable();

        public static IQueryable<IGrouping<TKey, TSource>> WithGroup<TSource, TKey>(this IOrderedEnumerable<TSource> source, Func<TSource, TKey> predicate)
            => source.GroupBy(predicate).AsQueryable();
    }
}