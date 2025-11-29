using System;
using Microsoft.Data.SqlClient;
using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dime.Repositories
{
    [ExcludeFromCodeCoverage]
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