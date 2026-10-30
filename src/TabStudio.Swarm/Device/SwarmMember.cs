using System.Net.Sockets;
using System.Text.Json;
using TabStudio.Link;
using TabStudio.Link.Messaging;
using TabStudio.Link.Networking;
using TabStudio.Link.Security;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Device;

/// <summary>
/// A device's side of a swarm: joins a tablet's swarm with its QR code or one-time code, stays joined, reconnects by
/// itself when Wi-Fi drops, and runs what the tablet sends (<c>SwarmMember.Runs.cs</c>), answering the tablet's
/// requests about the app, its debugger and the screen (<c>SwarmMember.Requests.cs</c>).
/// </summary>
/// <remarks>
/// Joining uses TabStudio Link's pairing in the <see cref="LinkRole.Swarm"/> role: the same TLS with pinned
/// certificates and the same single-use code. Events are raised on the thread pool. Every member is thread-safe.
/// </remarks>
public sealed partial class SwarmMember : IAsyncDisposable
{
    private readonly SwarmMemberOptions _options;
    private readonly ISwarmDeviceHost _host;
    private readonly SwarmFileCache _cache;
    private readonly Lock _gate = new();
    private LinkClientSession? _session;
    private LinkPairedDevice? _coordinator;
    private CancellationTokenSource? _reconnect;
    private SwarmMembership _membership;
    private string? _coordinatorName;
    private string? _error;
    private int _disposed;

