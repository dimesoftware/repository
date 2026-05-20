using System;

namespace Dime.Repositories
{
    /// <summary>
    /// Definition for a repository which supports all CRUD operations
    /// </summary>
    /// <typeparam name="TEntity">The collection type</typeparam>
    public partial interface IRepository<TEntity> :
        IQueryRepository<TEntity>,
        IQueryRepositoryAsync<TEntity>,
        IPagedQueryRepositoryAsync<TEntity>,
        ICommandRepositoryAsync<TEntity>,
        IDisposable where TEntity : class
    {
    }
}