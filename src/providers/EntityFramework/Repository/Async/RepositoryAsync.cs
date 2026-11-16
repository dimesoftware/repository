using System;
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
                    Exception exception = ToRepositoryException(dbUpdateEx);
                    if (exception == null)
                        throw;

                    throw exception;
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
                            return await SaveChangesAsync(context);
                        }
                        return true;
                    }

                    foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries) await failedEntry.ReloadAsync();
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
    }
}