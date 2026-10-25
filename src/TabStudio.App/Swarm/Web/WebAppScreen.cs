using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.App.Platform;

namespace TabStudio.App.Swarm.Web;

/// <summary>
/// The web preview as one of Swarm's screens: the running web app's page in the platform's embedded browser, which
/// TabStudio scripts (see <see cref="WebScreenScript"/>) to name the page's elements, capture what a person does on
/// it, replay taps and keys through the browser's own touch and key input, photograph it and check its layout.
/// </summary>
/// <remarks>
/// It needs a browser that runs script, <see cref="IScriptableWebView"/>: Android's system WebView. Desktops have no
/// embedded browser, so there a web app has no screen. The screen reads the page a few times a second, which sets
/// the page up again after every load, follows its size and theme, and collects what a person did on it.
/// </remarks>
public sealed class WebAppScreen : IAppScreen, IDisposable
{
    /// <summary>The web preview's screen id.</summary>
    public const string WebPreviewId = "web-preview";

    /// <summary>The web preview's name, as screens show it.</summary>
    public const string WebPreviewName = "Web Preview";

    /// <summary>How often the page is read for its state and what a person did on it.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(400);

    /// <summary>How long a replayed tap holds its finger down.</summary>
    private static readonly TimeSpan TapHold = TimeSpan.FromMilliseconds(60);

    /// <summary>How long a replayed long press holds its finger down: longer than Android's long press timeout.</summary>
    private static readonly TimeSpan LongPressHold = TimeSpan.FromMilliseconds(800);

    /// <summary>How long the page has to see a replayed touch before the screen asks where it landed.</summary>
    private static readonly TimeSpan LandWait = TimeSpan.FromMilliseconds(150);

    /// <summary>How long a page that moved under a finger has to settle before the finger aims again.</summary>
    private static readonly TimeSpan SettleWait = TimeSpan.FromMilliseconds(600);

    private readonly DispatcherTimer _poll;
    private IScriptableWebView? _view;
    private ScreenProfile _profile = new(WebPreviewName, FormFactor.Tablet, 0, 0);
    private bool _running;
    private bool _polling;
    private bool _disposed;

