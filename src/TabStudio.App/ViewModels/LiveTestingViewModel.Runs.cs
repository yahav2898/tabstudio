using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Workspaces;
using TabStudio.LiveTesting;
using TabStudio.LiveTesting.Building;
using TabStudio.LiveTesting.Results;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>When live runs happen: after edits and saves, never two at once, and not while the IDE or the device is busy.</summary>
public sealed partial class LiveTestingViewModel
{
    private readonly List<TestKey> _requested = [];
    private readonly Dictionary<string, IReadOnlyList<TestCase>> _discovered = new(StringComparer.Ordinal);
    private CancellationTokenSource? _delay;
    private CancellationTokenSource? _run;
    private Task _runLoop = Task.CompletedTask;
    private bool _runWanted;
    private int _runId;

    /// <summary>How long a test may run in a live run: shorter on small devices, so one hung test does not hold the others back.</summary>
    private TimeSpan TestTimeout => _host.Profile.Class == DeviceClass.Compact ? TimeSpan.FromSeconds(20) : TimeSpan.FromSeconds(30);

    /// <summary>How long to wait after the last keystroke: longer on small and warm devices.</summary>
    private TimeSpan TypingDelay
    {
        get
        {
            var delay = _host.Profile.Class switch
            {
                DeviceClass.Compact => TimeSpan.FromSeconds(2),
                DeviceClass.Standard => TimeSpan.FromMilliseconds(1200),
                _ => TimeSpan.FromMilliseconds(800),
            };
            return _host.Thermal == ThermalState.Warm ? delay * 2 : delay;
        }
    }

    /// <summary>The user changed a document's text.</summary>
    /// <param name="document">The document.</param>
    public void OnDocumentEdited(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!IsActive || !document.HasLanguageService)
        {
            return;
        }

        if (State is LiveTestingState.UpToDate or LiveTestingState.BuildFailed)
        {
            State = LiveTestingState.Pending;
        }

