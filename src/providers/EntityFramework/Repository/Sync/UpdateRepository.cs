using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public TEntity Update(TEntity entity) => Update(entity, true);

        public virtual TEntity Update(TEntity entity, bool commitChanges = true)
        {
            using TContext ctx = Context;
            ctx.Entry(entity).State = EntityState.Modified;

            if (commitChanges)
                SaveChanges(ctx);

            return entity;
        }

        public void Update(IEnumerable<TEntity> entities, bool commitChanges = true)
        {
            if (!entities.Any())
                return;

            using TContext ctx = Context;
            ctx.ChangeTracker.Clear();

            foreach (TEntity entity in entities)
            {
                EntityEntry<TEntity> entry = ctx.Entry(entity);
                entry.State = EntityState.Modified;
                entry.DetachNavigationProperties(ctx);
            }

            if (commitChanges)
                SaveChanges(ctx);
        }

        /// <summary>
        /// Updates entities matching the where clause by setting a property to a new value.
        /// Uses ExecuteUpdate for efficient bulk updates without loading entities.
        /// </summary>
        public virtual int Update<TValue>(Expression<Func<TEntity, bool>> where, Expression<Func<TEntity, TValue>> propertySelector, TValue newValue)
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>().With(where).ExecuteUpdate(setters => setters.SetProperty(propertySelector, newValue));
        }

        /// <summary>
        /// Updates entities matching the where clause by setting a property using an expression that references the existing value.
        /// Uses ExecuteUpdate for efficient bulk updates without loading entities.
        /// </summary>
        public virtual int Update<TValue>(Expression<Func<TEntity, bool>> where, Expression<Func<TEntity, TValue>> propertySelector, Expression<Func<TEntity, TValue>> valueExpression)
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>().With(where).ExecuteUpdate(setters => setters.SetProperty(propertySelector, valueExpression));
        }
    }
}