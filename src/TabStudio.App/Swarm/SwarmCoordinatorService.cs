using System.Net.Sockets;
using TabStudio.App.Services;
using TabStudio.App.Services.Link;
using TabStudio.Link.Security;
using TabStudio.Swarm;
using TabStudio.Swarm.Coordinator;

namespace TabStudio.App.Swarm;

/// <summary>
/// The tablet's swarm in the app: the <see cref="SwarmCoordinator"/>, with the same identity as TabStudio Link and its
/// own list of paired devices; a <see cref="DeviceAppScreen"/> in the <see cref="AppScreenRegistry"/> for every device,
/// so the Device Matrix, mirrored input and recorded tests treat devices like every other screen; and the settings it
/// remembers.
/// </summary>
/// <remarks>
/// <para>
/// The paired devices are kept under <c>State/Link/swarm-devices.json</c>. A swarm that was on when the app closed is
/// on again when a workspace opens, so its devices rejoin by themselves.
/// </para>
/// <para>Every event is raised on the UI thread. Every member is thread-safe.</para>
/// </remarks>
public sealed class SwarmCoordinatorService : IAsyncDisposable, IDisposable
{
    private readonly LinkService _link;
    private readonly IUiDispatcher _ui;
    private readonly AppScreenRegistry _registry;
    private readonly SwarmSettingsStore _store;
    private readonly SwarmInputMirror _mirror;
    private readonly LinkTrustStore _devices;
    private readonly Lock _gate = new();
    private readonly Dictionary<LinkFingerprint, DeviceAppScreen> _screens = [];
    private SwarmCoordinator? _coordinator;
    private SwarmSettings _settings;
    private string? _error;
    private int _disposed;

