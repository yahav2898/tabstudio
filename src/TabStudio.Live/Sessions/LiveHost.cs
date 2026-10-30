using System.Text.Json;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Live.Files;
using TabStudio.Live.Protocol;
using TabStudio.Live.Text;

namespace TabStudio.Live.Sessions;

/// <summary>
/// The device that shares its workspace: listens for guests over TabStudio Link, pairs them with the session's QR code
/// or code, serves them the shared files and documents, merges everyone's edits, and passes on where everyone is and
/// what it runs (<c>LiveHost.*.cs</c>).
/// </summary>
/// <remarks>
/// <para>
/// A session is a TabStudio Link host in the <see cref="LinkRole.Live"/> role that serves many devices at once, with
/// the same TLS 1.3 and pinned certificates, the same pairing proofs, and a code that stays the same while it is shown,
/// so a whole class can join from one projected QR code. A removed guest cannot come back into the same session.
/// </para>
/// <para>
/// The host is the session's authority: every edit passes through it, in the order it applied them, so every guest
/// gets every edit after what it depends on; it checks every request against <see cref="LivePermissions"/>, and serves
/// files only from the shared folder. State lives on <see cref="LiveSession.Dispatcher"/>'s thread.
/// </para>
/// </remarks>
public sealed partial class LiveHost : LiveSession, IAsyncDisposable
{
    /// <summary>The host's number in its session.</summary>
    public const int HostId = 1;

    private readonly LiveHostOptions _options;
    private readonly ILiveHostWorkspace _workspace;
    private readonly LiveWorkspaceFiles _files;
    private readonly Dictionary<string, LiveFileEntry> _shared = new(StringComparer.Ordinal);
    private readonly Dictionary<LinkFingerprint, Guest> _guests = [];

    /// <summary>Guests whose connection dropped longer ago than the reconnect grace: off the list, but still known.</summary>
    private readonly Dictionary<LinkFingerprint, Guest> _away = [];
    private readonly HashSet<LinkFingerprint> _removed = [];
    private readonly LinkTrustStore _trust = new(null);
    private readonly CancellationTokenSource _stopping = new();
    private LinkHost? _link;
    private LinkDiscoveryResponder? _discovery;
    private IReadOnlyList<string> _addresses = [];
    private int _nextId = HostId + 1;
    private int _disposed;

