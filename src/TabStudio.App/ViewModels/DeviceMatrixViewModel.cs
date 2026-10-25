using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Previews;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.LayoutChecks;
using TabStudio.App.Swarm.Web;

namespace TabStudio.App.ViewModels;

/// <summary>One layout check in the Device Matrix's settings, which turns it on or off.</summary>
public sealed partial class LayoutRuleViewModel : ObservableObject
{
    private readonly Action? _changed;

    /// <summary>Creates the setting of one check.</summary>
    /// <param name="kind">The check.</param>
    /// <param name="isEnabled">Whether it runs.</param>
    /// <param name="changed">Called when the user turns it on or off.</param>
    public LayoutRuleViewModel(LayoutIssueKind kind, bool isEnabled, Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        Kind = kind;
        IsEnabled = isEnabled;

        // Set after the first value, which is not a change the user made.
        _changed = changed;
    }

    /// <summary>The check.</summary>
    public LayoutIssueKind Kind { get; }

    /// <summary>Its code, such as SWRM001.</summary>
    public string Code => LayoutRules.Code(Kind);

    /// <summary>Its name.</summary>
    public string Title => LayoutRules.Title(Kind);

    /// <summary>What it finds.</summary>
    public string Description => LayoutRules.Description(Kind);

    /// <summary>Whether it runs.</summary>
    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    partial void OnIsEnabledChanged(bool value) => _changed?.Invoke();
}

/// <summary>
/// The Device Matrix: every screen the app runs on, side by side, as tiles: the tablet's app preview, the virtual
/// devices the tablet draws, and the phones in the swarm. It owns the virtual devices: they run while the matrix is
/// open, one instance of the app each, and come back as they were the next time it opens.
/// </summary>
public sealed partial class DeviceMatrixViewModel : ObservableObject, IDisposable
{
    /// <summary>The font scale of a virtual device with large text, as Android's largest common setting.</summary>
    public const double LargeFontScale = 1.3;

    /// <summary>The smallest zoom, at which a tablet is a thumbnail.</summary>
    public const double MinimumZoom = 0.2;

    /// <summary>The largest zoom.</summary>
    public const double MaximumZoom = 1.5;

    private const double ZoomStep = 1.25;

    private readonly AppScreenRegistry _registry;
    private readonly DeviceMatrixSettingsStore _store;
    private readonly ILayoutInspector _inspector;
    private readonly ScreenLayoutChecks _checks;
    private readonly Func<AvaloniaAppHost?> _runningApp;
    private readonly Action<LayoutCheckSettings> _applyRules;
    private readonly List<VirtualAppScreen> _devices = [];
    private bool _disposed;

    /// <summary>Opens the matrix: its virtual devices start, on the app if it runs.</summary>
    /// <param name="registry">Every screen the app runs on.</param>
    /// <param name="store">Where the matrix's settings are kept.</param>
    /// <param name="inspector">The layout checks the virtual devices run.</param>
    /// <param name="checks">The layout checks of every screen, for the tiles' badges.</param>
    /// <param name="runningApp">The Avalonia app running now, if any.</param>
    /// <param name="applyRules">Applies the layout checks the user turned on or off.</param>
    public DeviceMatrixViewModel(AppScreenRegistry registry, DeviceMatrixSettingsStore store, ILayoutInspector inspector, ScreenLayoutChecks checks, Func<AvaloniaAppHost?> runningApp, Action<LayoutCheckSettings> applyRules)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(inspector);
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(runningApp);
        ArgumentNullException.ThrowIfNull(applyRules);
        _registry = registry;
        _store = store;
        _inspector = inspector;
        _checks = checks;
        _runningApp = runningApp;
        _applyRules = applyRules;

        var settings = store.Load();
        Zoom = Math.Clamp(settings.Zoom, MinimumZoom, MaximumZoom);
        Rules = [.. LayoutRules.All.Select(kind => new LayoutRuleViewModel(kind, settings.Rules.IsEnabled(kind), OnRulesChanged))];
        HasApp = runningApp() is not null;

        _registry.Screens.CollectionChanged += OnScreensChanged;
        _checks.FindingsChanged += OnFindingsChanged;
        foreach (var device in settings.Devices ?? [.. DevicePresets.Defaults.Select(p => VirtualDeviceSettings.From(NewId(), p.ToProfile()))])
        {
            AddScreen(device.Id, device.ToProfile());
        }

