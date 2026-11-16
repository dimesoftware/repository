using System;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext> : ISqlRepository<TEntity>
        where TEntity : class, new()
        where TContext : DbContext
    {
        public EfRepository(TContext dbContext)
            : this(dbContext, new RepositoryConfiguration())
        {
        }

        public EfRepository(TContext dbContext, RepositoryConfiguration configuration)
        {
            Context = dbContext;
            Configuration = configuration;
        }

        public EfRepository(IDbContextFactory<TContext> contextFactory, RepositoryConfiguration configuration)
        {
            ContextFactory = contextFactory;
            Configuration = configuration;
        }

        private IDbContextFactory<TContext> ContextFactory { get; set; }

        private TContext _context;

        protected TContext Context
        {
            get => ContextFactory?.CreateDbContext() ?? _context;
            set => _context = value;
        }

        /// <summary>
        /// True when this repository was built with an <see cref="IDbContextFactory{TContext}"/>.
        /// Operations that benefit from independent DbContexts (e.g. running a data SELECT and a
        /// COUNT(*) concurrently) consult this flag — EF Core forbids parallel operations on a
        /// single context, so the optimization is only safe when a factory can mint a fresh one.
        /// </summary>
        protected bool HasContextFactory => ContextFactory is not null;

        /// <summary>
        /// Returns a fresh <typeparamref name="TContext"/> instance. Equivalent to <see cref="Context"/>
        /// when a factory is in use; in single-context mode the same instance is returned both times
        /// (callers must take care not to run operations concurrently in that case).
        /// </summary>
        protected TContext CreateContext()
            => ContextFactory?.CreateDbContext() ?? _context;

        public RepositoryConfiguration Configuration { get; set; }

        /// <summary>
        /// True when the repository runs on a transaction, whose contexts share one connection.
        /// </summary>
        private bool RunsInTransaction => ContextFactory is EfRepositoryTransaction<TContext>;

        /// <summary>
        /// True when the repository runs in a unit that the execution strategy replays as a whole.
        /// Save errors it cannot classify are then rethrown as EF raised them, so the strategy can decide on a replay.
        /// </summary>
        internal bool RunsInReplayableUnit => ContextFactory is EfRepositoryTransaction<TContext> { IsReplayable: true };

        /// <summary>
        /// Maps a save error to a typed repository exception, or returns null when it should surface as EF raised it.
        /// </summary>
        private Exception ToRepositoryException(DbUpdateException dbUpdateEx)
        {
            // SQL Server raises the SqlException directly under the DbUpdateException.
            SqlException sqlException = dbUpdateEx.InnerException as SqlException;
            if (sqlException == null && dbUpdateEx.InnerException?.InnerException == null)
                return null;

            // Typed exceptions are never transient, so the execution strategy only gets to see the rest.
            bool classified = sqlException is { Number: 2627 or 547 or 2601 };
            if (RunsInReplayableUnit && !classified)
                return null;

            if (sqlException == null)
                return new DatabaseAccessException(dbUpdateEx.Message, dbUpdateEx.InnerException);

            return sqlException.Number switch
            {
                2627 => new ConcurrencyException(sqlException.Message, sqlException),
                547 => new ConstraintViolationException(sqlException.Message, sqlException),
                2601 => new ConstraintViolationException(sqlException.Message, sqlException),
                _ => new DatabaseAccessException(sqlException.Message, sqlException)
            };
        }

        public virtual bool SaveChanges(TContext context)
        {
            int retryMax;
            bool saveFailed;
            do
            {
                try
                {
                    if (Configuration?.SaveInBatch ?? false)
                        return false;

                    int result = context.SaveChanges();
                    return 0 < result;
                }
                catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx)
                {
                    if (Configuration?.SaveStrategy == ConcurrencyStrategy.ClientFirst)
                    {
                        foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                        {
                            PropertyValues dbValues = failedEntry.GetDatabaseValues();
                            if (dbValues == null)
                                continue;

                            failedEntry.OriginalValues.SetValues(dbValues);
                            return SaveChanges(context);
                        }

                        return true;
                    }

                    foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                        failedEntry.Reload();

                    return true;
                }
                catch (DbUpdateException dbUpdateEx)
                {
                    Exception exception = ToRepositoryException(dbUpdateEx);
                    if (exception == null)
                        throw;

                    throw exception;
                }
            }
            while (saveFailed && retryMax <= 3);
        }

        public void Dispose()
        {
            // A factory hands out a context per operation, so there is no context to dispose here.
            if (HasContextFactory || _context == null)
                return;

            _context.Dispose();
            _context = null;
        }

        public static explicit operator TContext(EfRepository<TEntity, TContext> repository)
            => repository.Context;
    }
}