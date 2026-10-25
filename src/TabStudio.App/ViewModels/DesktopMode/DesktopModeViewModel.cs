using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Toolbox;

namespace TabStudio.App.ViewModels.DesktopMode;

/// <summary>
/// Desktop mode: with a monitor connected, the full IDE moves to it and the tablet becomes a touch Toolbox and
/// Properties panel. This decides when: it offers desktop mode when a display is connected (or starts it on its own
/// when the user chose "Always"), turns it on and off from the status bar and View > Desktop Mode, and ends it when
/// the display goes away. The views move the IDE; everything it shows lives in the view models, so nothing is lost.
/// </summary>
public sealed partial class DesktopModeViewModel : ObservableObject, IDisposable
{
    /// <summary>The least height, in device-independent units, the automatic scale leaves the IDE on a display.</summary>
    private const double AutomaticMinimumHeight = 800;

    /// <summary>The least width the automatic scale leaves the IDE, so its side bar and editor fit side by side.</summary>
    private const double AutomaticMinimumWidth = 1100;

    private readonly DesktopModeSettingsStore _store;
    private DesktopModeSettings _settings;
    private bool _isActive;
    private WorkspaceViewModel? _workspace;
    private bool _disposed;

    /// <summary>Creates desktop mode, and offers it right away if a display is already connected.</summary>
    /// <param name="displays">The platform's external displays.</param>
    /// <param name="store">Where the user's choices are kept.</param>
    /// <param name="toolbox">The Toolbox's controls, for the companion.</param>
    public DesktopModeViewModel(IExternalDisplayService displays, DesktopModeSettingsStore store, ToolboxCatalog toolbox)
    {
        ArgumentNullException.ThrowIfNull(displays);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(toolbox);

        Displays = displays;
        _store = store;
        _settings = store.Load();
        Companion = new DesktopCompanionViewModel(toolbox, this);
        ScaleOptions = [.. Enum.GetValues<DesktopScale>().Select(scale => new DesktopScaleOption(scale, LabelOf(scale), SetScaleCommand))];
        UpdateScaleOptions();
        Displays.DisplayChanged += OnDisplayChanged;
        Display = displays.Display;
        if (Display is not null)
        {
            Offer();
        }
    }

    /// <summary>Raised when the companion is done with the keyboard, so the IDE on the display gets it back.</summary>
    public event EventHandler? KeyboardFocusRequested;

    /// <summary>The platform's external displays, which the views show the IDE on.</summary>
    public IExternalDisplayService Displays { get; }

    /// <summary>Whether the platform can show the IDE on another display; false on the desktop host.</summary>
    public bool IsSupported => Displays.IsSupported;

    /// <summary>The connected display, if any.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDisplay), nameof(DisplayName), nameof(DisplayDescription), nameof(EffectiveScale), nameof(ScaleLabel), nameof(StatusText), nameof(StatusToolTip))]
    [NotifyCanExecuteChangedFor(nameof(EnterCommand), nameof(ToggleCommand), nameof(UseAlwaysCommand))]
    public partial ExternalDisplay? Display { get; private set; }

    /// <summary>Whether a display is connected.</summary>
    public bool HasDisplay => Display is not null;

    /// <summary>The display's name, such as "HDMI Screen".</summary>
    public string DisplayName => Display?.Name ?? "the display";

    /// <summary>"HDMI Screen · 1920 × 1080 · 100%", for the companion's header.</summary>
    public string DisplayDescription => Display is { } display ? $"{display.Name} · {display.Resolution} · {ScaleLabel}" : string.Empty;

    /// <summary>
    /// Whether the IDE is on the display. Setting it starts or ends desktop mode; starting does nothing without a
    /// display, and the property then stays false.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (value)
            {
                Enter();
            }
            else
            {
                Exit();
            }

