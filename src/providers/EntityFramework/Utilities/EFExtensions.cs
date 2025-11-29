using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dime.Repositories
{
    [ExcludeFromCodeCoverage]
    internal static class EFExtensions
    {
        private static IEntityType GetEntityType<T>(DbContext context)
            => context.Model.FindEntityType(typeof(T));

        internal static IQueryable<TEntity> Include<TEntity>(this IQueryable<TEntity> query, DbContext context, params string[] includes) where TEntity : class
        {
            if (includes == null)
                return query;

            List<string> includeList = [];
            if (includes.Length != 0)
                return includes
                    .Where(x => !string.IsNullOrEmpty(x) && !includeList.Contains(x))
                    .Aggregate(query, (current, include) => current.Include(include));

            IEnumerable<INavigation> navigationProperties = context.Model.FindEntityType(typeof(TEntity)).GetNavigations();
            if (navigationProperties == null)
                return query;

            foreach (INavigation navigationProperty in navigationProperties)
            {
                if (includeList.Contains(navigationProperty.Name))
                    continue;

                includeList.Add(navigationProperty.Name);
                query = query.Include(navigationProperty.Name);
            }

            return query;
        }

        internal static IQueryable<TResult> IncludeView<TEntity, TResult>(this IQueryable<TResult> query, DbContext context, params string[] includes)
            where TEntity : class
            where TResult : class
        {
            if (includes != null && includes.Length != 0)
                return includes.Where(include => include != null)
                    .Aggregate(query, (current, include) => current.Include(context, include));

            return GetEntityType<TEntity>(context)
                .GetNavigations()
                .Aggregate(query, (current, navigationProperty) => current.Include(context, navigationProperty.Name));
        }

        public static Task<List<TSource>> ToListAsyncSafe<TSource>(this IQueryable<TSource> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            return source is not IAsyncEnumerable<TSource> ? Task.FromResult(source.ToList()) : source.ToListAsync();
        }

        /// <summary>
        /// Detaches all navigation properties from change tracking to prevent conflicts when the same navigation entity appears multiple times.
        /// </summary>
        /// <typeparam name="TEntity">The entity type</typeparam>
        /// <param name="entry">The entity entry to detach navigation properties from</param>
        /// <param name="context">The database context</param>
        internal static void DetachNavigationProperties<TEntity>(this EntityEntry<TEntity> entry, DbContext context) where TEntity : class
        {
            IEntityType entityType = context.Model.FindEntityType(typeof(TEntity));
            if (entityType == null)
                return;

            IEnumerable<INavigation> navigationProperties = entityType.GetNavigations();
            if (navigationProperties == null)
                return;

            foreach (INavigation navigation in navigationProperties)
            {
                try
                {
                    if (navigation.IsCollection)
                    {
                        // For collection navigations, detach the collection items
                        if (entry.Collection(navigation.Name).CurrentValue is not IEnumerable<object> collection)
                            continue;

                        foreach (object navEntity in collection)
                        {
                            if (navEntity == null)
                                continue;

                            EntityEntry navEntry = context.Entry(navEntity);
                            if (navEntry.State != EntityState.Detached)
                                navEntry.State = EntityState.Detached;
                        }
                    }
                    else
                    {
                        // For reference navigations, detach the referenced entity
                        if (entry.Reference(navigation.Name).CurrentValue is object navEntity && navEntity != null)
                        {
                            EntityEntry navEntry = context.Entry(navEntity);
                            if (navEntry.State != EntityState.Detached)
                                navEntry.State = EntityState.Detached;
                        }
                    }
                }
                catch
                {
                    // Ignore errors when accessing navigation properties (e.g., not loaded)
                    continue;
                }
            }
        }
    }
}