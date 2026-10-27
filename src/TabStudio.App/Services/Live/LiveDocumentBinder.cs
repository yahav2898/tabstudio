using AvaloniaEdit.Document;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Live;
using TabStudio.Live;
using TabStudio.Live.Sessions;
using TabStudio.Live.Text;

namespace TabStudio.App.Services.Live;

/// <summary>One other participant's caret and selection in a document, in offsets of this device's text.</summary>
/// <param name="Participant">The participant.</param>
/// <param name="Name">Its name, shown on its caret.</param>
/// <param name="Color">Its color, 0 to 7.</param>
/// <param name="Anchor">Where its selection starts.</param>
/// <param name="Head">Where its caret is.</param>
public sealed record LiveRemoteCaret(int Participant, string Name, int Color, int Anchor, int Head);

/// <summary>
/// Keeps an editor's text and a shared document in step: what is typed here goes to everyone, what others type
/// comes into the editor without being taken for this participant's own typing, and Undo takes back only this
/// participant's edits. It also gives the editor the other participants' carets and this participant's place.
/// </summary>
/// <remarks>
/// While a document is shared, its editor's own undo stack is off: <see cref="Undo"/> and <see cref="Redo"/> replace
/// it. Call every member on the UI thread, the session's thread.
/// </remarks>
public sealed class LiveDocumentBinder : ILiveTextView, IDisposable
{
    private readonly DocumentViewModel _document;
    private readonly int _undoLimit;
    private IReadOnlyList<LiveRemoteCaret> _carets = [];
    private (int Anchor, int Head, int TopLine)? _place;
    private bool _applying;
    private bool _disposed;

    /// <summary>Binds a document's editor text to a shared document, giving the editor the shared text.</summary>
    /// <param name="live">The shared document.</param>
    /// <param name="document">The editor's document.</param>
    public LiveDocumentBinder(LiveDocument live, DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(document);
        Live = live;
        _document = document;
        var text = document.Document;
        var shared = live.Text.ToString();
        if (!string.Equals(text.Text, shared, StringComparison.Ordinal))
        {
            Apply(() => text.Text = shared);
        }

        _undoLimit = text.UndoStack.SizeLimit;
        text.UndoStack.SizeLimit = 0;
        text.Changed += OnTextChanged;
        text.UpdateStarted += OnUpdateStarted;
        text.UpdateFinished += OnUpdateFinished;
        live.Undo.Changed += OnUndoChanged;
        live.RemoteEdited += OnRemoteEdited;
        Session.PresenceChanged += OnPresenceChanged;
        Session.ParticipantsChanged += OnParticipantsChanged;
        Session.DocumentSaved += OnDocumentSaved;
        live.Attach(this);
        document.SetLive(this);
        OnUndoChanged(this, EventArgs.Empty);
        UpdateCarets();
    }

    /// <summary>Raised when another participant's caret or selection moved, or someone came or left.</summary>
    public event EventHandler? CaretsChanged;

    /// <summary>Raised when the participant this device follows moved in this document: the editor shows the same place.</summary>
    public event EventHandler<LivePresence>? FollowMoved;

    /// <summary>Raised when the editor's user typed or tapped while following: following stops.</summary>
    public event EventHandler? FollowStopped;

    /// <summary>The shared document.</summary>
    public LiveDocument Live { get; }

    /// <summary>The session.</summary>
    public LiveSession Session => Live.Session;

    /// <summary>Whether this participant may only watch: the editor takes no typing.</summary>
    public bool IsReadOnly => !Live.CanEdit;

    /// <summary>The other participants' carets and selections in this document.</summary>
    public IReadOnlyList<LiveRemoteCaret> Carets => _carets;

    /// <summary>Says where this participant is in the document: its selection and the first line it sees.</summary>
    /// <param name="anchor">Where the selection starts.</param>
    /// <param name="head">Where the caret is.</param>
    /// <param name="topLine">The first visible line, one-based.</param>
    public void ReportPlace(int anchor, int head, int topLine)
    {
        if (!_disposed)
        {
            _place = (anchor, head, topLine);
            Session.ReportPresence(Live, anchor, head, topLine);
        }
    }

    /// <summary>
    /// Says again where this participant is in the document, as its tab became the active one: where its editor last
    /// reported, or at its caret when the editor has not shown it yet.
    /// </summary>
    public void ReportActive()
    {
        if (_disposed)
        {
            return;
        }

        var length = _document.Document.TextLength;
        var (anchor, head, top) = _place ?? (_document.CaretOffset, _document.CaretOffset, 1);
        Session.ReportPresence(Live, Math.Clamp(anchor, 0, length), Math.Clamp(head, 0, length), top);
    }

    /// <summary>Takes back this participant's last edit, here and on every device.</summary>
    /// <returns>Whether there was one to take back.</returns>
    public bool Undo() => ApplyOwn(Live.UndoLast());

    /// <summary>Does this participant's last undone edit again, here and on every device.</summary>
    /// <returns>Whether there was one to do again.</returns>
    public bool Redo() => ApplyOwn(Live.RedoLast());

