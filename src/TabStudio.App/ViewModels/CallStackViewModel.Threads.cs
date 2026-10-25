using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabStudio.App.ViewModels;

/// <summary>A thread of the paused program, in the Call Stack panel's thread box.</summary>
/// <param name="Id">The debug adapter's id of the thread.</param>
/// <param name="Name">Its name, such as <c>MainThread</c>.</param>
public sealed record DebugThreadOption(int Id, string Name);

/// <summary>The threads of a program a debug adapter paused: the Call Stack shows the chosen thread's frames.</summary>
public sealed partial class CallStackViewModel
{
    private bool _showingThreads;

    /// <summary>The threads, when the program has more than one.</summary>
    public ObservableCollection<DebugThreadOption> Threads { get; } = [];

    /// <summary>Whether the thread box is shown: the program has more than one thread.</summary>
    public bool HasThreads => Threads.Count > 1;

    /// <summary>The thread whose frames are shown.</summary>
    [ObservableProperty]
    public partial DebugThreadOption? SelectedThread { get; set; }

    /// <summary>Raised when the user chooses another thread.</summary>
    public event EventHandler<DebugThreadOption>? ThreadChosen;

    /// <summary>Shows the threads of a pause, with the one that stopped chosen.</summary>
    /// <param name="threads">The threads.</param>
    /// <param name="selected">The thread whose frames are shown.</param>
    public void ShowThreads(IReadOnlyList<DebugThreadOption> threads, DebugThreadOption? selected)
    {
        ArgumentNullException.ThrowIfNull(threads);
        _showingThreads = true;
        try
        {
            Threads.Clear();
            foreach (var thread in threads)
            {
                Threads.Add(thread);
            }

            SelectedThread = selected;
        }
        finally
        {
            _showingThreads = false;
        }

        OnPropertyChanged(nameof(HasThreads));
    }

    /// <summary>Forgets the threads, when the program runs again or the session ends.</summary>
    public void ClearThreads() => ShowThreads([], null);

    partial void OnSelectedThreadChanged(DebugThreadOption? value)
    {
        if (!_showingThreads && value is not null)
        {
            ThreadChosen?.Invoke(this, value);
        }
    }
}
