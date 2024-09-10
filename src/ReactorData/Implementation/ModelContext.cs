using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace ReactorData.Implementation;

partial class ModelContext : IModelContext
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<object, IEntity>> _sets = [];

    private readonly Queue<(IEntity Entity, EntityStatus Status)> _operationQueue = [];

    private readonly ConcurrentDictionary<IEntity, EntityStatus> _entityStatus = [];

    private readonly ActionBlock<Operation> _operationsBlock;

    private readonly IStorage? _storage;

    private readonly ConcurrentDictionary<Type, List<WeakReference<IObservableQuery>>> _queries = [];

    private readonly SemaphoreSlim _notificationSemaphore = new(1);
    private readonly ModelContext? _owner;

    public ModelContext(IServiceProvider serviceProvider, ModelContextOptions options)
    {
        _operationsBlock = new ActionBlock<Operation>(DoWork);
        _storage = serviceProvider.GetService<IStorage>();
        Dispatcher = serviceProvider.GetService<IDispatcher>();

        Options = options;
        Options.ConfigureContext?.Invoke(this);
    }

    private ModelContext(ModelContext owner)
    {
        _owner = owner;

        _operationsBlock = new ActionBlock<Operation>(DoWork);

        Dispatcher = _owner.Dispatcher;
        Options = _owner.Options;
    }

    public ModelContextOptions Options { get; }

    public IDispatcher? Dispatcher { get; }

    public EntityStatus GetEntityStatus(IEntity entity)
    {
        if (_entityStatus.TryGetValue(entity, out var entityStatus))
        {
            return entityStatus; 
        }

        var key = entity.GetKey();
        if (key != null)
        {
            var set = _sets.GetOrAdd(entity.GetType(), []);
            if (set.TryGetValue(key, out var attachedEntity))
            {
                if (entity == attachedEntity)
                {
                    return EntityStatus.Attached;
                }
            }
        }

        return EntityStatus.Detached;
    }

    private async Task DoWork(Operation operation)
    {
        try
        {
            await operation.Do(this);
        }
        catch (Exception ex)
        {
            try
            {
                 Dispatcher?.OnError(ex);
            }
            catch { }
        }
    }

    public IModelContext CreateScope()
    {
        return new ModelContext(this);
    }

    public void Add(params IEntity[] entities)
    {
        _operationsBlock.Post(new OperationAdd(entities));
    }
    public void Replace(IEntity oldEntiy, IEntity newEntity)
    {
        _operationsBlock.Post(new OperationUpdate(oldEntiy, newEntity));
    }

    public void Delete(params IEntity[] entities)
    {
        _operationsBlock.Post(new OperationDelete(entities));
    }

    public void Load<T>(
        Expression<Func<IQueryable<T>, IQueryable<T>>>? predicate = null, 
        Func<T, T, bool>? compareFunc = null,
        bool forceReload = false,
        Action<IEnumerable<T>>? onLoad = null
        ) where T : class, IEntity
    {
        _operationsBlock.Post(
            new OperationFetch(
                typeof(T),
                LoadFunction: storage => storage.Load(predicate?.Compile()),
                CompareFunc: compareFunc != null ? (storageEntity, localEntity) => compareFunc((T)storageEntity, (T)localEntity) : null,
                ForceReload: forceReload,
                OnLoad: onLoad != null ? items => onLoad?.Invoke(items.Cast<T>()) : null));
    }

    public void Save()
    {
        _operationsBlock.Post(new OperationSave());
    }

    public IReadOnlyList<T> Set<T>() where T : class, IEntity
    {
        var typeofT = typeof(T);
        var set = _sets.GetOrAdd(typeofT, []);

        return set
            .Select(_=>_.Value)
            .Cast<T>()
            .Concat(_entityStatus.Where(_ => _.Value == EntityStatus.Added).Select(_ => _.Key).OfType<T>())
            .Except(_entityStatus.Where(_ => _.Value == EntityStatus.Deleted).Select(_ => _.Key).OfType<T>())
            .ToList();
    }
    public void DiscardChanges()
    {
        _operationsBlock.Post(new OperationDiscardChanges());
    }

    public async Task Flush()
    {
        var signalEvent = new AsyncAutoResetEvent();
        _operationsBlock.Post(new OperationFlush(signalEvent));
        await signalEvent.WaitAsync();
    }

    public void RunBackgroundTask(Func<IModelContext, Task> task)
    {
        _operationsBlock.Post(new OperationBackgroundTask(task));
    }

    public IQuery<T> Query<T>(Expression<Func<IQueryable<T>, IQueryable<T>>>? predicateExpression = null) where T : class, IEntity
    {
        var predicate = predicateExpression?.Compile();

        var typeofT = typeof(T);
        var queries = _queries.GetOrAdd(typeofT, []);

        var observableQuery = new ObservableQuery<T>(this, predicate);
        queries.Add(new WeakReference<IObservableQuery>(observableQuery));
        
        return observableQuery.Query;
    }

    public T? FindByKey<T>(object key) where T : class, IEntity
    {
        var typeofT = typeof(T);
        var set = _sets.GetOrAdd(typeofT, []);

        if (set.TryGetValue(key, out var entity))
        {
            return (T)entity;
        }

        return default;
    }


    private void NotifyChanges(Type typeOfEntity, IEntity[]? changedEntities = null, bool forceReload = false)
    {
        var queries = _queries.GetOrAdd(typeOfEntity, []);

        try
        {
            _notificationSemaphore.Wait();
            List<WeakReference<IObservableQuery>>? referencesToRemove = null;
            foreach (var queryReference in queries.ToArray())
            {
                if (queryReference.TryGetTarget(out var query))
                {
                    query.NotifyChanges(changedEntities, forceReload);
                }
                else
                {
                    referencesToRemove ??= [];
                    referencesToRemove.Add(queryReference);
                }
            }

            if (referencesToRemove?.Count > 0)
            {
                foreach (var queryReference in referencesToRemove)
                {
                    queries.Remove(queryReference);
                }
            }
        }
        finally
        {
            _notificationSemaphore.Release();
        }

    }
}
