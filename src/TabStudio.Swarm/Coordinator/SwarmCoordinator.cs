using System.Net.Sockets;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm.Web;

namespace TabStudio.Swarm.Coordinator;

/// <summary>
/// The tablet's side of a swarm: listens for phones and tablets over TabStudio Link, pairs them with the QR code or
/// the one-time code, and sends every build of the app to all of them at once (<c>SwarmCoordinator.Runs.cs</c>).
/// It debugs the app on every device (<c>SwarmCoordinator.Debugging.cs</c>) and tunnels the web app it serves to them.
/// </summary>
/// <remarks>
/// <para>
/// A swarm is a TabStudio Link host in the <see cref="LinkRole.Swarm"/> role that serves many devices at once: the same
/// TLS with pinned certificates, the same pairing, and the same single-use code, which renews after every device that
/// pairs so the next one scans a fresh code.
/// </para>
/// <para>
/// A device that leaves, fails or stops answering never stops the others: each device is deployed to, debugged and
/// lost on its own. Events are raised on the thread pool. Every member is thread-safe.
/// </para>
/// </remarks>
public sealed partial class SwarmCoordinator : IAsyncDisposable
{
    private readonly SwarmCoordinatorOptions _options;
    private readonly Lock _gate = new();
    private readonly List<SwarmDevice> _devices = [];
    private readonly SwarmWebRelay _relay;
    private readonly CancellationTokenSource _stopping = new();
    private LinkHost? _host;
    private LinkDiscoveryResponder? _discovery;
    private IReadOnlyList<string> _addresses = [];
    private int _disposed;

    /// <summary>Creates a coordinator. Call <see cref="Start"/> to listen.</summary>
    /// <param name="options">Who it is, and where it listens.</param>
    public SwarmCoordinator(SwarmCoordinatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _relay = new SwarmWebRelay(() => CurrentRun?.Build.Web?.Address);
    }

    /// <summary>Raised when a device joined, left, was lost or was removed.</summary>
    public event EventHandler? DevicesChanged;

    /// <summary>Raised when something about a device changed: its state, screen, latency or last update.</summary>
    public event EventHandler<SwarmDevice>? DeviceChanged;

    /// <summary>Raised when the pairing code changed: a device paired, a wrong code was tried, or it expired.</summary>
    public event EventHandler? PairingCodeChanged;

    /// <summary>Raised for output of a console app on a device.</summary>
    public event EventHandler<SwarmOutputEventArgs>? OutputReceived;

    /// <summary>Raised when the app ended on a device, crashed with its stack trace included.</summary>
    public event EventHandler<SwarmExitEventArgs>? AppExited;

    /// <summary>Raised when a device received a build, or could not.</summary>
    public event EventHandler<SwarmDeployment>? Deployed;

    /// <summary>Raised for every input a person made on a device's screen.</summary>
    public event EventHandler<SwarmInputEventArgs>? InputCaptured;

    /// <summary>The coordinator's options.</summary>
    public SwarmCoordinatorOptions Options => _options;

    /// <summary>Whether the coordinator listens for devices.</summary>
    public bool IsListening
    {
        get
        {
            lock (_gate)
            {
                return _host is { IsRunning: true };
            }
        }
    }

    /// <summary>The TCP port the coordinator listens on, while listening.</summary>
    public int Port
    {
        get
        {
            lock (_gate)
            {
                return _host?.Port ?? 0;
            }
        }
    }

    /// <summary>The addresses devices reach the coordinator at, the most likely first.</summary>
    public IReadOnlyList<string> Addresses
    {
        get
        {
            lock (_gate)
            {
                return _addresses;
            }
        }
    }

    /// <summary>The one-time code a device types to join, while listening.</summary>
    public LinkPairingCode? PairingCode => Host()?.PairingCode;

    /// <summary>When the pairing code expires, while listening.</summary>
    public DateTimeOffset? PairingCodeExpires => Host()?.PairingCodeExpires;

