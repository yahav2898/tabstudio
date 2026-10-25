using System.Globalization;
using TabStudio.App.Services.Toolchains;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Runtimes;
using TabStudio.WindowsCompatibility.Sessions;

namespace TabStudio.App.Services.WindowsCompatibility;

/// <summary>
/// Windows Compatibility on this device: whether it can run here and why not, whether it is installed, and the
/// Windows desktop session apps run in, started on first use and kept between runs so apps start fast.
/// </summary>
/// <remarks>
/// On Android its parts are the Toolchain Store's "windows" toolchain. On a desktop computer Wine and Xvfb are the
/// computer's own and the .NET runtime for Windows is downloaded into TabStudio's data folder on first use, so the
/// desktop build and its tests run the same flow.
/// </remarks>
public sealed class WindowsCompatibilityService : IAsyncDisposable
{
    /// <summary>The toolchain's id in the catalog.</summary>
    public const string ToolchainId = "windows";

    private readonly ToolchainServices _toolchains;
    private readonly string _hostFolder;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HttpClient? _http;
    private WindowsSession? _session;

    /// <summary>Creates the service.</summary>
    /// <param name="toolchains">The device's toolchains.</param>
    /// <param name="paths">Where the app keeps its data.</param>
    public WindowsCompatibilityService(ToolchainServices toolchains, AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(toolchains);
        ArgumentNullException.ThrowIfNull(paths);
        _toolchains = toolchains;
        _hostFolder = Path.Combine(paths.Root, "WindowsCompatibility");
        if (toolchains.Runtime is HostRuntime)
        {
            _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        }
    }

    /// <summary>The toolchain, as the catalog describes it.</summary>
    public ToolchainDefinition? Toolchain => _toolchains.Catalog.Find(ToolchainId);

    /// <summary>Where its parts are.</summary>
    public WindowsLayout Layout => WindowsLayout.For(_toolchains.Runtime, _hostFolder);

    /// <summary>The Toolchain Store's state of the toolchain.</summary>
    public ToolchainStatus Status => _toolchains.Manager.Status(ToolchainId);

    /// <summary>Whether the Toolchain Store can install it on this device.</summary>
    public bool CanInstall => _toolchains.Manager.CanInstall && Problem is null;

    /// <summary>Whether everything is there to run Windows apps.</summary>
    public bool IsInstalled => Problem is null && (_toolchains.Manager.CanInstall ? Status.IsReady && Layout.IsComplete : HasHostTools);

    /// <summary>The session apps run in, while one runs.</summary>
    public WindowsSession? Session => _session is { IsRunning: true } session ? session : null;

    /// <summary>
    /// Why Windows apps cannot run on this device at all, or <see langword="null"/> when they can (once installed).
    /// </summary>
    public string? Problem
    {
        get
        {
            if (OperatingSystem.IsWindows())
            {
                return "This computer runs Windows itself: run the app with Visual Studio, or pair it as a Windows Bridge.";
            }

            if (!OperatingSystem.IsLinux() && !OperatingSystem.IsAndroid())
            {
                return "Windows Compatibility runs on Android tablets and on Linux.";
            }

            if (Environment.SystemPageSize != 4096)
            {
                return string.Create(CultureInfo.InvariantCulture, $"This device uses {Environment.SystemPageSize / 1024} KB memory pages; Wine, which runs Windows apps, needs 4 KB pages.");
            }

            if (_toolchains.Manager.CanInstall && Toolchain is { } toolchain && !toolchain.SupportsArchitecture(_toolchains.Platform.Architecture))
            {
                return Status.Note ?? "Windows Compatibility needs a tablet with a 64-bit processor.";
            }

            if (!_toolchains.Manager.CanInstall && _toolchains.Runtime is HostRuntime && !HasHostTools)
            {
                return "Install Wine and Xvfb on this computer to run Windows apps here.";
            }

            return null;
        }
    }

    /// <summary>Whether the computer has its own Wine and Xvfb, on a desktop computer.</summary>
    private bool HasHostTools => _toolchains.Runtime.HasCommand("wine") && _toolchains.Runtime.HasCommand("Xvfb");

    /// <summary>
    /// The running session, started with the given settings when there is none or when its display scale differs;
    /// a running one is resized to the settings.
    /// </summary>
    /// <param name="options">The desktop's size and scale, and how WPF draws.</param>
    /// <param name="progress">Receives what happens, for the status bar and the Output panel.</param>
    /// <param name="cancellationToken">Gives up.</param>
    /// <exception cref="TabStudio.WindowsCompatibility.WindowsCompatibilityException">The session could not start; the message says why.</exception>
    public async Task<WindowsSession> EnsureSessionAsync(WindowsSessionOptions options, IProgress<string>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is { IsRunning: true } running && running.Options.Dpi == options.Dpi)
            {
                await running.ApplyAsync(options, cancellationToken).ConfigureAwait(false);
                return running;
            }

            if (_session is { } stale)
            {
                await stale.DisposeAsync().ConfigureAwait(false);
                _session = null;
            }

            var layout = Layout;
            if (_http is not null && Toolchain is { } toolchain)
            {
                await new HostRuntimeInstaller(new Downloader(_http)).EnsureAsync(layout, toolchain, progress, cancellationToken).ConfigureAwait(false);
            }

            _session = await WindowsSession.StartAsync(layout, options, progress, cancellationToken).ConfigureAwait(false);
            return _session;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Stops the session, if one runs, and every app in it.</summary>
    public async Task StopSessionAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_session is { } session)
            {
                _session = null;
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopSessionAsync().ConfigureAwait(false);
        _gate.Dispose();
        _http?.Dispose();
    }
}
