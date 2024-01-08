using System.Collections.Generic;
using System.Threading.Tasks;

namespace ReactorData;

public interface IContainer
{
    void Add(IEntity entity);

    void AddRange(IEnumerable<IEntity> entity);

    void Update(IEntity entity);

    void Delete(IEntity entity);

    void Save();

    IEnumerable<T> Set<T>();

    Task Flush();

    EntityStatus GetEntityStatus(IEntity entity);
}

public enum EntityStatus
{
    Detached,
    Attached,
    Added, 
    Updated, 
    Deleted
}
