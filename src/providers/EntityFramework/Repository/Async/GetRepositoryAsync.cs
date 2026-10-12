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
        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> where)
        {
            TContext ctx = Context;
            return await ctx.Set<TEntity>().AsNoTracking().AnyAsync(where);
        }

        public virtual async Task<TEntity> FindByIdAsync(object? id)
        {
            TContext ctx = Context;
            return await ctx.Set<TEntity>().FindAsync(id);
        }

        public virtual async Task<TEntity> FindByIdAsync(object? id, params string[] includes)
        {
            TContext ctx = Context;
            foreach (string include in includes)
                ctx.Set<TEntity>().Include(include).AsNoTracking();

            return await ctx.Set<TEntity>().FindAsync(id);
        }

        public virtual async Task<TEntity> FindOneAsync(Expression<Func<TEntity, bool>> where)
        {
            TContext ctx = Context;
            return await ctx.Set<TEntity>()
                .AsNoTracking()
                .With(where)
                .FirstOrDefaultAsync();
        }

        public virtual async Task<TEntity> FindOneAsync(Expression<Func<TEntity, bool>> where, params string[] includes)
        {
            TContext ctx = Context;
            IQueryable<TEntity> query = ctx.Set<TEntity>()
                .Include(ctx, includes)
                .AsNoTracking()
                .With(where);

            return await query.FirstOrDefaultAsync();
        }

        public async Task<TResult> FindOneAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            bool? ascending = default,
            int? page = default,
            int? pageSize = default,
            params string[] includes) where TResult : class
        {
            using TContext ctx = Context;
            IQueryable<TResult> query = ctx.Set<TEntity>()
                .AsNoTracking()
                .With(where)
                .WithOrder(orderBy, ascending ?? true)
                .With(page, pageSize, orderBy)
                .With(pageSize)
                .WithSelect(select)
                .Include(ctx, includes);

            return await query.FirstOrDefaultAsync();
        }

        public virtual async Task<IEnumerable<TEntity>> FindAllAsync(Expression<Func<TEntity, bool>> where, params string[] includes)
        {
            TContext ctx = Context;
            IQueryable<TEntity> query = ctx.Set<TEntity>()
                .Include(ctx, includes)
                .AsNoTracking()
                .With(where);

            return await query.ToListAsync();
        }

        public virtual async Task<IEnumerable<TResult>> FindAllAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
        {
            using TContext ctx = Context;
            IQueryable<TResult> query = ctx.Set<TEntity>()
                .Include(ctx, includes)
                .AsNoTracking()
                .With(where)
                .WithOrder(orderBy, ascending ?? true)
                .With(page, pageSize, orderBy)
                .With(pageSize)
                .WithSelect(select);

            return await query.ToListAsync();
        }

        public virtual async Task<IEnumerable<TResult>> FindAllAsync<TResult>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, TResult>> select,
            Expression<Func<TEntity, dynamic>> orderBy,
            bool? ascending,
            int? page,
            int? pageSize,
            bool splitQuery,
            params string[] includes)
            where TResult : class
        {
            using TContext ctx = Context;
            IQueryable<TEntity> baseQuery = ctx.Set<TEntity>()
                .Include(ctx, includes)
                .AsNoTracking()
                .With(where)
                .WithOrder(orderBy, ascending ?? true)
                .With(page, pageSize, orderBy)
                .With(pageSize);

            if (splitQuery)
                baseQuery = baseQuery.AsSplitQuery();

            IQueryable<TResult> query = baseQuery.WithSelect(select);

            return await query.ToListAsync();
        }

        public virtual async Task<IEnumerable<TEntity>> FindAllAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
        {
            TContext ctx = Context;
            IQueryable<TEntity> query = ctx.Set<TEntity>()
                .Include(ctx, includes)
                .AsNoTracking()
                .With(where)
                .WithOrder(orderBy, ascending ?? true)
                .With(page, pageSize, orderBy)
                .With(pageSize);

            return await query.ToListAsync();
        }

        /// <summary>
        /// Filters, groups and projects in one SQL query. The grouping and the aggregates run in the database;
        /// unlike the grouped FindAllPagedAsync overload there is no LINQ-to-Objects step.
        /// </summary>
        public virtual async Task<IEnumerable<TResult>> FindGroupedAsync<TKey, TResult>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, TKey>> groupBy,
            Expression<Func<IGrouping<TKey, TEntity>, TResult>> select)
        {
            ArgumentNullException.ThrowIfNull(groupBy);
            ArgumentNullException.ThrowIfNull(select);

            using TContext ctx = Context;
            IQueryable<TResult> query = ctx.Set<TEntity>()
                .AsNoTracking()
                .With(where)
                .GroupBy(groupBy)
                .Select(select);

            return await query.ToListAsync();
        }
    }
}
