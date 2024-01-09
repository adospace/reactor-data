using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ReactorData;

public interface IContainer
{
    void Add(IEntity entity);

    void AddRange(IEnumerable<IEntity> entity);

    void Update(IEntity entity);

    void Delete(IEntity entity);

    void Save();

    IEnumerable<T> Set<T>() where T : class, IEntity;

    Task Flush();

    EntityStatus GetEntityStatus(IEntity entity);

    Action<Exception>? OnError { get; set; }

    IQuery<T> Query<T>(Expression<Func<T, bool>>? expression, Expression<Func<T, object>>? sortFunc = null) where T : class, IEntity;

    void Load<T>(Expression<Func<T, bool>> predicate) where T : class, IEntity;
}

public enum EntityStatus
{
    Detached,
    Attached,
    Added, 
    Updated, 
    Deleted
}