    /// <summary>Creates the device's side of a swarm. Nothing connects until asked.</summary>
    /// <param name="options">Who the device is, and where it keeps builds.</param>
    /// <param name="host">The device's screen.</param>
    public SwarmMember(SwarmMemberOptions options, ISwarmDeviceHost host)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(host);
        _options = options;
        _host = host;
        _cache = new SwarmFileCache(options.CacheDirectory, options.CacheLimit);
    }

    /// <summary>Raised when anything about the membership or the app changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised for the console app's output, so the device can show it too.</summary>
    public event EventHandler<SwarmConsoleText>? ConsoleOutput;

    /// <summary>Where the device stands with a swarm.</summary>
    public SwarmMembership Membership
    {
        get
        {
            lock (_gate)
            {
                return _membership;
            }
        }
    }

    /// <summary>The coordinator's name, while joined or reconnecting.</summary>
    public string? CoordinatorName
    {
        get
        {
            lock (_gate)
            {
                return _coordinatorName;
            }
        }
    }

    /// <summary>The coordinator this device joined, or reconnects to.</summary>
    public LinkPairedDevice? Coordinator
    {
        get
        {
            lock (_gate)
            {
                return _coordinator;
            }
        }
    }

    /// <summary>Why joining failed, why the connection was lost, or why the device left.</summary>
    public string? Error
    {
        get
        {
            lock (_gate)
            {
                return _error;
            }
        }
    }

    /// <summary>The round trip to the coordinator, while joined.</summary>
    public TimeSpan? Latency
    {
        get
        {
            lock (_gate)
            {
                return _session?.Connection.Latency;
            }
        }
    }

    /// <summary>The coordinators this device paired with.</summary>
    public LinkTrustStore Coordinators => _options.Coordinators;

    /// <summary>The swarm connection, while joined.</summary>
    private LinkConnection? Connection
    {
        get
        {
            lock (_gate)
            {
                return _session?.Connection is { IsOpen: true } open ? open : null;
            }
        }
    }

    /// <summary>Joins a swarm: pairs with it by its scanned QR code or typed code, or connects to one it paired with.</summary>
    /// <param name="target">The coordinator, and the pairing code when pairing.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the device joined; <see cref="Error"/> says why not.</returns>
    public async Task<bool> JoinAsync(LinkTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        await LeaveCoreAsync(notify: true, error: null).ConfigureAwait(false);
        Update(() =>
        {
            _membership = SwarmMembership.Joining;
            _error = null;
            _coordinatorName = target.Name;
        });

        try
        {
            await ConnectAsync(target, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (IsConnectFailure(ex))
        {
            Update(() =>
            {
                _membership = SwarmMembership.Off;
                _coordinatorName = null;
                _error = Describe(ex);
            });
            return false;
        }
    }

    /// <summary>Joins a swarm this device paired with before.</summary>
    /// <param name="coordinator">The coordinator.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <returns>Whether the device joined; <see cref="Error"/> says why not.</returns>
    public Task<bool> RejoinAsync(LinkPairedDevice coordinator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        return JoinAsync(LinkTarget.Paired(coordinator), cancellationToken);
    }

    /// <summary>Leaves the swarm: the app stops, the tablet is told, and the device stops reconnecting.</summary>
    public Task LeaveAsync() => LeaveCoreAsync(notify: true, error: null);

    /// <summary>Forgets a coordinator: the device has to pair with it again. Leaves its swarm first when in it.</summary>
    /// <param name="coordinator">The coordinator.</param>
    public async Task ForgetAsync(LinkPairedDevice coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);
        if (Coordinator?.Fingerprint == coordinator.Fingerprint)
        {
            await LeaveAsync().ConfigureAwait(false);
        }

        _options.Coordinators.Forget(coordinator.Fingerprint);
    }

    /// <summary>Tells the tablet that the device's screen changed, for example when it was turned.</summary>
    public void ReportScreen()
    {
        if (Connection is { } connection)
        {
            _ = NotifyAsync(connection, SwarmMethods.Screen, SwarmJson.Body(_host.Screen, SwarmJsonContext.Default.SwarmScreenInfo));
        }
    }

    /// <summary>Tells the tablet about an input a person made on the app here, for mirroring and recording.</summary>
    /// <param name="input">The input.</param>
    public void ReportInput(SwarmInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (Connection is { } connection)
        {
            _ = NotifyAsync(connection, SwarmMethods.Captured, SwarmJson.Body(input, SwarmJsonContext.Default.SwarmInput));
        }
    }

    /// <summary>Leaves the swarm and stops the app.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await LeaveCoreAsync(notify: true, error: null).ConfigureAwait(false);
    }

    private static bool IsConnectFailure(Exception error) =>
        error is LinkAuthenticationException or LinkUnreachableException or LinkRemoteException or LinkConnectionLostException
            or LinkProtocolException or IOException or SocketException or TimeoutException or OperationCanceledException;

    private static string Describe(Exception error) => error switch
    {
        TimeoutException => "The tablet did not answer in time. Check that both devices are on the same Wi-Fi.",
        OperationCanceledException => "Cancelled.",
        LinkRemoteException { Code: LinkErrorCodes.UnknownMethod } => "That device is not a TabStudio Swarm. On the tablet, open the Swarm window and tap Add a device.",
        _ => error.Message,
    };

    private static async Task NotifyAsync(LinkConnection connection, string method, JsonElement? body = null)
    {
        try
        {
            await connection.NotifyAsync(method, body).ConfigureAwait(false);
        }
        catch (LinkConnectionLostException)
        {
            // The coordinator is gone; the device reconnects and reports again.
        }
    }

    /// <summary>Connects, joins, and makes the connection the current one.</summary>
    private async Task ConnectAsync(LinkTarget target, CancellationToken cancellationToken)
    {
        var options = new LinkClientOptions
        {
            Identity = _options.Identity,
            DeviceName = _options.Name,
            DeviceKind = _options.Kind,
            Role = LinkRole.Swarm,
            PingInterval = _options.PingInterval,
            AppVersion = _options.AppVersion,
        };
        var session = await LinkClient.ConnectAsync(options, target, new MemberLink(this), cancellationToken).ConfigureAwait(false);
        try
        {
            var hello = new SwarmHello(_options.Name, _options.Kind, _host.Screen, _host.Platform);
            var answer = await session.Connection.RequestAsync(
                SwarmMethods.Join,
                SwarmJson.Body(hello, SwarmJsonContext.Default.SwarmHello),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var welcome = answer.Read(SwarmJsonContext.Default.SwarmWelcome);
            Adopt(session, welcome, target.Addresses);
        }
        catch
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private void Adopt(LinkClientSession session, SwarmWelcome welcome, IReadOnlyList<string> tried)
    {
        var now = DateTimeOffset.UtcNow;
        var known = _options.Coordinators.Find(session.Host.Fingerprint);
        var addresses = new[] { session.Host.Address }
            .Concat(tried.Select(a => LinkEndpoint.TryParse(a, out var endpoint) ? endpoint.Host : a))
            .Concat(known?.Addresses ?? [])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
        var coordinator = new LinkPairedDevice(welcome.Coordinator, session.Host.Kind, session.Host.Fingerprint, known?.PairedAt ?? now, now, addresses, session.Port);
        _options.Coordinators.Remember(coordinator);

        Update(() =>
        {
            _session = session;
            _coordinator = coordinator;
            _coordinatorName = welcome.Coordinator;
            _membership = SwarmMembership.Joined;
            _error = null;
        });

        session.Connection.LatencyChanged += (_, _) => RaiseChanged();
        session.Connection.Closed += (_, e) => OnSessionClosed(session, e);
        if (!session.Connection.IsOpen)
        {
            OnSessionClosed(session, new LinkClosedEventArgs(session.Connection.CloseError));
        }

        // The tablet learns what the device does now, after a reconnection too.
        ReportState();
    }

    private void OnSessionClosed(LinkClientSession session, LinkClosedEventArgs e)
    {
        LinkPairedDevice? coordinator;
        lock (_gate)
        {
            if (!ReferenceEquals(_session, session))
            {
                return;
            }

            coordinator = _coordinator;
            _session = null;
            _membership = coordinator is null ? SwarmMembership.Off : SwarmMembership.Reconnecting;
            _error = e.Error is null ? $"{_coordinatorName ?? "The tablet"} closed the swarm." : $"Lost {_coordinatorName ?? "the tablet"}: {e.Error.Message}";
        }

        session.Dispose();
        RaiseChanged();
        if (coordinator is not null && Volatile.Read(ref _disposed) == 0)
        {
            StartReconnecting(coordinator);
        }
    }

    private void StartReconnecting(LinkPairedDevice coordinator)
    {
        var reconnect = new CancellationTokenSource();
        CancellationTokenSource? previous;
        lock (_gate)
        {
            previous = _reconnect;
            _reconnect = reconnect;
        }

        previous?.Cancel();
        previous?.Dispose();
        _ = Task.Run(() => ReconnectAsync(coordinator, reconnect.Token));
    }

    private async Task ReconnectAsync(LinkPairedDevice coordinator, CancellationToken cancellationToken)
    {
        var delays = _options.ReconnectDelays;
        for (var attempt = 0; !cancellationToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await Task.Delay(delays[Math.Min(attempt, delays.Count - 1)], cancellationToken).ConfigureAwait(false);
                var discovered = await LinkDiscovery.FindAsync(coordinator.Fingerprint, TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
                var current = _options.Coordinators.Find(coordinator.Fingerprint) ?? coordinator;
                await ConnectAsync(LinkTarget.Paired(current, discovered.Select(d => d.Endpoint.ToString())), cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (LinkAuthenticationException ex) when (ex.Refusal is LinkRefusal.NotPaired or LinkRefusal.VersionMismatch or LinkRefusal.WrongHost or LinkRefusal.WrongRole)
            {
                // Trying again cannot help: the tablet removed this device, or runs another version.
                await LeaveCoreAsync(notify: false, error: ex.Message).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsConnectFailure(ex))
            {
                Update(() => _error = Describe(ex));
            }
        }
    }

    /// <summary>Ends the membership: stops the app and reconnecting, and closes the connection.</summary>
    private async Task LeaveCoreAsync(bool notify, string? error)
    {
        LinkClientSession? session;
        CancellationTokenSource? reconnect;
        lock (_gate)
        {
            session = _session;
            reconnect = _reconnect;
            _session = null;
            _reconnect = null;
            _coordinator = null;
            _coordinatorName = null;
            _membership = SwarmMembership.Off;
            _error = error;
        }

        if (reconnect is not null)
        {
            await reconnect.CancelAsync().ConfigureAwait(false);
            reconnect.Dispose();
        }

        await StopAppAsync().ConfigureAwait(false);
        if (session is not null)
        {
            if (notify && session.Connection.IsOpen)
            {
                await NotifyAsync(session.Connection, SwarmMethods.Leave).ConfigureAwait(false);
            }

            await session.DisposeAsync().ConfigureAwait(false);
        }

        RaiseChanged();
    }

    private void Update(Action change)
    {
        lock (_gate)
        {
            change();
        }

        RaiseChanged();
    }

    private void RaiseChanged()
    {
        var changed = Changed;
        if (changed is not null)
        {
            _ = Task.Run(() => changed(this, EventArgs.Empty));
        }
    }
}

/// <summary>Output of the console app on this device.</summary>
/// <param name="Text">The text, which may hold several lines.</param>
/// <param name="IsError">Whether it was written to standard error.</param>
public sealed record SwarmConsoleText(string Text, bool IsError);
