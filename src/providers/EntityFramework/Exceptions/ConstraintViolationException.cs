using System;
using System.Diagnostics.CodeAnalysis;

namespace Dime.Repositories
{
    [Serializable]
    [ExcludeFromCodeCoverage]
    public class ConstraintViolationException : Exception
    {
        public ConstraintViolationException()
        {
        }

        public ConstraintViolationException(string message) : base(message)
        {
        }

        public ConstraintViolationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
