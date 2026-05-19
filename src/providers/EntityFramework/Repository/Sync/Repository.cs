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
                    if (dbUpdateEx.InnerException?.InnerException == null)
                        throw;

                    if (dbUpdateEx.InnerException.InnerException is not SqlException sqlException)
                        throw new DatabaseAccessException(dbUpdateEx.Message, dbUpdateEx.InnerException);

                    throw sqlException.Number switch
                    {
                        2627 => (Exception)new ConcurrencyException(sqlException.Message, sqlException),
                        547 => new ConstraintViolationException(sqlException.Message, sqlException),
                        2601 => new ConstraintViolationException(sqlException.Message, sqlException),
                        _ => new DatabaseAccessException(sqlException.Message, sqlException)
                    };
                }
            }
            while (saveFailed && retryMax <= 3);
        }

        public void Dispose()
        {
            if (Context == null)
                return;

            Context.Dispose();
            Context = null;
        }

        public static explicit operator TContext(EfRepository<TEntity, TContext> repository)
            => repository.Context;
    }
}