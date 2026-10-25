using System.Net.Sockets;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.WindowsBridge.Protocol;

namespace TabStudio.WindowsBridge.Hosting;

/// <summary>
/// The Windows Bridge on the PC: a TabStudio Link host for tablets that pair with it, which keeps a mirror of each
/// tablet's workspace, builds it with the PC's own tools, runs the programs, streams their windows and plays the
/// tablet's input into them, and tunnels the tablet's debugger to netcoredbg.
/// </summary>
/// <remarks>
/// <para>
/// It pairs exactly like a TabStudio Link host and serves only paired tablets, one at a time. A tablet's workspace
/// and its running programs outlive its connection: after Wi-Fi drops or the PC sleeps, the tablet reconnects and
/// finds its programs still running.
/// </para>
/// <para><see cref="Changed"/> is raised on the thread pool. Every member is thread-safe.</para>
/// </remarks>
public sealed class BridgeServer : IAsyncDisposable
{
    private readonly BridgeServerOptions _options;
    private readonly IBridgeDesktop _desktop;
    private readonly Lock _gate = new();
    private readonly Dictionary<LinkFingerprint, BridgeTablet> _tablets = [];
    private LinkHost? _host;
    private LinkDiscoveryResponder? _discovery;
    private BridgeToolchainReport _toolchains = new([], Builds.BridgeToolset.None, BridgeCapabilities.None);
    private int _disposed;

