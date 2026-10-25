using System.Collections.Specialized;
using Avalonia.Threading;
using TabStudio.App.Swarm.LayoutChecks;

namespace TabStudio.App.Swarm;

/// <summary>One screen's side of a finding: the issue as that screen found it, and its picture at the time.</summary>
/// <param name="ScreenId">The screen's id.</param>
/// <param name="Profile">The screen, when it was checked.</param>
/// <param name="Issue">The issue on it.</param>
/// <param name="Screenshot">What the screen showed, as a PNG image, if it could be taken.</param>
public sealed record ScreenFinding(string ScreenId, ScreenProfile Profile, LayoutIssue Issue, byte[]? Screenshot);

/// <summary>A layout problem of one element, with every screen it shows on.</summary>
/// <param name="Kind">The kind of problem.</param>
/// <param name="Severity">The most serious it is on any screen.</param>
/// <param name="ElementPath">The element, as every screen names it.</param>
/// <param name="Message">What is wrong, as the first screen that has it says.</param>
/// <param name="Screens">The screens it shows on, in the order of the screens.</param>
public sealed record LayoutFinding(LayoutIssueKind Kind, LayoutIssueSeverity Severity, string ElementPath, string Message, IReadOnlyList<ScreenFinding> Screens)
{
    /// <summary>Its code, such as SWRM001.</summary>
    public string Code => LayoutRules.Code(Kind);

    /// <summary>The screens' names, such as "Small phone, Tablet".</summary>
    public string ScreensText => string.Join(", ", Screens.Select(s => s.Profile.Name));
}

/// <summary>
/// Keeps the layout checks of every screen in the <see cref="AppScreenRegistry"/> current. A screen is checked a
/// moment after the app starts or restarts on it, once its views have settled, and again when it changes size,
/// theme, direction or font scale; its findings are merged with the other screens', one per element and problem.
/// </summary>
/// <remarks>
/// Phones in the swarm check themselves and answer over TabStudio Link; a screen that does not answer in time keeps
/// its earlier findings. Everything here happens on the UI thread; screens may raise their events on any thread.
/// </remarks>
public sealed class ScreenLayoutChecks : IDisposable
{
    /// <summary>How long a screen's views have to settle, such as data a view model loads, before they are checked.</summary>
    public static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(700);

    /// <summary>How long a phone may take to answer.</summary>
    private static readonly TimeSpan AnswerTimeout = TimeSpan.FromSeconds(15);

    private readonly AppScreenRegistry _registry;
    private readonly Func<LayoutCheckSettings> _settings;
    private readonly Dictionary<IAppScreen, ScreenState> _states = new(ReferenceEqualityComparer.Instance);
    private bool _disposed;

