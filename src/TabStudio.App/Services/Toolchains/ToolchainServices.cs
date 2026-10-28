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
    private readonly EmbeddedGlibcToolchainInstaller? _embeddedInstaller;
    private readonly GlibcToolchainInstaller? _glibcInstaller;
    private readonly Lock _gate = new();
    private HttpClient? _hostHttp;
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
        // Swift and JetBrains' Kotlin language server are built for glibc: they run in a shared Ubuntu environment
        // beside Alpine (Toolchains/glibc), as Flutter and the embedded SDKs run in their own.
        _glibcInstaller = new GlibcToolchainInstaller(
            new LinuxToolchainInstaller(Environment, _packages, downloader), Path.Combine(paths.Root, "Toolchains", "glibc"), Platform, downloader, [paths.Projects]);
        _embeddedInstaller = new EmbeddedGlibcToolchainInstaller(new FlutterToolchainInstaller(
            _glibcInstaller, Path.Combine(paths.Root, "Toolchains", "flutter"), Platform, downloader, [paths.Projects]),
            Path.Combine(paths.Root, "Toolchains", "embedded-glibc"), Platform, downloader, [paths.Projects]);
        Manager = new ToolchainManager(catalog, _embeddedInstaller);
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

    /// <summary>
    /// An HTTP client for package indexes, such as PyPI's JSON API: the Linux environment's on the tablet, its own
    /// on a computer.
    /// </summary>
    internal HttpClient Http
    {
        get
        {
            if (_http is { } http)
            {
                return http;
            }

            lock (_gate)
            {
                if (_hostHttp is null)
                {
                    _hostHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
                    _hostHttp.DefaultRequestHeaders.UserAgent.ParseAdd("TabStudio/0.1 (+https://github.com/yahav2898/tabstudio)");
                }

                return _hostHttp;
            }
        }
    }

    /// <summary>Whether <see cref="InstallAlpinePackagesAsync"/> can install packages: on the tablet, in its Linux environment.</summary>
    internal bool CanInstallAlpinePackages => _packages is not null && Environment is not null;

    /// <summary>
    /// Installs Alpine packages into the Linux environment, such as the compilers and headers a Python package
    /// builds with, with the Toolchain Store's verified, resumable downloads. Packages already installed are kept.
    /// </summary>
    /// <param name="names">The packages, such as <c>build-base</c> and <c>libpq-dev</c>.</param>
    /// <param name="line">Progress, for the log.</param>
    /// <param name="cancellationToken">Stops the install.</param>
    /// <returns>Whether they are installed; <see langword="false"/> on a computer, which uses its own tools.</returns>
    internal async Task<bool> InstallAlpinePackagesAsync(IReadOnlyList<string> names, Action<string> line, CancellationToken cancellationToken)
    {
        if (_packages is not { } packages || Environment is not { } environment)
        {
            return false;
        }

        await Manager.PrepareRuntimeAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await WithPackageOperationAsync(async () =>
        {
            var installed = packages.Installed;
            var missing = names.Where(n => installed.Find(n) is null).ToList();
            if (missing.Count == 0)
            {
                return;
            }

            var plan = ApkResolver.PlanInstall(await packages.LoadIndexAsync(refresh: false, cancellationToken).ConfigureAwait(false), installed.Packages, missing);
            if (plan.Missing.Count > 0)
            {
                throw new InvalidOperationException($"Alpine Linux has no package {string.Join(", ", plan.Missing)}.");
            }

            environment.EnsureSpace(plan.DownloadSize + plan.InstalledSize, string.Join(", ", missing));
            await packages.DownloadAsync(plan, (bytes, package) => line($"Downloading {package.Name} ({bytes / 1024} KB)"), cancellationToken).ConfigureAwait(false);
            await packages.InstallAsync(missing, upgrade: false, line, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        return true;
    }

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

    /// <summary>
    /// The version of an embedded SDK on this device, read from the SDK's own version file: in the embedded glibc
    /// environment on a tablet, or where <c>PICO_SDK_PATH</c>, <c>IDF_PATH</c> or <c>ZEPHYR_BASE</c> points on a computer.
    /// <see langword="null"/> when it is not installed or does not say.
    /// </summary>
    /// <param name="toolchainId">The SDK's Toolchain Store entry, such as <c>pico-sdk</c>.</param>
    public string? EmbeddedSdkVersion(string toolchainId)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolchainId);
        if (TabStudio.Toolchains.Embedded.Installation.EmbeddedSdkBootstrap.Recipes.Get(toolchainId) is not { } recipe)
        {
            return null;
        }

        var variable = toolchainId switch { "pico-sdk" => "PICO_SDK_PATH", "esp-idf" => "IDF_PATH", "zephyr" => "ZEPHYR_BASE", _ => null };
        var directory = _embeddedInstaller is not null
            ? Path.Combine(_embeddedInstaller.Rootfs, recipe.SourceDirectory.TrimStart('/'))
            : variable is null ? null : System.Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrEmpty(directory) ? null : TabStudio.Toolchains.Embedded.Projects.EmbeddedSdkRequirements.InstalledVersion(toolchainId, directory);
    }

    /// <summary>Prepares PlatformIO's native-first x86 fallback without changing compatible native package selection.</summary>
    /// <param name="line">Verified runtime preparation output.</param>
    /// <param name="cancellationToken">Stops preparation.</param>
    public Task PrepareEmbeddedBox64Async(Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_embeddedInstaller is null)
        {
            throw new NotSupportedException("PlatformIO's optional Box64 package mode requires the Android embedded glibc environment.");
        }
        return Manager.ConfigureRuntimeAsync(token => _embeddedInstaller.PrepareBox64Async(line, token), cancellationToken);
    }

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

    /// <summary>Installs permissioned libusb and POSIX serial bridges without installing a compiler.</summary>
    /// <param name="line">Package and native compilation output.</param>
    /// <param name="cancellationToken">Stops installation and compilation.</param>
    public Task PrepareEmbeddedSupportAsync(Action<string> line, CancellationToken cancellationToken) =>
        Manager.ConfigureRuntimeAsync(async token =>
        {
            if (Environment is not { } environment || _packages is null)
            {
                throw new NotSupportedException("USB-OTG requires the Android Linux environment.");
            }

            var packages = TabStudio.Toolchains.Embedded.UsbBridgeInstallation.Packages;
            if (packages.Any(name => _packages.Installed.Find(name) is null))
            {
                var plan = await _packages.PlanInstallAsync(packages, token).ConfigureAwait(false);
                await _packages.DownloadAsync(plan, (_, package) => line("Downloading " + package.Name), token).ConfigureAwait(false);
                await _packages.InstallAsync(packages, upgrade: false, line, token).ConfigureAwait(false);
            }

            var sources = TabStudio.Toolchains.Embedded.UsbBridgeInstallation.SupportFiles;
            var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                string.Join('\n', packages) + "\n" + string.Join('\n', sources.Select(source => source.Key + "\n" + source.Value)) +
                string.Join('\n', TabStudio.Toolchains.Embedded.UsbBridgeInstallation.BuildCommands.Select(command => command.CommandLine)))));
            var stamp = Path.Combine(environment.Rootfs, TabStudio.Toolchains.Embedded.UsbBridgeInstallation.ToolsDirectory.TrimStart('/'), ".bridge-build.sha256");
            var rebuild = !File.Exists(stamp) || await File.ReadAllTextAsync(stamp, token).ConfigureAwait(false) != fingerprint;
            foreach (var source in sources)
            {
                var destination = Path.Combine(environment.Rootfs, TabStudio.Toolchains.Embedded.UsbBridgeInstallation.ToolsDirectory.TrimStart('/'), source.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                if (!File.Exists(destination) || await File.ReadAllTextAsync(destination, token).ConfigureAwait(false) != source.Value)
                {
                    await File.WriteAllTextAsync(destination, source.Value, token).ConfigureAwait(false);
                    rebuild = true;
                }
            }

            rebuild |= TabStudio.Toolchains.Embedded.UsbBridgeInstallation.BuildCommands.Any(command =>
                !File.Exists(Path.Combine(environment.Rootfs, command.Arguments[9].TrimStart('/'))));
            if (rebuild) File.Delete(stamp);
            foreach (var command in TabStudio.Toolchains.Embedded.UsbBridgeInstallation.BuildCommands)
            {
                if (!rebuild) continue;
                var result = await TabStudio.Toolchains.Processes.CommandRunner.RunAsync(Runtime, command, line, token).ConfigureAwait(false);
                if (!result.Succeeded) throw new IOException("Could not build the embedded USB bridge: " + result.Tail());
            }
            if (!File.Exists(Path.Combine(environment.Rootfs, "usr", "lib", "libhidapi-libusb.so.0")))
                throw new IOException("The signed hidapi package did not provide its libusb backend; CMSIS-DAP v1 cannot use Android hidraw.");
            await File.WriteAllTextAsync(stamp, fingerprint, token).ConfigureAwait(false);
        }, cancellationToken);

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
        _embeddedInstaller?.Dispose();
        _glibcInstaller?.Dispose();
        _packages?.Dispose();
        _http?.Dispose();
        _hostHttp?.Dispose();
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
