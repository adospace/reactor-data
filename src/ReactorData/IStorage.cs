using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

public interface IStorage
{
    Task Save(IEnumerable<StorageOperation> operations);

    Task<IEnumerable<IEntity>> Load<T>(Func<IQueryable<T>, IQueryable<T>>? queryFunction = null) where T : class, IEntity;
}

public abstract class StorageOperation(IEnumerable<IEntity> entities)
{
    public IEnumerable<IEntity> Entities { get; } = entities;
}

public class StorageAdd(IEnumerable<IEntity> entities) : StorageOperation(entities);

public class StorageUpdate(IEnumerable<IEntity> entities) : StorageOperation(entities);

public class StorageDelete(IEnumerable<IEntity> entities) : StorageOperation(entities);


