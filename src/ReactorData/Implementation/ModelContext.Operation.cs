using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ReactorData.Implementation;

partial class ModelContext
{
    abstract record Operation()
    {
        internal abstract ValueTask Do(ModelContext context);
    };

    abstract record OperationPending(IEnumerable<IEntity> entities) : Operation
    {
        public IEnumerable<IEntity> Entities { get; } = entities;
    }

    record OperationAdd(IEnumerable<IEntity> entities) : OperationPending(entities)
    {
        internal override ValueTask Do(ModelContext context)
        {
            foreach (var entity in entities)
            {
                var entityStatus = context.GetEntityStatus(entity);

                if (entityStatus != EntityStatus.Detached)
                {
                    continue;
                }

                context._entityStatus[entity] = EntityStatus.Added;

                context.NotifyChanges(entity.GetType());

                context._operationQueue.Enqueue((entity, EntityStatus.Added));
            }

            context._pendingOperations.Enqueue(this);
            return ValueTask.CompletedTask;
        }
    }

    record OperationUpdate(IEnumerable<IEntity> entities) : OperationPending(entities)
    {
        internal override ValueTask Do(ModelContext context)
        {
            foreach (var entity in entities)
            {
                var entityStatus = context.GetEntityStatus(entity);

                if (entityStatus == EntityStatus.Detached)
                {
                    continue;
                }

                if (entityStatus != EntityStatus.Added)
                {
                    context._entityStatus[entity] = EntityStatus.Updated;
                }

                context.NotifyChanges(entity.GetType(), [entity]);

                if (entityStatus != EntityStatus.Updated)
                {
                    context._operationQueue.Enqueue((entity, EntityStatus.Updated));
                }
            }

            context._pendingOperations.Enqueue(this);
            return ValueTask.CompletedTask;
        }
    }

    record OperationDelete(IEnumerable<IEntity> entities) : OperationPending(entities)
    {
        internal override ValueTask Do(ModelContext context)
        {
            foreach (var entity in entities)
            {
                var entityStatus = context.GetEntityStatus(entity);

                if (entityStatus == EntityStatus.Deleted)
                {
                    continue;
                }

                if (entityStatus == EntityStatus.Added)
                {
                    context._entityStatus.Remove(entity, out var _);
                }
                else
                {
                    context._entityStatus[entity] = EntityStatus.Deleted;
                }

                context.NotifyChanges(entity.GetType());

                context._operationQueue.Enqueue((entity, EntityStatus.Deleted));
            }

            context._pendingOperations.Enqueue(this);
            return ValueTask.CompletedTask;

        }
    }

    record OperationFetch(
        Func<IStorage, Task<IEnumerable<IEntity>>> loadFunction, 
        Func<IEntity, IEntity, bool>? compareFunc = null,
        bool forceReload = false,
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

            if (forceReload)
            {
                foreach (var entity in entities)
                {
                    var entityType = entity.GetType();
                    var set = context._sets.GetOrAdd(entityType, []);
                    
                    set.Clear();
                }
            }

            foreach (var entity in entities)
            {
                var entityType = entity.GetType();
                var set = context._sets.GetOrAdd(entityType, []);

                var entityKey = entity.GetKey().EnsureNotNull();

                if (set.TryGetValue(entityKey, out var localEntity))
                {
                    if (CompareFunc?.Invoke(entity, localEntity) == false)
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
                if (forceReload)
                {
                    context.NotifyChanges(queryTypeToNofity, forceReload: true);
                }
                else
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

    record OperationDiscardChanges() : Operation
    {
        internal override ValueTask Do(ModelContext context)
        {
            HashSet<Type> queryTypesToNofity = [];
            HashSet<IEntity> changedEntities = [];
            ConcurrentDictionary<Type, HashSet<IEntity>> entitiesChanged = [];

            foreach (var (Entity, Status) in context._operationQueue)
            {
                var currentEntityStatus = context.GetEntityStatus(Entity);

                //if (currentEntityStatus != Status)
                //{
                //    continue;
                //}
                changedEntities.Add(Entity);

                var entityType = Entity.GetType();
                queryTypesToNofity.Add(entityType);
            }

            context._operationQueue.Clear();
            context._entityStatus.Clear();
            context._pendingOperations.Clear();

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                context.NotifyChanges(queryTypeToNofity, changedEntities.ToArray());
            }

            return ValueTask.CompletedTask;
        }
    }

    record OperationSave() : Operation
    {
        internal override async ValueTask Do(ModelContext context)
        {
            var storage = context._owner?._storage ?? context._storage;
            if (storage != null)
            {
                var listOfStorageOperation = new List<StorageOperation>();
                foreach (var (Entity, Status) in context._operationQueue)
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

                await storage.Save(listOfStorageOperation);
            }

            HashSet<Type> queryTypesToNofity = [];
            foreach (var (Entity, Status) in context._operationQueue)
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
                    case EntityStatus.Updated:
                        {
                            var entityType = Entity.GetType();
                            var set = context._sets.GetOrAdd(entityType, []);
                            set[Entity.GetKey().EnsureNotNull()] = Entity;

                            queryTypesToNofity.Add(entityType);
                        }
                        break;
                    case EntityStatus.Deleted:
                        {
                            var set = context._sets.GetOrAdd(Entity.GetType(), []);
                            set.Remove(Entity.GetKey().EnsureNotNull());

                            queryTypesToNofity.Add(Entity.GetType());
                        }
                        break;
                }
            }

            context._operationQueue.Clear();
            context._entityStatus.Clear();
            context._pendingOperations.Clear();

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                context.NotifyChanges(queryTypeToNofity);
            }
        }
    }

    record OperationFlush(AsyncAutoResetEvent signal) : Operation
    {
        public AsyncAutoResetEvent Signal { get; } = signal;

        internal override ValueTask Do(ModelContext context)
        {
            Signal.Set();

            return ValueTask.CompletedTask;
        }
    }
}
