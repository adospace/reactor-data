using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ReactorData;

/// <summary>
/// Reactive container of entities. An entity is an object of a type decorated with the <see cref="ModelAttribute"/> attribute.
/// When a storage is configured, entities are automatically persisted in background.
/// </summary>
public interface IModelContext
{
    /// <summary>
    /// Adds one or more entities to the context
    /// </summary>
    /// <param name="entities">Entities to add</param>
    /// <remarks>Entities are marked with <see cref="EntityStatus.Added"/>. To persist any change you have to call <see cref="Save"/></remarks>
    void Add(params IEntity[] entities);

    /// <summary>
    /// Replaces one entity in to the context
    /// </summary>
    /// <param name="oldEntity">Old entity to replace</param>
    /// <param name="newEntity">New entity to put in the container</param>    
    /// <remarks>Old entity is marked as <see cref="EntityStatus.Detached"/> while new entity is put in the container with status <see cref="EntityStatus.Updated"/>. To persist any change you have to call <see cref="Save"/></remarks>
    void Replace(IEntity oldEntity, IEntity newEntity);

    /// <summary>
    /// Delete one or more entities
    /// </summary>
    /// <param name="entities">Entities to delete</param>
    /// <remarks>Entities are marked with <see cref="EntityStatus.Deleted"/> only when not already in the <see cref="EntityStatus.Added"/> status. To persist any change you have to call <see cref="Save"/></remarks>
    void Delete(params IEntity[] entities);

    /// <summary>
    /// Save any pending changes in a background thread. After the save is applied pending entities will have the <see cref="EntityStatus.Attached"/> status
    /// </summary>
    void Save();

    /// <summary>
    /// Find an entity by Key (usually is the Id property of the entity)
    /// </summary>
    /// <typeparam name="T">Type of the entity to return</typeparam>
    /// <param name="key">Key to search</param>
    /// <returns>The entity found or null</returns>
    T? FindByKey<T>(object key) where T : class, IEntity;

    /// <summary>
    /// Discard any pending change and revert the context to the initial state
    /// </summary>
    void DiscardChanges();

    /// <summary>
    /// Wait for any pending operation to the context to complete
    /// </summary>
    /// <returns>Task to wait</returns>
    Task Flush();

    /// <summary>
    /// Get the <see cref="EntityStatus"/> af an entity
    /// </summary>
    /// <param name="entity">Entity to get status</param>
    /// <returns>The <see cref="EntityStatus"/> of the entity. If the entity is not yet known by the context the <see cref="EntityStatus.Detached"/> is returned</returns>
    EntityStatus GetEntityStatus(IEntity entity);

    /// <summary>
    /// Callback called when an internal error is raised by the context. The call could be not in the UI thread.
    /// </summary>
    Action<Exception>? OnError { get; set; }

    /// <summary>
    /// Create a query (<see cref="IQuery{T}"/>) that is update everytime the context is modified.
    /// </summary>
    /// <typeparam name="T">Type to listent</typeparam>
    /// <param name="predicate">Optional query predicate to apply to entities modified</param>
    /// <returns>An <see cref="IQuery{T}"/> object that implements <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>. The object is notified with any change to entities that pass the <see cref="Predicate{T}"/> filter</returns>
    IQuery<T> Query<T>(Expression<Func<IQueryable<T>, IQueryable<T>>>? predicate = null) where T : class, IEntity;

    /// <summary>
    /// Load entities from the datastore. You can specify which kind of entity to load and a <see cref="Predicate{T}"/> function used to filter entities to load.
    /// </summary>
    /// <typeparam name="T">Types of entities to load</typeparam>
    /// <param name="predicate">Optional function used to filter out entities loaded from the storage</param>
    /// <param name="compareFunc">Optional function used to compare entities. Returns true when the entities are equal.</param>
    /// <param name="forceReload">True if all previous loaded entities of type T must be discarded before loading</param>
    /// <param name="onLoad">Optional callback function that is called (using the configured dispatcher) when the load completes</param>
    void Load<T>(
        Expression<Func<IQueryable<T>, IQueryable<T>>>? predicate = null, 
        Func<T, T, bool>? compareFunc = null,
        bool forceReload = false,
        Action<IEnumerable<T>>? onLoad = null) where T : class, IEntity;

    /// <summary>
    /// Creates a scoped (child) context that is funcionally separated by this context but that uses the same storage
    /// </summary>
    /// <returns>The scoped context</returns>
    IModelContext CreateScope();

    /// <summary>
    /// Run background task for the context
    /// </summary>
    /// <param name="task">Task to execute in background</param>
    /// <remarks>During the execution of the task, all the pending operations are suspended</remarks>
    void RunBackgroundTask(Func<IModelContext, Task> task);
}

/// <summary>
/// Identify the status of an entity
/// </summary>
public enum EntityStatus
{
    Detached,
    Attached,
    Added, 
    Updated, 
    Deleted
}

public static class ModelContextExtensions
{
    /// <summary>
    /// Update one or more entities "in-place"
    /// </summary>
    /// <param name="modelContext">Context the contains the entity to update</param>
    /// <param name="entities">Entities to update</param>
    /// <remarks>Differently from the <see cref="IModelContext.Replace(IEntity, IEntity)"/> keeps the same entities already added to the container. Be aware that if you attached a query on a UI list like the .NET MAUI CollectionView you have to use the Replace function instead to see the items updated.</remarks>
    public static void Update(this IModelContext modelContext, params IEntity[] entities)
    {
        foreach (var entity in entities)
        {
            modelContext.Replace(entity, entity);
        }
    }
}
