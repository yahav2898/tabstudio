using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Services.Toolchains;

/// <summary>
/// The toolchains of this device, for every open workspace: the catalog, the <see cref="ToolchainManager"/> that
/// installs them, and the runtime their programs run in.
/// </summary>
/// <remarks>
/// <para>
/// On Android, toolchains are Alpine Linux packages in TabStudio's Linux environment, kept under
/// <c>Toolchains/linux</c> in the app's storage and run through PRoot from the app's native library folder; the
/// projects folder, and each workspace opened, are visible to programs at their own paths. On a desktop computer,
/// TabStudio uses the toolchains installed on the computer, so tests and the desktop build run the same flows.
/// </para>
/// <para>
/// Nothing is installed or downloaded until the user asks for it in the Toolchain Store or answers a prompt.
/// </para>
/// </remarks>
public sealed class ToolchainServices : IDisposable
{
    private readonly HttpClient? _http;
    private readonly AlpinePackageManager? _packages;
    private readonly Lock _gate = new();
    private Task? _firstRefresh;

    /// <summary>Creates the toolchains for the app.</summary>
    /// <param name="paths">Where the app keeps its data.</param>
    /// <param name="platform">What the device offers, from the platform head; a desktop computer when <see langword="null"/>.</param>
    public ToolchainServices(AppPaths paths, ToolchainPlatform? platform = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Platform = platform ?? ToolchainPlatform.Desktop;
        var catalog = ToolchainCatalog.Default;
        if (!Platform.UsesLinuxEnvironment)
        {
            Manager = new ToolchainManager(catalog, new HostToolchainInstaller(new HostRuntime()));
            return;
        }

        // Downloads have no overall timeout: the downloader detects stalled connections itself.
        _http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("TabStudio/0.1 (+https://github.com/yahav2898/tabstudio)");
        var downloader = new Downloader(_http);
        Environment = new AlpineEnvironment(Path.Combine(paths.Root, "Toolchains", "linux"), catalog.Linux, Platform, downloader, [paths.Projects]);
        _packages = new AlpinePackageManager(Environment, _http, downloader, new ApkTool(Environment.Runtime));
        Manager = new ToolchainManager(catalog, new FlutterToolchainInstaller(
            new LinuxToolchainInstaller(Environment, _packages, downloader), Path.Combine(paths.Root, "Toolchains", "flutter"), Platform, downloader, [paths.Projects]));
    }

    /// <summary>Wraps a manager built elsewhere, such as one over a fake mirror in tests.</summary>
    /// <param name="manager">The manager.</param>
    /// <param name="platform">What the device offers.</param>
    /// <param name="environment">The Linux environment the manager installs into, if it uses one.</param>
    public ToolchainServices(ToolchainManager manager, ToolchainPlatform platform, AlpineEnvironment? environment = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(platform);
        Manager = manager;
        Platform = platform;
        Environment = environment;
    }

    /// <summary>Installs, updates and removes toolchains, and knows their states.</summary>
    public ToolchainManager Manager { get; }

    /// <summary>The toolchain catalog.</summary>
    public ToolchainCatalog Catalog => Manager.Catalog;

    /// <summary>Where toolchains run their programs.</summary>
    public IToolchainRuntime Runtime => Manager.Runtime;

    /// <summary>What the device offers.</summary>
    public ToolchainPlatform Platform { get; }

    /// <summary>The Linux environment on Android, or <see langword="null"/> on a desktop computer.</summary>
    public AlpineEnvironment? Environment { get; }

    /// <summary>
    /// Reads the toolchains' states the first time it is called, and returns that same reading afterwards. Never
    /// throws: a toolchain whose state cannot be read stays as the catalog describes it.
    /// </summary>
    public Task EnsureStatusAsync()
    {
        lock (_gate)
        {
            return _firstRefresh ??= RefreshQuietlyAsync();
        }
    }

    /// <summary>Makes a folder, such as an opened workspace outside the projects folder, visible to programs.</summary>
    /// <param name="folder">The folder.</param>
    public void ShareFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        Runtime.ShareFolder(folder);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Manager.Dispose();
        _packages?.Dispose();
        _http?.Dispose();
    }

    private async Task RefreshQuietlyAsync()
    {
        try
        {
            // The network may have changed since the environment was set up; DNS follows it.
            if (Environment is { IsSetUp: true } environment)
            {
                await Task.Run(environment.Configure).ConfigureAwait(false);
            }

            await Manager.RefreshAsync().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ToolchainInstallException or InvalidDataException)
        {
            // The Store shows the catalog's estimates; the next operation reads the states again.
        }
    }
}
