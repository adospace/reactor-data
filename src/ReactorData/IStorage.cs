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

public abstract record StorageOperation(IEntity Entity);

public record StorageInsert(IEntity Entity) : StorageOperation(Entity);

public record StorageUpdate(IEntity Entity) : StorageOperation(Entity);

public record StorageDelete(IEntity Entity) : StorageOperation(Entity);


