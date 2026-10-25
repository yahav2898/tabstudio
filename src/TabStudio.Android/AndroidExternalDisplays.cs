using Android.Content;
using Android.Content.Res;
using Android.Graphics.Drawables;
using Android.Hardware.Display;
using Android.OS;
using Android.Views;
using Avalonia.Android;
using Avalonia.Controls;
using TabStudio.App.Platform;
using AndroidColor = Android.Graphics.Color;
using AndroidPresentation = Android.App.Presentation;

namespace TabStudio.Android;

/// <summary>
/// External displays on Android: the presentation displays DisplayManager reports (a monitor on USB-C or HDMI, a
/// wireless display, or the developer option's simulated display), and the IDE shown on one of them in a
/// <see cref="AndroidPresentation"/> hosting a second Avalonia view.
/// </summary>
/// <remarks>
/// <para>
/// Avalonia draws each <see cref="AvaloniaView"/> on its own surface, so the tablet's view and the presentation's view
/// render side by side in one process, from one UI thread. The presentation's view is created with a context of the
/// display, at the scale desktop mode picks for it, so the IDE is laid out for the monitor rather than the tablet.
/// </para>
/// <para>
/// Input: a mouse or touch screen on the display (where Android routes the pointer there, as on Android 16 and later
/// with connected displays, or with an external touch screen) reaches the presentation's view directly. A keyboard
/// connected to the tablet sends its keys to Android's focused window, normally the tablet's, and Avalonia, which has
/// one keyboard focus for the whole app, delivers them to the focused element of the IDE on the display.
/// </para>
/// </remarks>
public sealed class AndroidExternalDisplays : Java.Lang.Object, IExternalDisplayService, DisplayManager.IDisplayListener
{
    private static readonly AndroidColor WindowColor = AndroidColor.ParseColor("#1B1D23");

    private readonly Context _context;
    private readonly Func<global::Android.App.Activity?> _activity;
    private readonly DisplayManager _displays;
    private AndroidPresentation? _presentation;
    private AvaloniaView? _view;

    /// <summary>Starts listening for displays.</summary>
    /// <param name="context">The application context.</param>
    /// <param name="activity">The activity presentations belong to, while there is one.</param>
    public AndroidExternalDisplays(Context context, Func<global::Android.App.Activity?> activity)
    {
        _context = context;
        _activity = activity;
        _displays = (DisplayManager)context.GetSystemService(Context.DisplayService)!;
        _displays.RegisterDisplayListener(this, new Handler(Looper.MainLooper!));
        Display = FindDisplay() is { } display ? Describe(display) : null;
    }

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public ExternalDisplay? Display { get; private set; }

    /// <inheritdoc />
    public bool IsPresenting => _presentation is not null;

    /// <inheritdoc />
    public event EventHandler? DisplayChanged;

    /// <inheritdoc />
    public event EventHandler? PresentationEnded;

    /// <inheritdoc />
    public bool Present(Control content, double scale)
    {
        ArgumentNullException.ThrowIfNull(content);
        Dismiss();
        if (_activity() is not { IsFinishing: false } activity || FindDisplay() is not { } display)
        {
            return false;
        }

        var presentation = new AndroidPresentation(activity, display);
        var view = new AvaloniaView(ScaledContext(presentation.Context, scale)) { Content = content };
        presentation.SetContentView(view);
        if (presentation.Window is { } window)
        {
            window.SetBackgroundDrawable(new ColorDrawable(WindowColor));

            // A monitor showing the IDE stays on while the user reads code, as a PC's does.
            window.AddFlags(WindowManagerFlags.KeepScreenOn);
        }

        try
        {
            presentation.Show();
        }
        catch (Java.Lang.RuntimeException)
        {
            // The display went away, or the system would not show a window on it (WindowManager.InvalidDisplayException).
            view.Content = null;
            presentation.Dispose();
            return false;
        }

        presentation.DismissEvent += OnPresentationDismissed;
        _presentation = presentation;
        _view = view;
        return true;
    }

    /// <inheritdoc />
    public void Dismiss()
    {
        if (_presentation is not { } presentation)
        {
            return;
        }

        presentation.DismissEvent -= OnPresentationDismissed;
        _presentation = null;
        ReleaseView();
        presentation.Dismiss();
        presentation.Dispose();
    }

    /// <inheritdoc />
    public void OnDisplayAdded(int displayId) => Refresh();

    /// <inheritdoc />
    public void OnDisplayChanged(int displayId) => Refresh();

    /// <inheritdoc />
    public void OnDisplayRemoved(int displayId) => Refresh();

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _displays.UnregisterDisplayListener(this);
            Dismiss();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Android ended the presentation: the display was disconnected, or its size changed. Desktop mode hears of the
    /// display first, so it can bring the IDE back or show it again at the new size.
    /// </summary>
    private void OnPresentationDismissed(object? sender, EventArgs e)
    {
        if (_presentation is null)
        {
            return;
        }

        _presentation.DismissEvent -= OnPresentationDismissed;
        _presentation.Dispose();
        _presentation = null;
        ReleaseView();
        Refresh();
        PresentationEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Stops the view drawing on a display that is going away, and lets go of the IDE in it.</summary>
    private void ReleaseView()
    {
        if (_view is { } view)
        {
            // Hiding it first stops Avalonia's rendering of the view before its surface goes.
            view.Visibility = ViewStates.Gone;
            view.Content = null;
            _view = null;
        }
    }

    private void Refresh()
    {
        var display = FindDisplay() is { } found ? Describe(found) : null;
        if (display != Display)
        {
            Display = display;
            DisplayChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The first presentation display other than the device's own screen.</summary>
    private Display? FindDisplay()
    {
        foreach (var display in _displays.GetDisplays(DisplayManager.DisplayCategoryPresentation) ?? [])
        {
            if (display.DisplayId != global::Android.Views.Display.DefaultDisplay && display.IsValid)
            {
                return display;
            }
        }

        return null;
    }

    /// <summary>Reads a display's size and density from a context of that display.</summary>
    private ExternalDisplay Describe(Display display)
    {
        var metrics = _context.CreateDisplayContext(display).Resources?.DisplayMetrics;
        return new ExternalDisplay(
            display.DisplayId,
            string.IsNullOrWhiteSpace(display.Name) ? "External display" : display.Name,
            metrics?.WidthPixels ?? 0,
            metrics?.HeightPixels ?? 0,
            metrics?.Density ?? 1);
    }

    /// <summary>A context of the presentation whose density gives the scale desktop mode picked for the display.</summary>
    private static Context ScaledContext(Context presentationContext, double scale)
    {
        // Android's density unit: 160 dots per inch is one pixel per device-independent unit.
        const int BaselineDensity = 160;
        var configuration = new Configuration(presentationContext.Resources!.Configuration);
        configuration.DensityDpi = (int)Math.Round(scale * BaselineDensity);
        return presentationContext.CreateConfigurationContext(configuration) ?? presentationContext;
    }
}
