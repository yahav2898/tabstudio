using TabStudio.Live.Files;
using TabStudio.Live.Text;
using TabStudio.Swarm;

namespace TabStudio.Live.Sessions;

/// <summary>
/// A TabStudio Live session as one device sees it: who is in it, the shared documents, where everyone is, and what
/// the host runs. <see cref="LiveHost"/> is the device that shares its workspace; <see cref="LiveGuest"/> one that
/// joined it.
/// </summary>
/// <remarks>
/// The state lives on one thread, the <see cref="Dispatcher"/>'s, and every event is raised there. Members that change
/// state must be called there too; members that wait for the network say so and may be called from it.
/// </remarks>
public abstract class LiveSession
{
    /// <summary>How long a participant's caret may move before the others are told, so a drag sends a few messages, not hundreds.</summary>
    internal static readonly TimeSpan PresenceInterval = TimeSpan.FromMilliseconds(40);

    private readonly Dictionary<string, LiveDocument> _documents = new(StringComparer.Ordinal);
    private readonly Dictionary<int, LivePresence> _presence = [];
    private IReadOnlyList<LiveParticipant> _participants = [];
    private readonly List<LiveOutput> _output = [];
    private readonly List<byte> _terminal = [];
    private LivePresence? _ownPresence;
    private bool _presenceScheduled;
    private int _outputLength;

    /// <summary>Creates the session's state on its thread.</summary>
    /// <param name="dispatcher">The session's thread.</param>
    protected LiveSession(ILiveDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        Dispatcher = dispatcher;
    }

    /// <summary>Raised when someone joined, left or reconnected, or what someone may do changed.</summary>
    public event EventHandler? ParticipantsChanged;

    /// <summary>Raised when a participant's document, caret, selection or scroll changed.</summary>
    public event EventHandler<LivePresence>? PresenceChanged;

    /// <summary>Raised when what the host runs changed.</summary>
    public event EventHandler? RunChanged;

    /// <summary>Raised when the host's debugged program paused somewhere else, or ran again.</summary>
    public event EventHandler? DebugChanged;

    /// <summary>Raised when the host's Error List changed.</summary>
    public event EventHandler? ErrorsChanged;

    /// <summary>Raised when the breakpoints changed.</summary>
    public event EventHandler? BreakpointsChanged;

    /// <summary>Raised for each piece of the host's Output.</summary>
    public event EventHandler<LiveOutput>? OutputWritten;

    /// <summary>Raised when the host's Output was cleared.</summary>
    public event EventHandler? OutputCleared;

    /// <summary>Raised for output of the host's Terminal.</summary>
    public event EventHandler<ReadOnlyMemory<byte>>? TerminalWritten;

    /// <summary>Raised when a shared document was saved on the host.</summary>
    public event EventHandler<string>? DocumentSaved;

    /// <summary>Raised once, when the session ended for this device, with why.</summary>
    public event EventHandler<string>? Ended;

    /// <summary>The session's thread.</summary>
    public ILiveDispatcher Dispatcher { get; }

    /// <summary>Whether this device is the host.</summary>
    public abstract bool IsHost { get; }

    /// <summary>This device's number in the session; 0 before it joined.</summary>
    public int SelfId { get; protected set; }

    /// <summary>This device as a participant, once it is in the session.</summary>
    public LiveParticipant? Self => Find(SelfId);

    /// <summary>Everyone in the session, the host first.</summary>
    public IReadOnlyList<LiveParticipant> Participants => _participants;

    /// <summary>Whether the session is in classroom mode: everyone follows the presenter, and raises a hand to speak.</summary>
    public bool IsClassroom { get; protected set; }

    /// <summary>Who presents in classroom mode: the host, unless the host handed control to a guest.</summary>
    public int PresenterId { get; protected set; } = 1;

    /// <summary>The presenter, in classroom mode.</summary>
    public LiveParticipant? Presenter => IsClassroom ? Find(PresenterId) : null;

    /// <summary>The shared workspace's name.</summary>
    public string WorkspaceName { get; protected set; } = string.Empty;

    /// <summary>The host's name.</summary>
    public string HostName { get; protected set; } = string.Empty;

    /// <summary>The workspace's folder on this device: the host's own, or a guest's copy; <see langword="null"/> before joining.</summary>
    public string? WorkspaceRoot { get; protected set; }