    /// <summary>Creates the service. Nothing listens until <see cref="StartAsync"/>.</summary>
    /// <param name="paths">Where the paired devices and settings are kept.</param>
    /// <param name="link">TabStudio Link, whose identity the swarm shares.</param>
    /// <param name="ui">Gets events back onto the UI thread.</param>
    /// <param name="registry">Every screen the app being built runs on, where the devices' screens go.</param>
    /// <param name="settings">The swarm's settings.</param>
    /// <param name="mirror">Mirrored input, which the swarm's setting turns on and off.</param>
    public SwarmCoordinatorService(AppPaths paths, LinkService link, IUiDispatcher ui, AppScreenRegistry registry, SwarmSettingsStore settings, SwarmInputMirror mirror)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mirror);
        _link = link;
        _ui = ui;
        _registry = registry;
        _store = settings;
        _settings = settings.Load();
        _mirror = mirror;
        _mirror.IsEnabled = _settings.MirrorInput;
        _devices = new LinkTrustStore(Path.Combine(paths.State, "Link", "swarm-devices.json"));
    }

    /// <summary>Raised when anything about the swarm changed: it started or stopped, a device joined, left or changed, or the code changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised for output of the console app on a device.</summary>
    public event EventHandler<SwarmOutputEventArgs>? OutputReceived;

    /// <summary>Raised when the app ended on a device, crashed with its stack trace included.</summary>
    public event EventHandler<SwarmExitEventArgs>? AppExited;

    /// <summary>Raised when a device received a build, or could not.</summary>
    public event EventHandler<SwarmDeployment>? Deployed;

    /// <summary>Raised when the debugged app paused on a device.</summary>
    public event EventHandler<SwarmPause>? Paused;

    /// <summary>Raised when the debugged app continued on a device, or the device was lost while paused.</summary>
    public event EventHandler<SwarmDevice>? Resumed;

    /// <summary>This tablet's name, as the devices show it.</summary>
    public string Name => _link.DeviceName;

    /// <summary>The coordinator, while the swarm is on.</summary>
    public SwarmCoordinator? Coordinator
    {
        get
        {
            lock (_gate)
            {
                return _coordinator;
            }
        }
    }

    /// <summary>Whether the tablet coordinates a swarm, listening for its devices.</summary>
    public bool IsOn => Coordinator?.IsListening == true;

    /// <summary>Why the swarm could not start, if it could not.</summary>
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

    /// <summary>The devices of the swarm, in the order they joined.</summary>
    public IReadOnlyList<SwarmDevice> Devices => Coordinator?.Devices ?? [];

    /// <summary>The devices that are connected now.</summary>
    public int ConnectedCount => Devices.Count(d => d.IsConnected);

    /// <summary>The devices paired with the swarm.</summary>
    public LinkTrustStore PairedDevices => _devices;

    /// <summary>Whether an input on any screen happens on every screen.</summary>
    public bool MirrorInput
    {
        get => Settings.MirrorInput;
        set
        {
            _mirror.IsEnabled = value;
            Save(Settings with { MirrorInput = value });
        }
    }

    /// <summary>Mirrored input, for the screens' failures to take an input.</summary>
    public SwarmInputMirror Mirror => _mirror;

    /// <summary>Whether a breakpoint on one device pauses every device until they continue together.</summary>
    public bool PauseAllOnBreakpoint
    {
        get => Settings.PauseAllOnBreakpoint;
        set
        {
            Save(Settings with { PauseAllOnBreakpoint = value });
            if (Coordinator is { } coordinator)
            {
                coordinator.PauseAllOnBreakpoint = value;
            }
        }
    }

    /// <summary>The screen of a device of the swarm.</summary>
    /// <param name="device">The device.</param>
    public DeviceAppScreen? ScreenOf(SwarmDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        lock (_gate)
        {
            return _screens.GetValueOrDefault(device.Fingerprint);
        }
    }

    private SwarmSettings Settings
    {
        get
        {
            lock (_gate)
            {
                return _settings;
            }
        }
    }

    /// <summary>Starts the swarm when it was on the last time, so its devices rejoin by themselves.</summary>
    public Task ResumeAsync() => Settings.IsOn && !IsOn ? StartAsync() : Task.CompletedTask;

    /// <summary>Starts coordinating a swarm: listens for devices, which join with the QR code.</summary>
    /// <param name="port">The port to listen on when it is free.</param>
    /// <param name="loopbackOnly">Listens on this device only, for tests and the self test.</param>
    /// <returns>Whether the swarm started; <see cref="Error"/> says why not.</returns>
    public Task<bool> StartAsync(int port = SwarmProtocol.DefaultPort, bool loopbackOnly = false)
    {
        if (IsOn)
        {
            return Task.FromResult(true);
        }

        var coordinator = new SwarmCoordinator(new SwarmCoordinatorOptions
        {
            Identity = _link.Identity,
            Devices = _devices,
            Name = _link.DeviceName,
            Kind = _link.DeviceKind,
            Port = port,
            LoopbackOnly = loopbackOnly,
        })
        {
            PauseAllOnBreakpoint = Settings.PauseAllOnBreakpoint,
        };

        try
        {
            coordinator.Start();
        }
        catch (SocketException ex)
        {
            lock (_gate)
            {
                _error = $"The swarm could not open a network port: {ex.Message}";
            }

            RaiseChanged();
            return Task.FromResult(false);
        }

        coordinator.DevicesChanged += (_, _) => _ui.Post(SyncScreens);
        coordinator.DeviceChanged += (_, device) => _ui.Post(() => OnDeviceChanged(device));
        coordinator.PairingCodeChanged += (_, _) => RaiseChanged();
        coordinator.OutputReceived += (_, e) => _ui.Post(() => OutputReceived?.Invoke(this, e));
        coordinator.AppExited += (_, e) => _ui.Post(() => AppExited?.Invoke(this, e));
        coordinator.Deployed += (_, e) => _ui.Post(() => OnDeployed(e));
        coordinator.Paused += (_, e) => _ui.Post(() => Paused?.Invoke(this, e));
        coordinator.Resumed += (_, e) => _ui.Post(() => Resumed?.Invoke(this, e));
        coordinator.InputCaptured += (_, e) => _ui.Post(() => ScreenOf(e.Device)?.OnInputCaptured(e.Input));
        lock (_gate)
        {
            _coordinator = coordinator;
            _error = null;
        }

        Save(Settings with { IsOn = true });
        RaiseChanged();
        return Task.FromResult(true);
    }

    /// <summary>Stops the swarm: the app stops on every device, and they wait to rejoin when it starts again.</summary>
    public async Task StopAsync()
    {
        SwarmCoordinator? coordinator;
        lock (_gate)
        {
            coordinator = _coordinator;
            _coordinator = null;
        }

        Save(Settings with { IsOn = false });
        if (coordinator is not null)
        {
            await coordinator.DisposeAsync().ConfigureAwait(false);
        }

        _ui.Post(SyncScreens);
        RaiseChanged();
    }

    /// <summary>Reads the network addresses again, for the QR code, after the tablet changed networks.</summary>
    public void RefreshAddresses() => Coordinator?.RefreshAddresses();

    /// <summary>Stops the swarm and takes its screens out of the registry.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        SwarmCoordinator? coordinator;
        lock (_gate)
        {
            coordinator = _coordinator;
            _coordinator = null;
        }

        if (coordinator is not null)
        {
            await coordinator.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Stops the swarm, waiting until done.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private void Save(SwarmSettings settings)
    {
        lock (_gate)
        {
            if (settings == _settings)
            {
                return;
            }

            _settings = settings;
        }

        _store.Save(settings);
        RaiseChanged();
    }

    /// <summary>Puts a screen in the registry for every device of the swarm, and takes out those of devices that left. UI thread.</summary>
    private void SyncScreens()
    {
        var coordinator = Coordinator;
        IReadOnlyList<SwarmDevice> devices = coordinator?.Devices ?? [];
        List<DeviceAppScreen> added = [];
        List<DeviceAppScreen> removed = [];
        lock (_gate)
        {
            foreach (var device in devices)
            {
                if (coordinator is not null && !_screens.ContainsKey(device.Fingerprint))
                {
                    var screen = new DeviceAppScreen(device, coordinator);
                    _screens[device.Fingerprint] = screen;
                    added.Add(screen);
                }
            }

            foreach (var (fingerprint, screen) in _screens.ToList())
            {
                if (!devices.Any(d => d.Fingerprint == fingerprint && ReferenceEquals(d, screen.Device)))
                {
                    _screens.Remove(fingerprint);
                    removed.Add(screen);
                }
            }
        }

        foreach (var screen in removed)
        {
            _registry.Remove(screen);
        }

        foreach (var screen in added)
        {
            _registry.Add(screen);
        }

        foreach (var device in devices)
        {
            ScreenOf(device)?.Refresh();
        }

        RaiseChangedNow();
    }

    private void OnDeviceChanged(SwarmDevice device)
    {
        ScreenOf(device)?.Refresh();
        RaiseChangedNow();
    }

    private void OnDeployed(SwarmDeployment deployment)
    {
        if (deployment.Succeeded)
        {
            ScreenOf(deployment.Device)?.OnUpdated();
        }

        Deployed?.Invoke(this, deployment);
        RaiseChangedNow();
    }

    private void RaiseChanged() => _ui.Post(RaiseChangedNow);

    private void RaiseChangedNow() => Changed?.Invoke(this, EventArgs.Empty);
}
