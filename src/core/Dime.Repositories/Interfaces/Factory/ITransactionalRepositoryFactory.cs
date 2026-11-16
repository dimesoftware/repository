using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dime.Repositories
{
    /// <summary>
    /// Definition for a repository factory that can run several operations in one transaction
    /// </summary>
    public interface ITransactionalRepositoryFactory
    {
        /// <summary>
        /// Starts a transaction that commits on <see cref="IRepositoryTransaction.CommitAsync"/> and rolls back when disposed without a commit.
        /// Use <see cref="ExecuteInTransactionAsync(Func{IRepositoryTransaction, CancellationToken, Task}, CancellationToken)"/> on a context with a retrying execution strategy.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The transaction that creates repositories inside it</returns>
        Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs the work in a transaction that commits when the work completes and rolls back when it throws.
        /// On a transient failure a retrying execution strategy replays the whole unit in a new transaction, so the work must have no side effects outside the database.
        /// A connection lost during the commit can replay a unit that did commit, so the work should also be idempotent.
        /// Save errors the library cannot classify surface as EF raised them: a DbUpdateException, or a RetryLimitExceededException once the retries run out.
        /// </summary>
        /// <param name="work">The unit of work, which creates its repositories from the transaction it is given</param>
        /// <param name="cancellationToken">The cancellation token</param>
        Task ExecuteInTransactionAsync(Func<IRepositoryTransaction, CancellationToken, Task> work, CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs the work in a transaction that commits when the work completes and rolls back when it throws.
        /// On a transient failure a retrying execution strategy replays the whole unit in a new transaction, so the work must have no side effects outside the database.
        /// A connection lost during the commit can replay a unit that did commit, so the work should also be idempotent.
        /// Save errors the library cannot classify surface as EF raised them: a DbUpdateException, or a RetryLimitExceededException once the retries run out.
        /// </summary>
        /// <typeparam name="TResult">The type of the result</typeparam>
        /// <param name="work">The unit of work, which creates its repositories from the transaction it is given</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The result of the attempt that committed</returns>
        Task<TResult> ExecuteInTransactionAsync<TResult>(Func<IRepositoryTransaction, CancellationToken, Task<TResult>> work, CancellationToken cancellationToken = default);
    }
}
