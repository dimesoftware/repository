﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using LinqKit;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public virtual Page<TResult> FindAllPaged<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, dynamic>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
            where TResult : class
        {
            using TContext ctx = Context;
            IQueryable<TResult> query =
                ctx.Set<TEntity>()
                    .AsExpandable()
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy, ascending ?? true)
                    .With(page, pageSize, orderBy)
                    .With(pageSize)
                    .WithSelect(select);

            IQueryable<TResult> fullGraphQuery = Include(query, includes);
            return new Page<TResult>(fullGraphQuery.ToList(), ctx.Count(where));
        }

        public Page<TResult> FindAllPaged<TResult>(
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
                    .AsExpandable()
                    .AsNoTracking()
                    .With(where)
                    .WithOrder(orderBy, ascending ?? true)
                    .With(page, pageSize, orderBy)
                    .With(pageSize)
                    .WithGroup(groupBy)
                    .WithSelect<TEntity, TResult, object>(select);

            IQueryable<TResult> fullGraphQuery = Include(query, includes);
            return new Page<TResult>(fullGraphQuery.ToList(), ctx.Count(where));
        }

        public Page<TResult> FindAllPaged<TResult>(
           Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            Expression<Func<TEntity, object>> groupBy = null,
            bool? ascending = default,
            int? page = default,
            int? pageSize = default,
            params string[] includes) where TResult : class
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>()
            .Include(ctx, includes)
            .AsExpandable()
            .AsNoTracking()
            .With(where)
            .WithOrder(orderBy)
            .With(page, pageSize, orderBy)
            .With(pageSize)
            .WithSelect(select)
            .ToPage(ctx.Count(where));
        }

        public Page<TResult> FindAllPaged<TResult>(
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
            using TContext ctx = Context;
            return ctx.Set<TEntity>()
            .Include(ctx, includes)
            .AsNoTracking()
            .AsExpandable()
            .AsQueryable()
            .With(where)
            .WithOrder(orderBy)
            .With(page, pageSize, orderBy)
            .With(pageSize)
            .WithSelect(select)
            .ToPage(ctx.Count(count));
        }

        public Page<TEntity> FindAllPaged(
           Expression<Func<TEntity, bool>> where = null,
           Expression<Func<TEntity, dynamic>> orderBy = null,
           bool? ascending = null,
           int? page = default,
           int? pageSize = default,
           params string[] includes)
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>()
            .Include(ctx, includes)
            .AsExpandable()
            .AsNoTracking()
            .With(where)
            .WithOrder(orderBy, ascending ?? true)
            .With(page, pageSize, orderBy)
            .With(pageSize)
            .AsQueryable()
            .ToPage(ctx.Count(where));
        }

        public Page<TEntity> FindAllPaged(
           Expression<Func<TEntity, bool>> where = null,
           Expression<Func<TEntity, dynamic>> orderBy = null,
           Expression<Func<TEntity, object>> groupBy = null,
           bool? ascending = default,
           int? page = default,
           int? pageSize = default,
           params string[] includes)
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>()
             .Include(ctx, includes)
             .AsExpandable()
             .AsNoTracking()
             .With(where)
             .WithOrder(orderBy, ascending ?? true)
             .With(page, pageSize, orderBy)
             .With(pageSize)
             .ToPage(ctx.Count(where));
        }
        public Page<TEntity> FindAllPaged(
           Expression<Func<TEntity, bool>> where = null,
           Expression<Func<TEntity, bool>> count = null,
           IEnumerable<IOrder<TEntity>> orderBy = null,
           int? page = default,
           int? pageSize = default,
           params string[] includes)
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>()
            .Include(ctx, includes)
            .AsNoTracking()
            .AsExpandable()
            .With(where)
            .WithOrder(orderBy)
            .With(page, pageSize, orderBy)
            .With(pageSize)
            .ToPage(ctx.Count(where));
        }

        public Page<TEntity> FindAllPaged(
           Expression<Func<TEntity, bool>> where = null,
           IEnumerable<IOrder<TEntity>> orderBy = null,
           int? page = default,
           int? pageSize = default,
           params string[] includes)
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>()
                .Include(ctx, includes)
                .AsNoTracking()
                .AsExpandable()
                .With(where)
                .WithOrder(orderBy)
                .With(page, pageSize, orderBy)
                .With(pageSize)
                .ToPage(ctx.Count(where));
        }

        public Page<TEntity> FindAllPaged(
           Expression<Func<TEntity, bool>> where = null,
           IEnumerable<Expression<Func<TEntity, object>>> orderBy = null,
           bool? ascending = default,
           int? page = default,
           int? pageSize = default,
           params string[] includes)
        {
            using TContext ctx = Context;
            return ctx.Set<TEntity>()
            .Include(ctx, includes)
            .AsExpandable()
            .AsNoTracking()
            .With(where)
            .WithOrder(orderBy, ascending ?? true)
            .With(page, pageSize, orderBy)
            .With(pageSize)
            .AsQueryable()
            .ToPage(ctx.Count(where));
        }
    }
}