    /// <summary>What the QR code shows, while listening on a network.</summary>
    public LinkInvitation? Invitation => Host() is { } host && Addresses is { Count: > 0 } addresses ? host.CreateInvitation(addresses) : null;

    /// <summary>The devices of the swarm, in the order they joined: connected ones, and ones that were lost and may come back.</summary>
    public IReadOnlyList<SwarmDevice> Devices
    {
        get
        {
            lock (_gate)
            {
                return [.. _devices];
            }
        }
    }

    /// <summary>Starts listening for devices.</summary>
    /// <exception cref="SocketException">No port could be opened.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        if (IsListening)
        {
            return;
        }

        var host = new LinkHost(
            new LinkHostOptions
            {
                Identity = _options.Identity,
                Clients = _options.Devices,
                DeviceName = _options.Name,
                DeviceKind = _options.Kind,
                Port = _options.Port,
                LoopbackOnly = _options.LoopbackOnly,
                MaxClients = _options.MaxDevices,
                Role = LinkRole.Swarm,
                AppVersion = _options.AppVersion,
            },
            (peer, connection) => new SwarmDeviceLink(this, peer, connection));
        host.Start();
        host.PairingCodeChanged += (_, _) => PairingCodeChanged?.Invoke(this, EventArgs.Empty);

        LinkDiscoveryResponder? discovery = null;
        if (!_options.LoopbackOnly)
        {
            discovery = new LinkDiscoveryResponder(_options.Identity.Fingerprint, () => Port, () => _options.Name);
            if (!discovery.Start())
            {
                // Another TabStudio on this device answers discovery; devices find the swarm by its known addresses.
                discovery.Dispose();
                discovery = null;
            }
        }

        lock (_gate)
        {
            _host = host;
            _discovery = discovery;
            _addresses = _options.LoopbackOnly ? ["127.0.0.1"] : LinkNetwork.GetLocalAddresses();
        }
    }

    /// <summary>Stops listening, stops the app on every device and disconnects them. They can join again later.</summary>
    public async Task StopAsync()
    {
        await StopRunAsync().ConfigureAwait(false);
        LinkHost? host;
        LinkDiscoveryResponder? discovery;
        lock (_gate)
        {
            host = _host;
            discovery = _discovery;
            _host = null;
            _discovery = null;
            _devices.Clear();
        }

        discovery?.Dispose();
        if (host is not null)
        {
            await host.DisposeAsync().ConfigureAwait(false);
        }

        _relay.CloseAll();
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Reads the network addresses again, for the QR code, after the tablet changed networks.</summary>
    public void RefreshAddresses()
    {
        lock (_gate)
        {
            if (_host is null || _options.LoopbackOnly)
            {
                return;
            }

            _addresses = LinkNetwork.GetLocalAddresses();
        }
    }

    /// <summary>Draws a new pairing code; the shown one stops working.</summary>
    public void RenewPairingCode() => Host()?.RenewPairingCode();

    /// <summary>Takes a device out of the swarm: its app stops, it disconnects and it has to pair again to come back.</summary>
    /// <param name="device">The device.</param>
    public async Task RemoveAsync(SwarmDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        device.IsLeaving = true;
        if (device.Connection is { } connection)
        {
            try
            {
                await connection.NotifyAsync(Protocol.SwarmMethods.Removed).ConfigureAwait(false);
            }
            catch (LinkConnectionLostException)
            {
                // Gone already.
            }

            Host()?.DisconnectClient(device.Fingerprint);
        }

        _options.Devices.Forget(device.Fingerprint);
        Forget(device);
    }

    /// <summary>Stops listening and disconnects every device.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        await StopAsync().ConfigureAwait(false);
        _stopping.Dispose();
    }

    private LinkHost? Host()
    {
        lock (_gate)
        {
            return _host;
        }
    }

    private void Forget(SwarmDevice device)
    {
        bool removed;
        lock (_gate)
        {
            removed = _devices.Remove(device);
        }

        if (removed)
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RaiseDeviceChanged(SwarmDevice device) => DeviceChanged?.Invoke(this, device);
}