        if (Trigger == LiveTestingTrigger.AfterTyping)
        {
            RunAfter(TypingDelay);
        }
    }

    /// <summary>A document was saved.</summary>
    /// <param name="document">The document.</param>
    public void OnDocumentSaved(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (IsActive && document.FileType.IsRoslyn)
        {
            RunAfter(TimeSpan.FromMilliseconds(100));
        }
    }

    /// <summary>The workspace was reloaded, for example after files were added: its builds may have changed.</summary>
    public void OnWorkspaceReloaded()
    {
        RefreshCommands();
        if (IsActive)
        {
            RunAfter(TimeSpan.FromMilliseconds(100));
        }
    }

    /// <summary>
    /// The IDE or the device changed: a build, program, debug session, Test Explorer run or notebook cell started or
    /// ended, or the device warmed up or cooled down. A live run gives way, and resumes when it can.
    /// </summary>
    public void OnIdeChanged()
    {
        if (!IsActive)
        {
            return;
        }

        if (!CanRunNow(out var waiting))
        {
            if (_run is { } run)
            {
                // The run gives way; the tests it did not reach run when the IDE is free again.
                _runWanted = true;
                run.Cancel();
            }
            else if (_runWanted)
            {
                State = waiting;
            }
        }
        else if (_runWanted && _run is null)
        {
            TryStartRun();
        }
    }

    /// <inheritdoc />
    public Task RunTestsAsync(IReadOnlyList<TestKey> tests)
    {
        ArgumentNullException.ThrowIfNull(tests);
        if (tests.Count == 0)
        {
            return Task.CompletedTask;
        }

        if (IsActive)
        {
            _requested.AddRange(tests);
            RequestRun();
            return Task.CompletedTask;
        }

        // While Live Unit Testing is paused or off, the Test Explorer runs them.
        var projects = tests
            .GroupBy(t => t.ProjectFilePath, StringComparer.Ordinal)
            .Select(g => new TestProjectSelection(g.Key, [.. g.Select(t => t.TestId)]))
            .ToList();
        var description = tests.Count == 1 ? NameOf(tests[0]) : TestDisplay.Tests(tests.Count);
        return _host.Tests.RunAsync(new TestRunRequest(description, projects));
    }

    /// <summary>Runs after a delay, unless another edit comes first.</summary>
    private void RunAfter(TimeSpan delay)
    {
        _delay?.Cancel();
        _delay?.Dispose();
        _delay = new CancellationTokenSource();
        _ = RunAfterAsync(delay, _delay.Token);
    }

    private async Task RunAfterAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        RequestRun();
    }

    /// <summary>Asks for a run now: it starts at once, or replaces the run in progress, or waits until the IDE and device allow.</summary>
    private void RequestRun()
    {
        if (!IsActive)
        {
            return;
        }

        _runWanted = true;
        if (_run is { } run)
        {
            // The run in progress is out of date; the loop starts a new one as soon as it stops.
            run.Cancel();
            return;
        }

        TryStartRun();
    }

    private void TryStartRun()
    {
        if (!IsActive || _engine is null || !_runLoop.IsCompleted)
        {
            return;
        }

        if (!CanRunNow(out var waiting))
        {
            State = waiting;
            return;
        }

        _runLoop = RunLoopAsync(_engine);
    }

    private bool CanRunNow(out LiveTestingState waiting)
    {
        if (_host.Thermal == ThermalState.Hot)
        {
            waiting = LiveTestingState.WaitingForDevice;
            return false;
        }

        if (!_host.CanRunLive)
        {
            waiting = LiveTestingState.WaitingForIde;
            return false;
        }

        waiting = LiveTestingState.UpToDate;
        return true;
    }

    /// <summary>Runs until no run is wanted any more, one run at a time.</summary>
    private async Task RunLoopAsync(LiveTestEngine engine)
    {
        LiveTestingState? outcome = null;
        while (_runWanted && IsActive && ReferenceEquals(_engine, engine) && CanRunNow(out _))
        {
            _runWanted = false;
            using var run = new CancellationTokenSource();
            _run = run;
            var id = ++_runId;
            try
            {
                outcome = await RunOnceAsync(engine, id, run.Token);
            }
            catch (OperationCanceledException)
            {
                // A newer edit, a pause or the IDE stopped it; the loop decides what comes next.
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Problem = "Live Unit Testing stopped: " + ex.Message;
                outcome = LiveTestingState.UpToDate;
            }
            finally
            {
                _run = null;
                RunningCount = 0;
                _host.Tests.ReportLiveRunEnded();
            }
        }

        if (!IsActive || !ReferenceEquals(_engine, engine))
        {
            // Stopped, or stopped and started again: a new engine runs once this loop is over.
            if (_runWanted && IsActive)
            {
                _ui.Post(TryStartRun);
            }

            return;
        }

        if (_runWanted && !CanRunNow(out var waiting))
        {
            State = waiting;
        }
        else if (outcome is { } finalState)
        {
            State = finalState;
        }

        RefreshNow();
    }

    /// <summary>One live run: the builds, then the tests the changes affect.</summary>
    /// <returns>The state to show once it ended: up to date, or waiting for a build that succeeds.</returns>
    private async Task<LiveTestingState> RunOnceAsync(LiveTestEngine engine, int id, CancellationToken cancellationToken)
    {
        State = LiveTestingState.Building;
        await _host.PrepareLiveRunAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var requested = _requested.ToList();
        _requested.Clear();
        Problem = null;
        var summary = await engine.RunAsync(_host.TestProjects, new Listener(this, id), requested, cancellationToken);

        BuildErrorCount = summary.FailedBuilds
            .SelectMany(b => b.Result.Diagnostics)
            .Where(d => d.Severity == DiagnosticSeverityLevel.Error)
            .Select(d => d.ToString())
            .Distinct(StringComparer.Ordinal)
            .Count();
        if (summary.Problems.Count > 0)
        {
            Problem = summary.Problems[0];
        }

        foreach (var project in _host.TestProjects.Where(p => !summary.FailedBuilds.Any(b => b.Result.ProjectName == p.Name)))
        {
            _host.Tests.ReportLiveResultsCurrent(project.FilePath, engine.Index.Tests.Where(t => t.Key.ProjectFilePath == project.FilePath && t.Result is not null).Select(t => t.Key.TestId));
        }

        return summary.FailedBuilds.Count > 0 ? LiveTestingState.BuildFailed : LiveTestingState.UpToDate;
    }

    /// <summary>Stops the run in progress and the wait before the next one.</summary>
    private void CancelPending()
    {
        _delay?.Cancel();
        _delay?.Dispose();
        _delay = null;
        _runWanted = false;
        _requested.Clear();
        _run?.Cancel();
    }

    /// <summary>Disposes an engine once its run, if any, has stopped.</summary>
    private void DisposeWhenIdle(LiveTestEngine engine) =>
        _ = _runLoop.ContinueWith(_ => engine.Dispose(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

    /// <summary>Runs an update on the UI thread if it belongs to the run in progress.</summary>
    private void PostForRun(int id, Action action) => _ui.Post(() =>
    {
        if (id == _runId && _run is not null && IsActive)
        {
            action();
        }
    });

    /// <summary>Takes the engine's progress, on background threads, to the status bar and the Test Explorer.</summary>
    private sealed class Listener(LiveTestingViewModel owner, int id) : ILiveTestListener
    {
        private int _queued;

        public void BuildStarted(ProjectInfo testProject) =>
            owner.PostForRun(id, () => owner.State = LiveTestingState.Building);

        public void BuildFinished(ProjectInfo testProject, CoverageBuild build)
        {
        }

        public void TestsDiscovered(ProjectInfo testProject, IReadOnlyList<TestCase> tests) =>
            owner.PostForRun(id, () =>
            {
                // A build that changed nothing keeps its session and its list: the Test Explorer already shows it.
                if (!owner._host.Tests.IsRunning
                    && !(owner._discovered.TryGetValue(testProject.FilePath, out var shown) && ReferenceEquals(shown, tests)))
                {
                    owner._discovered[testProject.FilePath] = tests;
                    owner._host.Tests.SetBuiltTests(testProject, tests);
                }
            });

        public void TestsQueued(ProjectInfo testProject, IReadOnlyList<TestCase> tests)
        {
            var queued = Interlocked.Add(ref _queued, tests.Count);
            owner.PostForRun(id, () =>
            {
                owner.RunningCount = queued;
                if (queued > 0)
                {
                    owner.State = LiveTestingState.Running;
                }

                if (!owner._host.Tests.IsRunning)
                {
                    owner._host.Tests.ReportQueued(testProject.FilePath, tests);
                }
            });
        }

        public void TestStarted(ProjectInfo testProject, TestCase test) =>
            owner.PostForRun(id, () =>
            {
                if (!owner._host.Tests.IsRunning)
                {
                    owner._host.Tests.ReportStarted(testProject.FilePath, test);
                }
            });

        public void TestFinished(ProjectInfo testProject, TestResult result) =>
            owner._ui.Post(() =>
            {
                // A result is a result even if the run was stopped right after it.
                if (owner.IsEnabled && !owner._host.Tests.IsRunning)
                {
                    owner._host.Tests.ReportFinished(testProject.FilePath, result);
                }
            });
    }
}
