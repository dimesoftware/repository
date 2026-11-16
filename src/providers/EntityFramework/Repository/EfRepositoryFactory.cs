using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Dime.Repositories
{
    public class EfRepositoryFactory<TContext> : IEfRepositoryFactory<TContext, RepositoryConfiguration>, ITransactionalRepositoryFactory
        where TContext : DbContext
    {
        public EfRepositoryFactory(IDbContextFactory<TContext> contextFactory)
            : this(contextFactory, new RepositoryConfiguration())
        {
        }

        public EfRepositoryFactory(IDbContextFactory<TContext> contextFactory, RepositoryConfiguration repositoryConfiguration)
        {
            ContextFactory = contextFactory;
            RepositoryConfiguration = repositoryConfiguration;
        }

        private IDbContextFactory<TContext> ContextFactory { get; set; }
        private RepositoryConfiguration RepositoryConfiguration { get; set; }

        public virtual IRepository<TEntity> Create<TEntity>() where TEntity : class, new()
            => Create<TEntity>(RepositoryConfiguration);

        public virtual IRepository<TEntity> Create<TEntity>(RepositoryConfiguration opts) where TEntity : class, new()
            => CreateRepository<TEntity>(ContextFactory, opts);

        /// <summary>
        /// Builds a repository on the given context factory, both outside and inside a transaction.
        /// Override it to hand out a custom repository type.
        /// </summary>
        protected virtual IRepository<TEntity> CreateRepository<TEntity>(IDbContextFactory<TContext> contextFactory, RepositoryConfiguration opts) where TEntity : class, new()
            => new EfRepository<TEntity, TContext>(contextFactory, opts);

        public void SetContextFactory(IDbContextFactory<TContext> contextFactory)
        {
            ContextFactory = contextFactory;
        }

        /// <summary>
        /// Starts a transaction on one connection.
        /// It throws on a context with a retrying execution strategy, which cannot replay it, so build the factory on a context factory without retries for transactional work.
        /// </summary>
        public virtual async Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            EfTransaction<TContext> transaction = await EfTransaction<TContext>.BeginAsync(ContextFactory, cancellationToken);
            return new RepositoryTransaction(this, transaction);
        }

        /// <summary>
        /// Hands out the repositories of a transaction, built by the factory that started it.
        /// </summary>
        /// <param name="factory">The factory that builds the repositories</param>
        /// <param name="transaction">The connection and transaction the repositories share</param>
        private sealed class RepositoryTransaction(EfRepositoryFactory<TContext> factory, EfTransaction<TContext> transaction)
            : IRepositoryTransaction, IDbContextFactory<TContext>
        {
            public IRepository<TEntity> Create<TEntity>() where TEntity : class, new()
            {
                transaction.ThrowIfCompleted();
                return factory.CreateRepository<TEntity>(this, factory.RepositoryConfiguration);
            }

            public TContext CreateDbContext()
                => transaction.CreateDbContext();

            public Task AcquireLockAsync(string key, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
                => transaction.AcquireLockAsync(key, timeout, cancellationToken);

            public Task CommitAsync(CancellationToken cancellationToken = default)
                => transaction.CommitAsync(cancellationToken);

            public Task RollbackAsync(CancellationToken cancellationToken = default)
                => transaction.RollbackAsync(cancellationToken);

            public ValueTask DisposeAsync()
                => transaction.DisposeAsync();
        }
    }
}