    /// <summary>Starts following the screens.</summary>
    /// <param name="registry">Every screen the app runs on.</param>
    /// <param name="settings">Which checks run, read at every check.</param>
    public ScreenLayoutChecks(AppScreenRegistry registry, Func<LayoutCheckSettings> settings)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(settings);
        _registry = registry;
        _settings = settings;
        _registry.Screens.CollectionChanged += OnScreensChanged;
        foreach (var screen in _registry.Screens)
        {
            Follow(screen);
        }
    }

    /// <summary>Raised on the UI thread when <see cref="Findings"/> changed.</summary>
    public event EventHandler? FindingsChanged;

    /// <summary>Every finding on every screen, the most serious first.</summary>
    public IReadOnlyList<LayoutFinding> Findings { get; private set; } = [];

    /// <summary>How many screens are being checked now.</summary>
    public int Checking => _states.Values.Count(s => s.Pending is not null);

    /// <summary>Checks every running screen again now, as after a change of the checks' settings.</summary>
    public void CheckAll()
    {
        foreach (var (screen, state) in _states)
        {
            if (screen.IsRunning)
            {
                Schedule(screen, state, TimeSpan.Zero);
            }
        }
    }

    /// <summary>Checks one screen again now.</summary>
    /// <param name="screen">The screen.</param>
    public void Check(IAppScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        if (_states.TryGetValue(screen, out var state))
        {
            Schedule(screen, state, TimeSpan.Zero);
        }
    }

    /// <summary>Forgets every finding, as when the workspace closes.</summary>
    public void Clear()
    {
        foreach (var state in _states.Values)
        {
            state.Pending?.Cancel();
            state.Issues = [];
            state.Screenshot = null;
        }

        Rebuild();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registry.Screens.CollectionChanged -= OnScreensChanged;
        foreach (var (screen, state) in _states)
        {
            screen.Changed -= state.OnChanged;
            state.Pending?.Cancel();
            state.Pending?.Dispose();
        }

        _states.Clear();
    }

    private void OnScreensChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var screen in _states.Keys.Except(_registry.Screens).ToList())
        {
            var state = _states[screen];
            screen.Changed -= state.OnChanged;
            state.Pending?.Cancel();
            _states.Remove(screen);
        }

        foreach (var screen in _registry.Screens.Where(s => !_states.ContainsKey(s)).ToList())
        {
            Follow(screen);
        }

        Rebuild();
    }

    private void Follow(IAppScreen screen)
    {
        var state = new ScreenState();
        state.OnChanged = (_, _) => Dispatcher.UIThread.Post(() => OnScreenChanged(screen, state));
        _states[screen] = state;
        screen.Changed += state.OnChanged;
        OnScreenChanged(screen, state);
    }

    /// <summary>A screen started the app, or changed what it looks like: it is checked once it settles.</summary>
    private void OnScreenChanged(IAppScreen screen, ScreenState state)
    {
        if (_disposed || !_states.ContainsKey(screen))
        {
            return;
        }

        var running = screen.IsRunning;
        var profile = screen.Profile;
        var started = running && !state.WasRunning;
        var changed = running && state.Profile is not null && state.Profile != profile;
        state.WasRunning = running;
        state.Profile = profile;
        if (started || changed)
        {
            Schedule(screen, state, SettleDelay);
        }
    }

    private void Schedule(IAppScreen screen, ScreenState state, TimeSpan delay)
    {
        state.Pending?.Cancel();
        state.Pending?.Dispose();
        var pending = new CancellationTokenSource();
        state.Pending = pending;
        _ = CheckAsync(screen, state, delay, pending);
    }

    private async Task CheckAsync(IAppScreen screen, ScreenState state, TimeSpan delay, CancellationTokenSource pending)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, pending.Token);
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(pending.Token);
            timeout.CancelAfter(AnswerTimeout);
            var issues = await screen.InspectLayoutAsync(timeout.Token);
            var screenshot = issues.Count > 0 ? await screen.CaptureAsync(timeout.Token) : null;
            if (pending.IsCancellationRequested || !_states.ContainsKey(screen))
            {
                return;
            }

            state.Issues = issues;
            state.Screenshot = screenshot;
            state.CheckedProfile = screen.Profile;
        }
        catch (OperationCanceledException)
        {
            // Replaced by a newer check, or the screen did not answer in time: its earlier findings stay.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // A screen that cannot be checked, such as a phone that left the swarm, keeps its earlier findings.
        }
        finally
        {
            if (ReferenceEquals(state.Pending, pending))
            {
                state.Pending = null;
                pending.Dispose();
            }
        }

        Rebuild();
    }

    /// <summary>Merges every screen's issues: one finding per element and problem, on all the screens that have it.</summary>
    private void Rebuild()
    {
        if (_disposed)
        {
            return;
        }

        var settings = _settings();
        var order = _registry.Screens.ToList();
        Findings =
        [
            .. _states
                .OrderBy(s => order.IndexOf(s.Key))
                .SelectMany(s => s.Value.Issues
                    .Where(issue => settings.IsEnabled(issue.Kind))
                    .Select(issue => new ScreenFinding(s.Key.Id, s.Value.CheckedProfile ?? s.Key.Profile, issue, s.Value.Screenshot)))
                .GroupBy(f => (f.Issue.Kind, f.Issue.ElementPath))
                .Select(g => new LayoutFinding(
                    g.Key.Kind,
                    g.Max(f => f.Issue.Severity),
                    g.Key.ElementPath,
                    g.OrderByDescending(f => f.Issue.Severity).First().Issue.Message,
                    [.. g]))
                .OrderByDescending(f => f.Severity)
                .ThenBy(f => f.Kind)
                .ThenBy(f => f.ElementPath, StringComparer.Ordinal),
        ];
        FindingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>What is known of one screen.</summary>
    private sealed class ScreenState
    {
        public EventHandler OnChanged { get; set; } = (_, _) => { };

        public bool WasRunning { get; set; }

        public ScreenProfile? Profile { get; set; }

        public ScreenProfile? CheckedProfile { get; set; }

        public IReadOnlyList<LayoutIssue> Issues { get; set; } = [];

        public byte[]? Screenshot { get; set; }

        public CancellationTokenSource? Pending { get; set; }
    }
}
