using System.Globalization;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Theming;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>
/// An extension in a list of the Extensions window: a search result, an installed extension or an available update,
/// with its icon, publisher, description, downloads and rating, and whether it is installed.
/// </summary>
public sealed partial class ExtensionItemViewModel : ObservableObject
{
    /// <summary>How many <c>Brush.Avatar.N</c> tokens the initials' tiles pick from (Theme/Colors.axaml).</summary>
    private const int Tints = 8;

    private readonly ExtensionsViewModel _owner;
    private bool _iconRequested;

    private ExtensionItemViewModel(ExtensionsViewModel owner, ExtensionIdentifier id, string displayName, string version, string description)
    {
        _owner = owner;
        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? id.Name : displayName;
        Version = version;
        Description = description;
        Initials = MakeInitials(DisplayName);
        IconTint = Tint(id);
    }

    /// <summary>The extension's identifier, <c>publisher.name</c>.</summary>
    public ExtensionIdentifier Id { get; }

    /// <summary>The extension's name.</summary>
    public string DisplayName { get; }

    /// <summary>The version listed: the registry's latest, or the installed one.</summary>
    public string Version { get; }

    /// <summary>What the extension does, in a sentence.</summary>
    public string Description { get; }

    /// <summary>The publisher, as the registry names it.</summary>
    public string Publisher { get; private init; } = string.Empty;

    /// <summary>Where the icon is on the registry, or <see langword="null"/>.</summary>
    public Uri? IconUrl { get; private init; }

    /// <summary>The icon file of an installed extension, or <see langword="null"/>.</summary>
    public string? IconPath { get; private init; }

    /// <summary>How often it was downloaded, such as "4.5M", or empty when unknown.</summary>
    public string DownloadsText { get; private init; } = string.Empty;

    /// <summary>The average rating, such as "4.6", or empty when it has none.</summary>
    public string RatingText { get; private init; } = string.Empty;

    /// <summary>Whether the registry verified that the publisher owns the namespace.</summary>
    public bool IsVerified { get; private init; }

    /// <summary>Whether the publisher deprecated the extension.</summary>
    public bool IsDeprecated { get; private init; }

    /// <summary>Whether the item shows download and rating counts.</summary>
    public bool HasStatistics => DownloadsText.Length > 0 || RatingText.Length > 0;

    /// <summary>Whether the item shows a rating.</summary>
    public bool HasRating => RatingText.Length > 0;

    /// <summary>The letters shown while the icon loads, or when there is none.</summary>
    public string Initials { get; }

    /// <summary>The color behind the letters, always the same for an extension: one of the avatar tokens, which carry white text in both themes.</summary>
    public IBrush IconTint { get; }

    /// <summary>The icon, once loaded; <see langword="null"/> shows the initials.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasIcon))]
    public partial Bitmap? Icon { get; private set; }

    /// <summary>Whether the icon is loaded.</summary>
    public bool HasIcon => Icon is not null;

    /// <summary>The installed version, or <see langword="null"/> when it is not installed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInstalled), nameof(CanInstall), nameof(CanUpdate), nameof(StateText), nameof(HasState))]
    public partial string? InstalledVersion { get; private set; }

    /// <summary>Whether the installed extension is enabled.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(HasState))]
    public partial bool IsEnabled { get; private set; } = true;

    /// <summary>The newer version on the registry, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate), nameof(CanUpdate), nameof(StateText), nameof(HasState))]
    public partial string? UpdateVersion { get; private set; }

    /// <summary>Whether it is being installed, updated or removed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstall), nameof(CanUpdate), nameof(StateText), nameof(HasState))]
    public partial bool IsBusy { get; private set; }

    /// <summary>How much of the download is done, from 0 to 1, while installing.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>Whether the extension is installed.</summary>
    public bool IsInstalled => InstalledVersion is not null;

    /// <summary>Whether a newer version is available.</summary>
    public bool HasUpdate => UpdateVersion is not null;

    /// <summary>Whether the list shows an Install button for it.</summary>
    public bool CanInstall => !IsInstalled && !IsBusy;

    /// <summary>Whether the list shows an Update button for it.</summary>
    public bool CanUpdate => IsInstalled && HasUpdate && !IsBusy;

    /// <summary>A short state for the list: "Installing…", "Installed" or "Disabled"; empty when a button says it.</summary>
    public string StateText => IsBusy ? "Installing…"
        : IsInstalled && !IsEnabled ? "Disabled"
        : IsInstalled && !HasUpdate ? "Installed"
        : string.Empty;

    /// <summary>Whether the list shows <see cref="StateText"/>.</summary>
    public bool HasState => StateText.Length > 0;

