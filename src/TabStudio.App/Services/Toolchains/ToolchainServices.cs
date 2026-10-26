using TabStudio.Toolchains.Alpine;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Runtimes;
using TabStudio.Toolchains.Microchip;
using System.Net;
using System.Text.RegularExpressions;

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
/// Opening a project installs its toolchain when "Set projects up when they open" is enabled (the default).
/// With it off, the Toolchain Store and install prompts start downloads.
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
    public ToolchainServices(AppPaths paths, ToolchainPlatform? platform = null, IUsbHost? usb = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        Platform = platform ?? ToolchainPlatform.Desktop;
        Usb = usb ?? new NoUsbHost();
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
        Manager = new ToolchainManager(catalog, new LinuxToolchainInstaller(Environment, _packages, downloader));
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

    internal AlpinePackageManager? ServicePackageManager => _packages;

    internal Task WithPackageOperationAsync(Func<Task> action, CancellationToken cancellationToken) =>
        Manager.WithRuntimeOperationAsync(action, cancellationToken);

    /// <summary>Installs, updates and removes toolchains, and knows their states.</summary>
    public ToolchainManager Manager { get; }

    /// <summary>The toolchain catalog.</summary>
    public ToolchainCatalog Catalog => Manager.Catalog;

    /// <summary>Where toolchains run their programs.</summary>
    public IToolchainRuntime Runtime => Manager.Runtime;

    /// <summary>What the device offers.</summary>
    public ToolchainPlatform Platform { get; }

    /// <summary>Android USB permissions and open connection lifetime.</summary>
    public IUsbHost Usb { get; } = new NoUsbHost();

    /// <summary>Installs USB support without requiring an XC license or compiler download.</summary>
    /// <param name="developmentTools">Also prepares native builds of optional programmer/simulator tools.</param>
    /// <param name="line">Setup output.</param>
    /// <param name="cancellationToken">Stops setup.</param>
    public Task PreparePicSupportAsync(bool developmentTools, Action<string> line, CancellationToken cancellationToken) =>
        Manager.ConfigureRuntimeAsync(async token =>
        {
            if (Environment is not { } environment || _packages is null)
            {
                throw new NotSupportedException("USB-OTG requires the Android Linux environment. Use the Windows Bridge.");
            }

            string[] packages = developmentTools
                ? ["python3", "libusb", "build-base", "libusb-dev", "hidapi-dev", "readline-dev", "popt-dev", "glib-dev", "bison", "flex", "pkgconf"]
                : ["python3", "libusb"];
            var installed = _packages.Installed;
            if (packages.Any(name => installed.Find(name) is null))
            {
                var plan = await _packages.PlanInstallAsync(packages, token).ConfigureAwait(false);
                await _packages.DownloadAsync(plan, (_, package) => line("Downloading " + package.Name), token).ConfigureAwait(false);
                await _packages.InstallAsync(packages, upgrade: false, line, token).ConfigureAwait(false);
            }
            foreach (var name in MicrochipInstallation.SupportFiles)
            {
                var destination = Path.Combine(environment.Rootfs, MicrochipInstallation.ToolsDirectory.TrimStart('/'), name);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllTextAsync(destination, MicrochipInstallation.Resource(name), token).ConfigureAwait(false);
            }
        }, cancellationToken);

    /// <summary>The Linux environment on Android, or <see langword="null"/> on a desktop computer.</summary>
    public AlpineEnvironment? Environment { get; }

    /// <summary>Retrieves Microchip's terms for the Store's explicit acceptance dialog.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<string> ReadMicrochipLicenseAsync(CancellationToken cancellationToken)
    {
        var http = _http ?? throw new InvalidOperationException("Compiler installation needs the tablet's Linux environment.");
        var html = await http.GetStringAsync(MicrochipInstallation.LicenseAddress, cancellationToken).ConfigureAwait(false);
        if (html.Length > 3_000_000)
        {
            throw new InvalidDataException("Microchip's license response is unexpectedly large.");
        }

        var text = Regex.Replace(html, @"<(script|style)\b[^>]*>[\s\S]*?</\1>", string.Empty, RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"</(?:p|div|li|h[1-6])\s*>", "\n\n", RegexOptions.IgnoreCase);
        text = WebUtility.HtmlDecode(Regex.Replace(text, "<[^>]+>", string.Empty));
        var start = text.IndexOf("SOFTWARE LICENSE AGREEMENT", StringComparison.Ordinal);
        var end = text.IndexOf("Questions about this Agreement", start >= 0 ? start : 0, StringComparison.Ordinal);
        if (start < 0 || end <= start || end - start < 5000)
        {
            throw new InvalidDataException("Could not read Microchip's complete software license. Open its license page and try again; no compiler was downloaded.");
        }

        var last = text.IndexOf("\n\n", end, StringComparison.Ordinal);
        return MicrochipInstallation.LicenseAddress + "\n\n" + text[start..(last > end ? last : end)].Trim();
    }

    /// <summary>Records the terms the user accepted for a specific pinned compiler download.</summary>
    /// <param name="id">Compiler id.</param>
    /// <param name="terms">The displayed terms.</param>
    public void AcceptMicrochipLicense(string id, string terms) =>
        MicrochipInstallation.AcceptLicense(Environment ?? throw new InvalidOperationException("No Linux environment."), id, terms);

    /// <summary>Reads pk2cmd's separate, restrictive license before downloading its source.</summary>
    /// <param name="token">Stops loading.</param>
    public async Task<string> ReadPicKitLicenseAsync(CancellationToken token)
    {
        const string address = "https://raw.githubusercontent.com/jaka-fi/pk2cmd/38ad052d453db7d5a7f4a115427cc640e1522ed3/license.txt";
        var text = await (_http ?? throw new NotSupportedException("Install PIC tools on Android.")).GetStringAsync(address, token).ConfigureAwait(false);
        if (text.Length is < 5000 or > 100000 || !text.Contains("Microchip", StringComparison.Ordinal)) throw new InvalidDataException("Could not read the complete PICkit license; its source was not downloaded.");
        return address + "\n\n" + text;
    }

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
