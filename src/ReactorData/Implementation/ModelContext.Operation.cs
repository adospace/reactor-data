using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace ReactorData.Implementation;

partial class ModelContext
{
    abstract record Operation()
    {
        internal abstract ValueTask Do(ModelContext context);
    };

    abstract record OperationPending : Operation
    {
        
    }

    record OperationAdd(IEnumerable<IEntity> Entities) : OperationPending
    {
        internal override ValueTask Do(ModelContext context)
        {
            foreach (var entity in Entities)
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

            return ValueTask.CompletedTask;
        }
    }

    record OperationUpdate(IEntity OldEntity, IEntity NewEntity) : OperationPending
    {
        internal override ValueTask Do(ModelContext context)
        {
            var oldEntityStatus = context.GetEntityStatus(OldEntity);

            if (oldEntityStatus == EntityStatus.Detached)
            {
                context._entityStatus[NewEntity] = EntityStatus.Updated;

                context._operationQueue.Enqueue((NewEntity, EntityStatus.Updated));

                context.NotifyChanges(NewEntity.GetType(), [NewEntity]);
            }
            else if (oldEntityStatus == EntityStatus.Attached)
            {
                var entityToUpdateKey = OldEntity.GetKey().EnsureNotNull();

                var entityType = OldEntity.GetType();
                var set = context._sets.GetOrAdd(entityType, []);

                set[entityToUpdateKey] = NewEntity;

                context._entityStatus[NewEntity] = EntityStatus.Updated;

                context._operationQueue.Enqueue((NewEntity, EntityStatus.Updated));

                context.NotifyChanges(NewEntity.GetType(), [NewEntity]);
            }
            else if (oldEntityStatus == EntityStatus.Added)
            {
                context._entityStatus.Remove(OldEntity, out var _);
                context._entityStatus[NewEntity] = EntityStatus.Added;

#if DEBUG
                if (!context._operationQueue.Any(_ => Equals(_.Entity.GetKey(), NewEntity.GetKey())))
                {
                    System.Diagnostics.Debug.Assert(false);
                }
#endif
                context._operationQueue.Enqueue((NewEntity, EntityStatus.Added));

                context.NotifyChanges(NewEntity.GetType(), [NewEntity]);
            }
            else if (oldEntityStatus == EntityStatus.Deleted)
            {
                context._entityStatus.Remove(OldEntity, out var _);
                context._entityStatus[NewEntity] = EntityStatus.Updated;

#if DEBUG
                if (!context._operationQueue.Any(_ => Equals(_.Entity.GetKey(), NewEntity.GetKey())))
                {
                    System.Diagnostics.Debug.Assert(false);
                }
#endif
                context._operationQueue.Enqueue((NewEntity, EntityStatus.Updated));

                context.NotifyChanges(NewEntity.GetType(), [NewEntity]);
            }

            return ValueTask.CompletedTask;
        }
    }

    record OperationDelete(IEnumerable<IEntity> Entities) : OperationPending
    {
        internal override ValueTask Do(ModelContext context)
        {
            foreach (var entity in Entities)
            {
                var entityStatus = context.GetEntityStatus(entity);

                if (entityStatus == EntityStatus.Deleted)
                {
                    continue;
                }

                if (entityStatus == EntityStatus.Added)
                {
                    context._entityStatus.Remove(entity, out var _);

                    context.NotifyChanges(entity.GetType());
                }
                else
                {
                    context._entityStatus[entity] = EntityStatus.Deleted;

                    context.NotifyChanges(entity.GetType());

                    context._operationQueue.Enqueue((entity, EntityStatus.Deleted));
                }
            }

            return ValueTask.CompletedTask;

        }
    }

