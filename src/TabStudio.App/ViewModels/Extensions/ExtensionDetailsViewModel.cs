using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Extensions.Contributions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>
/// An extension's details page: its icon, name, publisher, version and description, the buttons to install, update,
/// enable, disable, uninstall it or use its theme, its README and change log rendered as Markdown, what it adds and
/// whether each part works on this tablet, and more information such as its license and where it was installed from.
/// </summary>
public sealed partial class ExtensionDetailsViewModel : ObservableObject, IDisposable
{
    private readonly ExtensionsViewModel _owner;
    private readonly CancellationTokenSource _loading = new();
    private ExtensionManifest? _registryManifest;

    /// <summary>Creates the details of an extension; <see cref="LoadAsync"/> fills them in.</summary>
    /// <param name="owner">The Extensions window.</param>
    /// <param name="item">The extension.</param>
    public ExtensionDetailsViewModel(ExtensionsViewModel owner, ExtensionItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(item);
        _owner = owner;
        Item = item;
        Item.PropertyChanged += OnItemPropertyChanged;
        Refresh();
    }

    /// <summary>The extension, as the list shows it.</summary>
    public ExtensionItemViewModel Item { get; }

    /// <summary>The installed extension, or <see langword="null"/>.</summary>
    public InstalledExtension? Installed => _owner.Service.Store.Find(Item.Id);

    /// <summary>The registry's latest version, once loaded; <see langword="null"/> for an extension the registry does not have.</summary>
    [ObservableProperty]
    public partial OpenVsxExtension? Registry { get; private set; }

    /// <summary>The page shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReadmeSelected), nameof(IsFeaturesSelected), nameof(IsChangelogSelected))]
    public partial ExtensionDetailsTab SelectedTab { get; set; }

    /// <summary>Whether the README is shown.</summary>
    public bool IsReadmeSelected => SelectedTab == ExtensionDetailsTab.Details;

    /// <summary>Whether the features are shown.</summary>
    public bool IsFeaturesSelected => SelectedTab == ExtensionDetailsTab.Features;

    /// <summary>Whether the change log is shown.</summary>
    public bool IsChangelogSelected => SelectedTab == ExtensionDetailsTab.Changelog;

    /// <summary>The publisher, as the registry names it.</summary>
    [ObservableProperty]
    public partial string Publisher { get; private set; } = string.Empty;

    /// <summary>The README.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReadmeSegmentViewModel> Readme { get; private set; } = [];

    /// <summary>Why there is no README, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasReadmeMessage))]
    public partial string? ReadmeMessage { get; private set; }

    /// <summary>Whether <see cref="ReadmeMessage"/> is shown.</summary>
    public bool HasReadmeMessage => ReadmeMessage is not null;

    /// <summary>Whether the README is loading.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    /// <summary>The change log.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChangelog))]
    public partial IReadOnlyList<ReadmeSegmentViewModel> Changelog { get; private set; } = [];

    /// <summary>Whether the extension has a change log.</summary>
    public bool HasChangelog => Changelog.Count > 0;

    /// <summary>What the extension adds, and whether each part works here.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ExtensionFeatureViewModel> Features { get; private set; } = [];

    /// <summary>A sentence about <see cref="Features"/>, such as "2 of 3 features work on this tablet."</summary>
    [ObservableProperty]
    public partial string FeaturesSummary { get; private set; } = string.Empty;

    /// <summary>More information: identifier, version, dates, license, source and verification.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ExtensionInfoRow> Info { get; private set; } = [];

    /// <summary>The source repository, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRepository))]
    public partial Uri? Repository { get; private set; }

    /// <summary>The home page, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHomepage))]
    public partial Uri? Homepage { get; private set; }

    /// <summary>The extension's page on the registry's website, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRegistryPage))]
    public partial Uri? RegistryPage { get; private set; }

    /// <summary>Whether a repository link is shown.</summary>
    public bool HasRepository => Repository is not null && _owner.CanOpenLinks;

    /// <summary>Whether a home page link is shown.</summary>
    public bool HasHomepage => Homepage is not null && _owner.CanOpenLinks;

    /// <summary>Whether a registry page link is shown.</summary>
    public bool HasRegistryPage => RegistryPage is not null && _owner.CanOpenLinks;

    /// <summary>A warning shown under the header, such as a deprecation or code that does not run, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    public partial string? Warning { get; private set; }

    /// <summary>Whether <see cref="Warning"/> is shown.</summary>
    public bool HasWarning => Warning is not null;

