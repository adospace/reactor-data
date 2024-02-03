using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ReactorData.Implementation;

partial class ModelContext
{
    abstract class Operation()
    {
        internal abstract ValueTask Do(ModelContext context);
    };

    //abstract class OperationPending() : Operation;

    //abstract class OperationSingle(IEntity entity) : OperationPending
    //{
    //    public IEntity Entity { get; } = entity;
    //}

    //class OperationAdd(IEntity entity) : OperationSingle(entity)
    //{
    //    internal override ValueTask Do(ModelContext context)
    //    {
    //        if (context._pendingInserts.TryAdd(Entity, true))
    //        {
    //            context._pendingQueue.Add(this);

    //            context.NotifyChanges(Entity.GetType());
    //        }

    //        return ValueTask.CompletedTask;
    //    }
    //}

    //class OperationUpdate(IEntity entity) : OperationSingle(entity)
    //{
    //    internal override ValueTask Do(ModelContext context)
    //    {
    //        var entityKey = (Entity.GetType(), Entity.GetKey().EnsureNotNull());

    //        if (context._pendingDeletes.TryRemove(entityKey, out _))
    //        {
    //            context._pendingQueue.RemoveFirst(_ => _ is OperationDelete operationDelete && operationDelete.Entity == Entity);
    //            context.NotifyChanges(Entity.GetType());
    //        }

    //        if (context._pendingUpdates.TryAdd(entityKey, Entity))
    //        {
    //            context._pendingQueue.Add(this);
    //            context.NotifyChanges(Entity.GetType(), Entity);
    //        }

    //        return ValueTask.CompletedTask;
    //    }
    //}

    //class OperationDelete(IEntity entity) : OperationSingle(entity)
    //{
    //    internal override ValueTask Do(ModelContext context)
    //    {
    //        var entityKey = (Entity.GetType(), Entity.GetKey().EnsureNotNull());
    //        if (context._pendingUpdates.TryGetValue(entityKey, out _))
    //        {
    //            context._pendingQueue.RemoveFirst(_ => _ is OperationUpdate operationUpdate && operationUpdate.Entity == Entity);
    //        }

    //        if (context._pendingDeletes.TryAdd(entityKey, Entity))
    //        {
    //            context._pendingQueue.Add(this);
    //            context.NotifyChanges(Entity.GetType());
    //        }

    //        return ValueTask.CompletedTask;
    //    }
    //}

    //class OperationAddRange(IEnumerable<IEntity> entities) : OperationPending
    //{
    //    public IEnumerable<IEntity> Entities { get; } = entities;

    //    internal override ValueTask Do(ModelContext context)
    //    {
    //        HashSet<Type> queryTypesToNofity = [];
    //        List<IEntity> entitiesToAdd = [];
    //        foreach (var entity in Entities)
    //        {
    //            if (context._pendingInserts.TryAdd(entity, true))
    //            {
    //                entitiesToAdd.Add(entity);

    //                queryTypesToNofity.Add(entity.GetType());
    //            }
    //        }

    //        context._pendingQueue.Add(new OperationAddRange(entitiesToAdd));

    //        foreach (var queryTypeToNofity in queryTypesToNofity)
    //        {
    //            context.NotifyChanges(queryTypeToNofity);
    //        }

    //        return ValueTask.CompletedTask;
    //    }
    //}

    //class OperationUpdateRange(IEnumerable<IEntity> entities) : OperationPending
    //{
    //    public IEnumerable<IEntity> Entities { get; } = entities;

    //    internal override ValueTask Do(ModelContext context)
    //    {
    //        HashSet<Type> queryTypesToNofity = [];
    //        List<IEntity> entitiesToUpdate = [];

    //        foreach (var entity in Entities)
    //        {
    //            var entityKey = (entity.GetType(), entity.GetKey().EnsureNotNull());

    //            if (context._pendingDeletes.TryRemove(entityKey, out _))
    //            {
    //                context._pendingQueue.RemoveFirst(_ => _ is OperationDelete operationDelete && operationDelete.Entity == entity);

    //                queryTypesToNofity.Add(entity.GetType());
    //            }

    //            if (context._pendingUpdates.TryAdd(entityKey, entity))
    //            {
    //                entitiesToUpdate.Add(entity);

    //                queryTypesToNofity.Add(entity.GetType());
    //            }
    //        }

    //        context._pendingQueue.Add(new OperationUpdateRange(entitiesToUpdate));

