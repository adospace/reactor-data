using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static System.Collections.Specialized.BitVector32;

namespace ReactorData.Implementation;

class Container : IContainer
{
    #region Operations
    abstract class Operation();

    abstract class OperationPending(IEntity entity) : Operation
    {
        public IEntity Entity { get; } = entity;
    }

    class OperationAdd(IEntity entity) : OperationPending(entity);

    class OperationUpdate(IEntity entity) : OperationPending(entity);

    class OperationRemove(IEntity entity) : OperationPending(entity);

    class OperationAddRange(IEnumerable<IEntity> entities) : Operation
    {
        public IEnumerable<IEntity> Entities { get; } = entities;
    }

    class OperationFetch(Func<IStorage, Task<IEnumerable<IEntity>>> loadFunction) : Operation
    {
        public Func<IStorage, Task<IEnumerable<IEntity>>> LoadFunction { get; } = loadFunction;
    }

    class OperationSave() : Operation;

    class OperationFlush(AsyncAutoResetEvent signal) : Operation
    {
        public AsyncAutoResetEvent Signal { get; } = signal;
    }
    #endregion

    private readonly ConcurrentDictionary<Type, Dictionary<object, IEntity>> _sets = [];

    private readonly List<OperationPending> _pendingQueue = [];
    private readonly ConcurrentDictionary<IEntity, bool> _pendingInserts = [];
    private readonly ConcurrentDictionary<object, IEntity> _pendingUpdates = [];
    private readonly ConcurrentDictionary<object, IEntity> _pendingDeletes = [];

    private readonly ActionBlock<Operation> _operationsBlock;

    private readonly IStorage? _storage;

    private readonly ConcurrentDictionary<Type, List<WeakReference<Query>>> _queries = [];

    private readonly SemaphoreSlim _notificationSemaphore = new(1);

    public Container(IServiceProvider serviceProvider)
    {
        _operationsBlock = new ActionBlock<Operation>(DoWork);
        _storage = serviceProvider.GetService<IStorage>();
    }

    public Action<Exception>? OnError { get; set; }

    public EntityStatus GetEntityStatus(IEntity entity)
    {
        if (_pendingInserts.TryGetValue(entity, out var _))
        {
            return EntityStatus.Added;
        }
        
        var key = entity.GetKey();

        if (key != null)
        {
            if (_pendingUpdates.TryGetValue(key, out var _))
            {
                return EntityStatus.Updated;
            }
            if (_pendingDeletes.TryGetValue(key, out var _))
            {
                return EntityStatus.Deleted;
            }

            var set = _sets.GetOrAdd(entity.GetType(), []);
            if (set.TryGetValue(key, out var _))
            {
                return EntityStatus.Attached;
            }
        }

        return EntityStatus.Detached;
    }

