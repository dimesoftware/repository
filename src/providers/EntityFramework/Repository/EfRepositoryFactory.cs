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
            => CreateRepository<TEntity>(transaction, opts);

        public void SetContextFactory(IDbContextFactory<TContext> contextFactory)
        {
            ContextFactory = contextFactory;
        }

        /// <summary>
        /// Starts a transaction on one connection.
        /// A retrying execution strategy rejects it, since the unit cannot be replayed.
        /// </summary>
        public virtual async Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            UnitScope.ThrowIfActive();
            return await EfRepositoryTransaction<TContext>.BeginAsync(this, ContextFactory, RepositoryConfiguration, cancellationToken);
        }

        /// <summary>
        /// Runs the work in the context's execution strategy, each attempt on a new connection and transaction.
        /// On a retrying strategy, save errors the repository cannot classify surface as EF raised them: a DbUpdateException, or a RetryLimitExceededException once the retries run out.
        /// </summary>
        public virtual Task ExecuteInTransactionAsync(Func<IRepositoryTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
            => ExecuteInTransactionAsync(work, verifySucceeded: null, cancellationToken);

        /// <summary>
        /// Runs the work in the context's execution strategy, each attempt on a new connection and transaction.
        /// When the connection drops during the commit, the strategy calls <paramref name="verifySucceeded"/> to learn whether the unit committed before it replays it.
        /// </summary>
        /// <param name="work">The unit of work, which creates its repositories from the transaction it is given</param>
        /// <param name="verifySucceeded">Returns true when the unit did commit, for example by looking up a row it wrote</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public virtual Task ExecuteInTransactionAsync(Func<IRepositoryTransaction, CancellationToken, Task> work, Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            return RunUnitAsync<object>(
                async (transaction, token) =>
                {
                    await work(transaction, token);
                    return null;
                },
                verifySucceeded,
                cancellationToken);
        }

        /// <summary>
        /// Runs the work in the context's execution strategy, each attempt on a new connection and transaction.
        /// On a retrying strategy, save errors the repository cannot classify surface as EF raised them: a DbUpdateException, or a RetryLimitExceededException once the retries run out.
        /// </summary>
        public virtual Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken = default)
            => ExecuteInTransactionAsync(work, verifySucceeded: null, cancellationToken);

        /// <summary>
        /// Runs the work in the context's execution strategy, each attempt on a new connection and transaction.
        /// When the connection drops during the commit, the strategy calls <paramref name="verifySucceeded"/> to learn whether the unit committed before it replays it.
        /// </summary>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="work">The unit of work, which creates its repositories from the transaction it is given</param>
        /// <param name="verifySucceeded">Returns true when the unit did commit, for example by looking up a row it wrote</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The result of the attempt that committed</returns>
        public virtual Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);
            return RunUnitAsync(work, verifySucceeded, cancellationToken);
        }

        private async Task<TResult> RunUnitAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken)
        {
            UnitScope.Enter();

            // This context only supplies the strategy; every attempt opens its own.
            await using TContext context = ContextFactory.CreateDbContext();
            IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
            TResult result = default;

            return await strategy.ExecuteAsync(
                strategy.RetriesOnFailure,
                async (_, replayable, token) =>
                {
                    await using EfRepositoryTransaction<TContext> transaction = await EfRepositoryTransaction<TContext>.BeginAttemptAsync(this, ContextFactory, RepositoryConfiguration, replayable, token);
                    result = await work(transaction, token);
                    await transaction.CommitAttemptAsync(token);
                    return result;
                },
                verifySucceeded == null ? null : async (_, _, token) => new ExecutionResult<TResult>(await verifySucceeded(token), result),
                cancellationToken);
        }
    }
}
