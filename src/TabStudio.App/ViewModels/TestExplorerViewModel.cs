using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Visual Studio's Test Explorer: the tests of every test project in a tree with their outcomes and durations,
/// counts and filters, Run All, Run Failed, Run Selected, Repeat Last Run and Cancel, and the details of the selected
/// test. Tests are listed from source before any build; results arrive live while tests run.
/// </summary>
/// <remarks>
/// Building and running belong to the workspace (<see cref="ITestRunHost"/>), which reports what happens through
/// the <c>Report…</c> methods. Everything here runs on the UI thread.
/// </remarks>
public sealed partial class TestExplorerViewModel : ObservableObject, ITestNodeActions
{
    private readonly ITestRunHost _host;
    private readonly IUiDispatcher _ui;
    private readonly List<ProjectTests> _projects = [];
    private readonly Dictionary<string, TestEntry> _byId = new(StringComparer.Ordinal);
    private readonly Dictionary<TestEntry, List<TestNodeViewModel>> _owners = [];
    private readonly Dictionary<string, bool> _expanded = new(StringComparer.Ordinal);
    private readonly List<TestRunSummary> _runSummaries = [];
    private List<TestEntry> _entries = [];
    private List<TestNodeViewModel> _roots = [];
    private bool _rebuildScheduled;
    private bool _buildFailed;
    private bool _cancelRequested;
    private int _runTotal;
    private int _runCompleted;

