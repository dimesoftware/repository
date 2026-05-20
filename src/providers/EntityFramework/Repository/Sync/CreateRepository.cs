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
        public virtual TEntity Create(TEntity entity)
        {
            using TContext ctx = Context;
            ctx.Entry(entity).State = EntityState.Added;
            TEntity createdItem = ctx.Set<TEntity>().Add(entity)?.Entity;
            SaveChanges(ctx);

            return createdItem;
        }

        public virtual TEntity Create(TEntity entity, Expression<Func<TEntity, bool>> condition)
        {
            using TContext ctx = Context;

            if (condition == null || !(condition != null && ctx.Set<TEntity>().Any(condition)))
            {
                ctx.Entry(entity).State = EntityState.Added;
                TEntity createdItem = ctx.Set<TEntity>()?.Add(entity)?.Entity;
                SaveChanges(ctx);

                return createdItem;
            }
            else
                return entity;
        }

        public virtual TEntity Create(TEntity entity, Func<TEntity, TContext, Task> beforeSaveAction)
        {
            using TContext ctx = Context;
            beforeSaveAction(entity, ctx);

            ctx.Entry(entity).State = EntityState.Added;
            TEntity createdItem = ctx.Set<TEntity>().Add(entity)?.Entity;
            SaveChanges(ctx);

            return createdItem;
        }

        public virtual TEntity Create(TEntity entity, bool commit)
        {
            using TContext ctx = Context;
            ctx.Entry(entity).State = EntityState.Added;
            TEntity createdItem = ctx.Set<TEntity>().Add(entity)?.Entity;

            if (commit)
                SaveChanges(ctx);

            return createdItem;
        }

        public virtual IQueryable<TEntity> Create(IQueryable<TEntity> entities)
        {
            if (!entities.Any())
                return entities;

            List<TEntity> newEntities = [];
            List<TEntity> entitiesToCreate = [.. entities];
            using TContext ctx = Context;
            foreach (TEntity entity in entitiesToCreate)
            {
                ctx.Entry(entity).State = EntityState.Added;
                TEntity newEntity = ctx.Set<TEntity>().Add(entity)?.Entity;
                newEntities.Add(newEntity);
            }

            SaveChanges(ctx);

            return newEntities.AsQueryable();
        }
    }
}