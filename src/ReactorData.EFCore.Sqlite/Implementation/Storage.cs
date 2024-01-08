using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.EFCore.Sqlite.Implementation;

class Storage<T>(IServiceProvider serviceProvider) : IStorage where T : DbContext
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly SemaphoreSlim _semaphore = new(1);
    private bool _initialized;

    private async ValueTask Initialize(T dbContext)
    {
        if (_initialized)
        {
            return;
        }
            
        try
        {
            _semaphore.Wait();

            if (_initialized)
            {
                return;
            }
            
            await dbContext.Database.MigrateAsync();

            _initialized = true;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IEnumerable<IEntity>> Load<TEntity>(Expression<Func<TEntity, bool>> predicate) where TEntity : class, IEntity
    {
        using var serviceScope = _serviceProvider.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<T>();

        await Initialize(dbContext);

        return await dbContext.Set<TEntity>().Where(predicate).ToListAsync();
    }

    public async Task Save(IEnumerable<StorageOperation> operations)
    {
        using var serviceScope = _serviceProvider.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<T>();

        await Initialize(dbContext);

        foreach (var operation in operations)
        {
            switch (operation)
            {
                case StorageInsert storageInsert:
                    dbContext.Add(storageInsert.Entity);
                    break;
                case StorageUpdate storageUpdate:
                    dbContext.Update(storageUpdate.Entity);
                    break;
                case StorageDelete storageDelete:
                    dbContext.Remove(storageDelete.Entity);
                    break;
            }
        }

        await dbContext.SaveChangesAsync();
    }
}
