using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        /// <summary>
        /// Runs a paged query: materializes the data list and computes the total in one call.
        /// <para>
        /// When the repository was constructed with an <see cref="IDbContextFactory{TContext}"/>,
        /// two independent <typeparamref name="TContext"/> instances are minted so the data
        /// <c>SELECT</c> and the <c>COUNT(*)</c> can run concurrently via
        /// <see cref="Task.WhenAll(Task[])"/>. This halves the wall-clock latency of a paged
        /// query at the cost of holding two DB connections briefly.
        /// </para>
        /// <para>
        /// In non-factory mode the repository owns a single <c>DbContext</c>; EF Core forbids
        /// concurrent operations on one context, so this method falls back to running the two
        /// queries sequentially against the same context.
        /// </para>
        /// </summary>
        /// <typeparam name="TResult">The projected row type.</typeparam>
        /// <param name="buildDataQuery">Builds the data query against the supplied context.</param>
        /// <param name="countPredicate">
        /// Optional WHERE predicate for the <c>COUNT(*)</c>. When <c>null</c>, all rows are counted.
        /// </param>
        protected async Task<(List<TResult> Data, int Total)> RunPagedAsync<TResult>(
            Func<TContext, IQueryable<TResult>> buildDataQuery,
            Expression<Func<TEntity, bool>> countPredicate)
        {
            if (HasContextFactory)
            {
                // Two freshly-minted contexts → genuine parallel execution. We own their
                // lifetime, so we dispose them at the end of the method.
                using TContext dataCtx = CreateContext();
                using TContext countCtx = CreateContext();

                Task<List<TResult>> dataTask = buildDataQuery(dataCtx).ToListAsync();
                Task<int> countTask = countCtx.CountAsync(countPredicate);
                await Task.WhenAll(dataTask, countTask);
                return (dataTask.Result, countTask.Result);
            }

            // Single shared context owned by the caller / repository's own Dispose.
            // We MUST NOT dispose here — callers commonly inspect the context after the
            // call (e.g. ctx.ChangeTracker, ctx.Entry(...)). EF also forbids concurrent
            // operations on one context, so the two queries run sequentially.
            TContext ctx = Context;
            List<TResult> data = await buildDataQuery(ctx).ToListAsync();
            int total = await ctx.CountAsync(countPredicate);
            return (data, total);
        }
    }
}
