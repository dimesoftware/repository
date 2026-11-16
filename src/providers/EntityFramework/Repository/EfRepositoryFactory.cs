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

        public void SetContextFactory(IDbContextFactory<TContext> contextFactory)
        {
            ContextFactory = contextFactory;
        }

        /// <summary>
        /// Starts a transaction on one connection.
        /// It throws on a context with a retrying execution strategy, which cannot replay it.
        /// </summary>
        public virtual async Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        {
            EfTransaction<TContext> transaction = await EfTransaction<TContext>.BeginAsync(ContextFactory, cancellationToken);
            return new RepositoryTransaction(this, transaction, inUnit: false);
        }

        public Task ExecuteInTransactionAsync(Func<IRepositoryTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default)
            => ExecuteInTransactionAsync(work, verifySucceeded: null, cancellationToken);

        /// <summary>
        /// Runs the work in a transaction that commits when the work completes and rolls back when it throws, and replays it on a transient failure.
        /// </summary>
        /// <param name="work">The unit of work, which creates its repositories from the transaction it is given</param>
        /// <param name="verifySucceeded">After a transient failure, returns true only when this unit did commit, for example by looking up a row it wrote; the unit is then not replayed</param>
        /// <param name="cancellationToken">The cancellation token</param>
        public Task ExecuteInTransactionAsync(Func<IRepositoryTransaction, CancellationToken, Task> work, Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);

            return ExecuteUnitAsync<object>(
                async (transaction, token) =>
                {
                    await work(transaction, token);
                    return null;
                },
                verifySucceeded,
                cancellationToken);
        }

        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken = default)
            => ExecuteInTransactionAsync(work, verifySucceeded: null, cancellationToken);

        /// <summary>
        /// Runs the work in a transaction that commits when the work completes and rolls back when it throws, and replays it on a transient failure.
        /// </summary>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="work">The unit of work, which creates its repositories from the transaction it is given</param>
        /// <param name="verifySucceeded">After a transient failure, returns true only when this unit did commit, for example by looking up a row it wrote; the unit is then not replayed</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The result of the attempt that committed</returns>
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(work);
            return ExecuteUnitAsync(work, verifySucceeded, cancellationToken);
        }

        /// <summary>
        /// Runs a unit in the context's execution strategy, each attempt on a new connection and transaction.
        /// Every ExecuteInTransactionAsync overload ends up here, so override it to wrap every unit.
        /// Replays need a strategy that derives from EF's ExecutionStrategy, as EnableRetryOnFailure does.
        /// </summary>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="work">The unit of work, which creates its repositories from the transaction it is given</param>
        /// <param name="verifySucceeded">After a transient failure, returns true only when this unit did commit; null always replays</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The result of the attempt that committed</returns>
        protected virtual async Task<TResult> ExecuteUnitAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, Func<CancellationToken, Task<bool>> verifySucceeded, CancellationToken cancellationToken)
        {
            // This context only supplies the strategy; every attempt opens its own.
            await using TContext context = ContextFactory.CreateDbContext();
            IExecutionStrategy strategy = context.Database.CreateExecutionStrategy();
            TResult result = default;

            return await strategy.ExecuteAsync(
                work,
                async (unitWork, token) =>
                {
                    result = default;
                    await using EfTransaction<TContext> transaction = await EfTransaction<TContext>.BeginAsync(ContextFactory, token);
                    result = await unitWork(new RepositoryTransaction(this, transaction, inUnit: true), token);
                    await transaction.CommitAsync(token);
                    return result;
                },
                verifySucceeded == null
                    ? null
                    : async (_, token) =>
                    {
                        bool committed = await verifySucceeded(token);
                        return new ExecutionResult<TResult>(committed, result);
                    },
                cancellationToken);
        }

        /// <summary>
        /// Hands out the repositories of a transaction, built by the factory that started it.
        /// </summary>
        /// <param name="factory">The factory that builds the repositories</param>
        /// <param name="transaction">The connection and transaction the repositories share</param>
        /// <param name="inUnit">True for one attempt of a unit, which commits, rolls back and disposes the transaction on its own</param>
        private sealed class RepositoryTransaction(EfRepositoryFactory<TContext> factory, EfTransaction<TContext> transaction, bool inUnit)
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
            {
                ThrowIfInUnit();
                return transaction.CommitAsync(cancellationToken);
            }

            public Task RollbackAsync(CancellationToken cancellationToken = default)
            {
                ThrowIfInUnit();
                return transaction.RollbackAsync(cancellationToken);
            }

            public ValueTask DisposeAsync()
                => inUnit ? ValueTask.CompletedTask : transaction.DisposeAsync();

            private void ThrowIfInUnit()
            {
                transaction.ThrowIfCompleted();
                if (inUnit)
                    throw new InvalidOperationException("ExecuteInTransactionAsync commits when the work completes and rolls back when it throws, so the work cannot commit or roll back the transaction itself.");
            }
        }
    }
}
