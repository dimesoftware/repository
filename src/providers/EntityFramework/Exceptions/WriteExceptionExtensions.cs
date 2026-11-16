using System;
using Microsoft.Data.SqlClient;

namespace Dime.Repositories
{
    internal static class WriteExceptionExtensions
    {
        private const int ForeignKeyViolation = 547;
        private const int UniqueIndexViolation = 2601;
        private const int UniqueConstraintViolation = 2627;

        /// <summary>
        /// Throws the repository exception that a write error maps to, or returns when the error should surface as the provider raised it.
        /// </summary>
        /// <param name="exception">The DbUpdateException of a save, or the SqlException of a set-based write</param>
        internal static void ThrowAsRepositoryException(this Exception exception)
        {
            // SQL Server raises the SqlException itself on a set-based write, and directly under the DbUpdateException on a save.
            SqlException sqlException = exception as SqlException ?? exception.InnerException as SqlException;
            if (sqlException is { Number: ForeignKeyViolation or UniqueIndexViolation or UniqueConstraintViolation })
                throw new ConstraintViolationException(sqlException.Message, exception);

            if (sqlException != null)
                throw new DatabaseAccessException(sqlException.Message, exception);

            // Errors of other providers surface as raised, unless they wrap a further cause.
            if (exception.InnerException?.InnerException != null)
                throw new DatabaseAccessException(exception.Message, exception);
        }
    }
}