    record OperationFetch(
        Type EntityTypeToLoad,
        Func<IStorage, Task<IEnumerable<IEntity>>> LoadFunction, 
        Func<IEntity, IEntity, bool>? CompareFunc = null,
        bool ForceReload = false,
        Action<IEnumerable<IEntity>>? OnLoad = null) : Operation
    {
        public Func<IStorage, Task<IEnumerable<IEntity>>> LoadFunction { get; } = LoadFunction;
        public Func<IEntity, IEntity, bool>? CompareFunc { get; } = CompareFunc;
        public Action<IEnumerable<IEntity>>? OnLoad { get; } = OnLoad;

        internal override async ValueTask Do(ModelContext context)
        {
            var storage = context._owner?._storage ?? context._storage;
            if (storage == null)
            {
                return;
            }

            try
            {
                context.IsLoading = true;

                var entities = await LoadFunction(storage);
                HashSet<Type> queryTypesToNofity = [];
                ConcurrentDictionary<Type, HashSet<IEntity>> entitiesChanged = [];

                queryTypesToNofity.Add(EntityTypeToLoad);

                if (ForceReload)
                {
                    var set = context._sets.GetOrAdd(EntityTypeToLoad, []);
                    set.Clear();
                }

                if (ForceReload)
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
                        set.TryAdd(entityKey, entity);
                    }

                    queryTypesToNofity.Add(entityType);
                }

                foreach (var queryTypeToNofity in queryTypesToNofity)
                {
                    if (ForceReload)
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
                    if (context.Dispatcher != null)
                    {
                        context.Dispatcher.Dispatch(() => OnLoad.Invoke(entities));
                    }
                    else
                    {
                        OnLoad.Invoke(entities);
                    }
                }
            }
            finally
            {
                context.IsLoading = false;
            }
        }
    }

    record OperationDiscardChanges() : Operation
    {
        internal override ValueTask Do(ModelContext context)
        {
            HashSet<Type> queryTypesToNofity = [];
            HashSet<IEntity> changedEntities = [];

            foreach (var (Entity, _) in context._operationQueue)
            {
                changedEntities.Add(Entity);

                var entityType = Entity.GetType();
                queryTypesToNofity.Add(entityType);
            }

            context._operationQueue.Clear();
            context._entityStatus.Clear();

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                context.NotifyChanges(queryTypeToNofity, [.. changedEntities]);
            }

            return ValueTask.CompletedTask;
        }
    }

    record OperationSave() : Operation
    {
        internal override async ValueTask Do(ModelContext context)
        {
            var storage = context._owner?._storage ?? context._storage;

            try
            {
                context.IsSaving = storage != null && context._operationQueue.Count > 0;
                if (storage != null)
                {
                    var listOfStorageOperation = new List<StorageOperation>();
                    var operationsAdded = new HashSet<object>();
                    foreach (var (Entity, Status) in context._operationQueue)
                    {
                        var currentEntityStatus = context.GetEntityStatus(Entity);

                        if (currentEntityStatus != Status)
                        {
                            continue;
                        }

                        var key = Entity.GetKey();
                        if (key != null)
                        {
                            if (operationsAdded.Contains(key))
                            {
                                System.Diagnostics.Debug.WriteLine($"StorageOperation: {Status} (Key already added: {key}) ");
                                continue;
                            }

                            operationsAdded.Add(key);
                        }

                        System.Diagnostics.Debug.WriteLine($"StorageOperation: {Status}");

                        switch (Status)
                        {
                            case EntityStatus.Added:
                                listOfStorageOperation.Add(new StorageAdd([Entity]));
                                break;
                            case EntityStatus.Updated:
                                listOfStorageOperation.Add(new StorageUpdate([Entity]));
                                break;
                            case EntityStatus.Deleted:
                                listOfStorageOperation.Add(new StorageDelete([Entity]));
                                break;
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
                                //set.Add(Entity.GetKey().EnsureNotNull(), Entity);
                                set[Entity.GetKey().EnsureNotNull()] = Entity;

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
                                set.TryRemove(Entity.GetKey().EnsureNotNull(), out var _);

                                queryTypesToNofity.Add(Entity.GetType());
                            }
                            break;
                    }
                }

                context._operationQueue.Clear();
                context._entityStatus.Clear();

                foreach (var queryTypeToNofity in queryTypesToNofity)
                {
                    context.NotifyChanges(queryTypeToNofity);
                }
            }
            finally
            {
                context.IsSaving = false;
            }
        }
    }

    record OperationFlush(AsyncAutoResetEvent Signal) : Operation
    {
        public AsyncAutoResetEvent Signal { get; } = Signal;

        internal override ValueTask Do(ModelContext context)
        {
            Signal.Set();

            return ValueTask.CompletedTask;
        }
    }

    record OperationBackgroundTask(Func<IModelContext, Task> BackgroundTask) : Operation
    {
        internal override async ValueTask Do(ModelContext context)
        {
            await BackgroundTask(context);
        }
    }
}
