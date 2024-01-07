using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using static System.Collections.Specialized.BitVector32;

namespace ReactorData.Implementation;

class Container : IContainer
{
    abstract record Operation();

    abstract record OperationPending(IEntity Entity) : Operation;

    record OperationAdd(IEntity Entity) : OperationPending(Entity);

    record OperationUpdate(IEntity Entity) : OperationPending(Entity);

    record OperationRemove(IEntity Entity) : OperationPending(Entity);

    record OperationAddRange(IEnumerable<IEntity> Entities) : Operation;

    record OperationFetch(Func<IStorage, Task<IEnumerable<IEntity>>> LoadFunction) : Operation;

    record OperationSave() : Operation;

    record OperationFlush(AsyncAutoResetEvent Signal) : Operation;

    readonly ConcurrentDictionary<Type, Dictionary<IEntity, bool>> _sets = [];

    readonly List<OperationPending> _pendingQueue = [];
    readonly ConcurrentDictionary<IEntity, bool> _pendingInserts = [];
    readonly ConcurrentDictionary<IEntity, bool> _pendingUpdates = [];
    readonly ConcurrentDictionary<IEntity, bool> _pendingDeletes = [];

    readonly ActionBlock<Operation> _operationsBlock;

    private readonly IStorage? _storage;

    public Container(IServiceProvider serviceProvider)
    {
        _operationsBlock = new ActionBlock<Operation>(DoWork);
        _storage = serviceProvider.GetService<IStorage>();
    }

    public EntityStatus GetEntityStatus(IEntity entity)
    {
        if (_pendingInserts.TryGetValue(entity, out var _))
        {
            return EntityStatus.Added;
        }
        if (_pendingUpdates.TryGetValue(entity, out var _))
        {
            return EntityStatus.Updated;
        }
        if (_pendingDeletes.TryGetValue(entity, out var _))
        {
            return EntityStatus.Deleted;
        }

        var set = _sets.GetOrAdd(entity.GetType(), []);
        if (set.TryGetValue(entity, out var _))
        {
            return EntityStatus.Attached;
        }

        return EntityStatus.Detached;
    }

    private async Task DoWork(Operation operation)
    {
        switch (operation)
        {
            case OperationAdd operationAdd:
                if (_pendingDeletes.TryRemove(operationAdd.Entity, out _) ||
                    _pendingUpdates.TryGetValue(operationAdd.Entity, out _))
                {
                    _pendingQueue.RemoveFirst(_ => _.Entity == operationAdd.Entity);
                }

                if (_pendingInserts.TryAdd(operationAdd.Entity, true))
                {
                    _pendingQueue.Add(operationAdd);
                }
                break;
            case OperationUpdate operationUpdate:
                if (_pendingInserts.TryGetValue(operationUpdate.Entity, out _))
                {
                    break;
                }

                if (_pendingDeletes.TryRemove(operationUpdate.Entity, out _))
                {
                    _pendingQueue.RemoveFirst(_ => _.Entity == operationUpdate.Entity);
                }

                if (_pendingUpdates.TryAdd(operationUpdate.Entity, true))
                {
                    _pendingQueue.Add(operationUpdate);
                }

                break;
            case OperationRemove operationRemove:
                if (_pendingInserts.TryRemove(operationRemove.Entity, out _) ||
                    _pendingUpdates.TryGetValue(operationRemove.Entity, out _))
                {
                    _pendingQueue.RemoveFirst(_ => _.Entity == operationRemove.Entity);
                }

                if (_pendingDeletes.TryAdd(operationRemove.Entity, true))
                {
                    _pendingQueue.Add(operationRemove);
                }
                break;
            case OperationAddRange operationAddRange:
                foreach(var entity in operationAddRange.Entities)
                {
                    if (_pendingDeletes.TryRemove(entity, out _) ||
                        _pendingUpdates.TryGetValue(entity, out _))
                    {
                        _pendingQueue.RemoveFirst(_ => _.Entity == entity);
                    }

                    if (_pendingInserts.TryAdd(entity, true))
                    {
                        _pendingQueue.Add(new OperationAdd(entity));
                    }
                }
                break;
            case OperationFetch operationFetch:
                if (_storage != null)
                {
                    var entities = await operationFetch.LoadFunction(_storage);
                    foreach (var entity in entities)
                    {
                        var set = _sets.GetOrAdd(entity.GetType(), []);
                        set.Add(entity, true);
                    }
                }
                break;

            case OperationSave operationSave:
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

                foreach (var pendingOperation in _pendingQueue)
                {
                    switch (pendingOperation)
                    {
                        case OperationAdd operationAdd:
                            {
                                var set = _sets.GetOrAdd(operationAdd.Entity.GetType(), []);
                                set.Add(operationAdd.Entity, true);
                            }
                            break;
                        case OperationUpdate operationUpdate:
                            break;
                        case OperationRemove operationRemove:
                            {
                                var set = _sets.GetOrAdd(operationRemove.Entity.GetType(), []);
                                set.Remove(operationRemove.Entity);
                            }
                            break;
                    }
                }

                _pendingQueue.Clear();
                _pendingInserts.Clear();
                _pendingUpdates.Clear();
                _pendingDeletes.Clear();
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
        _operationsBlock.Post(new OperationUpdate(entity));
    }

    public void Delete(IEntity entity)
    {
        _operationsBlock.Post(new OperationRemove(entity));
    }

    public void Fetch<T>(Expression<Func<T, bool>> predicate) where T : class, IEntity
    {
        _operationsBlock.Post(new OperationFetch(storage => storage.Load(predicate)));
    }

    public void Save()
    {
        _operationsBlock.Post(new OperationSave());
    }

    public IEnumerable<T> Set<T>()
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
        var signalEvent = new AsyncAutoResetEvent(false);
        _operationsBlock.Post(new OperationFlush(signalEvent));
        await signalEvent.WaitAsync();
    }
}
