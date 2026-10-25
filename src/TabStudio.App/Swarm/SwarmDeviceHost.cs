using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Platform;
using TabStudio.App.Previews;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Compilation.Running;
using TabStudio.Swarm;
using TabStudio.Swarm.Device;

namespace TabStudio.App.Swarm;

/// <summary>What a device of a swarm shows full screen.</summary>
public enum SwarmDeviceMode
{
    /// <summary>Nothing yet: it waits for the tablet to run an app.</summary>
    Waiting,

    /// <summary>An Avalonia app, in the device's app preview.</summary>
    App,

    /// <summary>A web app the tablet serves, in a web view.</summary>
    Web,

    /// <summary>A console app's output.</summary>
    Console,
}

/// <summary>
/// The device's screen in a swarm: shows the Avalonia app the tablet sent in the device's own app preview, a web app in
/// a web view, or a console app's output, and answers the tablet's requests about the screen through the app preview's
/// screen in the <see cref="AppScreenRegistry"/>.
/// </summary>
/// <remarks>
/// <para>
/// An Avalonia app is debugged here when the tablet asks: its assemblies are connected to the device's debugger before
/// its code runs, and the <see cref="DispatcherWaitPump"/> keeps the screen drawing while it is paused where the
/// platform allows it.
/// </para>
/// <para>
/// Input, pictures, layout checks and texts go to the registry's screen of kind <see cref="AppScreenKind.Preview"/>,
/// which the app preview registers. Without one, pictures are rendered from the app's view and texts read from its
/// elements here, and replayed input is not delivered.
/// </para>
/// <para>Properties change on the UI thread. The <see cref="ISwarmDeviceHost"/> members may be called from any thread.</para>
/// </remarks>
public sealed partial class SwarmDeviceHost : ObservableObject, ISwarmDeviceHost
{
    /// <summary>How long a notice stays on the screen.</summary>
    private static readonly TimeSpan NoticeTime = TimeSpan.FromSeconds(3.5);

    private readonly IUiDispatcher _ui;
    private readonly AppScreenRegistry _registry;
    private readonly IWebViewFactory _webViews;
    private readonly IStoragePicker _browser;
    private readonly Lock _gate = new();
    private SwarmScreenInfo _screen = SwarmScreenInfo.Unknown;
    private IAppScreen? _previewScreen;
    private HostedRun? _current;
    private int _notices;

    /// <summary>Creates the device's screen.</summary>
    /// <param name="ui">Gets work onto the UI thread.</param>
    /// <param name="registry">The screens of the app being built, where the app preview registers its screen.</param>
    /// <param name="webViews">Creates the web view for web apps.</param>
    /// <param name="browser">Opens pages a web app links to outside itself.</param>
    /// <param name="profile">The device profile, which limits the console's text.</param>
    public SwarmDeviceHost(IUiDispatcher ui, AppScreenRegistry registry, IWebViewFactory webViews, IStoragePicker browser, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(ui);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(webViews);
        ArgumentNullException.ThrowIfNull(browser);
        ArgumentNullException.ThrowIfNull(profile);
        _ui = ui;
        _registry = registry;
        _webViews = webViews;
        _browser = browser;
        Console = new OutputViewModel(ui, profile.OutputCharacterLimit);
        Web = new WebPreviewViewModel(webViews, browser);
        _registry.Screens.CollectionChanged += (_, _) => WatchPreviewScreen();
        WatchPreviewScreen();
    }

    /// <summary>Raised on the UI thread for every input a person made on the app here, for the tablet's mirroring and recording.</summary>
    public event EventHandler<AppInput>? InputCaptured;

    /// <summary>Raised when the screen's size, scale or theme changed, so the tablet learns it.</summary>
    public event EventHandler? ScreenChanged;

    /// <summary>The device's app preview, which shows the Avalonia app full screen.</summary>
    public AppPreviewViewModel Preview { get; } = new();

    /// <summary>The web view of a web app.</summary>
    [ObservableProperty]
    public partial WebPreviewViewModel Web { get; private set; }

    /// <summary>The console app's output.</summary>
    public OutputViewModel Console { get; }