    /// <summary>Creates a session for a workspace. Call <see cref="StartAsync"/> to share it.</summary>
    /// <param name="options">Who the host is, what it shares and where it listens.</param>
    /// <param name="workspace">The host's IDE.</param>
    /// <param name="dispatcher">The session's thread: the UI thread in the app.</param>
    public LiveHost(LiveHostOptions options, ILiveHostWorkspace workspace, ILiveDispatcher dispatcher)
        : base(dispatcher)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(workspace);
        _options = options;
        _workspace = workspace;
        _files = new LiveWorkspaceFiles(options.WorkspaceRoot);
        WorkspaceRoot = _files.Root;
        WorkspaceName = options.WorkspaceName;
        HostName = LiveNames.Clean(options.Name);
        DefaultRole = options.DefaultRole == LiveRole.Viewer ? LiveRole.Viewer : LiveRole.Editor;
        SelfId = HostId;
        IsClassroom = options.IsClassroom;
        PresenterId = HostId;
        SetParticipants(BuildParticipants(), IsClassroom, PresenterId);
    }

    /// <summary>Raised on the session's thread when the session's code changed, so the QR code is drawn again.</summary>
    public event EventHandler? PairingCodeChanged;

    /// <summary>Raised on the session's thread when a guest joined for the first time.</summary>
    public event EventHandler<LiveParticipant>? GuestJoined;

    /// <inheritdoc />
    public override bool IsHost => true;

    /// <summary>The host's options.</summary>
    public LiveHostOptions Options => _options;

    /// <summary>What a guest may do when it joins; the host can change each guest afterwards.</summary>
    public LiveRole DefaultRole { get; set; }

    /// <summary>Whether the session takes no new guests; guests in it can still reconnect.</summary>
    public bool IsLocked { get; set; }

    /// <summary>Whether the host listens for guests.</summary>
    public bool IsListening => Volatile.Read(ref _link) is { IsRunning: true };

    /// <summary>The TCP port the host listens on, while listening.</summary>
    public int Port => Volatile.Read(ref _link)?.Port ?? 0;

    /// <summary>The addresses guests reach the host at, the most likely first.</summary>
    public IReadOnlyList<string> Addresses => Volatile.Read(ref _addresses);

    /// <summary>The code a guest types to join, while listening.</summary>
    public LinkPairingCode? PairingCode => Volatile.Read(ref _link)?.PairingCode;

    /// <summary>When the code changes, while listening.</summary>
    public DateTimeOffset? PairingCodeExpires => Volatile.Read(ref _link)?.PairingCodeExpires;

    /// <summary>What the QR code shows, while listening on a network.</summary>
    public LinkInvitation? Invitation =>
        Volatile.Read(ref _link) is { } link && Addresses is { Count: > 0 } addresses ? link.CreateInvitation(addresses) : null;

    /// <summary>The files the session shares.</summary>
    public IReadOnlyCollection<LiveFileEntry> SharedFiles => _shared.Values;

    /// <summary>How many guests are connected now.</summary>
    public int ConnectedGuests => _guests.Values.Count(g => g.Connection is not null);

    /// <summary>
    /// Lists the shared files and starts listening for guests. Call on the session's thread.
    /// </summary>
    /// <param name="cancellationToken">Gives up listing.</param>
    /// <exception cref="IOException">The workspace is too large to share.</exception>
    /// <exception cref="System.Net.Sockets.SocketException">No port could be opened.</exception>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        var files = await Task.Run(() => _files.Collect(cancellationToken), cancellationToken).ConfigureAwait(false);
        var link = new LinkHost(
            new LinkHostOptions
            {
                Identity = _options.Identity,
                Clients = _trust,
                DeviceName = HostName,
                DeviceKind = _options.Kind,
                Port = _options.Port,
                LoopbackOnly = _options.LoopbackOnly,
                MaxClients = Math.Clamp(_options.MaxGuests, 1, LiveProtocol.MaxGuests),
                Role = LinkRole.Live,
                AppVersion = _options.AppVersion,
                KeepsPairingCode = true,
                WrongCodesBeforeRenewal = 5,
                HandshakeQueueTimeout = TimeSpan.FromSeconds(15),
                PairingCodeLifetime = _options.PairingCodeLifetime,
            },
            (peer, connection) => new GuestHandler(this, peer, connection));
        link.PairingCodeChanged += (_, _) => Dispatcher.Post(() => PairingCodeChanged?.Invoke(this, EventArgs.Empty));
        link.Start();

        LinkDiscoveryResponder? discovery = null;
        if (!_options.LoopbackOnly)
        {
            discovery = new LinkDiscoveryResponder(_options.Identity.Fingerprint, () => Port, () => HostName);
            if (!discovery.Start())
            {
                // Another TabStudio on this device answers discovery; guests reconnect by the addresses they know.
                discovery.Dispose();
                discovery = null;
            }
        }

        IReadOnlyList<string> addresses = _options.LoopbackOnly ? ["127.0.0.1"] : LinkNetwork.GetLocalAddresses();
        await Dispatcher.InvokeAsync(() =>
        {
            foreach (var file in files)
            {
                _shared[file.Path] = file;
            }

            Volatile.Write(ref _addresses, addresses);
            Volatile.Write(ref _link, link);
            _discovery = discovery;
            StartWatching();
            PairingCodeChanged?.Invoke(this, EventArgs.Empty);
        }).ConfigureAwait(false);
    }

    /// <summary>Reads the network addresses again, for the QR code, after the device changed networks.</summary>
    public void RefreshAddresses()
    {
        if (!_options.LoopbackOnly)
        {
            Volatile.Write(ref _addresses, LinkNetwork.GetLocalAddresses());
        }
    }

    /// <summary>Draws a new code; the shown one stops working for guests that have not joined yet.</summary>
    public void RenewPairingCode() => Volatile.Read(ref _link)?.RenewPairingCode();

    /// <summary>
    /// Shares a document the host opened: guests that open it get its edits from now on. A document already shared is
    /// returned as it is. Call on the session's thread.
    /// </summary>
    /// <param name="fullPath">The document's file.</param>
    /// <param name="text">The editor's text.</param>
    /// <returns>The shared document, or <see langword="null"/> when the file is outside the shared folder.</returns>
    public LiveDocument? Share(string fullPath, string text)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        ArgumentNullException.ThrowIfNull(text);
        if (HasEnded || RelativePathOf(fullPath) is not { } path || LiveWorkspacePaths.ToFullPath(WorkspaceRoot!, path) is not { } full)
        {
            return null;
        }

        return FindDocument(path) ?? CreateDocument(path, full, text);
    }

    /// <inheritdoc />
    public override Task<LiveDocument?> OpenDocumentAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        return Task.FromResult(OpenShared(path));
    }

    /// <inheritdoc />
    public override void CloseDocument(LiveDocument document)
    {
        // The host keeps its documents shared: a guest may still be editing one the host closed.
    }

    /// <summary>
    /// Tells guests that a shared file was saved on the host, so their copies of it change and their editors stop
    /// showing it as unsaved. Call on the session's thread after the host's own Save.
    /// </summary>
    /// <param name="fullPath">The file.</param>
    public void NotifySaved(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        if (RelativePathOf(fullPath) is not { } path || _files.Find(path) is not { } entry)
        {
            return;
        }

        _shared[path] = entry;
        Broadcast(LiveMethods.Saved, LiveJson.Body(new LiveFilesNotice([entry], []), LiveJsonContext.Default.LiveFilesNotice));
        RaiseSaved(path);
    }

    /// <summary>Ends the session: every guest is told and disconnected, and the code stops working.</summary>
    public async Task EndAsync()
    {
        if (HasEnded)
        {
            return;
        }

        var notice = LiveJson.Body(new LiveEndNotice($"{HostName} ended the session."), LiveJsonContext.Default.LiveEndNotice);
        var flushes = new List<Task>();
        foreach (var guest in _guests.Values)
        {
            if (guest.Sender is { } sender)
            {
                sender.Notify(LiveMethods.Ended, notice);
                flushes.Add(sender.FlushAsync());
            }
        }

        await Task.WhenAny(Task.WhenAll(flushes), Task.Delay(TimeSpan.FromSeconds(2))).ConfigureAwait(false);
        await Dispatcher.InvokeAsync(() => End($"You ended the session for {_guests.Count} guests.")).ConfigureAwait(false);
        await DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Stops listening and disconnects every guest.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        StopWatching();
        _discovery?.Dispose();
        if (Volatile.Read(ref _link) is { } link)
        {
            await link.DisposeAsync().ConfigureAwait(false);
        }

        _stopping.Dispose();
    }

    /// <summary>Everyone in the session, the host first, as the list shows them.</summary>
    private List<LiveParticipant> BuildParticipants()
    {
        var participants = new List<LiveParticipant>(_guests.Count + 1)
        {
            new(HostId, HostName, _options.Kind, 0, LiveRole.Host)
            {
                CanStep = true,
                CanUseTerminal = true,
                IsPresenter = IsClassroom && PresenterId == HostId,
            },
        };
        participants.AddRange(_guests.Values.Select(g => g.Participant).OrderBy(p => p.Id));
        return participants;
    }

    /// <summary>Tells everyone who is in the session.</summary>
    private void PublishParticipants()
    {
        var participants = BuildParticipants();
        SetParticipants(participants, IsClassroom, PresenterId);
        Broadcast(LiveMethods.Participants, LiveJson.Body(new LiveParticipantsNotice(participants, IsClassroom, PresenterId), LiveJsonContext.Default.LiveParticipantsNotice));
    }

    /// <summary>Sends a notification to every connected guest, or every one but one.</summary>
    private void Broadcast(string method, JsonElement? body, Guest? except = null, ReadOnlyMemory<byte>? attachment = null)
    {
        foreach (var guest in _guests.Values)
        {
            if (!ReferenceEquals(guest, except) && guest.Sender is { } sender)
            {
                sender.Notify(method, body, attachment);
            }
        }
    }

    /// <summary>The color of a new guest: the first one nobody connected has.</summary>
    private int NextColor()
    {
        var used = _guests.Values.Where(g => g.Connection is not null).Select(g => g.Participant.Color).ToHashSet();
        for (var color = 1; color < LiveProtocol.Colors; color++)
        {
            if (!used.Contains(color))
            {
                return color;
            }
        }

        return 1 + (_guests.Count % (LiveProtocol.Colors - 1));
    }

    private Guest? FindGuest(int id)
    {
        foreach (var guest in _guests.Values)
        {
            if (guest.Id == id)
            {
                return guest;
            }
        }

        return null;
    }

    private bool IsShared(string path) => _shared.ContainsKey(path) || FindDocument(path) is not null;

    /// <summary>A guest of the session, connected or waiting to reconnect.</summary>
    private sealed class Guest(LinkFingerprint fingerprint, LiveParticipant participant)
    {
        /// <summary>The guest's certificate, by which it is known when it reconnects.</summary>
        public LinkFingerprint Fingerprint { get; } = fingerprint;

        /// <summary>The guest as everyone sees it.</summary>
        public LiveParticipant Participant { get; set; } = participant;

        /// <summary>Its connection, while connected.</summary>
        public LinkConnection? Connection { get; set; }

        /// <summary>Its queue of notifications, while connected.</summary>
        public LiveSender? Sender { get; set; }

        /// <summary>The documents it has open, whose edits it gets.</summary>
        public HashSet<string> Documents { get; } = new(StringComparer.Ordinal);

        /// <summary>What it could do before classroom mode made it a viewer.</summary>
        public LiveRole RoleBeforeClassroom { get; set; } = LiveRole.Editor;

        /// <summary>Its number.</summary>
        public int Id => Participant.Id;
    }
}
