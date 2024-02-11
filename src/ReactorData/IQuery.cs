using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

/// <summary>
/// Reactive query of entities that supports the <see cref="INotifyCollectionChanged"/> interface
/// </summary>
/// <typeparam name="T">Type of the entities monitored by the query</typeparam>
public interface IQuery<T> : 
    ICollection<T>, 
    IEnumerable<T>, 
    IEnumerable, 
    IList<T>, 
    IReadOnlyCollection<T>, 
    IReadOnlyList<T>, 
    ICollection, 
    IList, 
    INotifyCollectionChanged, 
    INotifyPropertyChanged where T : class, IEntity
{
    new int Count => ((ICollection)this).Count;
}

