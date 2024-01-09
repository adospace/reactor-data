using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;

namespace ReactorData.Implementation;

abstract class Query
{
    public abstract void NotifyChanges(IEntity[]? changedEntities);
}

class Query<T> : Query, IQuery<T> where T : class, IEntity
{
    private readonly ObservableCollection<T> _collection;
    private readonly ModelContext _container;
    private readonly Func<T, bool>? _predicate;
    private readonly Func<T, object>? _sortFunc;

    //class SortableQuery : ISortableList<T>
    //{
    //    private readonly IEnumerable<T> _list;

    //    public SortableQuery(IEnumerable<T> list)
    //    {
    //        _list = list;
    //    }

    //    public ISortableList<T> OrderBy<TKey>(Expression<Func<T, TKey>> expression)
    //    {
    //        _list.OrderBy(expression.Compile());
    //        return this;
    //    }
    //}

    public Query(ModelContext container, Func<T, bool>? predicate = null, Func<T, object>? sortFunc = null)
    {
        _container = container;
        _predicate = predicate;
        _sortFunc = sortFunc;

        _collection = new ObservableCollection<T>(GetContainerList());
        
        _collection.CollectionChanged += InternalCollectionChanged;
        ((INotifyPropertyChanged)_collection).PropertyChanged += InternalPropertyChanged;
    }

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int Count => _collection.Count;

    public T this[int index] => _collection[index];

    public override void NotifyChanges(IEntity[]? changedEntities)
    {
        var changedEntitiesMap = changedEntities != null ? new HashSet<IEntity>(changedEntities) : null;
        var newList = GetContainerList();

        var newListEnumerator = newList.GetEnumerator();
        int i = 0;

        while (i < _collection.Count)
        {
            if (!newListEnumerator.MoveNext())
            {
                break;
            }

            var currentItem = newListEnumerator.Current;

            if (currentItem.GetKey()?.Equals(_collection[i].GetKey()) == true)
            {
                if (changedEntitiesMap?.Contains(currentItem) == true)
                {
                    _collection[i] = currentItem;
                }
                i++;
                continue;
            }

            _collection.Insert(i, currentItem);
            i++;
        }

        while (i < _collection.Count)
        {
            _collection.RemoveAt(i);
            i++;
        }

        while (newListEnumerator.MoveNext())
        {
            _collection.Add(newListEnumerator.Current);
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _collection.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }


    private IEnumerable<T> GetContainerList()
    {
        var newList = _container.Set<T>();

        if (_predicate != null)
        {
            newList = newList.Where(_predicate);
        }

        if (_sortFunc != null)
        {
            newList = newList.OrderBy(_sortFunc);
        }
        else
        {
            //by default order by key
            newList = newList.OrderBy(_ => _.GetKey());
        }

        return newList;
    }

    private void InternalCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }

    private void InternalPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        PropertyChanged?.Invoke(this, e);
    }

}


//public interface ISortableList<T>
//{
//    ISortableList<T> OrderBy<TKey>(Expression<Func<T, TKey>> expression);
//}