    //        foreach (var queryTypeToNofity in queryTypesToNofity)
    //        {
    //            context.NotifyChanges(queryTypeToNofity, entitiesToUpdate.Where(_ => _.GetType() == queryTypeToNofity).ToArray());
    //        }

    //        return ValueTask.CompletedTask;
    //    }
    //}

    //class OperationDeleteRange(IEnumerable<IEntity> entities) : OperationPending
    //{
    //    public IEnumerable<IEntity> Entities { get; } = entities;

    //    internal override ValueTask Do(ModelContext context)
    //    {
    //        HashSet<Type> queryTypesToNofity = [];
    //        List<IEntity> entitiesToDelete = [];

    //        foreach (var entity in Entities)
    //        {
    //            var entityKey = (entity.GetType(), entity.GetKey().EnsureNotNull());

    //            if (context._pendingUpdates.TryGetValue(entityKey.EnsureNotNull(), out _))
    //            {
    //                context._pendingQueue.RemoveFirst(_ => _ is OperationUpdate operationUpdate && operationUpdate.Entity == entity);
    //            }

    //            if (context._pendingDeletes.TryAdd(entityKey, entity))
    //            {
    //                entitiesToDelete.Add(entity);

    //                if (!queryTypesToNofity.Contains(entity.GetType()))
    //                {
    //                    queryTypesToNofity.Add(entity.GetType());
    //                }
    //            }
    //        }

    //        context._pendingQueue.Add(new OperationDeleteRange(entitiesToDelete));

    //        foreach (var queryTypeToNofity in queryTypesToNofity)
    //        {
    //            context.NotifyChanges(queryTypeToNofity);
    //        }

    //        return ValueTask.CompletedTask;
    //    }
    //}

