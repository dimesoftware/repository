using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Dime.Repositories
{
    /// <summary>
    /// Runs every repository it creates on one connection and one transaction.
    /// Each operation still gets its own context, enlisted in the shared transaction.
    /// </summary>
    internal sealed class EfRepositoryTransaction<TContext> : IRepositoryTransaction, IDbContextFactory<TContext>
        where TContext : DbContext
    {
        private const int MaxLockKeyLength = 255;

        private readonly EfRepositoryFactory<TContext> _owner;
        private readonly IDbContextFactory<TContext> _contextFactory;
        private readonly RepositoryConfiguration _configuration;
        private readonly TContext _root;
        private IDbContextTransaction _transaction;
        private bool _completed;

        private EfRepositoryTransaction(EfRepositoryFactory<TContext> owner, IDbContextFactory<TContext> contextFactory, RepositoryConfiguration configuration, bool inUnit, bool replayable)
        {
            _owner = owner;
            _contextFactory = contextFactory;
            _configuration = configuration;
            IsInUnit = inUnit;
            IsReplayable = replayable;
            _root = contextFactory.CreateDbContext();
        }

        /// <summary>
        /// True when this is one attempt of a unit, which commits and rolls back on its own.
        /// </summary>
        internal bool IsInUnit { get; }

        /// <summary>
        /// True when this is one attempt of a unit that the execution strategy replays as a whole.
        /// </summary>
        internal bool IsReplayable { get; }

        internal static Task<EfRepositoryTransaction<TContext>> BeginAsync(
            EfRepositoryFactory<TContext> owner,
            IDbContextFactory<TContext> contextFactory,
            RepositoryConfiguration configuration,
            CancellationToken cancellationToken)
            => BeginAsync(owner, contextFactory, configuration, inUnit: false, replayable: false, cancellationToken);

        /// <summary>
        /// Starts a transaction for one attempt of a unit, which a retrying execution strategy replays as a whole.
        /// </summary>
        internal static Task<EfRepositoryTransaction<TContext>> BeginAttemptAsync(
            EfRepositoryFactory<TContext> owner,
            IDbContextFactory<TContext> contextFactory,
            RepositoryConfiguration configuration,
            bool replayable,
            CancellationToken cancellationToken)
            => BeginAsync(owner, contextFactory, configuration, inUnit: true, replayable, cancellationToken);

        private static async Task<EfRepositoryTransaction<TContext>> BeginAsync(
            EfRepositoryFactory<TContext> owner,
            IDbContextFactory<TContext> contextFactory,
            RepositoryConfiguration configuration,
            bool inUnit,
            bool replayable,
            CancellationToken cancellationToken)
        {
            EfRepositoryTransaction<TContext> transaction = new(owner, contextFactory, configuration, inUnit, replayable);
            try
            {
                if (!inUnit && transaction._root.Database.CreateExecutionStrategy().RetriesOnFailure)
                    throw new InvalidOperationException("A transaction cannot run on a context with a retrying execution strategy, because it cannot be replayed. Use ExecuteInTransactionAsync instead.");

                transaction._transaction = await transaction._root.Database.BeginTransactionAsync(cancellationToken);
                return transaction;
            }
            catch
            {
                await transaction.DisposeAsync();
                throw;
            }
        }

        public IRepository<TEntity> Create<TEntity>() where TEntity : class, new()
            => _owner.CreateRepositoryInTransaction<TEntity>(this, _configuration);

        public TContext CreateDbContext()
        {
            ThrowIfCompleted();

            TContext context = _contextFactory.CreateDbContext();
            context.Database.SetDbConnection(_root.Database.GetDbConnection(), contextOwnsConnection: false);
            context.Database.UseTransaction(_transaction.GetDbTransaction());
            return context;
        }

        public async Task AcquireLockAsync(string key, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            ThrowIfCompleted();
            ArgumentException.ThrowIfNullOrEmpty(key);
            if (key.Length > MaxLockKeyLength)
                throw new ArgumentException($"The lock key cannot be longer than {MaxLockKeyLength} characters.", nameof(key));

            if (!_root.Database.IsSqlServer())
                throw new NotSupportedException("Locks are only supported on SQL Server.");

            SqlParameter status = new("@status", SqlDbType.Int) { Direction = ParameterDirection.Output };
            SqlParameter resource = new("@resource", SqlDbType.NVarChar, MaxLockKeyLength) { Value = key };
            SqlParameter lockTimeout = new("@timeout", SqlDbType.Int) { Value = timeout is null ? -1 : (int)timeout.Value.TotalMilliseconds };

            await _root.Database.ExecuteSqlRawAsync(
                "EXEC @status = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout",
                [status, resource, lockTimeout],
                cancellationToken);

            // sp_getapplock returns 0 or 1 when the lock is granted and a negative code otherwise.
            int result = (int)status.Value;
            if (result >= 0)
                return;

            throw result == -1
                ? new ConcurrencyException($"The lock '{key}' was not granted within the timeout.")
                : new DatabaseAccessException($"The lock '{key}' was not granted (sp_getapplock returned {result}).");
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfInUnit();
            return CommitAttemptAsync(cancellationToken);
        }

        /// <summary>
        /// Commits the transaction, also when it is one attempt of a unit.
        /// </summary>
        internal async Task CommitAttemptAsync(CancellationToken cancellationToken)
        {
            ThrowIfCompleted();
            await _transaction.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfInUnit();
            ThrowIfCompleted();
            await _transaction.RollbackAsync(cancellationToken);
            _completed = true;
        }

        private void ThrowIfCompleted()
        {
            if (_completed)
                throw new InvalidOperationException("The transaction has already completed.");
        }

        private void ThrowIfInUnit()
        {
            if (IsInUnit)
                throw new InvalidOperationException("The unit commits when its work completes and rolls back when it throws, so the work cannot commit or roll back itself.");
        }

        public async ValueTask DisposeAsync()
        {
            _completed = true;

            // Disposing an uncommitted transaction rolls it back.
            try
            {
                if (_transaction != null)
                    await _transaction.DisposeAsync();
            }
            finally
            {
                await _root.DisposeAsync();
            }
        }
    }
}
