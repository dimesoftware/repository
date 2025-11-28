using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public virtual async Task DeleteAsync()
        {
            TContext ctx = Context;
            await ctx.Set<TEntity>().ExecuteDeleteAsync();
        }

        public virtual async Task DeleteAsync(object? id)
        {
            if (id is IEnumerable)
            {
                IEnumerable<object?> ids = (id as IEnumerable).Cast<object?>();
                await DeleteAsync(ids);
                return;
            }

            TContext ctx = Context;
            TEntity item = await ctx.Set<TEntity>().FindAsync(id);
            if (item != default(TEntity))
            {
                ctx.Set<TEntity>().Remove(item);
                await SaveChangesAsync(ctx);
            }
        }

        public virtual async Task DeleteAsync(IEnumerable<object?> ids)
        {
            TContext ctx = Context;
            foreach (object id in ids.Distinct().ToList())
            {
                TEntity item = await ctx.Set<TEntity>().FindAsync(id);
                if (item == default(TEntity))
                    continue;

                ctx.Set<TEntity>().Remove(item);
            }

            await SaveChangesAsync(ctx);
        }

        public virtual async Task DeleteAsync(object? id, bool commit)
        {
            TContext ctx = Context;
            TEntity item = await ctx.Set<TEntity>().FindAsync(id);
            if (item != default(TEntity))
            {
                ctx.Set<TEntity>().Remove(item);
                if (commit)
                    await SaveChangesAsync(ctx);
            }
        }

        public virtual async Task DeleteAsync(TEntity entity)
        {
            TContext ctx = Context;
            ctx.Set<TEntity>().Remove(entity);
            await SaveChangesAsync(ctx);
        }

        public async Task DeleteAsync(IEnumerable<TEntity> entities)
        {
            if (!entities.Any())
                return;

            TContext ctx = Context;
            foreach (TEntity entity in entities)
                ctx.Set<TEntity>().Remove(entity);

            await SaveChangesAsync(ctx);
        }

        public virtual async Task DeleteAsync(TEntity entity, bool commit)
        {
            TContext ctx = Context;
            ctx.Set<TEntity>().Remove(entity);

            if (commit)
                await SaveChangesAsync(ctx);
        }

        public virtual async Task DeleteAsync(Expression<Func<TEntity, bool>> where)
        {
            TContext ctx = Context;
            await ctx.Set<TEntity>().With(where).ExecuteDeleteAsync();
        }
    }
}