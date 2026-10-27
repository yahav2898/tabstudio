using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>
/// A document shared in a session: its <see cref="SharedText"/>, this participant's undo, and the editor that shows it
/// on this device. Local edits go to the other devices, and their edits come back to the editor, on the session's
/// thread.
/// </summary>
/// <remarks>Every member must be used on the session's thread (<see cref="LiveSession.Dispatcher"/>).</remarks>
public sealed class LiveDocument
{
    private readonly LiveSession _session;
    private ILiveTextView? _view;

    internal LiveDocument(LiveSession session, string path, string fullPath, SharedText text)
    {
        _session = session;
        Path = path;
        FullPath = fullPath;
        Text = text;
        Undo = new TextUndoHistory(text);
    }

    /// <summary>Raised after the text changed for any reason: an edit here or on another device, an undo, a reload.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised after another device's edits changed the text, with the participants that made them.</summary>
    public event EventHandler<IReadOnlyCollection<int>>? RemoteEdited;

    /// <summary>The document's path, relative to the workspace's folder.</summary>
    public string Path { get; }

    /// <summary>The document's path on this device: the host's file, or the guest's copy.</summary>
    public string FullPath { get; }

    /// <summary>The shared text.</summary>
    public SharedText Text { get; private set; }

    /// <summary>This participant's undo and redo.</summary>
    public TextUndoHistory Undo { get; private set; }

    /// <summary>The session the document belongs to.</summary>
    public LiveSession Session => _session;

    /// <summary>Whether this participant may edit it now.</summary>
    public bool CanEdit => _session.Self is { } self && LivePermissions.CanEdit(self);

    /// <summary>The editor that shows the document on this device, if one does.</summary>
    public ILiveTextView? View => _view;

    /// <summary>Shows the document in an editor. The editor's text must be <see cref="Text"/>'s.</summary>
    /// <param name="view">The editor.</param>
    public void Attach(ILiveTextView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        _view = view;
    }

    /// <summary>The editor stopped showing the document.</summary>
    /// <param name="view">The editor that stops.</param>
    public void Detach(ILiveTextView view)
    {
        if (ReferenceEquals(_view, view))
        {
            _view = null;
        }
    }

    /// <summary>
    /// Records an edit the editor made on this device, sends it to the others and remembers it for Undo. A participant
    /// that may not edit gets <see langword="false"/>: its editor puts the text back.
    /// </summary>
    /// <param name="offset">Where the edit was.</param>
    /// <param name="removedText">What it removed.</param>
    /// <param name="insertedText">What it inserted.</param>
    /// <returns>Whether the edit was taken.</returns>
    public bool Edit(int offset, string removedText, string insertedText)
    {
        ArgumentNullException.ThrowIfNull(removedText);
        ArgumentNullException.ThrowIfNull(insertedText);
        if (!CanEdit)
        {
            return false;
        }

        var operations = new List<TextOperation>(2);

        // A replacement is one step; typing one character after another joins the step before it.
        var replaces = removedText.Length > 0 && insertedText.Length > 0;
        if (replaces)
        {
            Undo.BeginGroup();
        }

        try
        {
            if (removedText.Length > 0 && Text.Delete(offset, removedText.Length) is { } deleted)
            {
                operations.Add(deleted);
                Undo.Record(deleted, removedText, offset);
            }

            if (insertedText.Length > 0 && Text.Insert(offset, insertedText) is { } inserted)
            {
                operations.Add(inserted);
                Undo.Record(inserted, null, offset);
            }
        }
        finally
        {
            if (replaces)
            {
                Undo.EndGroup();
            }
        }

        if (operations.Count > 0)
        {
            _session.SendEdits(this, operations);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return true;
    }

    /// <summary>Takes back this participant's last step, here and on every device.</summary>
    /// <returns>The changes for this device's editor, or <see langword="null"/> when there is nothing to undo.</returns>
    public IReadOnlyList<TextChange>? UndoLast() => CanEdit ? Revert(Undo.Undo()) : null;

    /// <summary>Does this participant's last undone step again, here and on every device.</summary>
    /// <returns>The changes for this device's editor, or <see langword="null"/> when there is nothing to redo.</returns>
    public IReadOnlyList<TextChange>? RedoLast() => CanEdit ? Revert(Undo.Redo()) : null;

    /// <summary>Starts an editor step, such as a paste or a Format Document, that Undo takes back at once.</summary>
    public void BeginStep() => Undo.BeginGroup();

    /// <summary>Ends the step <see cref="BeginStep"/> started.</summary>
    public void EndStep() => Undo.EndGroup();

    /// <summary>A place in the text that stays put while others edit.</summary>
    /// <param name="offset">The offset.</param>
    public TextPosition PositionAt(int offset) => Text.PositionAt(offset);

    /// <summary>Where a place is now, or <see langword="null"/> when it names text this device does not have yet.</summary>
    /// <param name="position">The place.</param>
    public int? OffsetOf(TextPosition position) => Text.IndexOf(position);

    /// <summary>Applies other devices' edits and shows them in the editor.</summary>
    internal void ApplyRemote(IReadOnlyList<TextOperation> operations)
    {
        var changes = new List<TextChange>();
        var authors = new HashSet<int>();
        foreach (var operation in operations)
        {
            var applied = Text.Apply(operation);
            if (applied.Count > 0)
            {
                authors.Add(operation.Replica);
                changes.AddRange(applied);
            }
        }

        if (changes.Count == 0)
        {
            return;
        }

        _view?.ApplyRemote(changes);
        Changed?.Invoke(this, EventArgs.Empty);
        RemoteEdited?.Invoke(this, authors);
    }

    /// <summary>Starts the document again from the host's edits, after the host refused this device's.</summary>
    internal void Reload(IReadOnlyList<TextOperation> operations, int replica)
    {
        var text = new SharedText(replica);
        foreach (var operation in operations)
        {
            text.Apply(operation);
        }

        Text = text;
        Undo = new TextUndoHistory(text);
        _view?.Reload(text.ToString());
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private IReadOnlyList<TextChange>? Revert(TextEditResult? result)
    {
        if (result is null)
        {
            return null;
        }

        if (result.Operations.Count > 0)
        {
            _session.SendEdits(this, result.Operations);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        return result.Changes;
    }
}