    /// <summary>The documents shared so far, on this device.</summary>
    public IReadOnlyCollection<LiveDocument> Documents => _documents.Values;

    /// <summary>Where every other participant is.</summary>
    public IReadOnlyCollection<LivePresence> Presence => _presence.Values;

    /// <summary>What the host runs.</summary>
    public LiveRunState RunState { get; private set; } = LiveRunState.Idle;

    /// <summary>Where the host's debugged program is paused, if it is.</summary>
    public LiveDebugState? Debug { get; private set; }

    /// <summary>The host's Error List.</summary>
    public IReadOnlyList<LiveDiagnostic> Errors { get; private set; } = [];

    /// <summary>Every breakpoint, on the workspace's relative paths.</summary>
    public IReadOnlyList<SwarmBreakpoint> Breakpoints { get; private set; } = [];

    /// <summary>Whether the session ended for this device.</summary>
    public bool HasEnded { get; private set; }

    /// <summary>A participant by number.</summary>
    /// <param name="id">The participant's number.</param>
    public LiveParticipant? Find(int id)
    {
        foreach (var participant in _participants)
        {
            if (participant.Id == id)
            {
                return participant;
            }
        }

        return null;
    }

    /// <summary>A shared document by its path in the workspace, if it is open on this device.</summary>
    /// <param name="path">The relative path.</param>
    public LiveDocument? FindDocument(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return _documents.GetValueOrDefault(path);
    }

    /// <summary>The path in the workspace of a file on this device, or <see langword="null"/> when it is not in the shared folder.</summary>
    /// <param name="fullPath">The file's path on this device.</param>
    public string? RelativePathOf(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        return WorkspaceRoot is { } root && fullPath.Length > 0 ? LiveWorkspacePaths.ToRelativePath(root, fullPath) : null;
    }

    /// <summary>The path on this device of a file in the workspace, or <see langword="null"/> when the path is unsafe.</summary>
    /// <param name="path">The relative path.</param>
    public string? FullPathOf(string path) =>
        WorkspaceRoot is { } root && LiveWorkspacePaths.Normalize(path) is { } normalized
            ? Path.Combine([root, .. normalized.Split('/')])
            : null;

    /// <summary>Where a participant is, if it said.</summary>
    /// <param name="participant">The participant.</param>
    public LivePresence? PresenceOf(int participant) => _presence.GetValueOrDefault(participant);

    /// <summary>
    /// Opens a shared document on this device: the host shares the file, a guest fetches its edits from the host. A
    /// document already open is returned as it is. Call on the session's thread; it waits for the host.
    /// </summary>
    /// <param name="path">The document's path in the workspace.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>The document, or <see langword="null"/> when it cannot be shared.</returns>
    public abstract Task<LiveDocument?> OpenDocumentAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>This device stopped showing a document. Call on the session's thread.</summary>
    /// <param name="document">The document.</param>
    public abstract void CloseDocument(LiveDocument document);

    /// <summary>
    /// Says where this participant is: its document, selection and first visible line. The others are told a moment
    /// later, with the latest. Call on the session's thread.
    /// </summary>
    /// <param name="document">The document it shows, or <see langword="null"/> for none.</param>
    /// <param name="anchor">Where its selection starts.</param>
    /// <param name="head">Where its caret is.</param>
    /// <param name="topLine">The first line it sees, one-based.</param>
    public void ReportPresence(LiveDocument? document, int anchor, int head, int topLine)
    {
        if (SelfId == 0 || HasEnded)
        {
            return;
        }

        var presence = document is null
            ? new LivePresence(SelfId, null, TextPosition.Start, TextPosition.Start, 1)
            : new LivePresence(SelfId, document.Path, document.PositionAt(anchor), document.PositionAt(head), Math.Max(1, topLine));
        if (_ownPresence == presence)
        {
            return;
        }

        _ownPresence = presence;
        if (!_presenceScheduled)
        {
            _presenceScheduled = true;
            _ = SendPresenceLaterAsync();
        }
    }

    /// <summary>Sends edits made on this device to the other devices.</summary>
    internal abstract void SendEdits(LiveDocument document, IReadOnlyList<TextOperation> operations);

    /// <summary>Sends this participant's presence.</summary>
    /// <param name="presence">Where it is.</param>
    private protected abstract void SendPresence(LivePresence presence);

