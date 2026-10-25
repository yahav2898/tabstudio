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
}
