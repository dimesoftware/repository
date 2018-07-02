﻿using System;
using System.Linq;
using System.Linq.Expressions;
using LinqKit;

namespace Dime.Repositories
{
    internal static partial class QueryFactory
    {
        /// <summary>
        /// Wrapper around LINQ WHERE
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicate">The predicate.</param>
        /// <returns></returns>
        /// <history>
        /// [HB] 17/08/2015 - Create
        /// </history>
        internal static IQueryable<TSource> With<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return predicate == null ? source : source.Where(predicate.Expand());
        }

        /// <summary>
        /// Wrapper around LINQ WHERE
        /// </summary>
        /// <typeparam name="TSource">The type of the source.</typeparam>
        /// <param name="source">The source.</param>
        /// <param name="predicate">The predicate.</param>
        /// <returns></returns>
        /// <history>
        /// [HB] 17/08/2015 - Create
        /// </history>
        internal static TSource WithFirst<TSource>(this IQueryable<TSource> source, Expression<Func<TSource, bool>> predicate)
        {
            return predicate == null ? source.FirstOrDefault() : source.FirstOrDefault(predicate.Expand());
        }
    }
}