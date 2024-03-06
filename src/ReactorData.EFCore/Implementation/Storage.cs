using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.EFCore.Implementation;


class Storage<T> : IStorage where T : DbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<Storage<T>>? _logger;
    private readonly SemaphoreSlim _semaphore = new(1);
    private bool _initialized;

    public Storage(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<Storage<T>>>();
    }

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

            _logger?.LogTrace("Migrating context {DbContext}...", typeof(T));

            await dbContext.Database.MigrateAsync();

            _logger?.LogTrace("Context {DbContext} migrated", typeof(T));

            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception raised when initializing the DbContext using migrations");
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

        try
        {
            IQueryable<TEntity> query = dbContext.Set<TEntity>();

            if (queryFunction != null)
            {
                query = queryFunction(query);
            }

            return await query.ToListAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unable to load entities of type {EntityType}", typeof(TEntity));
            return [];
            //throw;
        }

        //return dbContext.ChangeTracker.Entries().Select(_=>_.Entity).Cast<IEntity>().ToList();
    }

    public async Task Save(IEnumerable<StorageOperation> operations)
    {

        try
        {
            using var serviceScope = _serviceProvider.CreateScope();
            var dbContext = serviceScope.ServiceProvider.GetRequiredService<T>();
            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            await Initialize(dbContext);

            _logger?.LogTrace("Apply changes for {Entities} entities", operations.Count());

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
                            _logger?.LogTrace("Insert entity {EntityId} : {Entity}", entity.GetKey(), System.Text.Json.JsonSerializer.Serialize(entity));
                            break;
                        case StorageUpdate storageUpdate:
                            dbContext.Entry(entity).State = EntityState.Modified;
                            //dbContext.Update(entity);
                            _logger?.LogTrace("Update entity {EntityId} : {Entity}", entity.GetKey(), System.Text.Json.JsonSerializer.Serialize(entity));
                            break;
                        case StorageDelete storageDelete:
                            dbContext.Entry(entity).State = EntityState.Deleted;
                            //dbContext.Remove(entity);
                            _logger?.LogTrace("Delete entity {EntityId} : {Entity}", entity.GetKey(), System.Text.Json.JsonSerializer.Serialize(entity));
                            break;
                    }

                }
            }

            await dbContext.SaveChangesAsync();

            _logger?.LogTrace("Apply changes for {Entities} entities completed", operations.Count());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Saving changes to context resulted in an unhandled exception");
            throw;
        }
    }
}
