using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// A set of writes that commit or roll back together.
    /// Repositories it creates share its connection.
    /// Disposing it without a commit rolls it back.
    /// An instance is not thread-safe and fails fast once it has completed.
    /// </summary>
    public interface IRepositoryTransaction : IRepositoryFactory, IAsyncDisposable
    {
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
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        Task CommitAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Discards every write made through the repositories it created.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        Task RollbackAsync(CancellationToken cancellationToken = default);
    }
}
