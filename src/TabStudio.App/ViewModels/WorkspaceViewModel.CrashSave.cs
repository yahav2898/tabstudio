using AvaloniaEdit.Document;
using TabStudio.App.Services;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The session a crash on a background thread can save. The editor's text can be read only on the UI thread, and a
/// crash on another thread ends the app before the UI thread runs again, so the session written then was the one the
/// last throttled save took, up to two seconds of typing earlier. Every change now keeps a snapshot of the document's
/// text, which costs no copy of it and can be read from any thread.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private readonly Lock _crashGate = new();
    private readonly Dictionary<DocumentViewModel, ITextSource> _textSnapshots = [];
    private SessionState? _lastSession;

    /// <summary>Keeps a snapshot of a document's text, as it is now; on the UI thread.</summary>
    /// <param name="document">The document.</param>
    private void TakeTextSnapshot(DocumentViewModel document)
    {
        var snapshot = document.Document.CreateSnapshot();
        lock (_crashGate)
        {
            _textSnapshots[document] = snapshot;
        }
    }

    /// <summary>Lets go of a closed document's snapshot.</summary>
    /// <param name="document">The document.</param>
    private void ForgetTextSnapshot(DocumentViewModel document)
    {
        lock (_crashGate)
        {
            _textSnapshots.Remove(document);
        }
    }

    /// <summary>Remembers the session the UI thread took last, for a crash on another thread to start from.</summary>
    /// <param name="state">The session.</param>
    private SessionState RememberSession(SessionState state)
    {
        lock (_crashGate)
        {
            _lastSession = state;
        }

        return state;
    }

    /// <summary>
    /// The session as it is now, from any thread: the one the UI thread took last, with the latest text of every open
    /// document that has unsaved changes, from its snapshot.
    /// </summary>
    /// <returns>The session, or <see langword="null"/> before the UI thread took one.</returns>
    internal SessionState? CaptureSessionFromAnyThread()
    {
        SessionState? state;
        KeyValuePair<DocumentViewModel, ITextSource>[] snapshots;
        lock (_crashGate)
        {
            state = _lastSession;
            snapshots = [.. _textSnapshots];
        }

        if (state is null)
        {
            return null;
        }

        var documents = new List<SessionDocument>(state.Documents.Count);
        foreach (var saved in state.Documents)
        {
            var match = Array.FindIndex(snapshots, s => string.Equals(s.Key.FilePath, saved.Path, StringComparison.Ordinal));
            documents.Add(match < 0 ? saved : Current(saved, snapshots[match].Key, snapshots[match].Value));
        }

        // A document opened since, with its unsaved text.
        foreach (var (document, text) in snapshots)
        {
            if (!documents.Exists(d => string.Equals(d.Path, document.FilePath, StringComparison.Ordinal)))
            {
                documents.Add(Current(new SessionDocument(document.FilePath, document.CaretOffset, null), document, text));
            }
        }

        return state with { Documents = documents };

        // A notebook's text is its cells', which only the UI thread can write out: its last saved copy stays.
        static SessionDocument Current(SessionDocument saved, DocumentViewModel document, ITextSource text) =>
            document.Notebook is not null ? saved
            : document.IsDirty ? saved with { CaretOffset = document.CaretOffset, UnsavedText = text.Text }
            : saved with { CaretOffset = document.CaretOffset, UnsavedText = null };
    }

    /// <summary>Saves the session from a thread other than the UI thread, as a crash there does before the app ends.</summary>
    private void SaveSessionFromAnyThread()
    {
        if (IsLiveGuest || _restoringSession || CaptureSessionFromAnyThread() is not { } state)
        {
            return;
        }

        _sessions.Save(state, _sessions.NextSequence());
    }
}
