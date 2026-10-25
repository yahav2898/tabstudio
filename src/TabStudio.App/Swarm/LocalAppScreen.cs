using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// A screen TabStudio draws itself, where the app being built is an Avalonia view in TabStudio's own window: the app
/// preview, or a virtual device. Everything the contract asks works on whatever app it shows: input is captured and
/// replayed through <see cref="ScreenInput"/>, pictures are drawn by Avalonia, and the layout checks walk its view.
/// </summary>
/// <remarks>
/// Each call may come from any thread, such as a phone's request over TabStudio Link; the work happens on the UI
/// thread. A screen nobody looks at is laid out on request, so its pictures and layout checks are current.
/// </remarks>
public abstract class LocalAppScreen : IAppScreen, IDisposable
{
    private readonly ILayoutInspector _inspector;
    private ScreenInput? _input;
    private bool _disposed;

    /// <summary>Creates a screen that shows no app yet.</summary>
    /// <param name="id">A stable id, unique among the screens.</param>
    /// <param name="kind">Where it is.</param>
    /// <param name="inspector">The layout checks.</param>
    protected LocalAppScreen(string id, AppScreenKind kind, ILayoutInspector inspector)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        ArgumentNullException.ThrowIfNull(inspector);
        Id = id;
        Kind = kind;
        _inspector = inspector;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public event EventHandler<AppInput>? InputCaptured;

    /// <inheritdoc />
    public string Id { get; }

    /// <inheritdoc />
    public AppScreenKind Kind { get; }

    /// <inheritdoc />
    public abstract ScreenProfile Profile { get; }

    /// <inheritdoc />
    public bool IsRunning => AppRoot is not null;

    /// <summary>The app's root view on this screen while the app runs: where element paths start.</summary>
    public Control? AppRoot { get; private set; }

    /// <summary>Everything the screen shows: the app's view on its background, what pictures draw.</summary>
    public Control? Surface { get; private set; }

    /// <summary>The size the screen is laid out at when Avalonia does not lay it out, because it is not on view.</summary>
    protected abstract Size LayoutSize { get; }

    /// <inheritdoc />
    public Task<bool> SendInputAsync(AppInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return OnUiThreadAsync(async () =>
        {
            if (_input is null)
            {
                return false;
            }

            EnsureLayout();
            return await _input.ReplayAsync(input, cancellationToken);
        });
    }

    /// <inheritdoc />
    public Task<byte[]?> CaptureAsync(CancellationToken cancellationToken) => CaptureAsync(highlight: null, cancellationToken);

    /// <summary>Takes a picture of what the app shows, with an area outlined, such as an element a layout issue is about.</summary>
    /// <param name="highlight">The area, in the screen's coordinates, as <see cref="LayoutIssue.Bounds"/> gives it.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A PNG image, or <see langword="null"/> while the app is not running.</returns>
    public Task<byte[]?> CaptureAsync(Rect? highlight, CancellationToken cancellationToken) => OnUiThreadAsync(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Surface is not { } surface || AppRoot is not { } root)
        {
            return Task.FromResult<byte[]?>(null);
        }

        EnsureLayout();

        // Layout issues give bounds on the app's view; the picture is of the whole screen around it.
        var area = highlight is { } bounds && root.TranslatePoint(bounds.Position, surface) is { } origin ? new Rect(origin, bounds.Size) : (Rect?)null;
        return Task.FromResult(ScreenCapture.Png(surface, Profile.Scale, area));
    });

    /// <inheritdoc />
    public Task<IReadOnlyList<LayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken) => OnUiThreadAsync(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (AppRoot is not { } root)
        {
            return Task.FromResult<IReadOnlyList<LayoutIssue>>([]);
        }

        EnsureLayout();
        return Task.FromResult(_inspector.Inspect(root, Profile));
    });

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Lays the screen out now when Avalonia will not, because it is in a part of TabStudio that is not on view, such
    /// as a side bar panel that is not selected.
    /// </summary>
    internal void EnsureLayout()
    {
        if (Surface is not { } surface)
        {
            return;
        }

        if (surface.IsEffectivelyVisible && surface.GetVisualRoot() is not null)
        {
            surface.UpdateLayout();
            return;
        }

        var size = LayoutSize;
        if (size.Width > 0 && size.Height > 0)
        {
            surface.Measure(size);
            surface.Arrange(new Rect(surface.Bounds.Position, size));
        }
    }

    /// <summary>Starts showing a running app: its view on the screen, with its input followed.</summary>
    /// <param name="surface">Everything the screen shows.</param>
    /// <param name="appRoot">The app's root view, inside the surface.</param>
    protected void ShowApp(Control surface, Control appRoot)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(appRoot);
        Dispatcher.UIThread.VerifyAccess();
        DetachInput();
        Surface = surface;
        AppRoot = appRoot;
        _input = new ScreenInput(surface, appRoot);
        _input.Captured += OnInputCaptured;
        RaiseChanged();
    }

    /// <summary>Stops showing the app, once it stopped or before it restarts.</summary>
    protected void HideApp()
    {
        Dispatcher.UIThread.VerifyAccess();
        var wasRunning = IsRunning;
        DetachInput();
        AppRoot = null;
        Surface = null;
        if (wasRunning)
        {
            RaiseChanged();
        }
    }

    /// <summary>Raises <see cref="Changed"/>: the profile, whether the app runs, or what it shows changed.</summary>
    protected void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Lets go of the app's view and its input.</summary>
    /// <param name="disposing">Whether this is <see cref="Dispose()"/> rather than a finalizer.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (disposing)
        {
            DetachInput();
            AppRoot = null;
            Surface = null;
        }
    }

    private void DetachInput()
    {
        if (_input is not null)
        {
            _input.Captured -= OnInputCaptured;
            _input.Dispose();
            _input = null;
        }
    }

    private void OnInputCaptured(object? sender, AppInput input) => InputCaptured?.Invoke(this, input);

    /// <summary>Runs work on the UI thread, where the app's views live, from whichever thread asks.</summary>
    private static Task<T> OnUiThreadAsync<T>(Func<Task<T>> work) =>
        Dispatcher.UIThread.CheckAccess() ? work() : Dispatcher.UIThread.InvokeAsync(work);
}
