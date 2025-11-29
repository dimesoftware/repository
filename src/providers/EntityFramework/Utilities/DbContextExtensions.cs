using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories
{
    [ExcludeFromCodeCoverage]
    internal static class DbContextExtensions
    {
        internal static int Count<TEntity>(this DbContext ctx, Expression<Func<TEntity, bool>> query = null)
            where TEntity : class
            => query == null
                ? ctx.Set<TEntity>().AsNoTracking().Count()
                : ctx.Set<TEntity>().AsNoTracking().Count(query);

        internal static Task<int> CountAsync<TEntity>(this DbContext ctx, Expression<Func<TEntity, bool>> query = null)
            where TEntity : class
            => query == null
                ? ctx.Set<TEntity>().AsNoTracking().CountAsync()
                : ctx.Set<TEntity>().AsNoTracking().CountAsync(query);
    }
}