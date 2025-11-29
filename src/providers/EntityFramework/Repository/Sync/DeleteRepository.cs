using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public virtual void Delete(object? id)
        {
            using TContext ctx = Context;
            TEntity item = ctx.Set<TEntity>().Find(id);
            if (item == default(TEntity))
                return;

            ctx.Set<TEntity>().Remove(item);
            SaveChanges(ctx);
        }

        public virtual void Delete(object? id, bool commit)
        {
            using TContext ctx = Context;
            TEntity item = ctx.Set<TEntity>().Find(id);
            if (item == default(TEntity))
                return;

            ctx.Set<TEntity>().Remove(item);
            if (commit)
                SaveChanges(ctx);
        }

        public virtual void Delete(TEntity entity)
        {
            using TContext ctx = Context;
            ctx.Set<TEntity>().Remove(entity);
            SaveChanges(ctx);
        }

        public void Delete(IEnumerable<TEntity> entities)
        {
            if (!entities.Any())
                return;

            using TContext ctx = Context;
            foreach (TEntity entity in entities)
                ctx.Set<TEntity>().Remove(entity);

            SaveChanges(ctx);
        }

        public virtual void Delete(TEntity entity, bool commit)
        {
            using TContext ctx = Context;
            ctx.Set<TEntity>().Remove(entity);

            if (commit)
                SaveChanges(ctx);
        }

        public virtual void Delete(Expression<Func<TEntity, bool>> where)
        {
            using TContext ctx = Context;
            ctx.Set<TEntity>().With(where).ExecuteDelete();
        }
    }
}