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
    private readonly ConcurrentDictionary<Type, Dictionary<object, IEntity>> _sets = [];

    private readonly ConcurrentQueue<(IEntity Entity, EntityStatus Status)> _pendingQueue = [];
    private readonly ConcurrentDictionary<IEntity, EntityStatus> _entityStatus = [];
    //private readonly ConcurrentDictionary<IEntity, bool> _pendingInserts = [];
    //private readonly ConcurrentDictionary<object, IEntity> _pendingUpdates = [];
    //private readonly ConcurrentDictionary<object, IEntity> _pendingDeletes = [];

    private readonly ActionBlock<Operation> _operationsBlock;

    private readonly IStorage? _storage;

    private readonly ConcurrentDictionary<Type, List<WeakReference<IObservableQuery>>> _queries = [];

    private readonly SemaphoreSlim _notificationSemaphore = new(1);
    private readonly ModelContext? _owner;

    public ModelContext(IServiceProvider serviceProvider, ModelContextOptions options)
    {
        _operationsBlock = new ActionBlock<Operation>(DoWork);
        _storage = serviceProvider.GetService<IStorage>();

        Options = options;
        Options.ConfigureContext?.Invoke(this);
    }

    private ModelContext(ModelContext owner)
    {
        _owner = owner;

        _operationsBlock = new ActionBlock<Operation>(DoWork);
        //_storage = _owner._storage;

        Options = _owner.Options;
    }

    public Action<Exception>? OnError { get; set; }

    public ModelContextOptions Options { get; }

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
            if (set.TryGetValue(key, out var _))
            {
                return EntityStatus.Attached;
            }
        }

        return EntityStatus.Detached;

        //if (_pendingInserts.TryGetValue(entity, out var _))
        //{
        //    return EntityStatus.Added;
        //}
        
        //var key = entity.GetKey();

        //if (key != null)
        //{
        //    var entityKey = (entity.GetType(), key);

        //    if (_pendingUpdates.TryGetValue(entityKey, out var _))
        //    {
        //        return EntityStatus.Updated;
        //    }

        //    if (_pendingDeletes.TryGetValue(entityKey, out var _))
        //    {
        //        return EntityStatus.Deleted;
        //    }

        //    var set = _sets.GetOrAdd(entity.GetType(), []);
        //    if (set.TryGetValue(key, out var _))
        //    {
        //        return EntityStatus.Attached;
        //    }
        //}

        //return EntityStatus.Detached;
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
                OnError?.Invoke(ex);
            }
            catch { }
        }
    }

    public IModelContext CreateScope()
    {
        return new ModelContext(this);
    }

    public void Add(IEntity entity)
    {
        var entityStatus = GetEntityStatus(entity);

        if (entityStatus != EntityStatus.Detached)
        {
            return;
        }

        _entityStatus.TryAdd(entity, EntityStatus.Added);

        NotifyChanges(entity.GetType());

        _pendingQueue.Enqueue((entity, EntityStatus.Added));

        //_operationsBlock.Post(new OperationAdd(entity));
    }

    public void AddRange(IEnumerable<IEntity> entities)
    {
        foreach (var entity in entities)
        {
            Add(entity);
        }
        //_operationsBlock.Post(new OperationAddRange(entities));
    }

    public void Update(IEntity entity)
    {
        //if (entity.GetKey() == null)
        //{
        //    if (!_pendingInserts.TryGetValue(entity, out var _))
        //    {
        //        throw new InvalidOperationException("Updating entity with uninitialized key");
        //    }
        //    return;
        //}

        var entityStatus = GetEntityStatus(entity);

        if (entityStatus == EntityStatus.Detached)
        {
            return;
        }

        if (entityStatus != EntityStatus.Added)
        {
            _entityStatus[entity] = EntityStatus.Updated;
        }

        NotifyChanges(entity.GetType(), [entity]);

        if (entityStatus != EntityStatus.Updated)
        {
            _pendingQueue.Enqueue((entity, EntityStatus.Updated));
        }

        //_operationsBlock.Post(new OperationUpdate(entity));
    }

    public void UpdateRange(IEnumerable<IEntity> entities)
    {
        foreach (var entity in entities)
        {
            Update(entity);
        }

        //List<IEntity> entitiesToUpdate = [];

        //foreach (var entity in entities)
        //{
        //    if (entity.GetKey() == null)
        //    {
        //        if (!_pendingInserts.TryGetValue(entity, out var _))
        //        {
        //            continue;
        //        }
        //        throw new InvalidOperationException("Updating entity with uninitialized key");
        //    }

        //    entitiesToUpdate.Add(entity);
        //}

        //_operationsBlock.Post(new OperationUpdateRange(entitiesToUpdate));
    }

    public void Delete(IEntity entity)
    {
        //if (entity.GetKey() == null)
        //{
        //    throw new InvalidOperationException("Deleting entity with uninitialized key");
        //}

        //_operationsBlock.Post(new OperationDelete(entity));

        var entityStatus = GetEntityStatus(entity);

        if (entityStatus == EntityStatus.Deleted)
        {
            return;
        }

        if (entityStatus == EntityStatus.Added)
        {
            _entityStatus.Remove(entity, out var _);
        }
        else
        {
            _entityStatus[entity] = EntityStatus.Deleted;
        }        

        NotifyChanges(entity.GetType());

        _pendingQueue.Enqueue((entity, EntityStatus.Deleted));
    }

    public void DeleteRange(IEnumerable<IEntity> entities)
    {
        foreach (var entity in entities)
        {
            Delete(entity);
        }

        //_operationsBlock.Post(new OperationDeleteRange(entities));
    }

    public void Load<T>(
        Expression<Func<IQueryable<T>, IQueryable<T>>>? predicate = null, 
        Func<T, T, bool>? compareFunc = null,
        Action<IEnumerable<T>>? onLoad = null
        ) where T : class, IEntity
    {
        _operationsBlock.Post(
            new OperationFetch(
                loadFunction: storage => storage.Load(predicate?.Compile()),
                compareFunc: compareFunc != null ? (storageEntity, localEntity) => compareFunc((T)storageEntity, (T)localEntity) : null,
                onLoad: onLoad != null ? items => onLoad?.Invoke(items.Cast<T>()) : null));
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

            //.Concat(_pendingQueue.OfType<OperationAdd>().Select(_ => _.Entity).OfType<T>())
            //.Except(_pendingQueue.OfType<OperationDelete>().Select(_ => _.Entity).OfType<T>())
            .ToList();
    }

    public async Task Flush()
    {
        var signalEvent = new AsyncAutoResetEvent();
        _operationsBlock.Post(new OperationFlush(signalEvent));
        await signalEvent.WaitAsync();
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

    private void NotifyChanges(Type typeOfEntity, params IEntity[] changedEntities)
    {
        var queries = _queries.GetOrAdd(typeOfEntity, []);

        try
        {
            _notificationSemaphore.Wait();
            List<WeakReference<IObservableQuery>>? referencesToRemove = null;
            foreach (var queryReference in queries)
            {
                if (queryReference.TryGetTarget(out var query))
                {
                    query.NotifyChanges(changedEntities);
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