    class OperationFetch(
        Func<IStorage, Task<IEnumerable<IEntity>>> loadFunction, 
        Func<IEntity, IEntity, bool>? compareFunc = null,
        Action<IEnumerable<IEntity>>? onLoad = null) : Operation
    {
        public Func<IStorage, Task<IEnumerable<IEntity>>> LoadFunction { get; } = loadFunction;
        public Func<IEntity, IEntity, bool>? CompareFunc { get; } = compareFunc;
        public Action<IEnumerable<IEntity>>? OnLoad { get; } = onLoad;

        internal override async ValueTask Do(ModelContext context)
        {
            var storage = context._owner?._storage ?? context._storage;
            if (storage == null)
            {
                return;
            }

            var entities = await LoadFunction(storage);
            HashSet<Type> queryTypesToNofity = [];
            ConcurrentDictionary<Type, HashSet<IEntity>> entitiesChanged = [];

            foreach (var entity in entities)
            {
                var entityType = entity.GetType();
                var set = context._sets.GetOrAdd(entityType, []);

                var entityKey = entity.GetKey().EnsureNotNull();

                if (set.TryGetValue(entityKey, out var localEntity))
                {
                    if (CompareFunc?.Invoke(entity, localEntity) == true)
                    {
                        var entityChangesInSet = entitiesChanged.GetOrAdd(entityType, []);
                        entityChangesInSet.Add(entity);
                    }

                    set[entityKey] = entity;
                }
                else
                {
                    set.Add(entityKey, entity);
                }

                queryTypesToNofity.Add(entityType);
            }

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                if (entitiesChanged.TryGetValue(queryTypeToNofity, out var entityChangesInSet))
                {
                    context.NotifyChanges(queryTypeToNofity, [.. entityChangesInSet]);
                }
                else
                {
                    context.NotifyChanges(queryTypeToNofity);
                }
            }

            if (OnLoad != null)
            {
                if (context.Options.Dispatcher != null)
                {
                    context.Options.Dispatcher.Invoke(() => OnLoad.Invoke(entities));
                }
                else
                {
                    OnLoad.Invoke(entities);
                }
            }
        }
    }

    class OperationSave() : Operation
    {
        internal override async ValueTask Do(ModelContext context)
        {
            if (context._owner != null)
            {
                System.Diagnostics.Debug.Assert(context._storage == null);

                foreach (var (Entity, Status) in context._pendingQueue)
                {
                    //context._owner._pendingQueue.Enqueue(pendingOperation);
                    switch (Status)
                    {
                        case EntityStatus.Added:
                            context._owner.Add(Entity);
                            break;
                        case EntityStatus.Updated:
                            context._owner.Update(Entity);
                            break;
                        case EntityStatus.Deleted:
                            context._owner.Delete(Entity);
                            break;
                    }
                }

                context._owner.Save();

                await context._owner.Flush();
            }
            else
            {
                if (context._storage != null)
                {
                    var listOfStorageOperation = new List<StorageOperation>();
                    foreach (var (Entity, Status) in context._pendingQueue)
                    {
                        var currentEntityStatus = context.GetEntityStatus(Entity);

                        if (currentEntityStatus != Status)
                        {
                            continue;
                        }

                        System.Diagnostics.Debug.WriteLine($"StorageOperation: {Status}");

                        switch (Status)
                        {
                            case EntityStatus.Added:
                                listOfStorageOperation.Add(new StorageAdd(new[] { Entity }));
                                break;
                            case EntityStatus.Updated:
                                listOfStorageOperation.Add(new StorageUpdate(new[] { Entity }));
                                break;
                            case EntityStatus.Deleted:
                                listOfStorageOperation.Add(new StorageDelete(new[] { Entity }));
                                break;
                            //case OperationAddRange operationAddRange:
                            //    listOfStorageOperation.Add(new StorageAdd(operationAddRange.Entities));
                            //    break;
                            //case OperationUpdateRange operationUpdateRange:
                            //    listOfStorageOperation.Add(new StorageUpdate(operationUpdateRange.Entities));
                            //    break;
                            //case OperationDeleteRange operationDeleteRange:
                            //    listOfStorageOperation.Add(new StorageDelete(operationDeleteRange.Entities));
                            //    break;
                        }
                    }

                    await context._storage.Save(listOfStorageOperation);
                }
            }

            HashSet<Type> queryTypesToNofity = [];
            foreach (var (Entity, Status) in context._pendingQueue)
            {
                var currentEntityStatus = context.GetEntityStatus(Entity);

                if (currentEntityStatus != Status)
                {
                    continue;
                }

                switch (Status)
                {
                    case EntityStatus.Added:
                        {
                            var entityType = Entity.GetType();
                            var set = context._sets.GetOrAdd(entityType, []);
                            set.Add(Entity.GetKey().EnsureNotNull(), Entity);

                            queryTypesToNofity.Add(entityType);
                        }
                        break;
                    //case EntityStatus.Deleted:
                    //    {
                    //        foreach (var entity in operationAddRage.Entities)
                    //        {
                    //            var entityType = entity.GetType();
                    //            var set = context._sets.GetOrAdd(entityType, []);
                    //            set.Add(entity.GetKey().EnsureNotNull(), entity);
                    //            queryTypesToNofity.Add(entityType);
                    //        }
                    //    }
                    //    break;
                    case EntityStatus.Updated:
                        {
                            var entityType = Entity.GetType();
                            var set = context._sets.GetOrAdd(entityType, []);
                            set[Entity.GetKey().EnsureNotNull()] = Entity;

                            queryTypesToNofity.Add(entityType);
                        }
                        break;
                    //case OperationUpdateRange operationUpdateRange:
                    //    {
                    //        foreach (var entity in operationUpdateRange.Entities)
                    //        {
                    //            var entityType = entity.GetType();
                    //            var set = context._sets.GetOrAdd(entityType, []);
                    //            set[entity.GetKey().EnsureNotNull()] = entity;
                    //            queryTypesToNofity.Add(entityType);
                    //        }
                    //    }
                    //    break;
                    case EntityStatus.Deleted:
                        {
                            var set = context._sets.GetOrAdd(Entity.GetType(), []);
                            set.Remove(Entity.GetKey().EnsureNotNull());

                            queryTypesToNofity.Add(Entity.GetType());
                        }
                        break;
                    //case OperationDeleteRange operationRemoveRange:
                    //    {
                    //        foreach (var entity in operationRemoveRange.Entities)
                    //        {
                    //            var entityType = entity.GetType();
                    //            var set = context._sets.GetOrAdd(entityType, []);
                    //            set.Remove(entity.GetKey().EnsureNotNull());

                    //            queryTypesToNofity.Add(entityType);
                    //        }
                    //    }
                    //    break;
                }
            }

            context._pendingQueue.Clear();
            context._entityStatus.Clear();
            //context._pendingInserts.Clear();
            //context._pendingUpdates.Clear();
            //context._pendingDeletes.Clear();

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                context.NotifyChanges(queryTypeToNofity);
            }
        }
    }

    class OperationFlush(AsyncAutoResetEvent signal) : Operation
    {
        public AsyncAutoResetEvent Signal { get; } = signal;

        internal override ValueTask Do(ModelContext context)
        {
            Signal.Set();

            return ValueTask.CompletedTask;
        }
    }



}
