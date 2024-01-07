using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

public interface IQuery
{
    void Listen(Action<EntityChangeSet> callback);
}

public interface IQueryListener
{
    void NotifyQueryChanges(IQuery query, EntityChangeSet callback);
}


public record EntityChangeSet();

