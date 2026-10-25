using TabStudio.App.Editing.Extensions;
using TabStudio.App.Services.Appearance;
using TabStudio.Extensions.Contributions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.OpenVsx;
using TabStudio.Extensions.Themes;

namespace TabStudio.App.Services.Extensions;

/// <summary>An installed extension with a newer version on the registry.</summary>
/// <param name="Installed">The installed extension.</param>
/// <param name="Latest">The registry's latest version.</param>
public sealed record ExtensionUpdate(InstalledExtension Installed, OpenVsxExtension Latest);

/// <summary>
/// VS Code extensions in TabStudio: the extensions folder, the Open VSX registry, and the user's color theme and file
/// icon theme. Whenever installed extensions or the themes change, it publishes what the enabled extensions contribute
/// to every editor through <see cref="EditorExtensions"/>.
/// </summary>
/// <remarks>Call it on the UI thread; long work (downloads, extraction) runs in the background.</remarks>
public sealed class ExtensionsService : IDisposable
{
    /// <summary>The folder under <see cref="AppPaths.Root"/> that holds installed extensions.</summary>
    public const string FolderName = "Extensions";

    private readonly AppPaths _paths;
    private readonly HttpClient _http;
    private readonly ExtensionSettingsStore _settingsStore;
    private readonly Dictionary<string, EditorColorTheme> _colorThemes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _drawableIconThemes = new(StringComparer.Ordinal);
    private readonly List<string> _problems = [];

