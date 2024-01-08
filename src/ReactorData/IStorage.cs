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

    Task<IEnumerable<IEntity>> Load<T>(Expression<Func<T, bool>> predicate) where T : class, IEntity;
}

public abstract class StorageOperation(IEntity entity)
{
    public IEntity Entity { get; } = entity;
}

public class StorageInsert(IEntity entity) : StorageOperation(entity);

public class StorageUpdate(IEntity entity) : StorageOperation(entity);

public class StorageDelete(IEntity entity) : StorageOperation(entity);


