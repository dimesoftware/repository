using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    public partial interface IPagedQueryRepositoryAsync<TEntity> : IDisposable where TEntity : class
    {
        /// <summary>
        /// Finds all paged asynchronous.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Same as the five-arg FindAllPagedAsync but with EF Core split-query opt-in on the data query.
        /// When <paramref name="splitQuery"/> is true, the data query uses AsSplitQuery() to avoid
        /// Cartesian fan-out on multi-collection includes. The Count query is unaffected.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="splitQuery">When true, opts the data query into AsSplitQuery</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where,
            IEnumerable<IOrder<TEntity>> orderBy,
            int? page,
            int? pageSize,
            bool splitQuery,
            params string[] includes);

        /// <summary>
        /// Finds all paged asynchronous.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">Size of the page.</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, bool>> count = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Finds all paged asynchronous.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            IEnumerable<Expression<Func<TEntity, object>>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Finds all paged asynchronous.
        /// </summary>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Finds all asynchronous.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TEntity>> FindAllPagedAsync(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, object>> orderBy = null,
            Expression<Func<TEntity, object>> groupBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes);

        /// <summary>
        /// Finds all asynchronous.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            Expression<Func<TEntity, object>> groupBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
            where TResult : class;

        /// <summary>
        /// Finds all asynchronous.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, bool>> count = null,
            Expression<Func<TEntity, TResult>> select = null,
            IEnumerable<IOrder<TEntity>> orderBy = null,
            Expression<Func<TEntity, object>> groupBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
            where TResult : class;

        /// <summary>
        /// Same as the nine-arg FindAllPagedAsync&lt;TResult&gt; (count + IEnumerable&lt;IOrder&gt; ordering)
        /// but with EF Core split-query opt-in on the data query. When <paramref name="splitQuery"/>
        /// is true, the data query uses AsSplitQuery() to avoid Cartesian fan-out across
        /// multi-collection projections. The Count query is always a separate SELECT COUNT(*) and is
        /// unaffected.
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="count">A separate predicate for the COUNT(*) query; defaults to <paramref name="where"/> when null</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/></param>
        /// <param name="orderBy">The sorting expressions to execute against the data store</param>
        /// <param name="groupBy">The optional grouping expression</param>
        /// <param name="ascending">Whether the sort is ascending (true) or descending (false)</param>
        /// <param name="page">The page number, multiplied by page size to compute skip</param>
        /// <param name="pageSize">The size of each page</param>
        /// <param name="splitQuery">When true, opts the data query into AsSplitQuery</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>A page of <typeparamref name="TResult"/></returns>
        Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, bool>> count,
            Expression<Func<TEntity, TResult>> select,
            IEnumerable<IOrder<TEntity>> orderBy,
            Expression<Func<TEntity, object>> groupBy,
            bool? ascending,
            int? page,
            int? pageSize,
            bool splitQuery,
            params string[] includes)
            where TResult : class;

        /// <summary>
        /// Finds all asynchronous.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Expression<Func<TEntity, TResult>> select = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
            where TResult : class;

        /// <summary>
        /// Same as the seven-arg FindAllPagedAsync&lt;TResult&gt; but with EF Core split-query opt-in.
        /// When <paramref name="splitQuery"/> is true, the data query for the page uses
        /// AsSplitQuery() to avoid Cartesian fan-out across multi-collection projections.
        /// The Count query is always a separate SELECT COUNT(*) and is not affected.
        /// </summary>
        /// <typeparam name="TResult">The projected class</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/></param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="ascending">Whether the sort is ascending (true) or descending (false)</param>
        /// <param name="page">The page number, multiplied by page size to compute skip</param>
        /// <param name="pageSize">The size of each page</param>
        /// <param name="splitQuery">When true, opts the data query into AsSplitQuery</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns>A page of <typeparamref name="TResult"/></returns>
        Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where,
            Expression<Func<TEntity, TResult>> select,
            Expression<Func<TEntity, object>> orderBy,
            bool? ascending,
            int? page,
            int? pageSize,
            bool splitQuery,
            params string[] includes)
            where TResult : class;

        /// <summary>
        /// Finds all asynchronous.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="where">The expression to execute against the data store</param>
        /// <param name="select">The expression for the projection of type <typeparamref name="TResult"/> that should be executed against the data store</param>
        /// <param name="orderBy">The sorting expression to execute against the data store</param>
        /// <param name="page">The page number which is multiplied by the page size to calculate the amount of items to skip</param>
        /// <param name="pageSize">The size of the batch of items that must be retrieved</param>
        /// <param name="includes">The optional list of related entities that should be eagerly loaded</param>
        /// <returns></returns>
        Task<IPage<TResult>> FindAllPagedAsync<TResult>(
            Expression<Func<TEntity, bool>> where = null,
            Func<TEntity, object> groupBy = null,
            Expression<Func<IGrouping<object, TEntity>, IEnumerable<TResult>>> select = null,
            Expression<Func<TEntity, object>> orderBy = null,
            bool? ascending = null,
            int? page = null,
            int? pageSize = null,
            params string[] includes)
            where TResult : class, new();
    }
}