    /// <summary>Creates the screen; it runs once <see cref="Show"/> gives it a browser.</summary>
    public WebAppScreen()
    {
        _poll = new DispatcherTimer(DispatcherPriority.Background) { Interval = PollInterval };
        _poll.Tick += (_, _) => _ = PollAsync();
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public event EventHandler<AppInput>? InputCaptured;

    /// <inheritdoc />
    public string Id => WebPreviewId;

    /// <inheritdoc />
    public AppScreenKind Kind => AppScreenKind.Preview;

    /// <inheritdoc />
    public ScreenProfile Profile => _profile;

    /// <summary>Whether a page of the web app has loaded in the browser and is set up as a screen.</summary>
    public bool IsRunning => _running;

    /// <summary>The browser the page is in, while the screen shows one.</summary>
    public IScriptableWebView? WebView => _view;

    /// <summary>The page's address, while it runs.</summary>
    public Uri? Page { get; private set; }

    /// <summary>Starts following the web app in a browser.</summary>
    /// <param name="view">The web preview's browser.</param>
    public void Show(IScriptableWebView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (ReferenceEquals(view, _view))
        {
            return;
        }

        _view = view;
        SetRunning(false);
        _poll.Start();
        _ = PollAsync();
    }

    /// <summary>Stops following the browser: the web app stopped.</summary>
    public void Hide()
    {
        _poll.Stop();
        _view = null;
        Page = null;
        SetRunning(false);
    }

    /// <inheritdoc />
    public Task<bool> SendInputAsync(AppInput input, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        return OnUiThreadAsync(() => ReplayAsync(input, cancellationToken));
    }

    /// <inheritdoc />
    public Task<byte[]?> CaptureAsync(CancellationToken cancellationToken) => OnUiThreadAsync<byte[]?>(() =>
        _running && _view is { } view ? view.CaptureAsync(ScreenCapture.MaxPixels, cancellationToken) : Task.FromResult<byte[]?>(null));

    /// <inheritdoc />
    public Task<IReadOnlyList<LayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken) => OnUiThreadAsync<IReadOnlyList<LayoutIssue>>(async () =>
    {
        if (!_running || _view is not { } view)
        {
            return [];
        }

        var answer = await view.EvaluateAsync(WebScreenScript.Inspect(_profile), cancellationToken);
        var issues = WebScreenJson.Read(answer, WebScreenJson.Default.ListWebScreenIssue) ?? [];
        return [.. issues.Select(ToLayoutIssue).OfType<LayoutIssue>()];
    });

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Hide();
    }

    /// <summary>Reads the page: sets it up if it is new, follows its size and theme, and passes on what a person did.</summary>
    private async Task PollAsync()
    {
        if (_polling || _view is not { } view)
        {
            return;
        }

        _polling = true;
        try
        {
            var answer = await view.EvaluateAsync(WebScreenScript.State, CancellationToken.None);
            if (!ReferenceEquals(view, _view))
            {
                return;
            }

            if (answer is null)
            {
                // No page in the browser yet.
                SetRunning(false);
                return;
            }

            if (WebScreenJson.Read(answer, WebScreenJson.Default.WebScreenState) is not { } state)
            {
                // A page that loaded since the last read: it is set up now, and read on the next tick.
                SetRunning(false);
                await view.EvaluateAsync(WebScreenScript.Install, CancellationToken.None);
                return;
            }

            var page = Uri.TryCreate(state.Url, UriKind.Absolute, out var address) && address.Scheme is "http" or "https" ? address : null;
            Page = page;
            UpdateProfile(state, view);
            SetRunning(state.Ready && page is not null && state.Width > 0 && state.Height > 0);
            foreach (var input in state.Inputs ?? [])
            {
                if (ToAppInput(input) is { } captured)
                {
                    InputCaptured?.Invoke(this, captured);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The page went while it was read: the next tick reads the one that came.
        }
        finally
        {
            _polling = false;
        }
    }

    private void UpdateProfile(WebScreenState state, IScriptableWebView view)
    {
        var profile = new ScreenProfile(
            WebPreviewName,
            PreviewAppScreen.FormFactorOf(TopLevel.GetTopLevel(view.Control)),
            Math.Round(state.Width),
            Math.Round(state.Height),
            state.Scale > 0 ? state.Scale : 1,
            state.Dark,
            state.Rtl ? TextDirection.RightToLeft : TextDirection.LeftToRight);
        if (profile != _profile)
        {
            _profile = profile;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SetRunning(bool running)
    {
        if (running != _running)
        {
            _running = running;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Replays an input: touches and keys through the browser's input, text and scrolls through the page.</summary>
    private async Task<bool> ReplayAsync(AppInput input, CancellationToken cancellationToken)
    {
        if (!_running || _view is not { } view)
        {
            return false;
        }

        switch (input.Kind)
        {
            case AppInputKind.Tap or AppInputKind.LongPress:
                return await TouchAsync(view, input, cancellationToken);

            case AppInputKind.Text:
                return input.Text is { Length: > 0 } text && await view.EvaluateAsync(WebScreenScript.Type(text), cancellationToken) == "true";

            case AppInputKind.Key:
                if (!Enum.TryParse<Key>(input.Key, ignoreCase: true, out var key) || !TryParseModifiers(input.Modifiers, out var modifiers))
                {
                    return false;
                }

                await view.EvaluateAsync(WebScreenScript.Expect(AppInputKind.Key, KeyNames.Of(key)), cancellationToken);
                return await view.PressKeyAsync(key, modifiers, cancellationToken);

            case AppInputKind.Scroll:
                return await view.EvaluateAsync(WebScreenScript.Scroll(input.ElementPath, input.ScrollDelta), cancellationToken) == "true";

            default:
                return false;
        }
    }

    /// <summary>
    /// A finger on an element. A page can move under it, as one does while a soft keyboard comes or goes: when the
    /// page saw the finger land elsewhere, the finger aims again once the page has settled, as a person's would.
    /// </summary>
    private static async Task<bool> TouchAsync(IScriptableWebView view, AppInput input, CancellationToken cancellationToken)
    {
        var hold = input.Kind == AppInputKind.LongPress ? LongPressHold : TapHold;
        for (var attempt = 0; ; attempt++)
        {
            var answer = await view.EvaluateAsync(WebScreenScript.PointAt(input.ElementPath, input.Position), cancellationToken);
            if (WebScreenJson.Read(answer, WebScreenJson.Default.WebScreenPoint) is not { } point)
            {
                return false;
            }

            await view.EvaluateAsync(WebScreenScript.Expect(input.Kind, null), cancellationToken);
            if (!await view.TouchAsync(new Point(point.X, point.Y), hold, cancellationToken))
            {
                return false;
            }

            await Task.Delay(LandWait, cancellationToken);
            if (attempt == 1 || await view.EvaluateAsync(WebScreenScript.Landed, cancellationToken) != "false")
            {
                return true;
            }

            await Task.Delay(SettleWait, cancellationToken);
        }
    }

    private static bool TryParseModifiers(string? text, out KeyModifiers modifiers)
    {
        modifiers = KeyModifiers.None;
        return string.IsNullOrWhiteSpace(text) || Enum.TryParse(text, ignoreCase: true, out modifiers);
    }

    /// <summary>An input the page reported, as the screens replay it.</summary>
    internal static AppInput? ToAppInput(WebScreenInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!Enum.TryParse<AppInputKind>(input.Kind, out var kind))
        {
            return null;
        }

        return kind switch
        {
            AppInputKind.Tap or AppInputKind.LongPress when input.Path is { Length: > 0 } path => new AppInput(kind, path, new Point(input.X, input.Y)),
            AppInputKind.Scroll when input.Path is { Length: > 0 } path => new AppInput(kind, path, ScrollDelta: new Vector(input.Dx, input.Dy)),
            AppInputKind.Text when input.Text is { Length: > 0 } text => new AppInput(kind, string.Empty, Text: text),
            AppInputKind.Key when input.Key is { Length: > 0 } key => new AppInput(kind, string.Empty, Key: key, Modifiers: input.Modifiers),
            _ => null,
        };
    }

    /// <summary>A problem the page reported, as the layout checks list it.</summary>
    internal static LayoutIssue? ToLayoutIssue(WebScreenIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        if (!Enum.TryParse<LayoutIssueKind>(issue.Kind, out var kind) || !Enum.TryParse<LayoutIssueSeverity>(issue.Severity, out var severity))
        {
            return null;
        }

        var bounds = issue.Bounds is { } box ? new Rect(box.X, box.Y, Math.Max(0, box.Width), Math.Max(0, box.Height)) : default;
        return new LayoutIssue(kind, severity, issue.Message, issue.Path, bounds);
    }

    private static Task<T> OnUiThreadAsync<T>(Func<Task<T>> work) =>
        Dispatcher.UIThread.CheckAccess() ? work() : Dispatcher.UIThread.InvokeAsync(work);
}
