using System.Collections.Generic;
using System.Linq;
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
                ctx.Entry(entity).State = EntityState.Modified;

            if (commitChanges)
                SaveChanges(ctx);
        }
    }
}