    private async Task DoWork(Operation operation)
    {
        try
        {
            await InternalDoWork(operation);
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

    private async Task InternalDoWork(Operation operation)
    {
        switch (operation)
        {
            case OperationAdd operationAdd:
                //if (_pendingDeletes.TryRemove(operationAdd.Entity, out _) ||
                //    _pendingUpdates.TryGetValue(operationAdd.Entity, out _))
                //{
                //    _pendingQueue.RemoveFirst(_ => _.Entity == operationAdd.Entity);
                //}

                if (_pendingInserts.TryAdd(operationAdd.Entity, true))
                {
                    _pendingQueue.Add(operationAdd);

                    NotifyChanges(operationAdd.Entity.GetType());
                }
                break;
            case OperationUpdate operationUpdate:
                {
                    //if (_pendingInserts.TryGetValue(operationUpdate.Entity, out _))
                    //{
                    //    break;
                    //}
                    var entityKey = operationUpdate.Entity.GetKey().EnsureNotNull();

                    if (_pendingDeletes.TryRemove(entityKey, out _))
                    {
                        _pendingQueue.RemoveFirst(_ => _.Entity == operationUpdate.Entity);
                        NotifyChanges(operationUpdate.Entity.GetType());
                    }

                    if (_pendingUpdates.TryAdd(entityKey, operationUpdate.Entity))
                    {
                        _pendingQueue.Add(operationUpdate);
                        NotifyChanges(operationUpdate.Entity.GetType());
                    }
                }
                break;
            case OperationRemove operationRemove:
                {
                    var entityKey = operationRemove.Entity.GetKey().EnsureNotNull();
                    if (_pendingUpdates.TryGetValue(entityKey, out _))
                    {
                        _pendingQueue.RemoveFirst(_ => _.Entity == operationRemove.Entity);
                    }

                    if (_pendingDeletes.TryAdd(entityKey, operationRemove.Entity))
                    {
                        _pendingQueue.Add(operationRemove);
                        NotifyChanges(operationRemove.Entity.GetType());
                    }
                }
                break;
            case OperationAddRange operationAddRange:
                {
                    HashSet<Type> queryTypesToNofity = [];

                    foreach (var entity in operationAddRange.Entities)
                    {
                        //if (_pendingDeletes.TryRemove(entity, out _) ||
                        //    _pendingUpdates.TryGetValue(entity, out _))
                        //{
                        //    _pendingQueue.RemoveFirst(_ => _.Entity == entity);
                        //}

                        if (_pendingInserts.TryAdd(entity, true))
                        {
                            _pendingQueue.Add(new OperationAdd(entity));

                            if (!queryTypesToNofity.Contains(entity.GetType()))
                            {
                                queryTypesToNofity.Add(entity.GetType());
                            }
                        }
                    }

                    foreach (var queryTypeToNofity in queryTypesToNofity)
                    {
                        NotifyChanges(queryTypeToNofity);
                    }
                }

                break;
            case OperationFetch operationFetch:
                if (_storage != null)
                {
                    var entities = await operationFetch.LoadFunction(_storage);
                    HashSet<Type> queryTypesToNofity = [];

                    foreach (var entity in entities)
                    {
                        var entityType = entity.GetType();
                        var set = _sets.GetOrAdd(entityType, []);
                        set.Add(entity.GetKey().EnsureNotNull(), entity);

                        if (!queryTypesToNofity.Contains(entityType))
                        {
                            queryTypesToNofity.Add(entityType);
                        }
                    }

                    foreach (var queryTypeToNofity in queryTypesToNofity)
                    {
                        NotifyChanges(queryTypeToNofity);
                    }
                }
                break;
            case OperationSave operationSave:
                {
                    if (_storage != null)
                    {
                        var listOfStorageOperation = new List<StorageOperation>();
                        foreach (var pendingOperation in _pendingQueue)
                        {
                            switch (pendingOperation)
                            {
                                case OperationAdd operationAdd:
                                    listOfStorageOperation.Add(new StorageInsert(operationAdd.Entity));
                                    break;
                                case OperationUpdate operationUpdate:
                                    listOfStorageOperation.Add(new StorageUpdate(operationUpdate.Entity));
                                    break;
                                case OperationRemove operationRemove:
                                    listOfStorageOperation.Add(new StorageDelete(operationRemove.Entity));
                                    break;
                            }
                        }

                        await _storage.Save(listOfStorageOperation);
                    }

                    HashSet<Type> queryTypesToNofity = [];
                    foreach (var pendingOperation in _pendingQueue)
                    {
                        switch (pendingOperation)
                        {
                            case OperationAdd operationAdd:
                                {
                                    var set = _sets.GetOrAdd(operationAdd.Entity.GetType(), []);
                                    set.Add(operationAdd.Entity.GetKey().EnsureNotNull(), operationAdd.Entity);
                                    if (!queryTypesToNofity.Contains(operationAdd.Entity.GetType()))
                                    {
                                        queryTypesToNofity.Add(operationAdd.Entity.GetType());
                                    }
                                }
                                break;
                            case OperationUpdate operationUpdate:
                                break;
                            case OperationRemove operationRemove:
                                {
                                    var set = _sets.GetOrAdd(operationRemove.Entity.GetType(), []);
                                    set.Remove(operationRemove.Entity.GetKey().EnsureNotNull());

                                    if (!queryTypesToNofity.Contains(operationRemove.Entity.GetType()))
                                    {
                                        queryTypesToNofity.Add(operationRemove.Entity.GetType());
                                    }
                                }
                                break;
                        }
                    }

                    _pendingQueue.Clear();
                    _pendingInserts.Clear();
                    _pendingUpdates.Clear();
                    _pendingDeletes.Clear();

                    foreach (var queryTypeToNofity in queryTypesToNofity)
                    {
                        NotifyChanges(queryTypeToNofity);
                    }
                }
                break;

            case OperationFlush operationFlush:
                operationFlush.Signal.Set();
                break;
        }
    }

    public void Add(IEntity entity)
    {
        _operationsBlock.Post(new OperationAdd(entity));
    }

    public void AddRange(IEnumerable<IEntity> entities)
    {
        _operationsBlock.Post(new OperationAddRange(entities));
    }

    public void Update(IEntity entity)
    {
        if (entity.GetKey() == null)
        {
            throw new InvalidOperationException("Updating entity with uninitialized key");
        }

        _operationsBlock.Post(new OperationUpdate(entity));
    }

    public void Delete(IEntity entity)
    {
        if (entity.GetKey() == null)
        {
            throw new InvalidOperationException("Deleting entity with uninitialized key");
        }

        _operationsBlock.Post(new OperationRemove(entity));
    }

    public void Load<T>(Expression<Func<T, bool>> predicate) where T : class, IEntity
    {
        _operationsBlock.Post(new OperationFetch(storage => storage.Load(predicate)));
    }

    public void Save()
    {
        _operationsBlock.Post(new OperationSave());
    }

    public IEnumerable<T> Set<T>() where T : class, IEntity
    {
        var typeofT = typeof(T);
        var set = _sets.GetOrAdd(typeofT, []);

        foreach (var existingEntityPair in set)
        {
            yield return (T)existingEntityPair.Key;
        }

        foreach (var existingPendingInsert in _pendingQueue.OfType<OperationAdd>())
        {
            yield return (T)existingPendingInsert.Entity;
        }
    }

    public async Task Flush()
    {
        var signalEvent = new AsyncAutoResetEvent();
        _operationsBlock.Post(new OperationFlush(signalEvent));
        await signalEvent.WaitAsync();
    }

    public IQuery<T> Query<T>(Expression<Func<T, bool>>? predicateExpression = null, Expression<Func<T, object>>? sortFuncExpression = null) where T : class, IEntity
    {
        var predicate = predicateExpression?.Compile();
        var sortFunction = sortFuncExpression?.Compile();

        var typeofT = typeof(T);
        var queries = _queries.GetOrAdd(typeofT, []);

        var query = new Query<T>(this, predicate, sortFunction);
        queries.Add(new WeakReference<Query>(query));
        
        return query;
    }

    private void NotifyChanges(Type typeOfEntity)
    {
        var queries = _queries.GetOrAdd(typeOfEntity, []);

        try
        {
            _notificationSemaphore.Wait();
            List<WeakReference<Query>>? referencesToRemove = null;
            foreach (var queryReference in queries)
            {
                if (queryReference.TryGetTarget(out var query))
                {
                    query.NotifyChanges();
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
