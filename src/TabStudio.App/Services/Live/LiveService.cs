using TabStudio.App.Services.Link;
using TabStudio.Link.Networking;
using TabStudio.Live;
using TabStudio.Live.Sessions;

namespace TabStudio.App.Services.Live;

/// <summary>
/// TabStudio Live in the app: the one session this device is in, as its host or as a guest, with the same identity as
/// TabStudio Link. A guest's copy of the shared workspace is kept under <c>Cache/Live</c> and deleted when the session
/// ends; copies left by a session the app did not end are deleted when the app starts.
/// </summary>
/// <remarks>The session lives on the UI thread, and every event is raised there.</remarks>
public sealed class LiveService : IAsyncDisposable, IDisposable
{
    private readonly AppPaths _paths;
    private readonly LinkService _link;

    /// <summary>Creates the service; no session runs until one is shared or joined.</summary>
    /// <param name="paths">Where guests' copies go.</param>
    /// <param name="link">TabStudio Link, whose identity and device name a session uses.</param>
    /// <param name="ui">The UI thread, where sessions live.</param>
    public LiveService(AppPaths paths, LinkService link, IUiDispatcher ui)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(ui);
        _paths = paths;
        _link = link;
        Dispatcher = new UiLiveDispatcher(ui);
    }

    /// <summary>Raised on the UI thread when a session started or ended, or a guest's state changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised on the UI thread when this device joined a session: the IDE opens the shared workspace.</summary>
    public event EventHandler<LiveGuest>? GuestJoined;

    /// <summary>Raised on the UI thread when this device's guest session ended, with why: the IDE closes the copy.</summary>
    public event EventHandler<string>? GuestEnded;

    /// <summary>Raised on the UI thread while a guest copies the shared files: how many it has, of how many.</summary>
    public event EventHandler<(int Done, int Total)>? GuestCopyProgress;

    /// <summary>
    /// Whether sessions listen on the loopback address only and on any free port, as the tests and the emulator's self
    /// test share sessions between participants in one process.
    /// </summary>
    public bool LoopbackOnly { get; set; }

    /// <summary>The session's thread: the UI thread.</summary>
    public ILiveDispatcher Dispatcher { get; }

    /// <summary>This device's name, as other participants see it.</summary>
    public string DeviceName => _link.DeviceName;

    /// <summary>The session this device hosts, if it hosts one.</summary>
    public LiveHost? Host { get; private set; }

    /// <summary>The session this device joined, if it joined one.</summary>
    public LiveGuest? Guest { get; private set; }

    /// <summary>The session this device is in, as host or guest.</summary>
    public LiveSession? Session => Host is not null ? Host : Guest;

    /// <summary>Why the last join failed, if it did.</summary>
    public string? JoinError { get; private set; }

    /// <summary>The folder of guests' copies of shared workspaces.</summary>
    public string CopiesDirectory => Path.Combine(_paths.Cache, "Live");

    /// <summary>
    /// Shares a workspace: starts listening for guests. Call on the UI thread.
    /// </summary>
    /// <param name="root">The workspace's folder; guests see nothing outside it.</param>
    /// <param name="name">The workspace's name.</param>
    /// <param name="entryPath">The solution guests open, relative to the folder, or <see langword="null"/> for the folder.</param>
    /// <param name="workspace">The IDE, which serves guests' requests.</param>
    /// <param name="loopbackOnly">Listens on the loopback address only, for tests and the self test.</param>
    /// <exception cref="InvalidOperationException">This device is in a session already.</exception>
    /// <exception cref="IOException">The workspace is too large to share.</exception>
    /// <exception cref="System.Net.Sockets.SocketException">No port could be opened.</exception>
    public async Task<LiveHost> StartHostingAsync(string root, string name, string? entryPath, ILiveHostWorkspace workspace, bool loopbackOnly = false)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        if (Session is { HasEnded: false })
        {
            throw new InvalidOperationException("This device is in a session already. End it first.");
        }

        var host = new LiveHost(
            new LiveHostOptions
            {
                Identity = _link.Identity,
                Name = _link.DeviceName,
                Kind = _link.DeviceKind,
                WorkspaceRoot = root,
                WorkspaceName = name,
                EntryPath = entryPath,
                Port = loopbackOnly || LoopbackOnly ? 0 : LiveProtocol.DefaultPort,
                LoopbackOnly = loopbackOnly || LoopbackOnly,
            },
            workspace,
            Dispatcher);
        try
        {
            await host.StartAsync();
        }
        catch
        {
            await host.DisposeAsync();
            throw;
        }

        host.Ended += (_, _) =>
        {
            if (ReferenceEquals(Host, host))
            {
                Host = null;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        };
        Host = host;
        Changed?.Invoke(this, EventArgs.Empty);
        return host;
    }

    /// <summary>
    /// Joins a session by its scanned QR code or its typed address and code. On success <see cref="GuestJoined"/> is
    /// raised and the IDE opens the shared workspace. Call on the UI thread.
    /// </summary>
    /// <param name="target">The host and the session's code.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether this device joined; <see cref="JoinError"/> says why not.</returns>
    public async Task<bool> JoinAsync(LinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Session is { HasEnded: false })
        {
            JoinError = Host is not null
                ? "This device shares a session. End it before joining another."
                : "This device is in a session already. Leave it before joining another.";
            return false;
        }

        var guest = new LiveGuest(
            new LiveGuestOptions
            {
                Identity = _link.Identity,
                Name = _link.DeviceName,
                Kind = _link.DeviceKind,
                MirrorDirectory = CopiesDirectory,
            },
            Dispatcher);
        guest.StateChanged += OnGuestStateChanged;
        guest.CopyProgress += OnGuestCopyProgress;
        Guest = guest;
        JoinError = null;
        Changed?.Invoke(this, EventArgs.Empty);
        if (await guest.JoinAsync(target, cancellationToken))
        {
            guest.Ended += (_, reason) => OnGuestEnded(guest, reason);
            Changed?.Invoke(this, EventArgs.Empty);
            GuestJoined?.Invoke(this, guest);
            return true;
        }

        JoinError = guest.Error;
        guest.StateChanged -= OnGuestStateChanged;
        guest.CopyProgress -= OnGuestCopyProgress;
        Guest = null;
        await guest.DisposeAsync();
        Changed?.Invoke(this, EventArgs.Empty);
        return false;
    }

    /// <summary>Ends the session this device is in: the host ends it for everyone, a guest leaves it.</summary>
    public async Task EndAsync()
    {
        if (Host is { } host)
        {
            await host.EndAsync();
        }
        else if (Guest is { } guest)
        {
            await guest.LeaveAsync();
        }
    }

    /// <summary>
    /// Deletes this device's copy of a workspace a guest session shared, once the IDE closed it. Copies of earlier
    /// sessions go too.
    /// </summary>
    public void DeleteCopies()
    {
        if (Guest is { HasEnded: false })
        {
            return;
        }

        Guest?.DeleteCopy();
        Guest = null;
        try
        {
            if (Directory.Exists(CopiesDirectory))
            {
                Directory.Delete(CopiesDirectory, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: they are deleted again the next time the app starts.
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Whether a path is inside the copy of the workspace this device joined.</summary>
    /// <param name="path">A solution, project, folder or file.</param>
    public bool IsGuestCopy(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (Guest is not { WorkspaceRoot: { } root } || Guest.HasEnded)
        {
            return false;
        }

        return IsInside(path, root);
    }

    /// <summary>Whether a path is inside any copy of a shared workspace, of this session or of an earlier one.</summary>
    /// <param name="path">A solution, project, folder or file.</param>
    public bool IsCopy(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return IsInside(path, CopiesDirectory);
    }

    private static bool IsInside(string path, string folder)
    {
        var full = Path.GetFullPath(path);
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        return string.Equals(full, prefix, StringComparison.Ordinal) || full.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ends the session without waiting, as the app's container disposes its services on the UI thread, which the
    /// session's last steps run on.
    /// </summary>
    public void Dispose() => _ = DisposeAsync().AsTask();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Host is { } host)
        {
            await host.DisposeAsync();
        }

        if (Guest is { } guest)
        {
            await guest.DisposeAsync();
        }
    }

    private void OnGuestStateChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

    private void OnGuestCopyProgress(object? sender, (int Done, int Total) progress) => GuestCopyProgress?.Invoke(this, progress);

    private void OnGuestEnded(LiveGuest guest, string reason)
    {
        if (ReferenceEquals(Guest, guest))
        {
            GuestEnded?.Invoke(this, reason);
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The UI thread, as a session's thread.</summary>
    private sealed class UiLiveDispatcher(IUiDispatcher ui) : ILiveDispatcher
    {
        public void Post(Action action) => ui.Post(action);
    }
}