    /// <summary>Creates the Test Explorer.</summary>
    /// <param name="host">Builds and runs tests, and opens files.</param>
    /// <param name="ui">The UI dispatcher.</param>
    public TestExplorerViewModel(ITestRunHost host, IUiDispatcher ui)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(ui);
        _host = host;
        _ui = ui;
        GroupByOptions =
        [
            GroupByOption(TestGroupBy.Hierarchy, "Project, Namespace, Class"),
            GroupByOption(TestGroupBy.Class, "Class"),
            GroupByOption(TestGroupBy.Outcome, "Outcome"),
            GroupByOption(TestGroupBy.Duration, "Duration"),
            GroupByOption(TestGroupBy.Traits, "Traits"),
        ];
        UpdateEmptyText();
    }

    /// <summary>The visible rows of the tree, in display order.</summary>
    public RangeObservableCollection<TestNodeViewModel> Rows { get; } = [];

    /// <summary>Every test, in discovery order.</summary>
    public IReadOnlyList<TestEntry> Entries => _entries;

    /// <summary>The choices of the "Group By" menu.</summary>
    public IReadOnlyList<TestGroupByOption> GroupByOptions { get; }

    /// <summary>How tests are grouped.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GroupByToolTip))]
    public partial TestGroupBy GroupBy { get; private set; }

    /// <summary>The "Group By" button's tooltip, naming the current grouping.</summary>
    public string GroupByToolTip => "Group By: " + GroupByOptions.First(o => o.Value == GroupBy).Name;

    /// <summary>The selected row.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunSelectedCommand))]
    public partial TestNodeViewModel? SelectedNode { get; set; }

    /// <summary>The details of the selected row.</summary>
    [ObservableProperty]
    public partial TestDetailsViewModel? Details { get; private set; }

    /// <summary>Only tests whose name, class or trait contains this text are listed.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>Only tests in this state are listed, or every test when <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingAll), nameof(IsShowingPassed), nameof(IsShowingFailed), nameof(IsShowingSkipped), nameof(IsShowingNotRun))]
    public partial TestState? OutcomeFilter { get; private set; }

    /// <summary>Whether no outcome filter is on.</summary>
    public bool IsShowingAll => OutcomeFilter is null;

    /// <summary>Whether only passed tests are listed.</summary>
    public bool IsShowingPassed => OutcomeFilter == TestState.Passed;

    /// <summary>Whether only failed tests are listed.</summary>
    public bool IsShowingFailed => OutcomeFilter == TestState.Failed;

    /// <summary>Whether only skipped tests are listed.</summary>
    public bool IsShowingSkipped => OutcomeFilter == TestState.Skipped;

    /// <summary>Whether only tests that have not run are listed.</summary>
    public bool IsShowingNotRun => OutcomeFilter == TestState.NotRun;

    /// <summary>The number of tests.</summary>
    [ObservableProperty]
    public partial int TotalCount { get; private set; }

    /// <summary>The number of tests that passed.</summary>
    [ObservableProperty]
    public partial int PassedCount { get; private set; }

    /// <summary>The number of tests that failed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFailures))]
    [NotifyCanExecuteChangedFor(nameof(RunFailedCommand))]
    public partial int FailedCount { get; private set; }

    /// <summary>Whether any test failed, for the red count on the Test Explorer's tab.</summary>
    public bool HasFailures => FailedCount > 0;

    /// <summary>The number of tests that were skipped.</summary>
    [ObservableProperty]
    public partial int SkippedCount { get; private set; }

    /// <summary>The number of tests that have not run.</summary>
    [ObservableProperty]
    public partial int NotRunCount { get; private set; }

    /// <summary>Whether the workspace has test projects.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunAllCommand))]
    public partial bool HasTestProjects { get; private set; }

    /// <summary>What the tree area says when it has no rows, or <see langword="null"/> when it has some.</summary>
    [ObservableProperty]
    public partial string? EmptyText { get; private set; }

    /// <summary>Whether a run is in progress, from its build to its last test.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    public partial bool IsRunning { get; private set; }

    /// <summary>Whether the run is still building, so its progress is unknown.</summary>
    [ObservableProperty]
    public partial bool IsProgressIndeterminate { get; private set; }

    /// <summary>The share of the run's tests that finished, from 0 to 1.</summary>
    [ObservableProperty]
    public partial double Progress { get; private set; }

    /// <summary>What the current or last run is doing or did, or <see langword="null"/> before the first run.</summary>
    [ObservableProperty]
    public partial string? RunStatusText { get; private set; }

    /// <summary>The outcome of the last run, for the icon next to <see cref="RunStatusText"/>.</summary>
    [ObservableProperty]
    public partial TestState RunStatusState { get; private set; }

    /// <summary>The last run, which "Repeat Last Run" runs again.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RepeatLastRunCommand))]
    public partial TestRunRequest? LastRequest { get; private set; }

    /// <summary>Whether a run can start: none is in progress and the IDE is not building or running a program.</summary>
    public bool CanStartRun => !IsRunning && _host.CanStartTestRun;

    /// <summary>Builds and runs tests, showing their results as they finish.</summary>
    /// <param name="request">The tests to run.</param>
    public async Task RunAsync(TestRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CanStartRun || request.Projects.Count == 0)
        {
            return;
        }

        LastRequest = request;
        _runSummaries.Clear();
        _runTotal = 0;
        _runCompleted = 0;
        _buildFailed = false;
        _cancelRequested = false;
        IsRunning = true;
        IsProgressIndeterminate = true;
        Progress = 0;
        RunStatusState = TestState.Running;
        RunStatusText = $"Building to run {request.Description}…";
        RefreshCommands();
        try
        {
            await _host.RunTestsAsync(request);
        }
        finally
        {
            IsRunning = false;
            FinishRun(request);
            RefreshCommands();
        }
    }

    /// <summary>Tells the commands that the IDE's state changed, so they enable or disable themselves.</summary>
    public void RefreshCommands()
    {
        RunAllCommand.NotifyCanExecuteChanged();
        RunFailedCommand.NotifyCanExecuteChanged();
        RunSelectedCommand.NotifyCanExecuteChanged();
        RepeatLastRunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
    }

    /// <inheritdoc />
    Task ITestNodeActions.RunAsync(TestNodeViewModel node) =>
        RunAsync(new TestRunRequest(node.Name, node.Selection()));

    /// <inheritdoc />
    Task ITestNodeActions.GoToAsync(TestNodeViewModel node) =>
        node.Location is { } location ? _host.NavigateToAsync(location.FilePath, location.Line, location.Column) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(CanRunAll))]
    private Task RunAllAsync() =>
        RunAsync(new TestRunRequest("all tests", _projects.Select(p => new TestProjectSelection(p.FilePath, null)).ToList()));

    private bool CanRunAll() => CanStartRun && HasTestProjects;

    [RelayCommand(CanExecute = nameof(CanRunFailed))]
    private Task RunFailedAsync()
    {
        var failed = _entries
            .Where(e => e.State == TestState.Failed)
            .GroupBy(e => e.ProjectFilePath, StringComparer.Ordinal)
            .Select(g => new TestProjectSelection(g.Key, g.Select(e => e.Id).ToList()))
            .ToList();
        return RunAsync(new TestRunRequest("failed tests", failed));
    }

    private bool CanRunFailed() => CanStartRun && FailedCount > 0;

    [RelayCommand(CanExecute = nameof(CanRunSelected))]
    private Task RunSelectedAsync() =>
        SelectedNode is { } node ? ((ITestNodeActions)this).RunAsync(node) : Task.CompletedTask;

    private bool CanRunSelected() => CanStartRun && SelectedNode is not null;

    [RelayCommand(CanExecute = nameof(CanRepeatLastRun))]
    private Task RepeatLastRunAsync() => LastRequest is { } request ? RunAsync(request) : Task.CompletedTask;

    private bool CanRepeatLastRun() => CanStartRun && LastRequest is not null;

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        _cancelRequested = true;
        _host.CancelTestRun();
        RunStatusText = "Cancelling…";
    }

    [RelayCommand]
    private void Filter(TestState state) => OutcomeFilter = OutcomeFilter == state ? null : state;

    [RelayCommand]
    private void ClearFilter() => OutcomeFilter = null;

    [RelayCommand]
    private void SetGroupBy(TestGroupBy groupBy)
    {
        GroupBy = groupBy;
        foreach (var option in GroupByOptions)
        {
            option.IsSelected = option.Value == groupBy;
        }
    }

    private TestGroupByOption GroupByOption(TestGroupBy value, string name) =>
        new(value, name, new RelayCommand(() => SetGroupBy(value))) { IsSelected = value == GroupBy };

    partial void OnGroupByChanged(TestGroupBy value) => Rebuild();

    partial void OnOutcomeFilterChanged(TestState? value) => Rebuild();

    partial void OnSearchTextChanged(string value) => Rebuild();

    partial void OnSelectedNodeChanged(TestNodeViewModel? value) => UpdateDetails();

    private ProjectTests? Find(string projectFilePath) => _projects.Find(p => p.FilePath == projectFilePath);

    private TestEntry? Find(string projectFilePath, string id) => _byId.GetValueOrDefault(projectFilePath + "\n" + id);
}