    /// <summary>A search result from the registry.</summary>
    /// <param name="owner">The Extensions window.</param>
    /// <param name="summary">The result.</param>
    public static ExtensionItemViewModel FromSummary(ExtensionsViewModel owner, OpenVsxSummary summary)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(summary);
        return new ExtensionItemViewModel(owner, summary.Id, summary.DisplayName, summary.Version, summary.Description)
        {
            Publisher = summary.Id.Publisher,
            IconUrl = summary.IconUrl,
            DownloadsText = summary.DownloadCount > 0 ? ExtensionImages.FormatCount(summary.DownloadCount) : string.Empty,
            RatingText = summary.AverageRating is { } rating and > 0 ? rating.ToString("0.0", CultureInfo.InvariantCulture) : string.Empty,
            IsVerified = summary.IsVerified,
            IsDeprecated = summary.IsDeprecated,
        };
    }

    /// <summary>An installed extension.</summary>
    /// <param name="owner">The Extensions window.</param>
    /// <param name="installed">The extension.</param>
    public static ExtensionItemViewModel FromInstalled(ExtensionsViewModel owner, InstalledExtension installed)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(installed);
        var manifest = installed.Manifest;
        return new ExtensionItemViewModel(owner, installed.Id, manifest.DisplayName, manifest.Version, manifest.Description)
        {
            Publisher = installed.Id.Publisher,
            IconPath = manifest.Icon is { } icon ? installed.Resolve(icon) : null,
        };
    }

    /// <summary>The registry's latest version of an extension, as the details page shows it.</summary>
    /// <param name="owner">The Extensions window.</param>
    /// <param name="extension">The version.</param>
    public static ExtensionItemViewModel FromRegistry(ExtensionsViewModel owner, OpenVsxExtension extension)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(extension);
        return new ExtensionItemViewModel(owner, extension.Id, extension.DisplayName, extension.Version, extension.Description)
        {
            Publisher = extension.PublisherDisplayName,
            IconUrl = extension.Files.Icon,
            DownloadsText = extension.DownloadCount > 0 ? ExtensionImages.FormatCount(extension.DownloadCount) : string.Empty,
            RatingText = extension.AverageRating is { } rating and > 0 ? rating.ToString("0.0", CultureInfo.InvariantCulture) : string.Empty,
            IsVerified = extension.IsVerified,
            IsDeprecated = extension.IsDeprecated,
        };
    }

    /// <summary>Loads the icon the first time the item is shown.</summary>
    public async Task LoadIconAsync()
    {
        if (_iconRequested)
        {
            return;
        }

        _iconRequested = true;
        try
        {
            Icon = IconPath is { } path ? await ExtensionImages.IconFileAsync(path, CancellationToken.None)
                : IconUrl is { } url ? await ExtensionImages.IconAsync(_owner.Client, url, CancellationToken.None)
                : null;
        }
        catch (OperationCanceledException)
        {
            _iconRequested = false;
        }
    }

    /// <summary>Shows whether and how the extension is installed.</summary>
    /// <param name="installed">The installed extension, or <see langword="null"/>.</param>
    /// <param name="updateVersion">A newer version on the registry, or <see langword="null"/>.</param>
    public void SetState(InstalledExtension? installed, string? updateVersion)
    {
        InstalledVersion = installed?.Version;
        IsEnabled = installed?.IsEnabled ?? true;
        UpdateVersion = installed is null ? null : updateVersion;
    }

    /// <summary>Shows the item as being installed, with the download's progress.</summary>
    /// <param name="busy">Whether it is busy.</param>
    /// <param name="progress">How much is done, from 0 to 1.</param>
    public void SetBusy(bool busy, double progress = 0)
    {
        IsBusy = busy;
        Progress = progress;
    }

    /// <summary>Installs the extension from the registry, as the list's Install button does.</summary>
    [RelayCommand]
    private Task InstallAsync() => _owner.InstallAsync(this);

    /// <summary>Updates the extension to the registry's latest version.</summary>
    [RelayCommand]
    private Task UpdateAsync() => _owner.UpdateAsync(this);

    private static string MakeInitials(string name)
    {
        var words = name.Split([' ', '-', '_', '.'], StringSplitOptions.RemoveEmptyEntries);
        var letters = words.Where(w => char.IsLetterOrDigit(w[0])).Take(2).Select(w => char.ToUpperInvariant(w[0]));
        var initials = new string([.. letters]);
        return initials.Length > 0 ? initials : "?";
    }

    private static IBrush Tint(ExtensionIdentifier id)
    {
        var hash = 0;
        foreach (var c in id.ToString().ToUpperInvariant())
        {
            hash = unchecked((hash * 31) + c);
        }

        return ThemeBrushes.Get(string.Create(CultureInfo.InvariantCulture, $"Brush.Avatar.{((hash & int.MaxValue) % Tints) + 1}"));
    }
}
