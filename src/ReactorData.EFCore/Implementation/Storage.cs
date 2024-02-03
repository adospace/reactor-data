using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.EFCore.Implementation;

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

    public async Task<IEnumerable<IEntity>> Load<TEntity>(Func<IQueryable<TEntity>, IQueryable<TEntity>>? queryFunction = null) where TEntity : class, IEntity
    {
        using var serviceScope = _serviceProvider.CreateScope();
        var dbContext = serviceScope.ServiceProvider.GetRequiredService<T>();

        await Initialize(dbContext);

        IQueryable<TEntity> query = dbContext.Set<TEntity>().AsNoTracking();

        if (queryFunction != null)
        {
            query = queryFunction(query);
        }

        return await query.ToListAsync();
    }

    public async Task Save(IEnumerable<StorageOperation> operations)
    {

        try
        {
            using var serviceScope = _serviceProvider.CreateScope();
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<T>();
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            await Initialize(dbContext);

            foreach (var operation in operations)
            {
                foreach (var entity in operation.Entities)
                {
                    dbContext.Attach(entity);

                    switch (operation)
                    {
                        case StorageAdd storageInsert:
                            dbContext.Entry(entity).State = EntityState.Added;
                            //dbContext.Add(entity);
                            break;
                        case StorageUpdate storageUpdate:
                            dbContext.Entry(entity).State = EntityState.Modified;
                            //dbContext.Update(entity);
                            break;
                        case StorageDelete storageDelete:
                            dbContext.Entry(entity).State = EntityState.Deleted;
                            //dbContext.Remove(entity);
                            break;
                    }

                }
            }

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            throw;
        }
    }
}
