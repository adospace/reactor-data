using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace ReactorData.Implementation;

partial class ModelContext
{
    abstract class Operation()
    {
        internal abstract ValueTask Do(ModelContext context);
    };

    abstract class OperationPending() : Operation;

    abstract class OperationSingle(IEntity entity) : OperationPending
    {
        public IEntity Entity { get; } = entity;
    }

    class OperationAdd(IEntity entity) : OperationSingle(entity)
    {
        internal override ValueTask Do(ModelContext context)
        {
            if (context._pendingInserts.TryAdd(Entity, true))
            {
                context._pendingQueue.Add(this);

                context.NotifyChanges(Entity.GetType());
            }

            return ValueTask.CompletedTask;
        }
    }

    class OperationUpdate(IEntity entity) : OperationSingle(entity)
    {
        internal override ValueTask Do(ModelContext context)
        {
            var entityKey = Entity.GetKey().EnsureNotNull();

            if (context._pendingDeletes.TryRemove(entityKey, out _))
            {
                context._pendingQueue.RemoveFirst(_ => _ is OperationDelete operationDelete && operationDelete.Entity == Entity);
                context.NotifyChanges(Entity.GetType());
            }

            if (context._pendingUpdates.TryAdd(entityKey, Entity))
            {
                context._pendingQueue.Add(this);
                context.NotifyChanges(Entity.GetType(), Entity);
            }

            return ValueTask.CompletedTask;
        }
    }

    class OperationDelete(IEntity entity) : OperationSingle(entity)
    {
        internal override ValueTask Do(ModelContext context)
        {
            var entityKey = Entity.GetKey().EnsureNotNull();
            if (context._pendingUpdates.TryGetValue(entityKey, out _))
            {
                context._pendingQueue.RemoveFirst(_ => _ is OperationUpdate operationUpdate && operationUpdate.Entity == Entity);
            }

            if (context._pendingDeletes.TryAdd(entityKey, Entity))
            {
                context._pendingQueue.Add(this);
                context.NotifyChanges(Entity.GetType());
            }

            return ValueTask.CompletedTask;
        }
    }

    class OperationAddRange(IEnumerable<IEntity> entities) : OperationPending
    {
        public IEnumerable<IEntity> Entities { get; } = entities;

        internal override ValueTask Do(ModelContext context)
        {
            HashSet<Type> queryTypesToNofity = [];
            List<IEntity> entitiesToAdd = [];
            foreach (var entity in Entities)
            {
                if (context._pendingInserts.TryAdd(entity, true))
                {
                    entitiesToAdd.Add(entity);

                    queryTypesToNofity.Add(entity.GetType());
                }
            }

            context._pendingQueue.Add(new OperationAddRange(entitiesToAdd));

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                context.NotifyChanges(queryTypeToNofity);
            }

