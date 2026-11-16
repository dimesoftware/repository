using System;
using System.Threading;

namespace Dime.Repositories
{
    /// <summary>
    /// Tracks whether the current async flow runs inside the work of a unit, whatever its context type.
    /// </summary>
    internal static class UnitScope
    {
        private static readonly AsyncLocal<bool> Active = new();

        /// <summary>
        /// Marks the flow as inside a unit; call it from an async method so the mark ends when that method returns.
        /// </summary>
        internal static void Enter()
        {
            ThrowIfActive();
            Active.Value = true;
        }

        internal static void ThrowIfActive()
        {
            // A transaction started inside a unit commits on its own, so a replay of the unit would apply it twice.
            if (Active.Value)
                throw new InvalidOperationException("A unit cannot start another transaction. Create repositories from the transaction the work is given.");
        }
    }
}
