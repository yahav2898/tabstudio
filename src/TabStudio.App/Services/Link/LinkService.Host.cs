using System.Net.Sockets;
using TabStudio.App.Platform;
using TabStudio.Link;
using TabStudio.Link.Language;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;

namespace TabStudio.App.Services.Link;

/// <summary>
/// The host's role: listening for a tablet, showing the pairing code, and compiling the tablet's workspace with this
/// device's Roslyn. Each tablet keeps its language service across reconnections, for ten minutes after it left.
/// </summary>
public sealed partial class LinkService
{
    /// <summary>How long a tablet's compilations are kept after it disconnected, for a quick reconnect.</summary>
    private static readonly TimeSpan KeepTabletFor = TimeSpan.FromMinutes(10);

    private readonly Dictionary<LinkFingerprint, TabletSession> _tablets = [];
    private LinkHost? _host;
    private LinkDiscoveryResponder? _discovery;
    private IReadOnlyList<string> _hostAddresses = [];
    private string? _hostError;

    /// <summary>Whether this device is a host, listening for a tablet.</summary>
    public bool IsHosting
    {
        get
        {
            lock (_gate)
            {
                return _host is { IsRunning: true };
            }
        }
    }

    /// <summary>The addresses tablets reach this host at, the most likely first.</summary>
    public IReadOnlyList<string> HostAddresses
    {
        get
        {
            lock (_gate)
            {
                return _hostAddresses;
            }
        }
    }

    /// <summary>The TCP port the host listens on, while hosting.</summary>
    public int HostPort
    {
        get
        {
            lock (_gate)
            {
                return _host?.Port ?? 0;
            }
        }
    }

    /// <summary>The one-time code a tablet types to pair, while hosting.</summary>
    public LinkPairingCode? PairingCode => CurrentHost()?.PairingCode;

    /// <summary>What the QR code shows, while hosting.</summary>
    public LinkInvitation? Invitation => CurrentHost() is { } host ? host.CreateInvitation(HostAddresses) : null;

    /// <summary>The tablet this host serves right now.</summary>
    public LinkPeer? ServedTablet => CurrentHost()?.Client;

    /// <summary>What the host does for its tablet, such as <c>Building HelloTablet</c>, or <see langword="null"/> when idle.</summary>
    public string? HostActivity => ServedTablet is { } tablet ? Tablet(tablet.Fingerprint)?.Server.Activity : null;

    /// <summary>The workspace the served tablet has open, once loaded.</summary>
    public string? ServedWorkspace => ServedTablet is { } tablet ? Tablet(tablet.Fingerprint)?.Server.WorkspaceName : null;

    /// <summary>How many requests the host answered for the served tablet.</summary>
    public long ServedRequests => ServedTablet is { } tablet ? Tablet(tablet.Fingerprint)?.Server.RequestCount ?? 0 : 0;

    /// <summary>Why hosting could not start, if it could not.</summary>
    public string? HostError
    {
        get
        {
            lock (_gate)
            {
                return _hostError;
            }
        }
    }

    /// <summary>Why the host holds back because of its temperature, or <see langword="null"/>.</summary>
    public string? HostThermalNote => ThermalNote(_deviceStatus.Thermal);

    /// <summary>Starts listening for tablets.</summary>
    /// <param name="port">The port to listen on when it is free.</param>
    /// <param name="loopbackOnly">Listens on this device only, for tests and the self test.</param>
    /// <returns>Whether hosting started; <see cref="HostError"/> says why not.</returns>
    public async Task<bool> StartHostingAsync(int port = LinkProtocol.DefaultPort, bool loopbackOnly = false)
    {
        if (IsHosting)
        {
            return true;
        }

        var host = new LinkHost(
            new LinkHostOptions
            {
                Identity = Identity,
                Clients = PairedTablets,
                DeviceName = DeviceName,
                DeviceKind = DeviceKind,
                Port = port,
                LoopbackOnly = loopbackOnly,
            },
            CreateSession);
        try
        {
            host.Start();
        }
        catch (SocketException ex)
        {
            await host.DisposeAsync().ConfigureAwait(false);
            lock (_gate)
            {
                _hostError = $"TabStudio Link could not open a network port: {ex.Message}";
            }

            RaiseChanged();
            return false;
        }

        host.PairingCodeChanged += (_, _) => RaiseChanged();
        host.ClientChanged += (_, _) => OnServedTabletChanged();
        host.DevicePaired += (_, _) => RaiseChanged();
        var discovery = loopbackOnly ? null : new LinkDiscoveryResponder(Identity.Fingerprint, () => HostPort, () => DeviceName);
        discovery?.Start();
        lock (_gate)
        {
            _host = host;
            _discovery = discovery;
            _hostError = null;
            _hostAddresses = loopbackOnly ? ["127.0.0.1"] : LinkNetwork.GetLocalAddresses();
        }

        RaiseChanged();
        return true;
    }