    /// <summary>Whether Install is offered.</summary>
    public bool CanInstall => Installed is null && !Item.IsBusy && Registry is not null;

    /// <summary>Whether Update is offered.</summary>
    public bool CanUpdate => Installed is not null && Item.HasUpdate && !Item.IsBusy;

    /// <summary>Whether Uninstall is offered.</summary>
    public bool CanUninstall => Installed is not null && !Item.IsBusy;

    /// <summary>Whether Enable is offered.</summary>
    public bool CanEnable => Installed is { IsEnabled: false } && !Item.IsBusy;

    /// <summary>Whether Disable is offered.</summary>
    public bool CanDisable => Installed is { IsEnabled: true } && !Item.IsBusy;

    /// <summary>Whether Set Color Theme is offered: the extension is enabled and has color themes.</summary>
    public bool CanUseColorTheme => Installed is { IsEnabled: true } installed && installed.Manifest.Contributes.Themes.Count > 0;

    /// <summary>Whether Set File Icon Theme is offered.</summary>
    public bool CanUseIconTheme => Installed is { IsEnabled: true } installed && installed.Manifest.Contributes.IconThemes.Count > 0;

    /// <summary>The version shown in the header: the installed one, with the registry's when it is newer.</summary>
    public string VersionText => Installed is { } installed
        ? Item.HasUpdate ? $"v{installed.Version} (v{Item.UpdateVersion} available)" : $"v{installed.Version}"
        : $"v{Registry?.Version ?? Item.Version}";

