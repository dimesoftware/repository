using System;
using System.Collections.Generic;
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
        private readonly List<TContext> _contexts = [];
        private IDbContextTransaction _transaction;
        private bool _completed;

        private EfRepositoryTransaction(EfRepositoryFactory<TContext> owner, IDbContextFactory<TContext> contextFactory, RepositoryConfiguration configuration, bool replayable)
        {
            _owner = owner;
            _contextFactory = contextFactory;
            _configuration = configuration;
            IsReplayable = replayable;
            _root = contextFactory.CreateDbContext();
        }

        /// <summary>
        /// True when this is one attempt of a unit that the execution strategy replays as a whole.
        /// </summary>
        internal bool IsReplayable { get; }

        internal static Task<EfRepositoryTransaction<TContext>> BeginAsync(
            EfRepositoryFactory<TContext> owner,
            IDbContextFactory<TContext> contextFactory,
            RepositoryConfiguration configuration,
            CancellationToken cancellationToken)
            => BeginAsync(owner, contextFactory, configuration, replayable: false, cancellationToken);

        /// <summary>
        /// Starts a transaction for one attempt of a unit that the execution strategy replays as a whole.
        /// </summary>
        internal static Task<EfRepositoryTransaction<TContext>> BeginAttemptAsync(
            EfRepositoryFactory<TContext> owner,
            IDbContextFactory<TContext> contextFactory,
            RepositoryConfiguration configuration,
            CancellationToken cancellationToken)
            => BeginAsync(owner, contextFactory, configuration, replayable: true, cancellationToken);

        private static async Task<EfRepositoryTransaction<TContext>> BeginAsync(
            EfRepositoryFactory<TContext> owner,
            IDbContextFactory<TContext> contextFactory,
            RepositoryConfiguration configuration,
            bool replayable,
            CancellationToken cancellationToken)
        {
            EfRepositoryTransaction<TContext> transaction = new(owner, contextFactory, configuration, replayable);
            try
            {
                if (!replayable && transaction._root.Database.CreateExecutionStrategy().RetriesOnFailure)
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
            _contexts.Add(context);
            context.Database.SetDbConnection(_root.Database.GetDbConnection(), contextOwnsConnection: false);
            context.Database.UseTransaction(_transaction.GetDbTransaction());
            return context;
        }

        public async Task AcquireLockAsync(string key, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
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

        public async Task CommitAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfCompleted();
            await _transaction.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfCompleted();
            await _transaction.RollbackAsync(cancellationToken);
            _completed = true;
        }

        private void ThrowIfCompleted()
        {
            if (_completed)
                throw new InvalidOperationException("The transaction has already completed.");
        }

        public async ValueTask DisposeAsync()
        {
            _completed = true;

            // Contexts handed out are disposed here too, in case their caller did not.
            foreach (TContext context in _contexts)
                await context.DisposeAsync();

            _contexts.Clear();

            // Disposing an uncommitted transaction rolls it back.
            if (_transaction != null)
                await _transaction.DisposeAsync();

            await _root.DisposeAsync();
        }
    }
}
