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
        /// Projected page (7-arg, single-expression orderBy).
        /// </summary>
        public virtual async Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
            where TResult : class
        {
            var (data, total) = await RunPagedAsync<TResult>(
                ctx => ctx.Set<TEntity>()
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy, ascending ?? true)
                    .With(page, pageSize, orderBy)
                    .With(pageSize)
                    .WithSelect(select)
                    .Include(ctx, includes),
                where);
            return new Page<TResult>(data, total);
        }

        /// <summary>
        /// Projected page (7-arg) with EF Core split-query opt-in on the data query.
        /// </summary>
        public virtual async Task<IPage<TResult>> FindAllPagedAsync<TResult>(
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
            var (data, total) = await RunPagedAsync<TResult>(
                ctx =>
                {
                    IQueryable<TEntity> baseQuery = ctx.Set<TEntity>()
                        .Include(ctx, includes)
                        .AsNoTracking()
                        .With(where)
                        .WithOrder(orderBy, ascending ?? true)
                        .With(page, pageSize, orderBy)
                        .With(pageSize);
                    if (splitQuery)
                        baseQuery = baseQuery.AsSplitQuery();
                    return baseQuery.WithSelect(select);
                },
                where);
            return new Page<TResult>(data, total);
        }

        /// <summary>
        /// Projected, grouped page. GroupBy projections are LINQ-to-Objects only, so the data
        /// query stays synchronous-to-list; the count, however, is a pure DB call and is
        /// parallelised with the materialisation in factory mode.
        /// </summary>
        public async Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Func<TEntity, object> groupBy = null,
            Expression<Func<IGrouping<object, TEntity>, IEnumerable<TResult>>> select = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            bool? ascending = default,
            int? page = default,
            int? pageSize = default,
            params string[] includes) where TResult : class, new()
        {
            using TContext ctx = Context;
            IQueryable<TResult> query =
                ctx.Set<TEntity>()
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy, ascending ?? true)
                    .With(page, pageSize, orderBy)
                    .With(pageSize)
                    .WithGroup(groupBy)
                    .WithSelect<TEntity, TResult, object>(select)
                    .Include(ctx, includes);

            List<TResult> data = [.. query];
            int total = await ctx.CountAsync(where);
            return new Page<TResult>(data, total);
        }

        /// <summary>
        /// Projected page (8-arg, IOrder ordering, optional groupBy).
        /// </summary>
        public async Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            Expression<Func<TEntity, object>> groupBy = null,
            bool? ascending = default,
            int? page = default,
            int? pageSize = default,
            params string[] includes) where TResult : class
        {
            var (data, total) = await RunPagedAsync<TResult>(
                ctx => ctx.Set<TEntity>()
                    .Include(ctx, includes)
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy)
                    .With(page, pageSize, orderBy)
                    .With(pageSize)
                    .WithSelect(select),
                where);
            return new Page<TResult>(data, total);
        }

        /// <summary>
        /// Projected page (9-arg) with a separate count predicate.
        /// </summary>
        public async Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, bool>> count = null,
            Expression<Func<TEntity, TResult>> select = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            Expression<Func<TEntity, object>> groupBy = null,
            bool? ascending = default,
            int? page = default,
            int? pageSize = default,
            params string[] includes) where TResult : class
        {
            var (data, total) = await RunPagedAsync<TResult>(
                ctx => ctx.Set<TEntity>()
                    .Include(ctx, includes)
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy)
                    .With(page, pageSize, orderBy)
                    .With(pageSize)
                    .WithSelect(select),
                count ?? where);
            return new Page<TResult>(data, total);
        }

        /// <summary>
        /// Projected page (10-arg) with separate count predicate and split-query opt-in.
        /// </summary>
        public async Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, bool>> count,
            Expression<Func<TEntity, TResult>> select,
            IEnumerable<IOrder<TEntity>> orderBy,
            Expression<Func<TEntity, object>> groupBy,
            bool? ascending,
            int? page,
            int? pageSize,
            bool splitQuery,
            params string[] includes) where TResult : class
        {
            var (data, total) = await RunPagedAsync<TResult>(
                ctx =>
                {
                    IQueryable<TEntity> baseQuery = ctx.Set<TEntity>()
                        .Include(ctx, includes)
                        .AsNoTracking()
                        .With(where)
                        .WithOrder(orderBy)
                        .With(page, pageSize, orderBy)
                        .With(pageSize);
                    if (splitQuery)
                        baseQuery = baseQuery.AsSplitQuery();
                    return baseQuery.WithSelect(select);
                },
                count ?? where);
            return new Page<TResult>(data, total);
        }

        /// <summary>
        /// Entity page (single-expression orderBy + ascending).
        /// </summary>
        public async Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            bool? ascending = null,
            int? page = default,
            int? pageSize = default,
            params string[] includes)
        {
            var (data, total) = await RunPagedAsync<TEntity>(
                ctx => ctx.Set<TEntity>()
                    .Include(ctx, includes)
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy, ascending ?? true)
                    .With(page, pageSize, orderBy)
                    .With(pageSize),
                where);
            return new Page<TEntity>(data, total);
        }

        /// <summary>
        /// Entity page with optional <c>groupBy</c> placeholder (unused in the data shape).
        /// </summary>
        public async Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            Expression<Func<TEntity, object>> groupBy = null,
            bool? ascending = default,
            int? page = default,
            int? pageSize = default,
            params string[] includes)
        {
            var (data, total) = await RunPagedAsync<TEntity>(
                ctx => ctx.Set<TEntity>()
                    .Include(ctx, includes)
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy, ascending ?? true)
                    .With(page, pageSize, orderBy)
                    .With(pageSize),
                where);
            return new Page<TEntity>(data, total);
        }

        /// <summary>
        /// Entity page with separate count predicate (IOrder ordering).
        /// </summary>
        public async Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, bool>> count = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            int? page = default,
            int? pageSize = default,
            params string[] includes)
        {
            var (data, total) = await RunPagedAsync<TEntity>(
                ctx => ctx.Set<TEntity>()
                    .Include(ctx, includes)
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy)
                    .With(page, pageSize, orderBy)
                    .With(pageSize),
                count ?? where);
            return new Page<TEntity>(data, total);
        }

        /// <summary>
        /// Entity page with separate count predicate (IOrder ordering, optional tracking).
        /// </summary>
        public async Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, bool>> count = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            int? page = default,
            int? pageSize = default,
            bool trackChanges = false,
            params string[] includes)
        {
            var (data, total) = await RunPagedAsync<TEntity>(
                ctx =>
                {
                    IQueryable<TEntity> q = ctx.Set<TEntity>()
                        .Include(ctx, includes)
                        .With(where)
                        .WithOrder(orderBy)
                        .With(page, pageSize, orderBy)
                        .With(pageSize);
                    return trackChanges ? q : q.AsNoTracking();
                },
                count ?? where);
            return new Page<TEntity>(data, total);
        }

        /// <summary>
        /// Entity page (5-arg, IOrder ordering).
        /// </summary>
        public async Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            int? page = default,
            int? pageSize = default,
            params string[] includes)
        {
            var (data, total) = await RunPagedAsync<TEntity>(
                ctx => ctx.Set<TEntity>()
                    .Include(ctx, includes)
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy)
                    .With(page, pageSize, orderBy)
                    .With(pageSize),
                where);
            return new Page<TEntity>(data, total);
        }

        /// <summary>
        /// Entity page (5-arg, IOrder ordering) with split-query opt-in.
        /// </summary>
        public async Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where,
            IEnumerable<IOrder<TEntity>> orderBy,
            int? page,
            int? pageSize,
            bool splitQuery,
            params string[] includes)
        {
            var (data, total) = await RunPagedAsync<TEntity>(
                ctx =>
                {
                    IQueryable<TEntity> q = ctx.Set<TEntity>()
                        .Include(ctx, includes)
                        .AsNoTracking()
                        .With(where)
                        .WithOrder(orderBy)
                        .With(page, pageSize, orderBy)
                        .With(pageSize);
                    return splitQuery ? q.AsSplitQuery() : q;
                },
                where);
            return new Page<TEntity>(data, total);
        }

        /// <summary>
        /// Entity page with multiple orderBy expressions + ascending flag.
        /// </summary>
        public async Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            IEnumerable<Expression<Func<TEntity, object>>> orderBy = null,
            bool? ascending = default,
            int? page = default,
            int? pageSize = default,
            params string[] includes)
        {
            var (data, total) = await RunPagedAsync<TEntity>(
                ctx => ctx.Set<TEntity>()
                    .Include(ctx, includes)
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy, ascending ?? true)
                    .With(page, pageSize, orderBy)
                    .With(pageSize),
                where);
            return new Page<TEntity>(data, total);
        }
    }
}
