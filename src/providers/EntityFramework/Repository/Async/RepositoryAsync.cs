using System;
using Microsoft.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public virtual async Task<bool> SaveChangesAsync()
        {
            int retryMax;
            bool saveFailed;
            do
            {
                try
                {
                    return !Configuration.SaveInBatch && 0 < await Context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx)
                {
                    if (Configuration.SaveStrategy == ConcurrencyStrategy.ClientFirst)
                    {
                        foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                        {
                            PropertyValues dbValues = await failedEntry.GetDatabaseValuesAsync();
                            if (dbValues == null)
                                continue;

                            failedEntry.OriginalValues.SetValues(dbValues);
                            return await SaveChangesAsync(Context);
                        }
                        return true;
                    }

                    foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries) await failedEntry.ReloadAsync();
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

        public virtual async Task<bool> SaveChangesAsync(TContext context)
        {
            int retryMax = 0;
            bool saveFailed = false;
            do
            {
                try
                {
                    if (!((!Configuration?.SaveInBatch) ?? true))
                        return false;

                    int result = await context.SaveChangesAsync();
                    return 0 < result;
                }
                catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx)
                {
                    if (Configuration.SaveStrategy == ConcurrencyStrategy.ClientFirst)
                    {
                        foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                        {
                            PropertyValues dbValues = await failedEntry.GetDatabaseValuesAsync();
                            if (dbValues == null)
                                continue;

                            failedEntry.OriginalValues.SetValues(dbValues);
                            return await SaveChangesAsync(Context);
                        }
                        return true;
                    }

                    foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries) await failedEntry.ReloadAsync();
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
    }
}