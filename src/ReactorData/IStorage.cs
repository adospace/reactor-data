using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

/// <summary>
/// Represents a storage in ReactorData. <see cref="IModelContext"/> calls methods of this interface when it needs to persists entities.
/// </summary>
public interface IStorage
{
    /// <summary>
    /// Called by the <see cref="IModelContext"/> when it needs to persists entities.
    /// </summary>
    /// <param name="operations">List of CRUD operations to execute</param>
    /// <returns>Task that <see cref="IModelContext"/> waits</returns>
    Task Save(IEnumerable<StorageOperation> operations);

    /// <summary>
    /// Called by <see cref="IModelContext"/> when it needs to load entities of a specific type.
    /// </summary>
    /// <typeparam name="T">Type of entities to load</typeparam>
    /// <param name="queryFunction">Query function to use when loading. For example, storages like EF core uses this query to filter records.</param>
    /// <returns>Task that <see cref="IModelContext"/> waits</returns>
    Task<IEnumerable<IEntity>> Load<T>(Func<IQueryable<T>, IQueryable<T>>? queryFunction = null) where T : class, IEntity;
}

/// <summary>
/// Generic CRUD operation
/// </summary>
/// <param name="entities">List of entities linked to the operation</param>
public abstract class StorageOperation(IEnumerable<IEntity> entities)
{
    public IEnumerable<IEntity> Entities { get; } = entities;
}

/// <summary>
/// Add operation
/// </summary>
/// <param name="entities">List of entities to add to the storage</param>
public class StorageAdd(IEnumerable<IEntity> entities) : StorageOperation(entities);

/// <summary>
/// Update operation
/// </summary>
/// <param name="entities">List of entities to update in the storage</param>
public class StorageUpdate(IEnumerable<IEntity> entities) : StorageOperation(entities);

/// <summary>
/// Delete operation
/// </summary>
/// <param name="entities">List of entities to delete in the storage</param>
public class StorageDelete(IEnumerable<IEntity> entities) : StorageOperation(entities);


