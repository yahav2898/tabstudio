using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.LiveTesting;

namespace TabStudio.App.ViewModels;

/// <summary>Where Live Unit Testing stands, for the status bar and the Test Explorer.</summary>
public enum LiveTestingState
{
    /// <summary>Live Unit Testing is off.</summary>
    Off,

    /// <summary>On, and every marker is up to date.</summary>
    UpToDate,

    /// <summary>The code changed since the last run; the tests run after a pause in typing, or on save.</summary>
    Pending,

    /// <summary>Building the test projects and the code they test.</summary>
    Building,

    /// <summary>Running the tests the latest edits affect.</summary>
    Running,

    /// <summary>The code does not compile; the markers show the last results until it does.</summary>
    BuildFailed,

    /// <summary>Paused by the user: the markers stay, nothing runs.</summary>
    Paused,

    /// <summary>A build, a program, a debug session or a Test Explorer run has the IDE; runs resume after it.</summary>
    WaitingForIde,

    /// <summary>The device is hot; runs resume when it cools down.</summary>
    WaitingForDevice,
}

/// <summary>
/// Visual Studio's Live Unit Testing: while it is on, every line of code under test shows whether tests run it and
/// how they fare, and the tests an edit affects run again by themselves, a moment after typing stops or when a file
/// is saved.
/// </summary>
/// <remarks>
/// <para>
/// Test > Live Unit Testing starts, pauses and stops it, the Test Explorer's toolbar turns it on and off, and the
/// status bar shows what it is doing. The engine (<see cref="LiveTestEngine"/>) builds and runs in the background,
/// so typing never waits for it; results reach the editors' markers and the Test Explorer as tests finish.
/// </para>
/// <para>
/// Runs never overlap and are cancelled by the next edit. They wait while the IDE builds, runs or debugs a program
/// or runs tests from the Test Explorer, and while the device is hot; on a warm device, and on a compact one, they
/// wait longer after typing, and a compact device runs them on save unless the user chooses otherwise. Everything
/// here runs on the UI thread.
/// </para>
/// </remarks>
public sealed partial class LiveTestingViewModel : ObservableObject, ILiveTestingEditor, IDisposable
{
    private readonly ILiveTestingHost _host;
    private readonly IUiDispatcher _ui;
    private LiveTestEngine? _engine;
    private bool _disposed;

    /// <summary>Creates Live Unit Testing for a workspace, off.</summary>
    /// <param name="host">The IDE.</param>
    /// <param name="ui">The UI dispatcher.</param>
    /// <param name="settings">The settings saved for the solution, if any.</param>
    public LiveTestingViewModel(ILiveTestingHost host, IUiDispatcher ui, LiveTestingSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(ui);
        _host = host;
        _ui = ui;
        Inclusion = new LiveTestInclusion(settings?.Rules);
        Inclusion.Changed += (_, _) => _ui.Post(OnInclusionChanged);
        Trigger = settings?.Trigger ?? (host.Profile.Class == DeviceClass.Compact ? LiveTestingTrigger.OnSave : LiveTestingTrigger.AfterTyping);
    }

    /// <summary>Which projects and tests run live, remembered per solution.</summary>
    public LiveTestInclusion Inclusion { get; }

    /// <summary>What Live Unit Testing is doing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEnabled), nameof(IsPaused), nameof(IsActive), nameof(IsBusy), nameof(StatusText), nameof(DetailText), nameof(ToggleToolTip))]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(PauseCommand), nameof(ContinueCommand), nameof(StopCommand))]
    public partial LiveTestingState State { get; private set; }

    /// <summary>When tests run.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RunsAfterTyping), nameof(RunsOnSave), nameof(StatusText), nameof(DetailText))]
    public partial LiveTestingTrigger Trigger { get; private set; }

    /// <summary>The number of included tests whose latest live run passed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(DetailText))]
    public partial int PassedCount { get; private set; }

    /// <summary>The number of included tests whose latest live run failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(DetailText), nameof(HasFailures))]
    public partial int FailedCount { get; private set; }