            // A check mark that asked for a change that did not happen goes back.
            OnPropertyChanged();
        }
    }

    /// <summary>Whether the tablet offers desktop mode for a display that was just connected.</summary>
    [ObservableProperty]
    public partial bool IsOfferVisible { get; private set; }

    /// <summary>Whether a connected display gets the IDE right away, without the offer. Saved.</summary>
    public bool UseDisplayAutomatically
    {
        get => _settings.UseDisplayAutomatically;
        set
        {
            if (value != _settings.UseDisplayAutomatically)
            {
                _settings = _settings with { UseDisplayAutomatically = value };
                _store.Save(_settings);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>How big the IDE is drawn on the display, as the user chose. Saved.</summary>
    public DesktopScale Scale => _settings.Scale;

    /// <summary>How many display pixels one device-independent unit of the IDE takes.</summary>
    public double EffectiveScale => ScaleFor(Display, _settings.Scale);

    /// <summary>"125%", or "Automatic · 135%".</summary>
    public string ScaleLabel
    {
        get
        {
            var percent = string.Create(CultureInfo.CurrentCulture, $"{EffectiveScale * 100:0}%");
            return _settings.Scale == DesktopScale.Automatic ? $"Automatic · {percent}" : percent;
        }
    }

    /// <summary>The sizes the user can pick from, for the companion's menu.</summary>
    public IReadOnlyList<DesktopScaleOption> ScaleOptions { get; }

    /// <summary>The status bar button's text.</summary>
    public string StatusText => _isActive ? "Desktop mode" : $"Use {DisplayName}";

    /// <summary>The status bar button's tooltip.</summary>
    public string StatusToolTip => _isActive
        ? "Bring the IDE back to the tablet"
        : $"Show the IDE on {DisplayName}, and use the tablet as a Toolbox and Properties panel";

    /// <summary>The tablet's touch companion while the IDE is on the display.</summary>
    public DesktopCompanionViewModel Companion { get; }

    /// <summary>The open workspace, which the companion works on; set by <see cref="MainViewModel"/>.</summary>
    internal WorkspaceViewModel? Workspace
    {
        get => _workspace;
        set
        {
            _workspace = value;
            Companion.Workspace = value;
        }
    }

    /// <summary>Gives the keyboard back to the IDE on the display, after the companion used it to type a value.</summary>
    public void ReturnKeyboardToDisplay() => KeyboardFocusRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Ends desktop mode after the system would not show the IDE on the display.</summary>
    public void OnPresentationFailed()
    {
        if (_isActive)
        {
            _isActive = false;
            NotifyActiveChanged();
        }

        _workspace?.ShowStatus($"TabStudio could not show the IDE on {DisplayName}");
    }

    /// <summary>
    /// The scale the IDE is drawn at on a display: the user's choice, or automatically the display's own density, but
    /// never so large that less than 1100 × 800 units fit, nor smaller than one pixel per unit, in steps of 5%.
    /// </summary>
    /// <param name="display">The display, if any.</param>
    /// <param name="scale">The user's choice.</param>
    public static double ScaleFor(ExternalDisplay? display, DesktopScale scale) => scale switch
    {
        DesktopScale.Percent100 => 1,
        DesktopScale.Percent125 => 1.25,
        DesktopScale.Percent150 => 1.5,
        DesktopScale.Percent175 => 1.75,
        DesktopScale.Percent200 => 2,
        _ when display is null => 1,
        _ => Math.Max(1, Math.Floor((20 * Math.Min(display.SystemScale, Math.Min(display.PixelHeight / AutomaticMinimumHeight, display.PixelWidth / AutomaticMinimumWidth))) + 1e-6) / 20),
    };

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Displays.DisplayChanged -= OnDisplayChanged;
        Companion.Dispose();
    }

    /// <summary>Moves the IDE to the display.</summary>
    [RelayCommand(CanExecute = nameof(CanEnter))]
    private void Enter()
    {
        IsOfferVisible = false;
        if (!CanEnter())
        {
            return;
        }

        _isActive = true;
        NotifyActiveChanged();
    }

    private bool CanEnter() => HasDisplay && !_isActive;

    /// <summary>Brings the IDE back to the tablet.</summary>
    [RelayCommand]
    private void Exit()
    {
        if (_isActive)
        {
            _isActive = false;
            NotifyActiveChanged();
        }
    }

    /// <summary>Turns desktop mode on or off: the status bar button.</summary>
    [RelayCommand(CanExecute = nameof(CanToggle))]
    private void Toggle()
    {
        if (_isActive)
        {
            Exit();
        }
        else
        {
            Enter();
        }
    }

    private bool CanToggle() => _isActive || HasDisplay;

    /// <summary>Moves the IDE to the display now and whenever a display is connected from now on.</summary>
    [RelayCommand(CanExecute = nameof(HasDisplay))]
    private void UseAlways()
    {
        UseDisplayAutomatically = true;
        Enter();
    }

    /// <summary>Hides the offer; the status bar button still starts desktop mode.</summary>
    [RelayCommand]
    private void DismissOffer() => IsOfferVisible = false;

    /// <summary>Draws the IDE at another size on the display.</summary>
    [RelayCommand]
    private void SetScale(DesktopScale scale)
    {
        if (scale == _settings.Scale)
        {
            return;
        }

        _settings = _settings with { Scale = scale };
        _store.Save(_settings);
        UpdateScaleOptions();
        OnPropertyChanged(nameof(Scale));
        OnPropertyChanged(nameof(EffectiveScale));
        OnPropertyChanged(nameof(ScaleLabel));
        OnPropertyChanged(nameof(DisplayDescription));
    }

    private void OnDisplayChanged(object? sender, EventArgs e)
    {
        var previous = Display;
        Display = Displays.Display;
        if (Display is null)
        {
            IsOfferVisible = false;
            if (_isActive)
            {
                _isActive = false;
                NotifyActiveChanged();
                _workspace?.ShowStatus($"{previous?.Name ?? "The display"} was disconnected, so the IDE is back on the tablet");
            }
        }
        else if (previous is null || previous.Id != Display.Id)
        {
            Offer();
        }
    }

    /// <summary>A display was connected: start desktop mode if the user chose "Always", otherwise offer it.</summary>
    private void Offer()
    {
        if (_isActive)
        {
            return;
        }

        if (_settings.UseDisplayAutomatically)
        {
            Enter();
        }
        else
        {
            IsOfferVisible = true;
        }
    }

    private void NotifyActiveChanged()
    {
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusToolTip));
        EnterCommand.NotifyCanExecuteChanged();
        ToggleCommand.NotifyCanExecuteChanged();
    }

    private void UpdateScaleOptions()
    {
        foreach (var option in ScaleOptions)
        {
            option.IsSelected = option.Scale == _settings.Scale;
        }
    }

    private static string LabelOf(DesktopScale scale) => scale switch
    {
        DesktopScale.Percent100 => "100% · the most space",
        DesktopScale.Percent125 => "125%",
        DesktopScale.Percent150 => "150%",
        DesktopScale.Percent175 => "175%",
        DesktopScale.Percent200 => "200% · for 4K and TVs",
        _ => "Automatic",
    };
}
