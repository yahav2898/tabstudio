using TabStudio.App.Platform;
using TabStudio.Link.Security;

namespace TabStudio.App.Services.Link;

/// <summary>
/// TabStudio Link for the whole app: this device's identity and the devices it is paired with, its link to a host
/// when it is the tablet (<c>LinkService.Client.cs</c>), and the host it can be for another device
/// (<c>LinkService.Host.cs</c>).
/// </summary>
/// <remarks>
/// <para>
/// The identity (a self-signed certificate and its key) and the paired devices are kept in the app's private storage,
/// under <c>State/Link</c>; the host's mirrors of tablets' workspaces under <c>Cache/Link</c>.
/// </para>
/// <para>
/// <see cref="Changed"/> is raised on the UI thread; <see cref="LanguageClientChanged"/> on the thread pool, for the
/// language services that switch between this device and the host. Every member is thread-safe.
/// </para>
/// </remarks>
public sealed partial class LinkService : IAsyncDisposable, IDisposable
{
    private readonly AppPaths _paths;
    private readonly CompilerServices _compiler;
    private readonly IDeviceStatus _deviceStatus;
    private readonly DeviceProfile _profile;
    private readonly IDeviceIdentity _device;
    private readonly IUiDispatcher _ui;
    private readonly Lazy<LinkIdentity> _identity;
    private readonly Lock _gate = new();
    private int _disposed;

    /// <summary>Creates the service. Nothing listens or connects until asked.</summary>
    /// <param name="paths">Where the identity, the paired devices and the mirrors are kept.</param>
    /// <param name="compiler">The compiler services a host compiles tablets' workspaces with.</param>
    /// <param name="deviceStatus">The device's temperature, which a host respects for its tablets too.</param>
    /// <param name="profile">The device profile, which decides whether a host uses skeleton references.</param>
    /// <param name="device">This device's name and kind, as other devices see them.</param>
    /// <param name="ui">Gets events back onto the UI thread.</param>
    public LinkService(AppPaths paths, CompilerServices compiler, IDeviceStatus deviceStatus, DeviceProfile profile, IDeviceIdentity device, IUiDispatcher ui)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(compiler);
        ArgumentNullException.ThrowIfNull(deviceStatus);
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(ui);

        _paths = paths;
        _compiler = compiler;
        _deviceStatus = deviceStatus;
        _profile = profile;
        _device = device;
        _ui = ui;

        var folder = Path.Combine(paths.State, "Link");
        _identity = new Lazy<LinkIdentity>(() => LinkIdentity.LoadOrCreate(Path.Combine(folder, "identity.p12")), LazyThreadSafetyMode.ExecutionAndPublication);
        PairedHosts = new LinkTrustStore(Path.Combine(folder, "hosts.json"));
        PairedTablets = new LinkTrustStore(Path.Combine(folder, "tablets.json"));
        PairedHosts.Changed += (_, _) => RaiseChanged();
        PairedTablets.Changed += (_, _) => RaiseChanged();
        _deviceStatus.ThermalChanged += OnThermalChanged;
    }

    /// <summary>Raised on the UI thread when anything about the link changed: its state, the latency, the host's activity, the pairing code.</summary>
    public event EventHandler? Changed;

    /// <summary>This device's name, as other devices show it.</summary>
    public string DeviceName => _device.Name;

    /// <summary>What this device is, one of <see cref="TabStudio.Link.Networking.LinkDeviceKinds"/>.</summary>
    public string DeviceKind => _device.Kind;

    /// <summary>This device's identity, made the first time it is needed.</summary>
    public LinkIdentity Identity => _identity.Value;

    /// <summary>The hosts this device links to.</summary>
    public LinkTrustStore PairedHosts { get; }

    /// <summary>The tablets this device serves as a host.</summary>
    public LinkTrustStore PairedTablets { get; }

    /// <summary>Disconnects from the host, stops hosting and releases the identity.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _deviceStatus.ThermalChanged -= OnThermalChanged;
        await DisconnectAsync().ConfigureAwait(false);
        await StopHostingAsync().ConfigureAwait(false);
        if (_identity.IsValueCreated)
        {
            _identity.Value.Dispose();
        }
    }

    /// <summary>Disconnects from the host, stops hosting and releases the identity, waiting until done.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private void RaiseChanged() => _ui.Post(() => Changed?.Invoke(this, EventArgs.Empty));

    /// <summary>What went wrong, for the Link dialog. TabStudio Link's own exceptions are already worded for people.</summary>
    private static string Describe(Exception error) => error switch
    {
        TimeoutException => "The other device did not answer in time. Check that both devices are on the same Wi-Fi.",
        OperationCanceledException => "Cancelled.",
        _ => error.Message,
    };
}
