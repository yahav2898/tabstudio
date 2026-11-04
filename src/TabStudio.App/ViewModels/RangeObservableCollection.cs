using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace TabStudio.App.ViewModels;

/// <summary>
/// An <see cref="ObservableCollection{T}"/> that can replace all of its items with one change notification,
/// so large lists such as the Solution Explorer update in a single layout pass.
/// </summary>
/// <typeparam name="T">The item type.</typeparam>
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    /// <summary>Replaces every item and raises a single reset notification.</summary>
    /// <param name="items">The new items.</param>
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>Inserts items at an index and raises a single notification for them all.</summary>
    /// <param name="index">Where the first item goes.</param>
    /// <param name="items">The items.</param>
    public void InsertRange(int index, IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            return;
        }

        CheckReentrancy();
        for (var i = 0; i < items.Count; i++)
        {
            Items.Insert(index + i, items[i]);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, items.ToList(), index));
    }

    /// <summary>Removes items from an index and raises a single notification for them all.</summary>
    /// <param name="index">The first item to remove.</param>
    /// <param name="count">How many.</param>
    public void RemoveRange(int index, int count)
    {
        if (count <= 0)
        {
            return;
        }

        CheckReentrancy();
        var removed = new List<T>(count);
        for (var i = 0; i < count; i++)
        {
            removed.Add(Items[index]);
            Items.RemoveAt(index);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, index));
    }
}