    /// <summary>Creates the bridge. Call <see cref="StartAsync"/> to look for the tools and listen.</summary>
    /// <param name="options">Who the bridge is and where it keeps its files.</param>
    /// <param name="desktop">The PC's desktop.</param>
    public BridgeServer(BridgeServerOptions options, IBridgeDesktop desktop)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(desktop);
        _options = options;
        _desktop = desktop;
        _desktop.StateChanged += OnDesktopStateChanged;
    }

    /// <summary>Raised on the thread pool when anything the bridge's window shows changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The bridge's options.</summary>
    public BridgeServerOptions Options => _options;

    /// <summary>The PC's desktop.</summary>
    public IBridgeDesktop Desktop => _desktop;

    /// <summary>Whether the bridge is listening.</summary>
    public bool IsRunning => Host() is { IsRunning: true };

    /// <summary>The TCP port the bridge listens on, once started.</summary>
    public int Port => Host()?.Port ?? 0;

    /// <summary>The one-time pairing code, while running.</summary>
    public LinkPairingCode? PairingCode => Host()?.PairingCode;

    /// <summary>The connected tablet, if any.</summary>
    public LinkPeer? ConnectedTablet => Host()?.Client;

    /// <summary>What the bridge found on the PC.</summary>
    public BridgeToolchainReport Toolchains
    {
        get
        {
            lock (_gate)
            {
                return _toolchains;
            }
        }
    }

    /// <summary>What the PC is and can build, as tablets are told.</summary>
    public BridgeInfo Info
    {
        get
        {
            var toolchains = Toolchains;
            return new BridgeInfo(_options.PcName, _desktop.OperatingSystem, toolchains.Toolchains, toolchains.Capabilities, _desktop.State);
        }
    }

    /// <summary>Every program running for a tablet, with the tablet it runs for.</summary>
    public IReadOnlyList<(string Tablet, BridgeAppInfo App)> Apps
    {
        get
        {
            List<BridgeTablet> tablets;
            lock (_gate)
            {
                tablets = [.. _tablets.Values];
            }

            return [.. tablets.SelectMany(t => t.Apps.Select(a => (t.Tablet.Name, a)))];
        }
    }

    /// <summary>What the bridge is doing for the connected tablet, such as <c>Building HelloWpf</c>, or <see langword="null"/>.</summary>
    public string? Activity => ConnectedTablet is { } tablet ? Tablet(tablet.Fingerprint)?.Activity : null;

    /// <summary>Looks for the PC's tools, then listens for tablets.</summary>
    /// <param name="cancellationToken">Gives up looking for tools.</param>
    /// <exception cref="SocketException">No port could be opened.</exception>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        await RefreshToolchainsAsync(cancellationToken).ConfigureAwait(false);
        var host = new LinkHost(
            new LinkHostOptions
            {
                Identity = _options.Identity,
                Clients = _options.Tablets,
                DeviceName = _options.PcName,
                DeviceKind = LinkDeviceKinds.Computer,
                Port = _options.Port,
                LoopbackOnly = _options.LoopbackOnly,
                AppVersion = _options.AppVersion,
            },
            CreateSession);
        host.Start();
        host.ClientChanged += (_, _) => OnClientChanged();
        host.PairingCodeChanged += (_, _) => RaiseChanged();
        host.DevicePaired += (_, _) => RaiseChanged();
        LinkDiscoveryResponder? discovery = null;
        if (!_options.LoopbackOnly)
        {
            discovery = new LinkDiscoveryResponder(_options.Identity.Fingerprint, () => Port, () => _options.PcName);
            discovery.Start(BridgeProtocol.DiscoveryPort);
        }

        lock (_gate)
        {
            _host = host;
            _discovery = discovery;
        }

        RaiseChanged();
    }

    /// <summary>Looks for the PC's tools again, such as after installing Visual Studio or netcoredbg.</summary>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task RefreshToolchainsAsync(CancellationToken cancellationToken = default)
    {
        var locations = _options.Locations ?? BridgeToolchainLocations.ForThisMachine(_options.ToolsFolder);
        var report = await BridgeToolchainDetector.DetectAsync(locations, cancellationToken).ConfigureAwait(false);
        lock (_gate)
        {
            _toolchains = report;
        }

        RaiseChanged();
    }

    /// <summary>Installs netcoredbg, so tablets can debug .NET apps here, and looks for the tools again.</summary>
    /// <param name="http">Downloads the release.</param>
    /// <param name="cancellationToken">Gives up.</param>
    public async Task InstallDebuggerAsync(HttpClient http, CancellationToken cancellationToken = default)
    {
        await NetcoredbgInstaller.InstallAsync(_options.ToolsFolder, http, cancellationToken).ConfigureAwait(false);
        await RefreshToolchainsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The invitation the QR code shows, for the PC's current addresses.</summary>
    /// <param name="addresses">The PC's addresses on the local network, the most likely first.</param>
    public LinkInvitation? CreateInvitation(IReadOnlyList<string> addresses) => Host()?.CreateInvitation(addresses);

    /// <summary>Draws a new pairing code; the shown one stops working.</summary>
    public void RenewPairingCode() => Host()?.RenewPairingCode();

    /// <summary>Disconnects the connected tablet; its programs keep running.</summary>
    public void DisconnectTablet() => Host()?.DisconnectClient();

    /// <summary>Forgets a tablet: it has to pair again. Disconnects it first when connected.</summary>
    /// <param name="tablet">The tablet.</param>
    public void ForgetTablet(LinkPairedDevice tablet)
    {
        ArgumentNullException.ThrowIfNull(tablet);
        if (ConnectedTablet?.Fingerprint == tablet.Fingerprint)
        {
            DisconnectTablet();
        }

        _options.Tablets.Forget(tablet.Fingerprint);
        RaiseChanged();
    }

    /// <summary>Stops a program, from the bridge's own window.</summary>
    /// <param name="appId">The program.</param>
    public void StopApp(int appId)
    {
        List<BridgeTablet> tablets;
        lock (_gate)
        {
            tablets = [.. _tablets.Values];
        }

        foreach (var tablet in tablets)
        {
            tablet.StopApp(appId);
        }
    }

    /// <summary>Stops listening, disconnects the tablet and stops every program it ran.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _desktop.StateChanged -= OnDesktopStateChanged;
        LinkHost? host;
        LinkDiscoveryResponder? discovery;
        List<BridgeTablet> tablets;
        lock (_gate)
        {
            host = _host;
            discovery = _discovery;
            tablets = [.. _tablets.Values];
            _host = null;
            _discovery = null;
            _tablets.Clear();
        }

        discovery?.Dispose();
        if (host is not null)
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }

        foreach (var tablet in tablets)
        {
            tablet.Dispose();
        }

        _desktop.KeepAwake(false);
        RaiseChanged();
    }

    /// <summary>Raises <see cref="Changed"/>, for the tablets' sessions.</summary>
    internal void RaiseChanged()
    {
        var changed = Changed;
        if (changed is not null)
        {
            _ = Task.Run(() => changed(this, EventArgs.Empty));
        }
    }

    private LinkHost? Host()
    {
        lock (_gate)
        {
            return _host;
        }
    }

    private BridgeTablet? Tablet(LinkFingerprint fingerprint)
    {
        lock (_gate)
        {
            return _tablets.GetValueOrDefault(fingerprint);
        }
    }

    /// <summary>Serves an authenticated tablet, with the workspace and programs it had before when it comes back.</summary>
    private BridgeTablet CreateSession(LinkPeer tablet, LinkConnection connection)
    {
        BridgeTablet session;
        lock (_gate)
        {
            if (!_tablets.TryGetValue(tablet.Fingerprint, out var existing))
            {
                var folder = Path.Combine(_options.DataFolder, "Tablets", Convert.ToHexString(tablet.Fingerprint.ToBytes())[..8]);
                existing = new BridgeTablet(this, tablet, folder);
                _tablets[tablet.Fingerprint] = existing;
            }

            session = existing;
        }

        session.Attach(tablet, connection);
        return session;
    }

    private void OnClientChanged()
    {
        var connected = ConnectedTablet;
        if (_options.KeepAwakeWhileConnected)
        {
            _desktop.KeepAwake(connected is not null);
        }

        RaiseChanged();
    }

    private void OnDesktopStateChanged(object? sender, EventArgs e)
    {
        // Tell the tablet at once, before sleep takes the network away.
        if (ConnectedTablet is { } tablet)
        {
            Tablet(tablet.Fingerprint)?.SendStatus();
        }

        RaiseChanged();
    }
}
