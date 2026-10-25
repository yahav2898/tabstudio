using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using TabStudio.App.Previews;

namespace TabStudio.App.Swarm;

/// <summary>
/// A virtual device: the tablet draws the running app again in a frame of another device's size, theme, text
/// direction and font scale. It is a screen of kind <see cref="AppScreenKind.Virtual"/>, with its own instance of the
/// app's main view and view model, which follows the app through every restart.
/// </summary>
public sealed class VirtualAppScreen : LocalAppScreen
{
    private ScreenProfile _profile;
    private AvaloniaAppHost? _app;
    private AppViewInstance? _instance;

    /// <summary>Creates a virtual device that shows no app yet.</summary>
    /// <param name="id">A stable id, unique among the screens.</param>
    /// <param name="profile">The device.</param>
    /// <param name="inspector">The layout checks.</param>
    public VirtualAppScreen(string id, ScreenProfile profile, ILayoutInspector inspector)
        : base(id, AppScreenKind.Virtual, inspector)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _profile = profile;
        Frame = new VirtualDeviceFrame();
        Frame.Apply(profile);
    }

    /// <inheritdoc />
    public override ScreenProfile Profile => _profile;

    /// <summary>The device's screen, which tiles of the Device Matrix show.</summary>
    public VirtualDeviceFrame Frame { get; }

    /// <summary>Why the app could not start on this device, if it could not, such as an exception in its view model.</summary>
    public string? Error { get; private set; }

    /// <inheritdoc />
    protected override Size LayoutSize => new(_profile.Width, _profile.Height);

    /// <summary>Makes the device another one: rotated, in the other theme or direction, or with larger fonts.</summary>
    /// <param name="profile">The device.</param>
    public void SetProfile(ScreenProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Dispatcher.UIThread.VerifyAccess();
        if (profile == _profile)
        {
            return;
        }

        _profile = profile;
        Frame.Apply(profile);
        RaiseChanged();
    }

    /// <summary>Shows a new instance of the running app, replacing the one before, as the app restarts.</summary>
    /// <param name="app">The running app.</param>
    public void Start(AvaloniaAppHost app)
    {
        ArgumentNullException.ThrowIfNull(app);
        Stop();
        _app = app;
        try
        {
            _instance = app.CreateView();
            Frame.App = _instance.Root;
            Error = null;
            ShowApp(Frame, _instance.MainView);
            EnsureLayout();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The app's own startup code threw for this instance; the others run on.
            Error = $"{_profile.Name} could not start the app: {(ex is InvalidOperationException { InnerException: { } inner } ? inner.Message : ex.Message)}";
            Frame.App = null;
            _instance = null;
            RaiseChanged();
        }
    }

    /// <inheritdoc />
    /// <remarks>A new instance of the running app replaces the one shown, with a new view model.</remarks>
    protected override bool Restart()
    {
        if (_app is not { } app)
        {
            return false;
        }

        Start(app);
        return IsRunning;
    }

    /// <inheritdoc />
    /// <remarks>The frame waits on the stage of the IDE's window, still running, until a tile shows it.</remarks>
    protected override void HoldOutsideWindow(Control surface)
    {
        if (!Frame.IsRetired && Frame.Parent is null && ScreenStage.OfApplication() is { } stage)
        {
            stage.Hold(Frame);
        }
    }

    /// <summary>Takes the app away, once it stops or before it restarts.</summary>
    public void Stop()
    {
        Dispatcher.UIThread.VerifyAccess();
        HideApp();
        _app = null;
        if (_instance is { } instance)
        {
            Frame.App = null;
            AvaloniaAppHost.Release(instance);
            _instance = null;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && Dispatcher.UIThread.CheckAccess())
        {
            Stop();
            Frame.Retire();
        }

        base.Dispose(disposing);
    }
}