    /// <summary>Adds a document shared on this device.</summary>
    private protected LiveDocument AddDocument(string path, string fullPath, SharedText text)
    {
        var document = new LiveDocument(this, path, fullPath, text);
        _documents[path] = document;
        return document;
    }

    /// <summary>Forgets a document on this device.</summary>
    private protected void RemoveDocument(string path) => _documents.Remove(path);

    /// <summary>Changes who is in the session.</summary>
    private protected void SetParticipants(IReadOnlyList<LiveParticipant> participants, bool isClassroom, int presenter)
    {
        _participants = participants;
        IsClassroom = isClassroom;
        PresenterId = presenter;
        foreach (var gone in _presence.Keys.Where(id => Find(id) is null).ToList())
        {
            _presence.Remove(gone);
        }

        ParticipantsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Records where another participant is.</summary>
    private protected void SetPresence(LivePresence presence)
    {
        if (presence.Participant == SelfId || Find(presence.Participant) is null)
        {
            return;
        }

        _presence[presence.Participant] = presence;
        PresenceChanged?.Invoke(this, presence);
    }

    /// <summary>Forgets where a participant was, when it left.</summary>
    private protected void ClearPresence(int participant) => _presence.Remove(participant);

    /// <summary>Changes what the host runs.</summary>
    private protected void SetRunState(LiveRunState state)
    {
        if (RunState == state)
        {
            return;
        }

        RunState = state;
        RunChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Changes where the debugged program is paused.</summary>
    private protected void SetDebug(LiveDebugState? state)
    {
        Debug = state;
        DebugChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Changes the host's Error List.</summary>
    private protected void SetErrors(IReadOnlyList<LiveDiagnostic> errors)
    {
        Errors = errors;
        ErrorsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Changes the breakpoints.</summary>
    private protected void SetBreakpoints(IReadOnlyList<SwarmBreakpoint> breakpoints)
    {
        Breakpoints = breakpoints;
        BreakpointsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The last of the host's Output since it was last cleared, up to <see cref="LiveProtocol.OutputTailLength"/>
    /// characters, for a window that starts showing the session after it joined.
    /// </summary>
    public IReadOnlyList<LiveOutput> OutputSoFar => _output;

    /// <summary>The last of the host's Terminal, up to <see cref="LiveProtocol.TerminalTailLength"/> bytes.</summary>
    public ReadOnlyMemory<byte> TerminalSoFar => _terminal.ToArray();

    /// <summary>Raises <see cref="OutputWritten"/>.</summary>
    private protected void RaiseOutput(LiveOutput output)
    {
        _output.Add(output);
        _outputLength += output.Text.Length;
        while (_outputLength > LiveProtocol.OutputTailLength && _output.Count > 1)
        {
            _outputLength -= _output[0].Text.Length;
            _output.RemoveAt(0);
        }

        OutputWritten?.Invoke(this, output);
    }

    /// <summary>Raises <see cref="OutputCleared"/>.</summary>
    private protected void RaiseOutputCleared()
    {
        _output.Clear();
        _outputLength = 0;
        OutputCleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Raises <see cref="TerminalWritten"/>.</summary>
    private protected void RaiseTerminal(ReadOnlyMemory<byte> data)
    {
        _terminal.AddRange(data.Span);
        if (_terminal.Count > LiveProtocol.TerminalTailLength)
        {
            _terminal.RemoveRange(0, _terminal.Count - LiveProtocol.TerminalTailLength);
        }

        TerminalWritten?.Invoke(this, data);
    }

    /// <summary>Raises <see cref="DocumentSaved"/>.</summary>
    private protected void RaiseSaved(string path) => DocumentSaved?.Invoke(this, path);

    /// <summary>Ends the session on this device, once.</summary>
    private protected void End(string reason)
    {
        if (HasEnded)
        {
            return;
        }

        HasEnded = true;
        Ended?.Invoke(this, reason);
    }

    private async Task SendPresenceLaterAsync()
    {
        await Task.Delay(PresenceInterval).ConfigureAwait(false);
        Dispatcher.Post(() =>
        {
            _presenceScheduled = false;
            if (_ownPresence is { } presence && !HasEnded)
            {
                SendPresence(presence);
            }
        });
    }
}
