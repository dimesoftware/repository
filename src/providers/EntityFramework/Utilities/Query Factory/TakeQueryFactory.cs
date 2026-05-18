using System.Linq;

namespace Dime.Repositories
{
    internal static class TakeQueryFactory
    {
        internal static IQueryable<TSource> With<TSource>(this IQueryable<TSource> source, int? takeCount)
        {
            int itemsToTake = takeCount.GetValueOrDefault();
            return itemsToTake == 0 ? source : source.Take(itemsToTake);
        }
    }
}