    /// <summary>Stops hosting: the tablet falls back to compiling on its own, and the compilations are released.</summary>
    public async Task StopHostingAsync()
    {
        LinkHost? host;
        LinkDiscoveryResponder? discovery;
        List<TabletSession> tablets;
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

        RaiseChanged();
    }

    /// <summary>Draws a new pairing code; the shown one stops working.</summary>
    public void RenewPairingCode() => CurrentHost()?.RenewPairingCode();

    /// <summary>Reads the network addresses again, for the QR code, after the device changed networks.</summary>
    public void RefreshHostAddresses()
    {
        lock (_gate)
        {
            if (_host is null || _host.Options.LoopbackOnly)
            {
                return;
            }

            _hostAddresses = LinkNetwork.GetLocalAddresses();
        }

        RaiseChanged();
    }

    /// <summary>Disconnects the served tablet, which falls back to compiling on its own.</summary>
    public void DisconnectTablet() => CurrentHost()?.DisconnectClient();

    /// <summary>Forgets a tablet: it has to pair again. Disconnects it first when it is served.</summary>
    /// <param name="tablet">The tablet.</param>
    public void ForgetTablet(LinkPairedDevice tablet)
    {
        ArgumentNullException.ThrowIfNull(tablet);
        if (ServedTablet?.Fingerprint == tablet.Fingerprint)
        {
            DisconnectTablet();
        }

        TabletSession? session;
        lock (_gate)
        {
            _tablets.Remove(tablet.Fingerprint, out session);
        }

        session?.Dispose();
        PairedTablets.Forget(tablet.Fingerprint);
    }

    /// <summary>
    /// Starts a host inside this app, on the loopback address, with an identity of its own, for checking the whole link
    /// on one device. It does not pair by itself: pair with its <see cref="LinkLoopbackHost.Invitation"/>.
    /// </summary>
    /// <param name="name">The host's name, as the status bar shows it.</param>
    /// <param name="kind">What the host is, one of <see cref="LinkDeviceKinds"/>.</param>
    internal LinkLoopbackHost CreateLoopbackHost(string name, string kind = LinkDeviceKinds.Computer) =>
        new(_compiler, Path.Combine(_paths.Cache, "Link", "Loopback"), name, kind);

    private LinkHost? CurrentHost()
    {
        lock (_gate)
        {
            return _host;
        }
    }

    private TabletSession? Tablet(LinkFingerprint fingerprint)
    {
        lock (_gate)
        {
            return _tablets.GetValueOrDefault(fingerprint);
        }
    }

    /// <summary>Serves an authenticated tablet with its language server, made the first time it connects.</summary>
    private LinkLanguageServer CreateSession(LinkPeer tablet, LinkConnection connection)
    {
        TabletSession session;
        lock (_gate)
        {
            if (!_tablets.TryGetValue(tablet.Fingerprint, out var existing))
            {
                var folder = Path.Combine(_paths.Cache, "Link", "Tablets", Convert.ToHexString(tablet.Fingerprint.ToBytes())[..16]);
                existing = new TabletSession(this, folder);
                _tablets[tablet.Fingerprint] = existing;
            }

            session = existing;
        }

        session.Attach(connection);
        return session.Server;
    }

    private void OnServedTabletChanged()
    {
        var served = ServedTablet;
        List<TabletSession> sessions;
        lock (_gate)
        {
            sessions = [.. _tablets.Values];
        }

        foreach (var session in sessions)
        {
            if (served is null || session != Tablet(served.Fingerprint))
            {
                session.Detach(KeepTabletFor, ForgetSession);
            }
        }

        RaiseChanged();
    }

