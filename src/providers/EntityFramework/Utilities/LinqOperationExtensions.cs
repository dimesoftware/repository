using System.Collections.Generic;
using System.Linq;

namespace Dime.Repositories
{
    internal static class OrderLinq
    {
        internal static IOrderedQueryable<T> OrderDescending<T>(this IEnumerable<T> query, string propertyName)
        {
            LinqOrderHelper<T> helper = new("OrderByDescending", propertyName);
            return helper.GetAsQueryable(query);
        }

        internal static IOrderedQueryable<T> Order<T>(this IEnumerable<T> query, string propertyName)
        {
            LinqOrderHelper<T> helper = new("OrderBy", propertyName);
            return helper.GetAsQueryable(query);
        }

        internal static IOrderedQueryable<TSource> ThenBy<TSource>(this IEnumerable<TSource> source, string property)
        {
            LinqOrderHelper<TSource> helper = new("ThenBy", property);
            return helper.GetAsQueryable(source);
        }

        internal static IOrderedQueryable<TSource> ThenByDescending<TSource>(this IEnumerable<TSource> source, string property)
        {
            LinqOrderHelper<TSource> helper = new("ThenByDescending", property);
            return helper.GetAsQueryable(source);
        }
    }
}