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
    public abstract void NotifyChanges(IEntity[]? changedEntities, bool forceReload = false);
}

class ObservableQuery<T> : IObservableQuery where T : class, IEntity
{
    class ObservableQueryCollection(ObservableQuery<T> owner, ObservableRangeCollection<T> observableCollection) 
        : ReadOnlyObservableCollection<T>(observableCollection), IQuery<T>
    {
        //note: required to keep the owener alive
        private readonly ObservableQuery<T> _owner = owner;
    }

    private readonly ModelContext _container;

    private readonly Func<IQueryable<T>, IQueryable<T>>? _predicate;
    
    private readonly ObservableRangeCollection<T> _collection;

    public ObservableQuery(ModelContext container, Func<IQueryable<T>, IQueryable<T>>? predicate = null)
    {
        _container = container;
        _predicate = predicate;

        _collection = new ObservableRangeCollection<T>(GetContainerList());
        Query = new ObservableQueryCollection(this, _collection);
    }

    public IQuery<T> Query { get; }

    public void NotifyChanges(IEntity[]? changedEntities = null, bool forceReload = false)
    {
        if (_container.Dispatcher != null)
        {
            _container.Dispatcher.Dispatch(() => InternalNotifyChanges(changedEntities, forceReload));
        }
        else
        {
            InternalNotifyChanges(changedEntities, forceReload);
        }
    }

    private void InternalNotifyChanges(IEntity[]? changedEntities, bool forceReload)
    {
        var newItems = GetContainerList();

        if (!forceReload)
        {
            var changedEntitiesMap = changedEntities != null ? new HashSet<IEntity>(changedEntities) : null;
            var changedEntitiesIdsMap = changedEntities != null ? new HashSet<object>(changedEntities.Where(_=>_.GetKey() != null).Select(_=>_.GetKey()!)) : null;
            static bool areEqual(T newItem, T existingItem)
            {
                var newKey = newItem.GetKey();
                var oldKey = existingItem.GetKey();
                return newItem == existingItem || (newKey == null && oldKey == null) || (newKey != null && oldKey != null && newKey.Equals(existingItem.GetKey()));
            }

            SyncLists(_collection, newItems, areEqual, item =>
            {
                var itemKey = item.GetKey();
                if (itemKey  != null)
                {
                    if (changedEntitiesIdsMap?.Contains(itemKey) == true)
                    {
                        return true;
                    }
                }

                return changedEntities?.Contains(item) == true;
            });
        }
        else
        {
            _collection.ReplaceRange(newItems);
        }
    }

    public static void SyncLists(
        ObservableRangeCollection<T> existingList,
        IList<T> newList,
        Func<T, T, bool> areEqual,
        Func<T, bool> replaceItem)
    {
        int existingIndex = 0;
        var itemsToAdd = new List<T>();

        foreach (var newItem in newList)
        {
            // Check if we've exceeded the bounds of the existing list; if so, add remaining new items
            if (existingIndex >= existingList.Count)
            {
                itemsToAdd.Add(newItem);
                continue;
            }

            // If the items match based on the equality function, move to the next item
            if (areEqual(existingList[existingIndex], newItem))
            {
                if (itemsToAdd.Count != 0)
                {
                    existingList.InsertRange(existingIndex, itemsToAdd);
                    existingIndex += itemsToAdd.Count;
                    itemsToAdd.Clear();
                }
                
                if (replaceItem(newItem))
                {
                    existingList[existingIndex] = newItem;
                }

                existingIndex++;
                continue;
            }

            // If the existing item doesn't match and the new item is not found ahead,
            // it means we need to remove the existing item
            if (!newList.Skip(existingIndex).Any(x => areEqual(existingList[existingIndex], x)))
            {
                existingList.RemoveAt(existingIndex);
                // Do not increment existingIndex as we removed the item at that index
                continue;
            }

            // Otherwise, the new item should be inserted before the current existing item
            itemsToAdd.Add(newItem);
        }

        // Add any items that are still pending to be added
        if (itemsToAdd.Count != 0)
        {
            existingList.AddRange(itemsToAdd);
        }

        // If there are any remaining elements in the existing list that are not in the new list, remove them
        var itemsToRemove = existingList.Skip(newList.Count).ToList();
        if (itemsToRemove.Count != 0)
        {
            if (itemsToRemove.Count <= 10)
            {
                foreach (var itemToRemove in itemsToRemove)
                {
                    existingList.Remove(itemToRemove);
                }    
            }
            else
            {
                existingList.RemoveRange(itemsToRemove);
            }
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