    /// <summary>Opens the extensions folder and publishes what the installed extensions contribute.</summary>
    /// <param name="paths">Where the app keeps its data.</param>
    /// <param name="http">The HTTP client for the registry.</param>
    /// <param name="appearance">The IDE's theme, which follows the chosen color theme; <see langword="null"/> leaves it alone.</param>
    public ExtensionsService(AppPaths paths, HttpClient http, AppearanceService? appearance = null)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(http);
        _paths = paths;
        _http = http;
        Appearance = appearance;
        _settingsStore = new ExtensionSettingsStore(paths);
        Settings = _settingsStore.Load();
        Store = new ExtensionStore(Path.Combine(paths.Root, FolderName));
        Store.Load();
        Client = CreateClient();
        Refresh();
    }

    /// <summary>Raised after the installed extensions, the themes or the registry changed.</summary>
    public event EventHandler? Changed;

    /// <summary>The installed extensions.</summary>
    public ExtensionStore Store { get; }

    /// <summary>The Open VSX registry.</summary>
    public OpenVsxClient Client { get; private set; }

    /// <summary>What the user chose.</summary>
    public ExtensionSettings Settings { get; private set; }

    /// <summary>What the enabled extensions contribute.</summary>
    public ContributionSet Contributions { get; private set; } = ContributionSet.Empty;

    /// <summary>The color theme in use, or <see langword="null"/> for TabStudio's own.</summary>
    public EditorColorTheme? ColorTheme { get; private set; }

    /// <summary>The IDE's theme, which the color themes set: a light one makes the whole IDE light, as in VS Code.</summary>
    public AppearanceService? Appearance { get; }

    /// <summary>The file icon theme in use, or <see langword="null"/> for TabStudio's badges.</summary>
    public FileIconTheme? IconTheme { get; private set; }

    /// <summary>
    /// Runs extensions' JavaScript, when TabStudio has an extension host; until then <see langword="null"/>, and the
    /// contributions that need code are shown as not running.
    /// </summary>
    public IExtensionCodeHost? CodeHost { get; set; }

    /// <summary>Contributions that could not be read, such as a damaged theme, as messages for the user.</summary>
    public IReadOnlyList<string> Problems => [.. _problems.Distinct(StringComparer.Ordinal), .. Store.Problems, .. Contributions.Problems];

    /// <summary>Downloads an extension from the registry, verifies it and installs it, replacing an installed version.</summary>
    /// <param name="extension">The version to install.</param>
    /// <param name="progress">Receives how much was downloaded, from 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <exception cref="OpenVsxException">The download failed or did not match what the registry published.</exception>
    /// <exception cref="ExtensionPackageException">The package is not a valid extension.</exception>
    public async Task<InstalledExtension> InstallAsync(OpenVsxExtension extension, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(extension);
        var path = Store.CreateDownloadPath();
        try
        {
            var download = await Client.DownloadAsync(extension, path, progress, cancellationToken);
            var verification = download.SignatureVerified ? PackageVerification.Signature
                : download.ChecksumVerified ? PackageVerification.Checksum
                : PackageVerification.None;
            var installed = await Store.InstallAsync(path, new ExtensionSource(ExtensionOrigin.Registry, Client.Registry, Sha256: download.Sha256, Verification: verification), cancellationToken);
            Refresh();
            return installed;
        }
        finally
        {
            TryDelete(path);
        }
    }

    /// <summary>Installs an extension from a .vsix file the user picked.</summary>
    /// <param name="vsix">The file's bytes.</param>
    /// <param name="fileName">The file's name, to show where it came from.</param>
    /// <param name="cancellationToken">Cancels the install.</param>
    /// <exception cref="ExtensionPackageException">The file is not a valid extension package.</exception>
    public async Task<InstalledExtension> InstallFileAsync(Stream vsix, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vsix);
        var installed = await Store.InstallAsync(vsix, new ExtensionSource(ExtensionOrigin.File, FileName: fileName), cancellationToken);
        Refresh();
        return installed;
    }

    /// <summary>Uninstalls an extension; a theme of it that was in use goes back to TabStudio's own.</summary>
    /// <param name="id">The extension.</param>
    public async Task UninstallAsync(ExtensionIdentifier id)
    {
        await Task.Run(() => Store.Uninstall(id));
        Refresh();
    }

    /// <summary>Enables or disables an extension.</summary>
    /// <param name="id">The extension.</param>
    /// <param name="enabled">Whether it is enabled.</param>
    public async Task SetEnabledAsync(ExtensionIdentifier id, bool enabled)
    {
        await Task.Run(() => Store.SetEnabled(id, enabled));
        Refresh();
    }

    /// <summary>Uses a color theme in every editor, and remembers the choice.</summary>
    /// <param name="key">The theme's <see cref="ContributedTheme.Key"/>, or <see langword="null"/> for TabStudio's own.</param>
    public void SelectColorTheme(string? key)
    {
        Settings = Settings with { ColorTheme = key };
        _settingsStore.Save(Settings);
        Refresh();
        if (ColorTheme is { } theme)
        {
            Appearance?.SetTheme(KindOf(theme));
        }
        else
        {
            Appearance?.Apply();
        }
    }

    /// <summary>Shows a color theme in every editor without remembering it, as the theme picker does while browsing.</summary>
    /// <param name="key">The theme's key, or <see langword="null"/> for TabStudio's own.</param>
    public void PreviewColorTheme(string? key)
    {
        ColorTheme = LoadColorTheme(key);
        EditorExtensions.Publish(this, Contributions, ColorTheme, IconTheme);
        if (ColorTheme is { } theme)
        {
            AppearanceService.Preview(KindOf(theme));
        }
        else
        {
            Appearance?.Apply();
        }
    }

    /// <summary>
    /// Uses TabStudio's own colors in a theme of the IDE (Dark, Light or Follow the system), leaving any extension's
    /// color theme, and remembers it.
    /// </summary>
    /// <param name="theme">The IDE's theme.</param>
    public void UseTabStudioTheme(AppTheme theme)
    {
        if (Settings.ColorTheme is not null)
        {
            Settings = Settings with { ColorTheme = null };
            _settingsStore.Save(Settings);
        }

        Refresh();
        Appearance?.SetTheme(theme);
    }

    /// <summary>Shows TabStudio's own colors in a theme of the IDE without remembering it, while the picker browses.</summary>
    /// <param name="theme">The IDE's theme.</param>
    public void PreviewTabStudioTheme(AppTheme theme)
    {
        ColorTheme = null;
        EditorExtensions.Publish(this, Contributions, null, IconTheme);
        AppearanceService.Preview(theme);
    }

    /// <summary>Uses a file icon theme in the Solution Explorer and on tabs, and remembers the choice.</summary>
    /// <param name="key">The theme's <see cref="ContributedIconTheme.Key"/>, or <see langword="null"/> for TabStudio's badges.</param>
    public void SelectIconTheme(string? key)
    {
        Settings = Settings with { IconTheme = key };
        _settingsStore.Save(Settings);
        Refresh();
    }

    /// <summary>
    /// Whether TabStudio can draw a file icon theme: its icons are SVG or PNG images. Themes that draw their icons with
    /// a font, such as Seti, cannot be shown.
    /// </summary>
    /// <param name="theme">The theme.</param>
    public bool CanDraw(ContributedIconTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (!_drawableIconThemes.TryGetValue(theme.Path, out var drawable))
        {
            try
            {
                drawable = FileIconTheme.Read(theme.Path, theme.Label, theme.ExtensionFolder).UsesImages;
            }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
            {
                drawable = false;
            }

            _drawableIconThemes[theme.Path] = drawable;
        }

        return drawable;
    }

    /// <summary>Uses another Open VSX registry, such as a company's own.</summary>
    /// <param name="registry">Its address, or <see langword="null"/> for open-vsx.org.</param>
    /// <exception cref="ArgumentException">The address is not an http or https address.</exception>
    public void SetRegistry(Uri? registry)
    {
        var normalized = registry is null ? null : OpenVsxClient.NormalizeRegistry(registry);
        Settings = Settings with { Registry = normalized is null || normalized == OpenVsxClient.DefaultRegistry ? null : normalized.AbsoluteUri };
        _settingsStore.Save(Settings);
        Client = CreateClient();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Finds installed extensions that have a newer version on the registry.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <exception cref="OpenVsxException">The registry cannot be reached.</exception>
    public async Task<IReadOnlyList<ExtensionUpdate>> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var installed = Store.Installed.Where(e => e.Source.Origin != ExtensionOrigin.Bundled).ToList();
        if (installed.Count == 0)
        {
            return [];
        }

        var latest = await Client.GetLatestAsync(installed.Select(e => e.Id), cancellationToken);
        return [.. installed
            .Select(e => (Installed: e, Latest: latest.FirstOrDefault(l => l.Id == e.Id)))
            .Where(p => p.Latest is not null && ExtensionVersions.IsNewer(p.Latest.Version, p.Installed.Version))
            .Select(p => new ExtensionUpdate(p.Installed, p.Latest!))];
    }

    /// <inheritdoc />
    public void Dispose()
    {
        EditorExtensions.Withdraw(this);
        Store.Dispose();
    }

    /// <summary>Collects the contributions of the enabled extensions, loads the chosen themes and publishes them.</summary>
    private void Refresh()
    {
        _problems.Clear();
        Contributions = ContributionSet.From(Store.Installed);
        foreach (var stale in _colorThemes.Keys.Where(key => Contributions.Themes.All(t => t.Key != key)).ToList())
        {
            _colorThemes.Remove(stale);
        }

        ColorTheme = LoadColorTheme(Settings.ColorTheme);
        IconTheme = LoadIconTheme(Settings.IconTheme);
        EditorExtensions.Publish(this, Contributions, ColorTheme, IconTheme);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The IDE's theme that goes with a color theme: light for a light one, dark otherwise.</summary>
    private static AppTheme KindOf(EditorColorTheme theme) => theme.Source.IsDark ? AppTheme.Dark : AppTheme.Light;

    private EditorColorTheme? LoadColorTheme(string? key)
    {
        if (key is null || Contributions.Themes.FirstOrDefault(t => t.Key == key) is not { } theme)
        {
            return null;
        }

        if (_colorThemes.TryGetValue(key, out var loaded))
        {
            return loaded;
        }

        try
        {
            loaded = new EditorColorTheme(ColorThemeReader.Read(theme.Path, theme.Kind, theme.Label, theme.ExtensionFolder), key);
            _colorThemes[key] = loaded;
            return loaded;
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            _problems.Add($"The color theme {theme.Label} cannot be read: {ex.Message}");
            return null;
        }
    }

    private FileIconTheme? LoadIconTheme(string? key)
    {
        if (key is null || Contributions.IconThemes.FirstOrDefault(t => t.Key == key) is not { } theme)
        {
            return null;
        }

        try
        {
            return FileIconTheme.Read(theme.Path, theme.Label, theme.ExtensionFolder);
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or IOException or UnauthorizedAccessException)
        {
            _problems.Add($"The file icon theme {theme.Label} cannot be read: {ex.Message}");
            return null;
        }
    }

    private OpenVsxClient CreateClient()
    {
        var cache = Path.Combine(_paths.Cache, "OpenVsx");
        try
        {
            return new OpenVsxClient(_http, Uri.TryCreate(Settings.Registry, UriKind.Absolute, out var registry) ? registry : null, cache);
        }
        catch (ArgumentException)
        {
            // A registry address that is not http or https: use open-vsx.org.
            return new OpenVsxClient(_http, null, cache);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The download folder is emptied the next time the extensions folder is loaded.
        }
    }
}