    /// <summary>What the device shows.</summary>
    [ObservableProperty]
    public partial SwarmDeviceMode Mode { get; private set; }

    /// <summary>The app shown, while one is.</summary>
    [ObservableProperty]
    public partial string? AppName { get; private set; }

    /// <summary>A short message shown for a moment, such as an input mirrored from another screen that did not arrive.</summary>
    [ObservableProperty]
    public partial string? Notice { get; private set; }

    /// <inheritdoc />
    public SwarmScreenInfo Screen
    {
        get
        {
            lock (_gate)
            {
                return _screen;
            }
        }
    }

    /// <inheritdoc />
    public string Platform { get; } = OperatingSystem.IsAndroid() ? "Android"
        : OperatingSystem.IsWindows() ? "Windows"
        : OperatingSystem.IsMacOS() ? "macOS"
        : "Linux";

    /// <summary>Tells the host how big the device's screen is, as the device page measures it.</summary>
    /// <param name="width">The width, in device-independent pixels.</param>
    /// <param name="height">The height, in device-independent pixels.</param>
    /// <param name="scale">Physical pixels per device-independent pixel.</param>
    /// <param name="isDark">Whether the device shows apps in their dark theme.</param>
    public void SetScreen(double width, double height, double scale, bool isDark)
    {
        var screen = new SwarmScreenInfo(Math.Round(width), Math.Round(height), scale, isDark);
        lock (_gate)
        {
            if (screen == _screen)
            {
                return;
            }

            _screen = screen;
        }

        ScreenChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public async Task<ISwarmAppRun> StartAsync(SwarmApp app, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(app);
        HostedRun? run = null;
        await _ui.InvokeAsync(() => run = Show(app));
        return run!;
    }

    /// <inheritdoc />
    public async Task<bool> SendInputAsync(SwarmInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        var screen = await OnUiThreadAsync(() => _previewScreen);
        return screen is not null && await screen.SendInputAsync(SwarmScreens.ToApp(input), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]?> CaptureAsync(CancellationToken cancellationToken)
    {
        var screen = await OnUiThreadAsync(() => _previewScreen);
        return screen is not null ? await screen.CaptureAsync(cancellationToken) : await OnUiThreadAsync(RenderView);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SwarmLayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken)
    {
        var screen = await OnUiThreadAsync(() => _previewScreen);
        return screen is null ? [] : [.. (await screen.InspectLayoutAsync(cancellationToken)).Select(SwarmScreens.ToSwarm)];
    }

    /// <inheritdoc />
    public async Task<string?> ReadTextAsync(string elementPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(elementPath);
        var screen = await OnUiThreadAsync(() => _previewScreen);
        return screen is IAppScreenElements elements
            ? await elements.ReadTextAsync(elementPath, cancellationToken)
            : await OnUiThreadAsync(() => Preview.View is { } view && ElementPaths.Find(view, elementPath) is { } element ? ElementPaths.TextOf(element) : null);
    }

    /// <inheritdoc />
    public void ShowNotice(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        _ui.Post(() =>
        {
            Notice = text;
            var shown = ++_notices;
            _ = ClearNoticeLaterAsync(shown);
        });
    }

    /// <summary>Shows the console app's output. UI thread.</summary>
    /// <param name="appName">The app.</param>
    public void ShowConsole(string appName)
    {
        if (Mode != SwarmDeviceMode.Console || AppName != appName)
        {
            Console.Clear();
        }

        Mode = SwarmDeviceMode.Console;
        AppName = appName;
    }

    /// <summary>Shows that nothing runs. UI thread.</summary>
    public void ShowWaiting()
    {
        Mode = SwarmDeviceMode.Waiting;
        AppName = null;
    }

    private async Task ClearNoticeLaterAsync(int shown)
    {
        await Task.Delay(NoticeTime);
        if (shown == _notices)
        {
            Notice = null;
        }
    }

    /// <summary>Shows an app full screen. UI thread.</summary>
    private HostedRun Show(SwarmApp app)
    {
        _current?.End(new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero));
        if (app.Kind == SwarmAppKind.Web)
        {
            Web = new WebPreviewViewModel(_webViews, _browser);
            Web.Show(app.Name, app.Address ?? throw new InvalidOperationException("The web app came without its address."), null);
            var web = new HostedRun(this, app.Name, appHost: null);
            Present(web, SwarmDeviceMode.Web, app.Name);
            return web;
        }

        var debugger = app.Debugger;
        if (debugger is not null)
        {
            debugger.WaitPump = DispatcherWaitPump.Instance;
        }

        // The debugger joins the app's assemblies once they are loaded, before any of the app's code runs.
        Action<IEnumerable<Assembly>>? attach = null;
        if (debugger is not null)
        {
            attach = debugger.Attach;
        }

        var entry = app.Entry ?? throw new InvalidOperationException("The app came without its assembly.");
        var host = AvaloniaAppHost.Start(entry, app.Dependencies, app.PackageAssemblies, attach);
        var run = new HostedRun(this, app.Name, host);
        host.Crashed += (_, exception) => _ui.Post(() => run.End(new RunResult(RunOutcome.Crashed, -1, TimeSpan.Zero, exception)));
        Preview.Show(app.Name, host.View, host.Note);
        Present(run, SwarmDeviceMode.App, app.Name);
        return run;
    }

    private void Present(HostedRun run, SwarmDeviceMode mode, string appName)
    {
        _current = run;
        Mode = mode;
        AppName = appName;
    }

    /// <summary>Takes an app off the screen. UI thread.</summary>
    private void Remove(HostedRun run)
    {
        if (!ReferenceEquals(_current, run))
        {
            return;
        }

        _current = null;
        Preview.ShowStopped();
        Web.ShowStopped();
        ShowWaiting();
    }

    /// <summary>Follows the app preview's screen in the registry, whose inputs go to the tablet. UI thread.</summary>
    private void WatchPreviewScreen()
    {
        var screen = _registry.Screens.FirstOrDefault(s => s.Kind == AppScreenKind.Preview);
        if (ReferenceEquals(screen, _previewScreen))
        {
            return;
        }

        if (_previewScreen is not null)
        {
            _previewScreen.InputCaptured -= OnPreviewInput;
        }

        _previewScreen = screen;
        if (screen is not null)
        {
            screen.InputCaptured += OnPreviewInput;
        }
    }

    private void OnPreviewInput(object? sender, AppInput input) => InputCaptured?.Invoke(this, input);

    /// <summary>A picture of the app's view, when no screen of the app preview takes pictures. UI thread.</summary>
    private byte[]? RenderView()
    {
        if (Preview.View is not { Bounds: { Width: >= 1, Height: >= 1 } bounds } view)
        {
            return null;
        }

        var scale = TopLevel.GetTopLevel(view)?.RenderScaling ?? 1;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)(bounds.Width * scale), (int)(bounds.Height * scale)), new Vector(96 * scale, 96 * scale));
        bitmap.Render(view);
        using var stream = new MemoryStream();
        bitmap.Save(stream);
        return stream.ToArray();
    }

    private async Task<T> OnUiThreadAsync<T>(Func<T> read)
    {
        var value = default(T);
        await _ui.InvokeAsync(() => value = read());
        return value!;
    }

    /// <summary>An app the device shows, until the swarm stops it or it crashes.</summary>
    private sealed class HostedRun(SwarmDeviceHost owner, string appName, AvaloniaAppHost? appHost) : ISwarmAppRun
    {
        private readonly TaskCompletionSource<RunResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _ended;

        public string AppName { get; } = appName;

        public Task<RunResult> Completion => _completion.Task;

        public async ValueTask DisposeAsync() =>
            await owner._ui.InvokeAsync(() => End(new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero)));

        /// <summary>Ends the app: takes it off the screen and unloads it. UI thread.</summary>
        public void End(RunResult result)
        {
            if (Interlocked.Exchange(ref _ended, 1) == 1)
            {
                return;
            }

            owner.Remove(this);
            appHost?.Dispose();
            _completion.TrySetResult(result);
        }
    }
}
