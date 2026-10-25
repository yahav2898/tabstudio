using System.Globalization;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Swarm;

namespace TabStudio.App.ViewModels;

/// <summary>
/// One tile of the Device Matrix: a screen the app runs on, with its profile in the header, a badge with its layout
/// issues, and, for a virtual device, the actions that change it: rotate, theme, direction, font scale and remove.
/// </summary>
/// <remarks>
/// The preview and virtual devices are drawn live; a phone in the swarm shows pictures it sends, refreshed whenever it
/// changes and every few seconds while the matrix is on view.
/// </remarks>
public sealed partial class ScreenTileViewModel : ObservableObject, IDisposable
{
    /// <summary>How often a phone's picture is refreshed while the matrix is on view.</summary>
    private static readonly TimeSpan SnapshotInterval = TimeSpan.FromSeconds(3);

    private readonly DeviceMatrixViewModel _matrix;
    private readonly DispatcherTimer? _snapshots;
    private bool _capturing;
    private bool _disposed;

    /// <summary>Creates the tile of a screen.</summary>
    /// <param name="screen">The screen.</param>
    /// <param name="matrix">The matrix it is in.</param>
    public ScreenTileViewModel(IAppScreen screen, DeviceMatrixViewModel matrix)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(matrix);
        Screen = screen;
        _matrix = matrix;
        Profile = screen.Profile;
        IsRunning = screen.IsRunning;
        screen.Changed += OnScreenChanged;
        if (screen.Kind == AppScreenKind.Device)
        {
            _snapshots = new DispatcherTimer { Interval = SnapshotInterval };
            _snapshots.Tick += (_, _) => _ = RefreshSnapshotAsync();
        }

