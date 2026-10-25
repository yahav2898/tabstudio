using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.Threading;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Swarm;

/// <summary>
/// The app preview as a screen, of kind <see cref="AppScreenKind.Preview"/>: the running Avalonia app beside the code,
/// with the profile of the device TabStudio runs on, or of the phone or tablet frame the preview draws it in. On a
/// phone that runs the app for the swarm, this is the screen that phone's TabStudio drives.
/// </summary>
/// <remarks>
/// The profile is worked out on the UI thread whenever the preview changes and kept, so any thread can read it.
/// The preview takes replayed input while it is drawn, as a phone's screen does while it is on.
/// </remarks>
public sealed class PreviewAppScreen : LocalAppScreen
{
    /// <summary>The id of the app preview's screen.</summary>
    public const string PreviewId = "preview";

    /// <summary>The preview's name, as tiles and the Error List show it.</summary>
    public const string PreviewName = "App Preview";

    /// <summary>How long the preview's size must rest before its new profile is announced.</summary>
    private static readonly TimeSpan ResizeSettle = TimeSpan.FromMilliseconds(300);

    private readonly AppPreviewViewModel _preview;
    private readonly DispatcherTimer _resized = new() { Interval = ResizeSettle };
    private ScreenProfile _profile = new(PreviewName, FormFactor.Tablet, 0, 0);
    private Size _lastSize;

    /// <summary>Creates the preview's screen, which shows nothing until an app runs.</summary>
    /// <param name="preview">The app preview.</param>
    /// <param name="inspector">The layout checks.</param>
    public PreviewAppScreen(AppPreviewViewModel preview, ILayoutInspector inspector)
        : base(PreviewId, AppScreenKind.Preview, inspector)
    {
        ArgumentNullException.ThrowIfNull(preview);
        _preview = preview;
        _preview.PropertyChanged += OnPreviewChanged;
        _resized.Tick += OnResizeSettled;
    }

    /// <inheritdoc />
    public override ScreenProfile Profile => _profile;

    /// <inheritdoc />
    protected override Size LayoutSize => new(_profile.Width, _profile.Height);

    /// <summary>Starts showing a running app, as the preview shows it.</summary>
    /// <param name="surface">The app's view in its theme, as the preview holds it.</param>
    /// <param name="mainView">The app's main view inside it.</param>
    public void Show(Control surface, Control mainView)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(mainView);
        if (Surface is { } previous)
        {
            previous.PropertyChanged -= OnSurfaceChanged;
        }

        surface.PropertyChanged += OnSurfaceChanged;
        UpdateProfile(surface, mainView);
        ShowApp(surface, mainView);
    }

    /// <summary>Stops showing the app, once it stopped or before it restarts.</summary>
    public void Hide()
    {
        if (Surface is { } surface)
        {
            surface.PropertyChanged -= OnSurfaceChanged;
        }

        _resized.Stop();
        HideApp();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _preview.PropertyChanged -= OnPreviewChanged;
            _resized.Stop();
            if (Surface is { } surface)
            {
                surface.PropertyChanged -= OnSurfaceChanged;
            }
        }

        base.Dispose(disposing);
    }

    private void OnPreviewChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppPreviewViewModel.Frame) && IsRunning && UpdateProfile(Surface, AppRoot))
        {
            RaiseChanged();
        }
    }

    private void OnSurfaceChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty)
        {
            if (_profile.Width <= 0 || _profile.Height <= 0)
            {
                // The first layout gives the screen its size at once.
                if (UpdateProfile(Surface, AppRoot))
                {
                    RaiseChanged();
                }

                return;
            }

            // A pane being resized changes size on every frame; its profile settles once it rests.
            _resized.Stop();
            _resized.Start();
        }
        else if (e.Property == ThemeVariantScope.ActualThemeVariantProperty || e.Property == Visual.FlowDirectionProperty)
        {
            if (UpdateProfile(Surface, AppRoot))
            {
                RaiseChanged();
            }
        }
    }

    private void OnResizeSettled(object? sender, EventArgs e)
    {
        _resized.Stop();
        if (IsRunning && UpdateProfile(Surface, AppRoot))
        {
            RaiseChanged();
        }
    }

    /// <summary>Works out the profile from the preview as it is now.</summary>
    /// <returns>Whether it changed.</returns>
    private bool UpdateProfile(Control? surface, Control? root)
    {
        if (surface is not null && surface.Bounds.Width > 0 && surface.Bounds.Height > 0)
        {
            _lastSize = surface.Bounds.Size;
        }

        var topLevel = surface is null ? null : TopLevel.GetTopLevel(surface);
        var (formFactor, size) = _preview.Frame switch
        {
            AppPreviewFrame.Phone => (FormFactor.Phone, new Size(_preview.FrameWidth, _preview.FrameHeight)),
            AppPreviewFrame.Tablet => (FormFactor.Tablet, new Size(_preview.FrameWidth, _preview.FrameHeight)),
            _ => (FormFactorOf(topLevel), _lastSize),
        };
        var profile = new ScreenProfile(
            PreviewName,
            formFactor,
            Math.Round(size.Width),
            Math.Round(size.Height),
            topLevel?.RenderScaling ?? 1,
            IsDark: surface?.ActualThemeVariant == ThemeVariant.Dark,
            Direction: (root ?? surface)?.FlowDirection == Avalonia.Media.FlowDirection.RightToLeft ? TextDirection.RightToLeft : TextDirection.LeftToRight);
        if (profile == _profile)
        {
            return false;
        }

        _profile = profile;
        return true;
    }

    /// <summary>The kind of device TabStudio runs on, from its window's narrower side.</summary>
    internal static FormFactor FormFactorOf(TopLevel? topLevel)
    {
        var narrower = topLevel is null ? double.MaxValue : Math.Min(topLevel.ClientSize.Width, topLevel.ClientSize.Height);
        return narrower switch
        {
            < 400 => FormFactor.Phone,
            < 600 => FormFactor.Phablet,
            _ => FormFactor.Tablet,
        };
    }
}