    private void ForgetSession(TabletSession session)
    {
        lock (_gate)
        {
            foreach (var (fingerprint, existing) in _tablets)
            {
                if (ReferenceEquals(existing, session))
                {
                    _tablets.Remove(fingerprint);
                    break;
                }
            }
        }

        session.Dispose();
    }

    private void OnThermalChanged(object? sender, EventArgs e)
    {
        List<TabletSession> sessions;
        lock (_gate)
        {
            sessions = [.. _tablets.Values];
        }

        foreach (var session in sessions)
        {
            session.ApplyThermal();
        }

        RaiseChanged();
    }

    private static string? ThermalNote(ThermalState thermal) => thermal switch
    {
        ThermalState.Normal => null,
        ThermalState.Warm => "Warm · building on one core",
        _ => "Hot · building on one core",
    };

    /// <summary>One tablet's language server and its language service, and the connection it is served on.</summary>
    private sealed class TabletSession : IDisposable
    {
        private readonly LinkService _owner;
        private readonly Lock _gate = new();
        private LinkConnection? _connection;
        private LocalLinkBackend? _backend;
        private CancellationTokenSource? _expiry;

        public TabletSession(LinkService owner, string mirrorFolder)
        {
            _owner = owner;
            Server = new LinkLanguageServer(mirrorFolder, CreateBackendAsync);
            Server.ActivityChanged += (_, _) =>
            {
                PushStatus();
                _owner.RaiseChanged();
            };
        }

        public LinkLanguageServer Server { get; }

        public void Attach(LinkConnection connection)
        {
            lock (_gate)
            {
                _connection = connection;
                _expiry?.Cancel();
                _expiry?.Dispose();
                _expiry = null;
            }

            PushStatus();
        }

        /// <summary>Forgets the connection, and the whole session when the tablet does not come back in time.</summary>
        public void Detach(TimeSpan keepFor, Action<TabletSession> expire)
        {
            CancellationTokenSource expiry;
            lock (_gate)
            {
                if (_connection is null || _expiry is not null)
                {
                    return;
                }

                _connection = null;
                expiry = new CancellationTokenSource();
                _expiry = expiry;
            }

            _ = Task.Delay(keepFor, expiry.Token).ContinueWith(
                task =>
                {
                    if (!task.IsCanceled)
                    {
                        expire(this);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public void ApplyThermal()
        {
            LocalLinkBackend? backend;
            lock (_gate)
            {
                backend = _backend;
            }

            backend?.Language.SetConcurrentBuild(_owner._deviceStatus.Thermal == ThermalState.Normal);
            PushStatus();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _expiry?.Cancel();
                _expiry?.Dispose();
                _expiry = null;
                _connection = null;
                _backend = null;
            }

            Server.Dispose();
        }

        private async Task<ILinkLanguageBackend> CreateBackendAsync(CancellationToken cancellationToken)
        {
            var roslyn = await _owner._compiler.CreateHostAsync().ConfigureAwait(false);
            var language = new LocalLanguageService(roslyn, _owner._compiler.Packages);
            if (_owner._profile.UseSkeletonReferences)
            {
                language.UseSkeletonReferences(Path.Combine(_owner._paths.Cache, "Skeletons"));
            }

            language.SetConcurrentBuild(_owner._deviceStatus.Thermal == ThermalState.Normal);
            var backend = new LocalLinkBackend(language);
            lock (_gate)
            {
                _backend = backend;
            }

            return backend;
        }

        /// <summary>Tells the tablet what the host is doing, so it can show it.</summary>
        private void PushStatus()
        {
            LinkConnection? connection;
            lock (_gate)
            {
                connection = _connection;
            }

            if (connection is not { IsOpen: true })
            {
                return;
            }

            var status = new LinkHostStatus(Server.Activity, ThermalNote(_owner._deviceStatus.Thermal), Environment.ProcessorCount);
            _ = SendAsync(connection, status);
        }

        private static async Task SendAsync(LinkConnection connection, LinkHostStatus status)
        {
            try
            {
                await connection.NotifyAsync(LinkHostStatus.Method, status.ToJson()).ConfigureAwait(false);
            }
            catch (LinkConnectionLostException)
            {
                // The tablet left; it gets the status when it comes back.
            }
        }
    }
}
