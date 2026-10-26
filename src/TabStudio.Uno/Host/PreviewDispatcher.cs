#nullable enable

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace TabStudio.UnoHost;

/// <summary>
/// Uno Platform's UI thread in TabStudio: a thread of its own, beside Avalonia's, that runs every piece of work Uno's
/// dispatcher schedules, one after another. A slow or stuck app therefore never blocks TabStudio's own UI, and Uno's
/// and Avalonia's dispatchers and synchronization contexts never meet on one thread.
/// </summary>
internal sealed class PreviewDispatcher : IDisposable
{
    private readonly BlockingCollection<Action> _work = new();
    private readonly Thread _thread;
    private readonly Action<Exception> _unhandled;

    public PreviewDispatcher(Action<Exception> unhandled)
    {
        _unhandled = unhandled;
        _thread = new Thread(Run) { Name = "Uno Platform UI", IsBackground = true };
        _thread.Start();
    }

    /// <summary>Whether the calling thread is Uno's UI thread.</summary>
    public bool HasThreadAccess => Environment.CurrentManagedThreadId == _thread.ManagedThreadId;

    /// <summary>Queues work for Uno's UI thread.</summary>
    public void Post(Action work)
    {
        if (!_work.IsAddingCompleted)
        {
            try
            {
                _work.Add(work);
            }
            catch (InvalidOperationException)
            {
                // The dispatcher stopped between the check and the add.
            }
        }
    }

    public void Dispose() => _work.CompleteAdding();

    private void Run()
    {
        foreach (var work in _work.GetConsumingEnumerable())
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                // Uno's dispatcher catches what its own work throws; this is what escapes the host's work.
                _unhandled(ex);
            }
        }
    }
}
