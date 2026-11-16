using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// A set of writes that commit or roll back together.
    /// Repositories it creates share its connection.
    /// Disposing it without a commit rolls it back; inside the work of a unit, the unit disposes it.
    /// An instance is not thread-safe and fails fast once it has completed.
    /// </summary>
    public interface IRepositoryTransaction : IAsyncDisposable
    {
        /// <summary>
        /// Creates a repository whose writes are part of the transaction.
        /// </summary>
        /// <returns>Repository instance for the collection type</returns>
        IRepository<T> Create<T>() where T : class, new();

        /// <summary>
        /// Takes an exclusive lock on the key that is held until the transaction ends.
        /// Callers that lock the same key run one after the other.
        /// </summary>
        /// <param name="key">The name of the lock, at most 255 characters</param>
        /// <param name="timeout">How long to wait for the lock; null waits until it is granted or the command times out</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task AcquireLockAsync(string key, TimeSpan? timeout = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Commits every write made through the repositories it created.
        /// Inside the work of a unit it throws, since the unit commits on its own.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Discards every write made through the repositories it created.
        /// Inside the work of a unit it throws; throw from the work instead.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
