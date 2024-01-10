using Microsoft.VisualBasic;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Xml.Linq;

namespace ReactorData.Implementation;

interface IObservableQuery
{
    public abstract void NotifyChanges(IEntity[]? changedEntities);
}

class ObservableQuery<T> : IObservableQuery where T : class, IEntity
{
    class ObservableQueryCollection(ObservableQuery<T> owner, ObservableCollection<T> observableCollection) 
        : ReadOnlyObservableCollection<T>(observableCollection), IQuery<T>
    {
        //note: required to keep the owener alive
        private readonly ObservableQuery<T> _owner = owner;
    }

    private readonly ModelContext _container;

    private readonly Func<IQueryable<T>, IQueryable<T>>? _predicate;
    
    private readonly ObservableCollection<T> _collection;

    public ObservableQuery(ModelContext container, Func<IQueryable<T>, IQueryable<T>>? predicate = null)
    {
        _container = container;
        _predicate = predicate;

        _collection = new ObservableCollection<T>(GetContainerList());
        Query = new ObservableQueryCollection(this, _collection);
    }

    public IQuery<T> Query { get; }

    public void NotifyChanges(IEntity[]? changedEntities)
    {
        if (_container.Options.Dispatcher != null)
        {
            _container.Options.Dispatcher.Invoke(() => InternalNotifyChanges(changedEntities));
        }
        else
        {
            InternalNotifyChanges(changedEntities);
        }
    }

    private void InternalNotifyChanges(IEntity[]? changedEntities)
    {
        var changedEntitiesMap = changedEntities != null ? new HashSet<IEntity>(changedEntities) : null;
        var newItems = GetContainerList();

        static bool areEqual(T newItem, T existingItem)
            => newItem.GetKey()?.Equals(existingItem.GetKey()) == true;

        SyncLists(_collection, newItems, areEqual, item => changedEntities?.Contains(item) == true);
    }

    public static void SyncLists(
        IList<T> existingList,
        IList<T> newList,
        Func<T, T, bool> areEqual,
        Func<T, bool> replaceItem)
    {
        int existingIndex = 0;
        int newIndex = 0;

        while (existingIndex < existingList.Count && newIndex < newList.Count)
        {
            if (areEqual(existingList[existingIndex], newList[newIndex]))
            {
                // The items are equal, move to the next item in both lists
                if (replaceItem(newList[newIndex]))
                {
                    existingList[existingIndex] = newList[newIndex];
                }
                existingIndex++;
                newIndex++;
            }
            else if (existingList.Contains(newList[newIndex]))
            {
                // The new item already exists later in the existing list; remove the current unmatched item in existing
                existingList.RemoveAt(existingIndex);
                // Do not increment existingIndex since we removed the item at existingIndex, the next item is now at existingIndex
            }
            else
            {
                // The new item doesn't exist in the existing list, insert it
                existingList.Insert(existingIndex, newList[newIndex]);
                existingIndex++;
                newIndex++;
            }
        }

        // Remove any leftover items from existing list that are not in new list
        while (existingIndex < existingList.Count)
        {
            existingList.RemoveAt(existingIndex); // Note: remaining items are at the same index after removal
        }

        // Append any remaining new items that have not been processed yet
        while (newIndex < newList.Count)
        {
            existingList.Add(newList[newIndex]);
            newIndex++;
        }
    }

    private T[] GetContainerList()
    {
        var newList = _container.Set<T>().AsQueryable<T>();

        if (_predicate != null)
        {
            newList = _predicate(newList);
        }

        return [.. newList];
    }
}



//public interface ISortableList<T>
//{
//    ISortableList<T> OrderBy<TKey>(Expression<Func<T, TKey>> expression);
//}