        SyncTiles();
        OnFindingsChanged(this, EventArgs.Empty);
    }

    /// <summary>A tile asks to be scrolled into view, such as one showing a finding.</summary>
    public event EventHandler<ScreenTileViewModel>? BringIntoViewRequested;

    /// <summary>Every screen, in the order of the screens: the preview, the virtual devices, then the phones.</summary>
    public ObservableCollection<ScreenTileViewModel> Tiles { get; } = [];

    /// <summary>The layout checks, each with its switch.</summary>
    public IReadOnlyList<LayoutRuleViewModel> Rules { get; }

    /// <summary>The devices to add, in portrait.</summary>
    public IReadOnlyList<DevicePreset> PortraitPresets { get; } = DevicePresets.Portrait;

    /// <summary>The devices to add, in landscape.</summary>
    public IReadOnlyList<DevicePreset> LandscapePresets { get; } = DevicePresets.Landscape;

    /// <summary>How large screens are drawn, 1 for their real size.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ZoomText))]
    public partial double Zoom { get; set; }

    /// <summary>Whether an Avalonia app is running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    public partial bool HasApp { get; private set; }

    /// <summary>The tile the user last tapped.</summary>
    [ObservableProperty]
    public partial ScreenTileViewModel? FocusedTile { get; private set; }

    /// <summary>The tile shown at full size, if one is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFullSize))]
    public partial ScreenTileViewModel? FullSizeTile { get; private set; }

    /// <summary>The layout finding whose element is outlined in its tiles, if one is.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShownFinding), nameof(ShownFindingText), nameof(IsShownFindingError))]
    public partial LayoutFinding? ShownFinding { get; private set; }

    /// <summary>The width typed for a custom device.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCustomDeviceCommand))]
    public partial string CustomWidth { get; set; } = "390";

    /// <summary>The height typed for a custom device.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCustomDeviceCommand))]
    public partial string CustomHeight { get; set; } = "844";

    /// <summary>The name typed for a custom device, if any.</summary>
    [ObservableProperty]
    public partial string CustomName { get; set; } = string.Empty;

    /// <summary>The number of layout issues on all screens, for the toolbar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    public partial int IssueCount { get; private set; }

    /// <summary>Whether a tile is shown at full size.</summary>
    public bool IsFullSize => FullSizeTile is not null;

    /// <summary>The zoom, such as "50%".</summary>
    public string ZoomText => string.Create(CultureInfo.CurrentCulture, $"{Zoom * 100:0}%");

    /// <summary>Whether a finding is outlined in its tiles.</summary>
    public bool HasShownFinding => ShownFinding is not null;

    /// <summary>Whether the finding outlined is an error, rather than a warning, for the banner's color.</summary>
    public bool IsShownFindingError => ShownFinding?.Severity == LayoutIssueSeverity.Error;

    /// <summary>The banner over the tiles while a finding is outlined.</summary>
    public string? ShownFindingText => ShownFinding is { } finding ? $"{finding.Code} · {finding.Message}" : null;

    /// <summary>Whether a web app runs, in the web preview's tile.</summary>
    public bool HasWebApp => Tiles.Any(t => t.Screen is WebAppScreen);

    /// <summary>The toolbar's summary, such as "4 screens · 3 layout issues".</summary>
    public string SummaryText
    {
        get
        {
            var screens = Tiles.Count == 1 ? "1 screen" : string.Create(CultureInfo.CurrentCulture, $"{Tiles.Count} screens");
            var issues = IssueCount switch
            {
                0 => HasApp || HasWebApp ? "no layout issues" : "the app is not running",
                1 => "1 layout issue",
                _ => string.Create(CultureInfo.CurrentCulture, $"{IssueCount} layout issues"),
            };
            return $"{screens} · {issues}";
        }
    }

    /// <summary>The app started or restarted: every virtual device shows a new instance of it.</summary>
    /// <param name="app">The running app.</param>
    public void OnAppStarted(AvaloniaAppHost app)
    {
        ArgumentNullException.ThrowIfNull(app);
        HasApp = true;
        ShownFinding = null;
        ClearHighlights();
        foreach (var device in _devices)
        {
            device.Start(app);
        }
    }

    /// <summary>The app is stopping or restarting: the virtual devices let go of it.</summary>
    /// <param name="restarting">Whether it starts again in a moment.</param>
    public void OnAppStopping(bool restarting)
    {
        foreach (var device in _devices)
        {
            device.Stop();
        }

        HasApp = restarting;
    }

    /// <summary>Outlines a finding's element in each tile that has it, and focuses the first.</summary>
    /// <param name="finding">The finding.</param>
    public void ShowFinding(LayoutFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        ShownFinding = finding;
        ScreenTileViewModel? first = null;
        foreach (var tile in Tiles)
        {
            var highlights = finding.Screens.Where(s => s.ScreenId == tile.Screen.Id).Select(s => s.Issue.Bounds).ToList();
            tile.SetHighlights(highlights);
            if (highlights.Count > 0)
            {
                first ??= tile;
            }
        }

        if (first is not null)
        {
            Focus(first);
            BringIntoViewRequested?.Invoke(this, first);
        }
    }

    /// <summary>Focuses a tile, as tapping it does.</summary>
    /// <param name="tile">The tile.</param>
    public void Focus(ScreenTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        foreach (var other in Tiles)
        {
            other.IsFocused = ReferenceEquals(other, tile);
        }

        FocusedTile = tile;
    }

    /// <summary>Shows a tile at full size, over the others.</summary>
    /// <param name="tile">The tile.</param>
    public void ShowFullSize(ScreenTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        Focus(tile);
        FullSizeTile = tile;
    }

    /// <summary>Removes a virtual device.</summary>
    /// <param name="tile">Its tile.</param>
    public void RemoveDevice(ScreenTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (tile.Virtual is not { } device)
        {
            return;
        }

        if (ReferenceEquals(FullSizeTile, tile))
        {
            FullSizeTile = null;
        }

        _devices.Remove(device);
        _registry.Remove(device);
        device.Dispose();
        SaveDevices();
    }

    /// <summary>Zooms so the tallest screen fits in the matrix's height.</summary>
    /// <param name="viewport">The room the tiles have.</param>
    /// <param name="chrome">The height each tile adds to its screen, such as its header.</param>
    public void FitTo(Size viewport, double chrome)
    {
        var tallest = Tiles.Count == 0 ? 0 : Tiles.Max(t => t.Profile.Height);
        if (tallest <= 0 || viewport.Height <= chrome)
        {
            return;
        }

        Zoom = Math.Clamp(Math.Floor((viewport.Height - chrome) / tallest * 100) / 100, MinimumZoom, MaximumZoom);
    }

    /// <summary>Saves the virtual devices as they are now.</summary>
    internal void SaveDevices() => Save();

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registry.Screens.CollectionChanged -= OnScreensChanged;
        _checks.FindingsChanged -= OnFindingsChanged;
        foreach (var device in _devices)
        {
            _registry.Remove(device);
            device.Dispose();
        }

        _devices.Clear();
        foreach (var tile in Tiles)
        {
            tile.Dispose();
        }

        Tiles.Clear();
    }

    partial void OnZoomChanged(double value)
    {
        foreach (var tile in Tiles)
        {
            tile.OnZoomChanged();
        }

        Save();
    }

    [RelayCommand]
    private void AddDevice(DevicePreset? preset)
    {
        if (preset is null)
        {
            return;
        }

        var name = UniqueName(preset.Name);
        var screen = AddScreen(NewId(), preset.ToProfile() with { Name = name });
        if (_runningApp() is { } app)
        {
            screen.Start(app);
        }

        SaveDevices();
        if (Tiles.FirstOrDefault(t => ReferenceEquals(t.Screen, screen)) is { } tile)
        {
            Focus(tile);
            BringIntoViewRequested?.Invoke(this, tile);
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddCustomDevice))]
    private void AddCustomDevice()
    {
        if (TryParseSide(CustomWidth) is { } width && TryParseSide(CustomHeight) is { } height)
        {
            AddDevice(DevicePresets.Custom(width, height, CustomName));
            CustomName = string.Empty;
        }
    }

    private bool CanAddCustomDevice() => TryParseSide(CustomWidth) is not null && TryParseSide(CustomHeight) is not null;

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(MaximumZoom, Math.Round(Zoom * ZoomStep, 2));

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(MinimumZoom, Math.Round(Zoom / ZoomStep, 2));

    [RelayCommand]
    private void ResetZoom() => Zoom = 1;

    [RelayCommand]
    private void CloseFullSize() => FullSizeTile = null;

    [RelayCommand]
    private void ClearFinding()
    {
        ShownFinding = null;
        ClearHighlights();
    }

    [RelayCommand]
    private void CheckLayout() => _checks.CheckAll();

    private VirtualAppScreen AddScreen(string id, ScreenProfile profile)
    {
        var screen = new VirtualAppScreen(id, profile, _inspector);
        _devices.Add(screen);
        _registry.Add(screen);
        return screen;
    }

    private void OnScreensChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncTiles();

    /// <summary>One tile per screen, in the screens' order.</summary>
    private void SyncTiles()
    {
        foreach (var gone in Tiles.Where(t => !_registry.Screens.Contains(t.Screen)).ToList())
        {
            Tiles.Remove(gone);
            gone.Dispose();
            if (ReferenceEquals(FullSizeTile, gone))
            {
                FullSizeTile = null;
            }
        }

        for (var i = 0; i < _registry.Screens.Count; i++)
        {
            var screen = _registry.Screens[i];
            var existing = Tiles.FirstOrDefault(t => ReferenceEquals(t.Screen, screen));
            if (existing is null)
            {
                Tiles.Insert(Math.Min(i, Tiles.Count), new ScreenTileViewModel(screen, this));
            }
            else if (Tiles.IndexOf(existing) != i && i < Tiles.Count)
            {
                Tiles.Move(Tiles.IndexOf(existing), i);
            }
        }

        foreach (var tile in Tiles)
        {
            tile.RefreshStatus();
        }

        OnPropertyChanged(nameof(SummaryText));
    }

    /// <summary>The tiles' badges follow the findings.</summary>
    private void OnFindingsChanged(object? sender, EventArgs e)
    {
        var findings = _checks.Findings;
        foreach (var tile in Tiles)
        {
            var mine = findings.SelectMany(f => f.Screens).Where(s => s.ScreenId == tile.Screen.Id).ToList();
            tile.SetIssues(mine.Count(s => s.Issue.Severity == LayoutIssueSeverity.Error), mine.Count(s => s.Issue.Severity == LayoutIssueSeverity.Warning));
        }

        IssueCount = findings.Count;
        if (ShownFinding is { } shown && !findings.Any(f => f.Kind == shown.Kind && f.ElementPath == shown.ElementPath))
        {
            ClearFinding();
        }
    }

    private void OnRulesChanged()
    {
        var rules = LayoutCheckSettings.All;
        foreach (var rule in Rules)
        {
            rules = rules.With(rule.Kind, rule.IsEnabled);
        }

        _applyRules(rules);
        Save();
    }

    private void ClearHighlights()
    {
        foreach (var tile in Tiles)
        {
            tile.SetHighlights([]);
        }
    }

    private void Save()
    {
        if (Rules is null)
        {
            // Still being created.
            return;
        }

        var rules = LayoutCheckSettings.All;
        foreach (var rule in Rules)
        {
            rules = rules.With(rule.Kind, rule.IsEnabled);
        }

        _store.Save(new DeviceMatrixSettings([.. _devices.Select(d => VirtualDeviceSettings.From(d.Id, d.Profile))], rules, Zoom));
    }

    private string UniqueName(string name)
    {
        var taken = _devices.Select(d => d.Profile.Name).ToHashSet(StringComparer.Ordinal);
        if (!taken.Contains(name))
        {
            return name;
        }

        for (var i = 2; ; i++)
        {
            var numbered = string.Create(CultureInfo.InvariantCulture, $"{name} {i}");
            if (!taken.Contains(numbered))
            {
                return numbered;
            }
        }
    }

    private static string NewId() => "virtual-" + Guid.NewGuid().ToString("N")[..8];

    private static double? TryParseSide(string text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) && value >= DevicePresets.MinimumSide && value <= DevicePresets.MaximumSide
            ? value
            : null;
}
