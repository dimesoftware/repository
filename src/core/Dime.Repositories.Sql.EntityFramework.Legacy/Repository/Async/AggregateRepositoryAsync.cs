﻿using System;
using System.Data.Entity;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        /// <summary>
        /// Counts the amount of records in the data store for the table that corresponds to the entity type <typeparamref name="TEntity"/>.
        /// </summary>
        /// <returns>A number of the amount of records</returns>
        public async Task<long> CountAsync()
        {
            using (TContext ctx = Context)
            {
                return await ctx.Set<TEntity>().CountAsync();
            }
        }

        /// <summary>
        /// Counts the amount of records in the data store for the table that corresponds to the entity type <typeparamref name="TEntity"/>.
        /// </summary>
        /// <returns>A number of the amount of records</returns>
        /// <param name="where">The expression to execute against the data store</param>
        public async Task<long> CountAsync(Expression<Func<TEntity, bool>> where)
        {
            using (TContext ctx = Context)
            {
                return await ctx.Set<TEntity>().CountAsync(where);
            }
        }
    }
}