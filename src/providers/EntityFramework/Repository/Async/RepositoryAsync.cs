using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Dime.Repositories
{
    public partial class EfRepository<TEntity, TContext>
    {
        public virtual Task<bool> SaveChangesAsync()
            => SaveChangesAsync(Context);

        public virtual async Task<bool> SaveChangesAsync(TContext context)
        {
            try
            {
                if (Configuration?.SaveInBatch ?? false)
                    return false;

                int result = await context.SaveChangesAsync();
                return 0 < result;
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx) when (Configuration?.SaveStrategy == ConcurrencyStrategy.Throw)
            {
                throw new ConcurrencyException(dbUpdateConcurrencyEx.Message, dbUpdateConcurrencyEx);
            }
            catch (DbUpdateConcurrencyException dbUpdateConcurrencyEx)
            {
                if (Configuration?.SaveStrategy == ConcurrencyStrategy.ClientFirst)
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

                foreach (EntityEntry failedEntry in dbUpdateConcurrencyEx.Entries)
                    await failedEntry.ReloadAsync();

                return true;
            }
            catch (DbUpdateException dbUpdateEx)
            {
                dbUpdateEx.ThrowAsRepositoryException();
                throw;
            }
        }
    }
}