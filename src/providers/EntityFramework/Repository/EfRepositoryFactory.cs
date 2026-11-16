using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

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

        internal IRepository<TEntity> CreateRepositoryInTransaction<TEntity>(EfRepositoryTransaction<TContext> transaction, RepositoryConfiguration opts) where TEntity : class, new()
        {
            IRepository<TEntity> repository = CreateRepository<TEntity>(transaction, opts);
            if (repository is EfRepository<TEntity, TContext> efRepository)
                efRepository.RunsInReplayableUnit = transaction.IsReplayable;

            return repository;
        }

        public void SetContextFactory(IDbContextFactory<TContext> contextFactory)
        {
            ContextFactory = contextFactory;
        }

        /// <summary>
        /// Starts a transaction on one connection.
        /// A retrying execution strategy rejects it, since the unit cannot be replayed.
        /// </summary>
        public virtual async Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
            => await EfRepositoryTransaction<TContext>.BeginAsync(this, ContextFactory, RepositoryConfiguration, cancellationToken);

        /// <summary>
        /// Runs the work in the context's execution strategy, each attempt on a new connection and transaction.
        /// </summary>
        public virtual Task ExecuteInTransactionAsync(Func<IRepositoryTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            return ExecuteInTransactionAsync<object>(
                async (transaction, token) =>
                {
                    await work(transaction, token);
                    return null;
                },
                cancellationToken);
        }

        /// <summary>
        /// Runs the work in the context's execution strategy, each attempt on a new connection and transaction.
        /// </summary>
        public virtual async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            // This context only supplies the strategy; every attempt opens its own.
            await using TContext context = ContextFactory.CreateDbContext();
            IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();

            return await strategy.ExecuteAsync(
                async token =>
                {
                    await using EfRepositoryTransaction<TContext> transaction = await EfRepositoryTransaction<TContext>.BeginAttemptAsync(this, ContextFactory, RepositoryConfiguration, token);
                    TResult result = await work(transaction, token);
                    await transaction.CommitAsync(token);
                    return result;
                },
                cancellationToken);
        }
    }
}
