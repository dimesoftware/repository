using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public virtual async Task<TEntity> UpdateAsync(TEntity entity, bool commitChanges = true)
        {
            TContext ctx = Context;
            ctx.Entry(entity).State = EntityState.Modified;

            if (commitChanges)
                await SaveChangesAsync(ctx);

            return entity;
        }

        public async Task UpdateAsync(IEnumerable<TEntity> entities, bool commitChanges = true)
        {
            if (!entities.Any())
                return;

            TContext ctx = Context;
            ctx.ChangeTracker.Clear();

            foreach (TEntity entity in entities)
            {
                EntityEntry<TEntity> entry = ctx.Entry(entity);
                entry.State = EntityState.Modified;
                entry.DetachNavigationProperties(ctx);
            }

            if (commitChanges)
                await SaveChangesAsync(ctx);
        }

        public virtual async Task<TEntity> UpdateAsync(TEntity entity, params string[] properties)
        {
            TContext ctx = Context;
            EntityEntry<TEntity> entry = ctx.Entry(entity);

            foreach (string property in properties)
                entry.Property(property).IsModified = true;

            entry.State = EntityState.Modified;
            await SaveChangesAsync(ctx);
            return entity;
        }

        public virtual async Task<TEntity> UpdateAsync(TEntity entity, params Expression<Func<TEntity, object>>[] properties)
        {
            TContext ctx = Context;
            EntityEntry<TEntity> entry = ctx.Entry(entity);

            foreach (Expression<Func<TEntity, object>> property in properties)
                entry.Property(property).IsModified = true;

            entry.State = EntityState.Modified;

            await SaveChangesAsync(ctx);

            return entity;
        }
    }
}