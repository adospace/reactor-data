using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace ReactorData;

public interface IModelContext
{
    void Add(params IEntity[] entities);

    void Update(params IEntity[] entities);

    void Delete(params IEntity[] entities);

    void Save();

    T? FindByKey<T>(object key) where T : class, IEntity;

    void DiscardChanges();

    //IReadOnlyList<T> Set<T>() where T : class, IEntity;

    Task Flush();

    EntityStatus GetEntityStatus(IEntity entity);

    Action<Exception>? OnError { get; set; }

    IQuery<T> Query<T>(Expression<Func<IQueryable<T>, IQueryable<T>>>? predicate = null) where T : class, IEntity;

    void Load<T>(
        Expression<Func<IQueryable<T>, IQueryable<T>>>? predicate = null, 
        Func<T, T, bool>? compareFunc = null,
        Action<IEnumerable<T>>? onLoad = null) where T : class, IEntity;

    IModelContext CreateScope();
}

public enum EntityStatus
{
    Detached,
    Attached,
    Added, 
    Updated, 
    Deleted
}
