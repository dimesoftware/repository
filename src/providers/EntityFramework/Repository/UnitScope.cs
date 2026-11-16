using System;
using System.Threading;

namespace Dime.Repositories
{
    /// <summary>
    /// Marks the async flow that runs the work of a unit that a retrying execution strategy replays.
    /// </summary>
    internal sealed class UnitScope : IDisposable
    {
        private static readonly AsyncLocal<UnitScope> Current = new();

        private bool _ended;

        private UnitScope()
        {
        }

        internal static bool IsRetrying => Current.Value is { _ended: false };

        /// <summary>
        /// Starts the scope; call it from an async method so the caller never sees it.
        /// </summary>
        internal static UnitScope BeginRetrying()
        {
            ThrowIfRetrying();

            UnitScope scope = new();
            Current.Value = scope;
            return scope;
        }

        internal static void ThrowIfRetrying()
        {
            // A transaction started inside the unit commits on its own, so a replay of the unit would apply it twice.
            if (IsRetrying)
                throw new InvalidOperationException("ExecuteInTransactionAsync replays its work on a transient failure, so the work cannot start another transaction. Create repositories from the transaction the work is given.");
        }

        // Tasks the work started still see this scope, so ending it reaches them too.
        public void Dispose() => _ended = true;
    }
}
