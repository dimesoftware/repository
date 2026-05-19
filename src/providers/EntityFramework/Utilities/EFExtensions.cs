using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Dime.Repositories
{
    internal static class EFExtensions
    {
        private static IEntityType GetEntityType<T>(DbContext context)
            => context.Model.FindEntityType(typeof(T));

        /// <summary>
        /// Applies the supplied <paramref name="includes"/> to <paramref name="query"/>.
        /// <para>
        /// Tracked under <see href="https://github.com/dimesoftware/repository/issues">a future
        /// opt-in change</see>: when <paramref name="includes"/> is <c>null</c> the call is a
        /// no-op; when it is an empty array (the typical params default), every navigation on
        /// <typeparamref name="TEntity"/> is eagerly loaded. This auto-walk is convenient but
        /// can silently trigger Cartesian fan-out on multi-collection entities.
        /// </para>
        /// </summary>
        internal static IQueryable<TEntity> Include<TEntity>(this IQueryable<TEntity> query, DbContext context, params string[] includes) where TEntity : class
        {
            if (includes == null)
                return query;

            HashSet<string> seen = [];
            if (includes.Length != 0)
                return includes
                    .Where(x => !string.IsNullOrEmpty(x) && seen.Add(x))
                    .Aggregate(query, (current, include) => current.Include(include));

            IEntityType entityType = context.Model.FindEntityType(typeof(TEntity));
            if (entityType == null)
                return query;

            IEnumerable<INavigation> navigationProperties = entityType.GetNavigations();
            if (navigationProperties == null)
                return query;

            foreach (INavigation navigationProperty in navigationProperties)
                if (seen.Add(navigationProperty.Name))
                    query = query.Include(navigationProperty.Name);

            return query;
        }

        /// <summary>
        /// Same auto-walk semantics as <see cref="Include{TEntity}"/> but on a projected query.
        /// </summary>
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