    /// <summary>Where a participant's selection is now, in this device's offsets.</summary>
    /// <param name="presence">The participant's presence in this document.</param>
    public (int Anchor, int Head)? OffsetsOf(LivePresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);
        return Live.OffsetOf(presence.Anchor) is { } anchor && Live.OffsetOf(presence.Head) is { } head ? (anchor, head) : null;
    }

    /// <summary>Shows the place of the participant this device follows.</summary>
    /// <param name="presence">Its presence in this document.</param>
    public void ShowFollowed(LivePresence presence) => FollowMoved?.Invoke(this, presence);

    /// <summary>The user typed or tapped while following: following stops.</summary>
    public void StopFollowing() => FollowStopped?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public void ApplyRemote(IReadOnlyList<TextChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (_disposed || changes.Count == 0)
        {
            return;
        }

        var text = _document.Document;
        Apply(() =>
        {
            text.BeginUpdate();
            try
            {
                foreach (var change in changes)
                {
                    text.Replace(change.Offset, change.RemovedLength, change.InsertedText);
                }
            }
            finally
            {
                text.EndUpdate();
            }
        });
        _document.MarkLiveEdited();
    }

    /// <inheritdoc />
    public void Reload(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!_disposed)
        {
            Apply(() => _document.Document.Text = text);
            UpdateCarets();
        }
    }

    /// <summary>Stops sharing the editor's text: its own undo stack comes back, empty.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var text = _document.Document;
        text.Changed -= OnTextChanged;
        text.UpdateStarted -= OnUpdateStarted;
        text.UpdateFinished -= OnUpdateFinished;
        Live.Undo.Changed -= OnUndoChanged;
        Live.RemoteEdited -= OnRemoteEdited;
        Session.PresenceChanged -= OnPresenceChanged;
        Session.ParticipantsChanged -= OnParticipantsChanged;
        Session.DocumentSaved -= OnDocumentSaved;
        Live.Detach(this);
        text.UndoStack.SizeLimit = _undoLimit;
        text.UndoStack.ClearAll();
        _document.SetLive(null);
    }

    private void Apply(Action change)
    {
        _applying = true;
        try
        {
            change();
        }
        finally
        {
            _applying = false;
        }
    }

    private bool ApplyOwn(IReadOnlyList<TextChange>? changes)
    {
        if (changes is null)
        {
            return false;
        }

        ApplyRemote(changes);
        return true;
    }

    private void OnTextChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_applying || _disposed)
        {
            return;
        }

        var removed = e.RemovedText.Text;
        var inserted = e.InsertedText.Text;
        if (Live.Edit(e.Offset, removed, inserted))
        {
            _document.MarkLiveEdited();
            return;
        }

        // A participant that only watches cannot change the text: it is put back as the session has it.
        var offset = e.Offset;
        var length = inserted.Length;
        _document.Document.Changed -= OnTextChanged;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
            {
                Apply(() => _document.Document.Replace(offset, length, removed));
                _document.Document.Changed += OnTextChanged;
            }
        });
    }

    private void OnUpdateStarted(object? sender, EventArgs e)
    {
        if (!_applying)
        {
            Live.BeginStep();
        }
    }

    private void OnUpdateFinished(object? sender, EventArgs e)
    {
        if (!_applying)
        {
            Live.EndStep();
        }
    }

    private void OnUndoChanged(object? sender, EventArgs e) => _document.SetLiveUndo(Live.Undo.CanUndo, Live.Undo.CanRedo);

    private void OnRemoteEdited(object? sender, IReadOnlyCollection<int> authors) => UpdateCarets();

    private void OnPresenceChanged(object? sender, LivePresence presence)
    {
        if (presence.Path == Live.Path || _carets.Any(c => c.Participant == presence.Participant))
        {
            UpdateCarets();
        }
    }

    private void OnParticipantsChanged(object? sender, EventArgs e)
    {
        _document.SetLiveReadOnly(IsReadOnly);
        UpdateCarets();
    }

    private void OnDocumentSaved(object? sender, string path)
    {
        if (path == Live.Path)
        {
            _document.MarkLiveSaved();
        }
    }

    /// <summary>Reads the others' carets in this document again, and the avatars on its tab.</summary>
    private void UpdateCarets()
    {
        var carets = new List<LiveRemoteCaret>();
        var avatars = new List<LiveAvatarViewModel>();
        foreach (var presence in Session.Presence)
        {
            if (presence.Path != Live.Path || presence.Participant == Session.SelfId || Session.Find(presence.Participant) is not { IsConnected: true } participant)
            {
                continue;
            }

            avatars.Add(LiveAvatarViewModel.Of(participant));
            if (OffsetsOf(presence) is { } offsets)
            {
                carets.Add(new LiveRemoteCaret(participant.Id, participant.Name, participant.Color, offsets.Anchor, offsets.Head));
            }
        }

        carets.Sort((a, b) => a.Participant.CompareTo(b.Participant));
        avatars.Sort((a, b) => a.Id.CompareTo(b.Id));
        _document.ShowLiveAvatars(avatars);
        if (!carets.SequenceEqual(_carets))
        {
            _carets = carets;
            CaretsChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