        UpdateStatus();
    }

    /// <summary>The screen.</summary>
    public IAppScreen Screen { get; }

    /// <summary>The virtual device, for a virtual device's tile.</summary>
    public VirtualAppScreen? Virtual => Screen as VirtualAppScreen;

    /// <summary>The app preview's screen, for its tile.</summary>
    public PreviewAppScreen? Preview => Screen as PreviewAppScreen;

    /// <summary>Whether this is a virtual device, which the tile's actions change.</summary>
    public bool IsVirtual => Screen.Kind == AppScreenKind.Virtual;

    /// <summary>Whether this is the tablet's own app preview.</summary>
    public bool IsPreview => Screen.Kind == AppScreenKind.Preview;

    /// <summary>Whether this is a phone or tablet in the swarm, shown by its pictures.</summary>
    public bool IsDevice => Screen.Kind == AppScreenKind.Device;

    /// <summary>How the screen looks to the app.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title), nameof(Details), nameof(ScreenWidth), nameof(ScreenHeight), nameof(IsPhoneSized), nameof(IsDark), nameof(IsRightToLeft), nameof(HasLargeText))]
    public partial ScreenProfile Profile { get; private set; }

    /// <summary>Whether the app runs on the screen.</summary>
    [ObservableProperty]
    public partial bool IsRunning { get; private set; }

    /// <summary>What the tile says instead of the app, when the app does not run on it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string? StatusText { get; private set; }

    /// <summary>The layout errors found on the screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors), nameof(HasWarningsOnly), nameof(IsClean), nameof(IssuesText))]
    public partial int ErrorCount { get; private set; }

    /// <summary>The layout warnings found on the screen.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors), nameof(HasWarningsOnly), nameof(IsClean), nameof(IssuesText))]
    public partial int WarningCount { get; private set; }

    /// <summary>Whether this is the tile the user last tapped.</summary>
    [ObservableProperty]
    public partial bool IsFocused { get; set; }

    /// <summary>The last picture of a phone in the swarm.</summary>
    [ObservableProperty]
    public partial Bitmap? Snapshot { get; private set; }

    /// <summary>Where the element of the finding being shown is on this screen, in its coordinates; empty for none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHighlights))]
    public partial IReadOnlyList<Rect> Highlights { get; private set; } = [];

    /// <summary>The screen's name.</summary>
    public string Title => Profile.Name;

    /// <summary>Its size, kind, theme and direction, such as "360 × 640 · Phone · Light".</summary>
    public string Details => ScreenDetails.Describe(Profile);

    /// <summary>Where the screen is: this tablet, a virtual device, or a phone in the swarm.</summary>
    public string KindText => Screen.Kind switch
    {
        AppScreenKind.Preview => "App Preview on this tablet",
        AppScreenKind.Virtual => "Virtual device",
        _ => "In the swarm",
    };

    /// <summary>Whether it is a phone or a large phone, for its icon.</summary>
    public bool IsPhoneSized => Profile.FormFactor is FormFactor.Phone or FormFactor.Phablet;

    /// <summary>Whether its theme is dark.</summary>
    public bool IsDark => Profile.IsDark;

    /// <summary>Whether its text runs right to left.</summary>
    public bool IsRightToLeft => Profile.Direction == TextDirection.RightToLeft;

    /// <summary>Whether its fonts are scaled up.</summary>
    public bool HasLargeText => Profile.FontScale > 1.01;

    /// <summary>Whether there is a status to show instead of the app.</summary>
    public bool HasStatus => StatusText is not null;

    /// <summary>Whether the layout checks found errors on it.</summary>
    public bool HasErrors => ErrorCount > 0;

    /// <summary>Whether they found warnings and no errors.</summary>
    public bool HasWarningsOnly => ErrorCount == 0 && WarningCount > 0;

    /// <summary>Whether they found nothing.</summary>
    public bool IsClean => ErrorCount == 0 && WarningCount == 0;

    /// <summary>The badge's text: how many issues, or a check mark's words.</summary>
    public string IssuesText => ErrorCount + WarningCount is var count and > 0
        ? count.ToString(CultureInfo.CurrentCulture)
        : "OK";

    /// <summary>Whether an element is outlined on the screen.</summary>
    public bool HasHighlights => Highlights.Count > 0;

    /// <summary>The screen's width in the matrix, at its zoom.</summary>
    public double ScreenWidth => Math.Round(Profile.Width * _matrix.Zoom);

    /// <summary>The screen's height in the matrix, at its zoom.</summary>
    public double ScreenHeight => Math.Round(Profile.Height * _matrix.Zoom);

    /// <summary>How large the matrix draws screens, for the tile's content.</summary>
    public double Zoom => _matrix.Zoom;

    /// <summary>The layout issues found on the screen, for its badge.</summary>
    /// <param name="errors">The errors.</param>
    /// <param name="warnings">The warnings.</param>
    public void SetIssues(int errors, int warnings)
    {
        ErrorCount = errors;
        WarningCount = warnings;
    }

    /// <summary>Outlines elements on the screen, or none.</summary>
    /// <param name="highlights">Where they are, in the screen's coordinates.</param>
    public void SetHighlights(IReadOnlyList<Rect> highlights)
    {
        ArgumentNullException.ThrowIfNull(highlights);
        Highlights = highlights;
    }

    /// <summary>Starts or stops refreshing a phone's picture, as the matrix comes on view or goes.</summary>
    /// <param name="onView">Whether the matrix is on view.</param>
    public void SetOnView(bool onView)
    {
        if (_snapshots is null)
        {
            return;
        }

        if (onView)
        {
            _snapshots.Start();
            _ = RefreshSnapshotAsync();
        }
        else
        {
            _snapshots.Stop();
        }
    }

    /// <summary>The matrix zoomed: the tile's size follows.</summary>
    internal void OnZoomChanged()
    {
        OnPropertyChanged(nameof(Zoom));
        OnPropertyChanged(nameof(ScreenWidth));
        OnPropertyChanged(nameof(ScreenHeight));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Screen.Changed -= OnScreenChanged;
        _snapshots?.Stop();
    }

    [RelayCommand(CanExecute = nameof(IsVirtual))]
    private void Rotate() => Change(p => p with { Width = p.Height, Height = p.Width });

    [RelayCommand(CanExecute = nameof(IsVirtual))]
    private void ToggleTheme() => Change(p => p with { IsDark = !p.IsDark });

    [RelayCommand(CanExecute = nameof(IsVirtual))]
    private void ToggleDirection() => Change(p => p with { Direction = p.Direction == TextDirection.RightToLeft ? TextDirection.LeftToRight : TextDirection.RightToLeft });

    [RelayCommand(CanExecute = nameof(IsVirtual))]
    private void ToggleFontScale() => Change(p => p with { FontScale = p.FontScale > 1.01 ? 1 : DeviceMatrixViewModel.LargeFontScale });

    [RelayCommand(CanExecute = nameof(IsVirtual))]
    private void Remove() => _matrix.RemoveDevice(this);

    [RelayCommand]
    private void ShowFullSize() => _matrix.ShowFullSize(this);

    [RelayCommand]
    private void Focus() => _matrix.Focus(this);

    private void Change(Func<ScreenProfile, ScreenProfile> change)
    {
        if (Virtual is { } screen)
        {
            screen.SetProfile(change(screen.Profile));
            _matrix.SaveDevices();
        }
    }

    private void OnScreenChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_disposed)
        {
            return;
        }

        Profile = Screen.Profile;
        IsRunning = Screen.IsRunning;
        UpdateStatus();
        if (IsDevice && _snapshots?.IsEnabled == true)
        {
            _ = RefreshSnapshotAsync();
        }
    });

    private void UpdateStatus() => StatusText =
        Virtual?.Error is { } error ? error
        : Screen.IsRunning ? null
        : _matrix.HasApp ? "Starting the app"
        : "Not running";

    private async Task RefreshSnapshotAsync()
    {
        if (_capturing || _disposed || !Screen.IsRunning)
        {
            return;
        }

        _capturing = true;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            if (await Screen.CaptureAsync(timeout.Token) is { } png && !_disposed)
            {
                using var stream = new MemoryStream(png);
                var previous = Snapshot;
                Snapshot = new Bitmap(stream);
                previous?.Dispose();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A phone that does not answer keeps its last picture.
        }
        finally
        {
            _capturing = false;
        }
    }
}
