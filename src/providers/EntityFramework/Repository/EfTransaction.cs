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
    /// One connection and one transaction, with a context per operation enlisted in it.
    /// </summary>
    internal sealed class EfTransaction<TContext> : IDbContextFactory<TContext>, IAsyncDisposable
        where TContext : DbContext
    {
        private const int MaxLockKeyLength = 255;

        private readonly IDbContextFactory<TContext> _contextFactory;
        private readonly TContext _root;
        private readonly IDbContextTransaction _transaction;
        private bool _completed;

        private EfTransaction(IDbContextFactory<TContext> contextFactory, TContext root, IDbContextTransaction transaction)
        {
            _contextFactory = contextFactory;
            _root = root;
            _transaction = transaction;
        }

        /// <summary>
        /// Opens a connection and starts a transaction on it.
        /// </summary>
        /// <param name="contextFactory">The factory of the contexts that join the transaction</param>
        /// <param name="cancellationToken">The cancellation token</param>
        internal static async Task<EfTransaction<TContext>> BeginAsync(IDbContextFactory<TContext> contextFactory, CancellationToken cancellationToken)
        {
            TContext root = contextFactory.CreateDbContext();
            try
            {
                // A retrying execution strategy only replays a transaction that runs inside its ExecuteAsync, where it reports no retries of its own.
                if (root.Database.CreateExecutionStrategy().RetriesOnFailure)
                    throw new InvalidOperationException("A transaction cannot run on a context with a retrying execution strategy, such as EnableRetryOnFailure, because the strategy cannot replay it. Use ExecuteInTransactionAsync instead.");

                IDbContextTransaction transaction = await root.Database.BeginTransactionAsync(cancellationToken);
                return new EfTransaction<TContext>(contextFactory, root, transaction);
            }
            catch
            {
                await root.DisposeAsync();
                throw;
            }
        }

        public TContext CreateDbContext()
        {
            ThrowIfCompleted();

            TContext context = _contextFactory.CreateDbContext();
            context.Database.SetDbConnection(_root.Database.GetDbConnection(), contextOwnsConnection: false);
            context.Database.UseTransaction(_transaction.GetDbTransaction());
            return context;
        }

        public async Task AcquireLockAsync(string key, TimeSpan? timeout, CancellationToken cancellationToken)
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

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            ThrowIfCompleted();
            await _transaction.CommitAsync(cancellationToken);
            _completed = true;
        }

        public async Task RollbackAsync(CancellationToken cancellationToken)
        {
            ThrowIfCompleted();
            await _transaction.RollbackAsync(cancellationToken);
            _completed = true;
        }

        public void ThrowIfCompleted()
        {
            if (_completed)
                throw new InvalidOperationException("The transaction has already completed, so it and the repositories it created can no longer be used.");
        }

        public async ValueTask DisposeAsync()
        {
            _completed = true;

            // Disposing an uncommitted transaction rolls it back.
            try
            {
                await _transaction.DisposeAsync();
            }
            finally
            {
                await _root.DisposeAsync();
            }
        }
    }
}
