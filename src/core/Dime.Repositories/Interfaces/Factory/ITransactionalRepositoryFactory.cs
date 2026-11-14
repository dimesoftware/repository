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
        /// </summary>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <returns>The transaction that creates repositories inside it</returns>
        Task<IRepositoryTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
    }
}