    /// <summary>
    /// The number of tests excluded from Live Unit Testing. The summary names them, so a failing test the Test
    /// Explorer still shows red is not hidden behind "0 failed".
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(DetailText))]
    public partial int ExcludedCount { get; private set; }

    /// <summary>The number of tests the current run runs.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(DetailText))]
    public partial int RunningCount { get; private set; }

    /// <summary>The number of errors of the last build that failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    public partial int BuildErrorCount { get; private set; }

    /// <summary>A problem of the last run that belongs to no test, such as a test project that could not be loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText))]
    public partial string? Problem { get; private set; }

    /// <summary>Whether Live Unit Testing is on, running or paused: the markers show.</summary>
    public bool IsEnabled => State != LiveTestingState.Off;

    /// <summary>Whether it is paused.</summary>
    public bool IsPaused => State == LiveTestingState.Paused;

    /// <summary>Whether it is on and not paused, so edits run tests.</summary>
    public bool IsActive => IsEnabled && !IsPaused;

    /// <summary>Whether it is building or running tests, for the status bar's animation.</summary>
    public bool IsBusy => State is LiveTestingState.Building or LiveTestingState.Running;

    /// <summary>Whether a test failed in its latest live run.</summary>
    public bool HasFailures => FailedCount > 0;

    /// <summary>Whether tests run after a pause in typing.</summary>
    public bool RunsAfterTyping => Trigger == LiveTestingTrigger.AfterTyping;

    /// <summary>Whether tests run when a file is saved.</summary>
    public bool RunsOnSave => Trigger == LiveTestingTrigger.OnSave;

    /// <summary>The Test Explorer toolbar button's tooltip.</summary>
    public string ToggleToolTip => IsEnabled ? "Stop Live Unit Testing" : "Start Live Unit Testing";

    /// <summary>The status bar's text, short: what Live Unit Testing is doing, or how the tests fare.</summary>
    public string StatusText => State switch
    {
        LiveTestingState.Off => string.Empty,
        LiveTestingState.Pending => RunsOnSave ? "Live Unit Testing: save to run tests" : "Live Unit Testing: changes pending",
        LiveTestingState.Building => "Live Unit Testing: building",
        LiveTestingState.Running => "Live Unit Testing: running " + TestDisplay.Tests(RunningCount),
        LiveTestingState.BuildFailed => "Live Unit Testing: build failed",
        LiveTestingState.Paused => "Live Unit Testing paused",
        LiveTestingState.WaitingForIde => "Live Unit Testing waits for the IDE",
        LiveTestingState.WaitingForDevice => "Live Unit Testing waits for the device to cool down",
        _ => string.Create(CultureInfo.InvariantCulture, $"Live Unit Testing: {PassedCount} passed, {FailedCount} failed{Excluded}"),
    };

    private string Excluded => ExcludedCount == 0 ? string.Empty : string.Create(CultureInfo.InvariantCulture, $", {ExcludedCount} excluded");

    /// <summary>The Test Explorer's line about Live Unit Testing: its state and when it runs.</summary>
    public string DetailText
    {
        get
        {
            var when = RunsAfterTyping ? "Tests run again a moment after you stop typing." : "Tests run again when you save.";
            return State switch
            {
                LiveTestingState.Off => "Live Unit Testing is off.",
                LiveTestingState.Pending => RunsOnSave
                    ? "The code changed. Save to run the tests the changes affect."
                    : "The code changed. The tests it affects run a moment after you stop typing.",
                LiveTestingState.Building => "Building the changed code… " + when,
                LiveTestingState.Running => "Running " + TestDisplay.Tests(RunningCount) + " affected by the latest changes… " + when,
                LiveTestingState.BuildFailed => string.Create(CultureInfo.InvariantCulture, $"The code does not compile ({BuildErrorCount} {(BuildErrorCount == 1 ? "error" : "errors")}). The markers show the last results until it does."),
                LiveTestingState.Paused => "Paused. The markers show the last results; Continue runs the tests the changes since then affect.",
                LiveTestingState.WaitingForIde => "Waiting until the IDE finishes building, running or debugging.",
                LiveTestingState.WaitingForDevice => "Waiting for the device to cool down before running tests.",
                _ => Problem ?? string.Create(CultureInfo.InvariantCulture, $"Up to date: {PassedCount} passed, {FailedCount} failed{Excluded}. {when}"),
            };
        }
    }

    /// <summary>Test > Live Unit Testing > Start.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private void Start()
    {
        if (IsEnabled)
        {
            return;
        }

        _engine = new LiveTestEngine(
            (project, uninstrumented, token) => _host.BuildForCoverageAsync(project, uninstrumented, token),
            _host.GetPackageRuntimeAssemblies,
            Inclusion,
            new LiveTestEngineOptions { TestTimeout = TestTimeout });
        _engine.Index.Changed += OnIndexChanged;
        State = LiveTestingState.UpToDate;
        RequestRun();
    }

    private bool CanStart() => State == LiveTestingState.Off && _host.TestProjects.Count > 0;

    /// <summary>Test > Live Unit Testing > Pause: stops running tests and keeps the markers.</summary>
    [RelayCommand(CanExecute = nameof(IsActive))]
    private void Pause()
    {
        CancelPending();
        State = LiveTestingState.Paused;
    }

    /// <summary>Test > Live Unit Testing > Continue: runs the tests the changes made while paused affect.</summary>
    [RelayCommand(CanExecute = nameof(IsPaused))]
    private void Continue()
    {
        State = LiveTestingState.UpToDate;
        RequestRun();
    }

    /// <summary>Test > Live Unit Testing > Stop: stops, removes the markers and forgets the results.</summary>
    [RelayCommand(CanExecute = nameof(IsEnabled))]
    private void Stop()
    {
        CancelPending();
        if (_engine is { } engine)
        {
            _engine = null;
            engine.Index.Changed -= OnIndexChanged;
            DisposeWhenIdle(engine);
        }

        State = LiveTestingState.Off;
        _discovered.Clear();
        PassedCount = 0;
        FailedCount = 0;
        ExcludedCount = 0;
        RunningCount = 0;
        Problem = null;
        foreach (var document in _host.OpenDocuments)
        {
            document.ShowLiveTestCoverage(null);
        }

        _host.Tests.ReportLiveRunEnded();
    }

    /// <summary>The Test Explorer's toolbar button: starts or stops Live Unit Testing.</summary>
    [RelayCommand]
    private void Toggle()
    {
        if (IsEnabled)
        {
            Stop();
        }
        else if (CanStart())
        {
            Start();
        }
    }

    /// <summary>Test > Live Unit Testing > Run After Typing Pauses.</summary>
    [RelayCommand]
    private void UseRunAfterTyping() => SetTrigger(LiveTestingTrigger.AfterTyping);

    /// <summary>Test > Live Unit Testing > Run on Save.</summary>
    [RelayCommand]
    private void UseRunOnSave() => SetTrigger(LiveTestingTrigger.OnSave);

    /// <summary>Chooses when tests run: after a pause in typing, or on save.</summary>
    /// <param name="trigger">When tests run.</param>
    public void SetTrigger(LiveTestingTrigger trigger)
    {
        if (Trigger != trigger)
        {
            Trigger = trigger;
            SaveSettings();
        }
    }

    /// <summary>Tells the commands that the workspace's test projects changed.</summary>
    public void RefreshCommands() => StartCommand.NotifyCanExecuteChanged();

    /// <summary>Takes the settings saved for a solution that was just opened.</summary>
    /// <param name="settings">The saved settings, or <see langword="null"/> when there are none.</param>
    public void ApplySettings(LiveTestingSettings? settings)
    {
        Trigger = settings?.Trigger ?? (_host.Profile.Class == DeviceClass.Compact ? LiveTestingTrigger.OnSave : LiveTestingTrigger.AfterTyping);
        Inclusion.Reset(settings?.Rules ?? []);
        RefreshCommands();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        CancelPending();
        if (_engine is { } engine)
        {
            _engine = null;
            engine.Index.Changed -= OnIndexChanged;
            DisposeWhenIdle(engine);
        }
    }

    private void SaveSettings() => _host.SaveLiveTestingSettings(new LiveTestingSettings(Trigger, Inclusion.Rules));

    private void OnInclusionChanged()
    {
        SaveSettings();
        _host.Tests.RefreshLiveTestingInclusion();
    }

    /// <summary>Starting and stopping shows and hides which Test Explorer rows are excluded.</summary>
    partial void OnStateChanged(LiveTestingState oldValue, LiveTestingState newValue)
    {
        if ((oldValue == LiveTestingState.Off) != (newValue == LiveTestingState.Off))
        {
            _host.Tests.RefreshLiveTestingInclusion();
        }
    }
}
