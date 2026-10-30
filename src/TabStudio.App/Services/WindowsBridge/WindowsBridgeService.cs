using TabStudio.App.Platform;
using TabStudio.App.Services.Link;
using TabStudio.Link.Security;
using TabStudio.WindowsBridge.Client;

namespace TabStudio.App.Services.WindowsBridge;

/// <summary>
/// The Windows Bridge for the whole app: on a tablet, the paired Windows PC that builds and runs the workspace's
/// Windows-only projects and streams them back (<c>WindowsBridgeService.Tablet.cs</c>); on TabStudio's desktop build
/// on Windows, being that PC for a tablet (<c>WindowsBridgeService.Pc.cs</c>).
/// </summary>
/// <remarks>
/// <para>
/// It shares TabStudio Link's identity, so the PC's certificate is the same for both roles, and keeps its paired
/// devices beside Link's, under <c>State/Link</c>: <c>bridges.json</c> on the tablet, <c>bridge-tablets.json</c> on the
/// PC. The PC's mirrors of tablets' workspaces and its tools, such as netcoredbg, are under <c>Cache/Bridge</c>.
/// </para>
/// <para><see cref="Changed"/> is raised on the UI thread. Every member is thread-safe.</para>
/// </remarks>
public sealed partial class WindowsBridgeService : IAsyncDisposable, IDisposable
{
    private readonly LinkService _link;
    private readonly AppPaths _paths;
    private readonly IUiDispatcher _ui;
    private readonly HttpClient _http;
    private readonly IBridgeDesktopFactory? _desktops;
    private readonly Lock _gate = new();
    private int _disposed;

    /// <summary>Creates the service. Nothing connects or listens until asked.</summary>
    /// <param name="link">The app's TabStudio Link, whose identity and device name the bridge uses.</param>
    /// <param name="paths">Where the paired devices, the mirrors and the tools are kept.</param>
    /// <param name="ui">Gets events back onto the UI thread.</param>
    /// <param name="http">Downloads netcoredbg, on a PC.</param>
    /// <param name="desktops">Makes the PC's desktop session, on Windows; <see langword="null"/> where this device cannot be a bridge.</param>
    public WindowsBridgeService(LinkService link, AppPaths paths, IUiDispatcher ui, HttpClient http, IBridgeDesktopFactory? desktops = null)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(http);
        _link = link;
        _paths = paths;
        _ui = ui;
        _http = http;
        _desktops = desktops;

        var folder = Path.Combine(paths.State, "Link");
        PairedPcs = new LinkTrustStore(Path.Combine(folder, "bridges.json"));
        BridgeTablets = new LinkTrustStore(Path.Combine(folder, "bridge-tablets.json"));
        PairedPcs.Changed += (_, _) => RaiseChanged();
        BridgeTablets.Changed += (_, _) => RaiseChanged();
        _settingsPath = Path.Combine(folder, "bridge.json");
        LoadSettings();
        InitializeTablet();
    }

    /// <summary>Raised on the UI thread when anything about the bridge changed.</summary>
    public event EventHandler? Changed;

    /// <summary>This device's name, as the other device shows it.</summary>
    public string DeviceName => _link.DeviceName;

    /// <summary>The start of this device's fingerprint, for comparing two screens.</summary>
    public string FingerprintText => _link.Identity.Fingerprint.ShortText;

    /// <summary>Disconnects from the PC and stops being a bridge.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        await DisconnectAsync().ConfigureAwait(false);
        await StopBridgeAsync().ConfigureAwait(false);
    }

    /// <summary>Disconnects from the PC and stops being a bridge, waiting until done.</summary>
    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    private void RaiseChanged() => _ui.Post(() => Changed?.Invoke(this, EventArgs.Empty));

    /// <summary>What went wrong, for the dialog; TabStudio Link's own exceptions are already worded for people.</summary>
    private static string Describe(Exception error) => error switch
    {
        TimeoutException => "The PC did not answer in time. Check that both devices are on the same Wi-Fi.",
        OperationCanceledException => "Cancelled.",
        _ => error.Message,
    };
}