    /// <summary>Loads the registry's details, the README and the change log.</summary>
    public async Task LoadAsync()
    {
        var token = _loading.Token;
        IsLoading = true;
        try
        {
            await LoadRegistryAsync(token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            Refresh();
            Readme = await LoadDocumentAsync(Installed is { } installed ? FindFile(installed.Folder, "README") : null, Registry?.Files.Readme, token);
            ReadmeMessage = Readme.Count == 0 && !token.IsCancellationRequested ? "This extension has no README." : null;
            Changelog = await LoadDocumentAsync(Installed is { } current ? FindFile(current.Folder, "CHANGELOG") : null, Registry?.Files.Changelog, token);
            foreach (var image in Readme.Concat(Changelog).OfType<ReadmeImageViewModel>())
            {
                _ = image.LoadAsync(_owner.Client, token);
            }
        }
        catch (OperationCanceledException)
        {
            // Another extension was selected.
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Shows the installed state again, after an install, update or uninstall.</summary>
    public void Refresh()
    {
        var installed = Installed;
        var manifest = installed?.Manifest ?? _registryManifest;
        Publisher = Registry?.PublisherDisplayName ?? Item.Publisher;
        if (manifest is not null)
        {
            var statuses = WithIconThemeSupport(ContributionSupport.Describe(manifest, _owner.Service.CodeHost), installed);
            Features = ExtensionFeatureViewModel.From(statuses, installed is { IsEnabled: true });
            var working = statuses.Count(s => s.Support == ContributionSupportLevel.Supported);
            FeaturesSummary = statuses.Count == 0 ? "This extension adds nothing TabStudio can show."
                : working == statuses.Count ? (statuses.Count == 1 ? "Everything it adds works on this tablet." : $"All {statuses.Count} things it adds work on this tablet.")
                : working == 0 ? "None of what it adds works on this tablet: it all needs the extension's code."
                : $"{working} of the {statuses.Count} things it adds work on this tablet; the rest need the extension's code.";
            Warning = Item.IsDeprecated || Registry?.IsDeprecated == true
                ? Registry?.Replacement is { } replacement ? $"This extension is deprecated. Use {replacement} instead." : "This extension is deprecated."
                : manifest.HasCode && working == 0 ? "This extension works through its code, which TabStudio does not run. Installing it adds nothing yet."
                : manifest.HasCode ? "Parts of this extension work through its code, which TabStudio does not run. The Features tab shows what works."
                : null;
        }
        else
        {
            Features = [];
            FeaturesSummary = IsLoading ? "Loading…" : "What this extension adds is not known until it can be read.";
        }

        Repository = manifest?.Repository ?? Registry?.Repository;
        Homepage = manifest?.Homepage ?? Registry?.Homepage;
        RegistryPage = Registry is null ? null : new Uri(_owner.Client.Registry, $"extension/{Item.Id.Publisher}/{Item.Id.Name}");
        Info = BuildInfo(installed, manifest);
        NotifyActions();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Item.PropertyChanged -= OnItemPropertyChanged;
        _loading.Cancel();
        _loading.Dispose();
    }

    [RelayCommand]
    private void SelectTab(ExtensionDetailsTab tab) => SelectedTab = tab;

    [RelayCommand]
    private Task InstallAsync() => _owner.InstallAsync(Item);

    [RelayCommand]
    private Task UpdateAsync() => _owner.UpdateAsync(Item);

    [RelayCommand]
    private Task UninstallAsync() => _owner.UninstallAsync(Item.Id);

    [RelayCommand]
    private Task EnableAsync() => _owner.SetEnabledAsync(Item.Id, enabled: true);

    [RelayCommand]
    private Task DisableAsync() => _owner.SetEnabledAsync(Item.Id, enabled: false);

    [RelayCommand]
    private Task UseColorThemeAsync() => _owner.UseColorThemeAsync(Item.Id);

    [RelayCommand]
    private Task UseIconThemeAsync() => _owner.UseIconThemeAsync(Item.Id);

    [RelayCommand]
    private Task OpenLinkAsync(Uri? link) => link is null ? Task.CompletedTask : _owner.OpenLinkAsync(link);

    private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => NotifyActions();

    private void NotifyActions()
    {
        OnPropertyChanged(nameof(CanInstall));
        OnPropertyChanged(nameof(CanUpdate));
        OnPropertyChanged(nameof(CanUninstall));
        OnPropertyChanged(nameof(CanEnable));
        OnPropertyChanged(nameof(CanDisable));
        OnPropertyChanged(nameof(CanUseColorTheme));
        OnPropertyChanged(nameof(CanUseIconTheme));
        OnPropertyChanged(nameof(VersionText));
    }

    /// <summary>File icon themes that draw their icons with a font cannot be shown: their features say so.</summary>
    private List<ContributionStatus> WithIconThemeSupport(IReadOnlyList<ContributionStatus> statuses, InstalledExtension? installed)
    {
        List<ContributedIconTheme> themes = installed is null ? [] : [.. _owner.Service.Contributions.IconThemes.Where(t => t.Owner == installed.Id)];
        return [.. statuses.Select(status =>
            status.Point == "iconThemes" && themes.FirstOrDefault(t => status.Title.EndsWith(t.Label, StringComparison.Ordinal)) is { } theme && !_owner.Service.CanDraw(theme)
                ? status with { Detail = "Draws its icons with a font, which TabStudio cannot show", Support = ContributionSupportLevel.NotSupported }
                : status)];
    }

    /// <summary>Reads the registry's latest version and its package.json, so the Features page can list what it adds before it is installed.</summary>
    private async Task LoadRegistryAsync(CancellationToken cancellationToken)
    {
        var installed = Installed;
        if (installed is { Source.Origin: ExtensionOrigin.Bundled })
        {
            return;
        }

        OpenVsxExtension registry;
        try
        {
            registry = await _owner.Client.GetExtensionAsync(Item.Id, cancellationToken: cancellationToken);
            Registry = registry;
        }
        catch (OpenVsxException) when (installed is not null)
        {
            // Installed from a file, or the registry cannot be reached: the installed package says enough.
            return;
        }
        catch (OpenVsxException ex)
        {
            ReadmeMessage = ex.Message;
            return;
        }

        if (installed is null && registry.Files.Manifest is { } manifest)
        {
            try
            {
                var json = await _owner.Client.GetTextAsync(manifest, cancellationToken);
                _registryManifest = ManifestReader.Read(json, await ReadNlsAsync(registry, cancellationToken));
            }
            catch (Exception ex) when (ex is OpenVsxException or ExtensionPackageException)
            {
                _registryManifest = null;
            }
        }
    }

    /// <summary>The package's English strings for the %placeholders% of its package.json, or <see langword="null"/>.</summary>
    private async Task<string?> ReadNlsAsync(OpenVsxExtension extension, CancellationToken cancellationToken)
    {
        var address = new Uri(_owner.Client.Registry, $"api/{extension.Id.Publisher}/{extension.Id.Name}/{extension.Version}/file/package.nls.json");
        try
        {
            return await _owner.Client.GetTextAsync(address, cancellationToken);
        }
        catch (OpenVsxException)
        {
            return null;
        }
    }

    /// <summary>Reads a README or change log from the installed package, or else from the registry, into segments.</summary>
    private async Task<IReadOnlyList<ReadmeSegmentViewModel>> LoadDocumentAsync(string? file, Uri? address, CancellationToken cancellationToken)
    {
        string? text = null;
        if (file is not null)
        {
            try
            {
                text = await File.ReadAllTextAsync(file, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                text = null;
            }
        }

        if (text is null && address is not null)
        {
            try
            {
                text = await _owner.Client.GetTextAsync(address, cancellationToken);
            }
            catch (OpenVsxException)
            {
                text = null;
            }
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var segments = await Task.Run(() => ReadmeParser.Parse(text), cancellationToken);
        return [.. segments.Select(segment => segment switch
        {
            ReadmeImageSource image => (ReadmeSegmentViewModel)CreateImage(image),
            ReadmeText markdown => new ReadmeTextViewModel(markdown.Blocks),
            _ => new ReadmeTextViewModel([]),
        })];
    }

    /// <summary>
    /// An image of the README: a web address as it is, a path inside the package from the installed extension's folder
    /// or through the registry's file API.
    /// </summary>
    private ReadmeImageViewModel CreateImage(ReadmeImageSource image)
    {
        var source = image.Source.Trim();
        if (Uri.TryCreate(source, UriKind.Absolute, out var absolute))
        {
            return absolute.Scheme is "http" or "https"
                ? new ReadmeImageViewModel(image.AltText, absolute, null)
                : new ReadmeImageViewModel(image.AltText, null, null);
        }

        var relative = source.TrimStart('.', '/');
        var question = relative.IndexOfAny(['?', '#']);
        if (question >= 0)
        {
            relative = relative[..question];
        }

        if (Installed is { } installed && installed.Resolve(relative) is { } path && File.Exists(path))
        {
            return new ReadmeImageViewModel(image.AltText, null, path);
        }

        if (Registry is { } registry)
        {
            var address = new Uri(_owner.Client.Registry, $"api/{registry.Id.Publisher}/{registry.Id.Name}/{registry.Version}/file/{Uri.EscapeDataString(relative).Replace("%2F", "/", StringComparison.Ordinal)}");
            return new ReadmeImageViewModel(image.AltText, address, null);
        }

        return new ReadmeImageViewModel(image.AltText, null, null);
    }

    private List<ExtensionInfoRow> BuildInfo(InstalledExtension? installed, ExtensionManifest? manifest)
    {
        var rows = new List<ExtensionInfoRow> { new("Identifier", Item.Id.ToString()) };
        if (installed is not null)
        {
            rows.Add(new("Installed version", installed.Version));
            rows.Add(new("Installed", installed.InstalledAt.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture)));
            rows.Add(new("Installed from", installed.Source switch
            {
                { Origin: ExtensionOrigin.File } source => $"VSIX file {source.FileName}",
                { Origin: ExtensionOrigin.Bundled } => "Included with TabStudio",
                { Origin: ExtensionOrigin.Registry } source => $"{source.Registry?.Host ?? "Open VSX"}, {Verification(source.Verification)}",
                _ => "Unknown",
            }));
        }

        if (Registry is { } registry)
        {
            rows.Add(new("Latest version", registry.Version));
            if (registry.Published is { } published)
            {
                rows.Add(new("Published", published.ToLocalTime().ToString("d MMM yyyy", CultureInfo.CurrentCulture)));
            }

            if (registry.DownloadCount > 0)
            {
                rows.Add(new("Downloads", registry.DownloadCount.ToString("N0", CultureInfo.CurrentCulture)));
            }

            if (registry.AverageRating is { } rating && registry.ReviewCount > 0)
            {
                rows.Add(new("Rating", string.Create(CultureInfo.CurrentCulture, $"{rating:0.0} of 5 ({registry.ReviewCount} reviews)")));
            }
        }

        if ((manifest?.License ?? Registry?.License) is { Length: > 0 } license)
        {
            rows.Add(new("License", license));
        }

        var categories = manifest is { Categories.Count: > 0 } ? manifest.Categories : Registry?.Categories ?? [];
        if (categories.Count > 0)
        {
            rows.Add(new("Categories", string.Join(", ", categories)));
        }

        if ((manifest?.EngineVersion ?? Registry?.EngineVersion) is { Length: > 0 } engine)
        {
            rows.Add(new("Made for", $"VS Code {engine}"));
        }

        return rows;
    }

    private static string Verification(PackageVerification verification) => verification switch
    {
        PackageVerification.Signature => "signature verified",
        PackageVerification.Checksum => "checksum verified",
        _ => "not verified",
    };

    /// <summary>A file such as README.md in an extension's folder, whatever its case and extension.</summary>
    private static string? FindFile(string folder, string name)
    {
        try
        {
            return Directory.EnumerateFiles(folder)
                .Where(f => Path.GetFileNameWithoutExtension(f).Equals(name, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => Path.GetExtension(f).Equals(".md", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