            return ValueTask.CompletedTask;
        }
    }

    class OperationUpdateRange(IEnumerable<IEntity> entities) : OperationPending
    {
        public IEnumerable<IEntity> Entities { get; } = entities;

        internal override ValueTask Do(ModelContext context)
        {
            HashSet<Type> queryTypesToNofity = [];
            List<IEntity> entitiesToUpdate = [];

            foreach (var entity in Entities)
            {
                var entityKey = entity.GetKey().EnsureNotNull();

                if (context._pendingDeletes.TryRemove(entityKey, out _))
                {
                    context._pendingQueue.RemoveFirst(_ => _ is OperationDelete operationDelete && operationDelete.Entity == entity);

                    queryTypesToNofity.Add(entity.GetType());
                }

                if (context._pendingUpdates.TryAdd(entityKey, entity))
                {
                    entitiesToUpdate.Add(entity);

                    queryTypesToNofity.Add(entity.GetType());
                }
            }

            context._pendingQueue.Add(new OperationUpdateRange(entitiesToUpdate));

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                context.NotifyChanges(queryTypeToNofity, entitiesToUpdate.Where(_ => _.GetType() == queryTypeToNofity).ToArray());
            }

            return ValueTask.CompletedTask;
        }
    }

    class OperationDeleteRange(IEnumerable<IEntity> entities) : OperationPending
    {
        public IEnumerable<IEntity> Entities { get; } = entities;

        internal override ValueTask Do(ModelContext context)
        {
            HashSet<Type> queryTypesToNofity = [];
            List<IEntity> entitiesToDelete = [];

            foreach (var entity in Entities)
            {
                var entityKey = entity.GetKey().EnsureNotNull();

                if (context._pendingUpdates.TryGetValue(entityKey.EnsureNotNull(), out _))
                {
                    context._pendingQueue.RemoveFirst(_ => _ is OperationUpdate operationUpdate && operationUpdate.Entity == entity);
                }

                if (context._pendingDeletes.TryAdd(entityKey, entity))
                {
                    entitiesToDelete.Add(entity);

                    if (!queryTypesToNofity.Contains(entity.GetType()))
                    {
                        queryTypesToNofity.Add(entity.GetType());
                    }
                }
            }

            context._pendingQueue.Add(new OperationDeleteRange(entitiesToDelete));

            foreach (var queryTypeToNofity in queryTypesToNofity)
            {
                context.NotifyChanges(queryTypeToNofity);
            }

            return ValueTask.CompletedTask;
        }
    }

    class OperationFetch(Func<IStorage, Task<IEnumerable<IEntity>>> loadFunction, Func<IEntity, IEntity, bool>? compareFunc = null) : Operation
    {
        public Func<IStorage, Task<IEnumerable<IEntity>>> LoadFunction { get; } = loadFunction;
        public Func<IEntity, IEntity, bool>? CompareFunc { get; } = compareFunc;

        internal override async ValueTask Do(ModelContext context)
        {
            if (context._storage != null)
            {
                var entities = await LoadFunction(context._storage);
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
            }
        }
    }

    class OperationSave() : Operation
    {
        internal override async ValueTask Do(ModelContext context)
        {
            if (context._storage != null)
            {
                var listOfStorageOperation = new List<StorageOperation>();
                foreach (var pendingOperation in context._pendingQueue)
                {
                    switch (pendingOperation)
                    {
                        case OperationAdd operationAdd:
                            listOfStorageOperation.Add(new StorageAdd(new[] { operationAdd.Entity }));
                            break;
                        case OperationUpdate operationUpdate:
                            listOfStorageOperation.Add(new StorageUpdate(new[] { operationUpdate.Entity }));
                            break;
                        case OperationDelete operationRemove:
                            listOfStorageOperation.Add(new StorageDelete(new[] { operationRemove.Entity }));
                            break;
                        case OperationAddRange operationAddRange:
                            listOfStorageOperation.Add(new StorageAdd(operationAddRange.Entities));
                            break;
                        case OperationUpdateRange operationUpdateRange:
                            listOfStorageOperation.Add(new StorageUpdate(operationUpdateRange.Entities));
                            break;
                        case OperationDeleteRange operationDeleteRange:
                            listOfStorageOperation.Add(new StorageDelete(operationDeleteRange.Entities));
                            break;
                    }
                }

                await context._storage.Save(listOfStorageOperation);
            }

            HashSet<Type> queryTypesToNofity = [];
            foreach (var pendingOperation in context._pendingQueue)
            {
                switch (pendingOperation)
                {
                    case OperationAdd operationAdd:
                        {
                            var entityType = operationAdd.Entity.GetType();
                            var set = context._sets.GetOrAdd(entityType, []);
                            set.Add(operationAdd.Entity.GetKey().EnsureNotNull(), operationAdd.Entity);
                            if (!queryTypesToNofity.Contains(entityType))
                            {
                                queryTypesToNofity.Add(entityType);
                            }
                        }
                        break;
                    case OperationAddRange operationAddRage:
                        {
                            foreach (var entity in operationAddRage.Entities)
                            {
                                var entityType = entity.GetType();
                                var set = context._sets.GetOrAdd(entityType, []);
                                set.Add(entity.GetKey().EnsureNotNull(), entity);
                                if (!queryTypesToNofity.Contains(entityType))
                                {
                                    queryTypesToNofity.Add(entityType);
                                }
                            }
                        }
                        break;
                    case OperationUpdate operationUpdate:
                        break;
                    case OperationDelete operationRemove:
                        {
                            var set = context._sets.GetOrAdd(operationRemove.Entity.GetType(), []);
                            set.Remove(operationRemove.Entity.GetKey().EnsureNotNull());

                            if (!queryTypesToNofity.Contains(operationRemove.Entity.GetType()))
                            {
                                queryTypesToNofity.Add(operationRemove.Entity.GetType());
                            }
                        }
                        break;
                    case OperationDeleteRange operationRemoveRange:
                        {
                            foreach (var entity in operationRemoveRange.Entities)
                            {
                                var entityType = entity.GetType();
                                var set = context._sets.GetOrAdd(entityType, []);
                                set.Remove(entity.GetKey().EnsureNotNull());

                                if (!queryTypesToNofity.Contains(entityType))
                                {
                                    queryTypesToNofity.Add(entityType);
                                }
                            }
                        }
                        break;
                }
            }

            context._pendingQueue.Clear();
            context._pendingInserts.Clear();
            context._pendingUpdates.Clear();
            context._pendingDeletes.Clear();

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
