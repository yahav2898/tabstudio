using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Compilation.Building;
using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Testing: finding tests in the source of the test projects, building them through the normal build, and running
/// them in-process for the Test Explorer.
/// </summary>
/// <remarks>
/// The test runner, F5 and notebooks each redirect the process's console, so a run only starts while the IDE is
/// <see cref="IdeState.Ready"/> or <see cref="IdeState.Failed"/>, and keeps it <see cref="IdeState.Busy"/> while
/// tests run, which keeps F5 and builds away until it ends.
/// </remarks>
public sealed partial class WorkspaceViewModel : ITestRunHost
{
    private readonly Dictionary<string, LoadedTests> _testSessions = new(StringComparer.Ordinal);
    private TestExplorerViewModel? _tests;
    private Task _testDiscovery = Task.CompletedTask;
    private CancellationTokenSource? _testRun;
    private string? _chordStatus;
    private string? _statusBeforeChord;

    /// <summary>The Test Explorer.</summary>
    public TestExplorerViewModel Tests => _tests ??= new TestExplorerViewModel(this, _ui);

    /// <inheritdoc />
    bool ITestRunHost.CanStartTestRun => (_language is not null || _toolchainTests.Count > 0 || _tests?.Entries.Any(e => e.Result?.Test.Framework == TestFramework.External) == true)
        && _testRun is null && (State is IdeState.Ready or IdeState.Failed || CanReplayUiTestsNow);

    /// <inheritdoc />
    async Task ITestRunHost.RunTestsAsync(TestRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Workspace is not { } workspace || _testRun is not null)
        {
            return;
        }
        try
        {
            request = await RunEmbeddedTestSelectionsAsync(request);
            request = await RunToolchainTestSelectionsAsync(request);
        }
        catch (OperationCanceledException)
        {
            Output.WriteLine("========== Embedded test run cancelled ==========", OutputKind.Info);
            return;
        }
        if (request.Projects.Count == 0 || _language is not { } language)
        {
            return;
        }

        // Recorded UI tests replay on the screens of the swarm, the app preview and the virtual devices.
        request = await RunUiTestsOnScreensAsync(request);
        if (State is not (IdeState.Ready or IdeState.Failed))
        {
            ReportTestsWaitingForTheApp(request);
            return;
        }
        var selections = request.Projects
            .Select(s => (Project: workspace.Projects.FirstOrDefault(p => p.FilePath == s.ProjectFilePath), s.Names))
            .Where(s => s.Project is not null)
            .Select(s => (Project: s.Project!, s.Names))
            .ToList();
        if (selections.Count == 0)
        {
            return;
        }

        // Running tests brings the Test Explorer forward, as in Visual Studio.
        SelectSideBarPanel(SideBarPanel.TestExplorer);
        using var run = new CancellationTokenSource();
        _testRun = run;
        var cancellation = run.Token;
        var running = false;
        try
        {
            var builds = new List<BuildResult>();
            await BuildProjectsAsync(selections.Select(s => s.Project).ToList(), showOutput: true, results: builds);
            if (builds.Count < selections.Count || builds.Exists(b => !b.Success))
            {
                Tests.ReportBuildFailed();
                return;
            }

            running = true;
            State = IdeState.Busy;
            Output.WriteLine("========== Starting test run ==========", OutputKind.Info);
            var summaries = new List<TestRunSummary>();
            for (var i = 0; i < selections.Count && !cancellation.IsCancellationRequested; i++)
            {
                var (project, names) = selections[i];
                StatusText = $"Running the tests of {project.Name}";
                if (await LoadTestSessionAsync(project, builds[i], language, cancellation) is not { } session)
                {
                    continue;
                }

                Tests.SetBuiltTests(project, session.Tests);
                var tests = names is null ? session.Tests.Where(t => !t.IsExplicit).ToList() : session.FindTests(names);
                summaries.Add(await RunTestSessionAsync(project, session, tests, cancellation));
            }

            ReportTestRun(summaries, cancellation.IsCancellationRequested);
        }
        catch (OperationCanceledException)
        {
            Output.WriteLine("========== Test run cancelled ==========", OutputKind.Info);
            StatusText = "Test run cancelled";
        }
        finally
        {
            _testRun = null;
            if (running)
            {
                State = IdeState.Ready;
            }
        }
    }

    /// <inheritdoc />
    void ITestRunHost.CancelTestRun() => _testRun?.Cancel();

    /// <summary>Shows a pending key chord, such as Ctrl+R before its second key, in the status bar, or clears it.</summary>
    /// <param name="text">What to show, or <see langword="null"/> when the chord ended.</param>
    public void ShowKeyChordStatus(string? text)
    {
        if (text is not null)
        {
            _statusBeforeChord ??= StatusText;
            _chordStatus = text;
            StatusText = text;
        }
        else if (_chordStatus is not null)
        {
            if (StatusText == _chordStatus)
            {
                StatusText = _statusBeforeChord ?? "Ready";
            }

            _chordStatus = null;
            _statusBeforeChord = null;
        }
    }

    /// <summary>
    /// Ctrl+R, T and the editor's "Run Tests": runs the data row, test, class or file at the caret of the active
    /// document.
    /// </summary>
    [RelayCommand]
    private async Task RunTestsInContextAsync()
    {
        if (ActiveDocument is not { } document
            || Workspace?.FindProjectForFile(document.FilePath) is not { IsTestProject: true } project
            || CaretTestFinder.Find(document.Document.Text, document.FilePath, document.CaretOffset) is not { } tests)
        {
            StatusText = "There are no tests at the caret";
            return;
        }

        await Tests.RunAsync(new TestRunRequest(tests.Description, [new TestProjectSelection(project.FilePath, tests.Names)]));
    }

    /// <summary>Starts watching the workspace for test projects. Called once, by the constructor.</summary>
    private void InitializeTests() => PropertyChanged += OnPropertyChangedForTests;

    /// <summary>Stops a run and releases the test sessions, when the workspace closes.</summary>
    private void DisposeTests()
    {
        _testRun?.Cancel();
        foreach (var loaded in _testSessions.Values)
        {
            loaded.Session.Dispose();
        }

        _testSessions.Clear();
    }

    private void OnPropertyChangedForTests(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Workspace):
                OnTestWorkspaceChanged();
                break;
            case nameof(State):
                Tests.RefreshCommands();
                break;
        }
    }

    /// <summary>After the workspace opened or reloaded: lists its test projects and finds their tests in source.</summary>
    private void OnTestWorkspaceChanged()
    {
        var projects = TestProjectsOf(Workspace);
        if (_testRun is null)
        {
            foreach (var removed in _testSessions.Keys.Where(path => !projects.Exists(p => p.FilePath == path)).ToList())
            {
                _testSessions[removed].Session.Dispose();
                _testSessions.Remove(removed);
            }
        }

        Tests.SetProjects([.. projects, .. ToolchainTestProjects()]);
        QueueTestDiscovery(projects);
    }

    /// <summary>
    /// After a C# file is saved: the results of the test projects that depend on its project are out of date, and a
    /// test project's tests are found again.
    /// </summary>
    /// <param name="document">The saved document.</param>
    private void OnDocumentSavedForTests(DocumentViewModel document)
    {
        if (!document.FileType.IsRoslyn || Workspace is not { } workspace || workspace.FindProjectForFile(document.FilePath) is not { } owner)
        {
            return;
        }

        var testProjects = TestProjectsOf(workspace);
        Tests.MarkStale(testProjects.Where(t => DependsOn(workspace, t, owner)).Select(t => t.FilePath));
        if (testProjects.Find(t => t.FilePath == owner.FilePath) is { } testProject)
        {
            QueueTestDiscovery([testProject]);
        }
    }

    /// <summary>
    /// The C# test projects whose tests Test Explorer finds and runs: those on xUnit, NUnit or MSTest, not TUnit, that
    /// need no platform workload, such as a .NET MAUI app's tests.
    /// </summary>
    private static List<ProjectInfo> TestProjectsOf(Workspace? workspace) =>
        workspace?.Projects
            .Where(p => p.IsTestProject && p.IsRoslynLanguage && !ProjectClassifier.UsesTUnit(p) && !ProjectClassifier.NeedsPlatformWorkload(p))
            .ToList() ?? [];

    /// <summary>Whether a project is, or references directly or indirectly, another project.</summary>
    private static bool DependsOn(Workspace workspace, ProjectInfo project, ProjectInfo dependency)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<ProjectInfo>([project]);
        while (pending.TryPop(out var next))
        {
            if (next.FilePath == dependency.FilePath)
            {
                return true;
            }

            if (!seen.Add(next.FilePath))
            {
                continue;
            }

            foreach (var reference in next.ProjectReferences)
            {
                if (workspace.Projects.FirstOrDefault(p => string.Equals(p.FilePath, reference, StringComparison.Ordinal)) is { } referenced)
                {
                    pending.Push(referenced);
                }
            }
        }

        return false;
    }

    /// <summary>Finds the tests of projects in their source, after the discoveries already queued.</summary>
    private void QueueTestDiscovery(List<ProjectInfo> projects)
    {
        if (projects.Count == 0 || Workspace is not { } workspace)
        {
            return;
        }

        var directories = workspace.Projects.Select(p => p.DirectoryPath).ToList();
        var unsaved = Documents.Where(d => d.IsDirty).ToDictionary(d => d.FilePath, d => d.Document.Text, StringComparer.Ordinal);
        var previous = _testDiscovery;
        _testDiscovery = DiscoverTestsAfterAsync(previous, projects, directories, new OpenDocumentTextProvider(unsaved), _background?.Token ?? CancellationToken.None);
    }

    private async Task DiscoverTestsAfterAsync(Task previous, List<ProjectInfo> projects, List<string> directories, OpenDocumentTextProvider sources, CancellationToken cancellationToken)
    {
        await previous;
        try
        {
            foreach (var project in projects)
            {
                var classes = await Task.Run(() => SourceTestScanner.Scan(project, directories, sources, cancellationToken), cancellationToken);
                if (!_disposed)
                {
                    Tests.SetSourceTests(project, classes);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The workspace closed.
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Discovery reads files that may change under it; a failure must not stop later discoveries.
            Output.WriteLine($"Finding tests stopped: {ex.Message}", OutputKind.Failure);
        }
    }

    /// <summary>
    /// Loads a built test project into a test session, or reuses the last one when the build produced the same
    /// assemblies: assemblies cannot be unloaded on Android, so an unchanged project is not loaded again.
    /// </summary>
    private async Task<TestSession?> LoadTestSessionAsync(ProjectInfo project, BuildResult build, ILanguageService language, CancellationToken cancellationToken)
    {
        List<CompiledAssembly> assemblies = [build.EntryAssembly!, .. build.Dependencies];
        var packages = language.GetPackageRuntimeAssemblies(project.FilePath);
        if (_testSessions.TryGetValue(project.FilePath, out var loaded) && loaded.IsFor(assemblies, packages))
        {
            return loaded.Session;
        }

        TestSession session;
        try
        {
            session = await TestSession.LoadAsync(assemblies, packages, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or OutOfMemoryException))
        {
            Output.WriteLine($"The tests of {project.Name} could not be loaded: {ex.Message}", OutputKind.Failure);
            return null;
        }

        loaded?.Session.Dispose();
        _testSessions[project.FilePath] = new LoadedTests(session, assemblies, packages);
        foreach (var message in session.DiscoveryMessages)
        {
            Output.WriteLine($"{project.Name}: {message}", OutputKind.Failure);
        }

        return session;
    }

    /// <summary>Runs tests of one session, reporting each start and result to the Test Explorer as it happens.</summary>
    private async Task<TestRunSummary> RunTestSessionAsync(ProjectInfo project, TestSession session, IReadOnlyList<TestCase> tests, CancellationToken cancellationToken)
    {
        var path = project.FilePath;
        Tests.ReportQueued(path, tests);

        // The runner raises these on its own threads.
        void OnStarted(object? sender, TestStartedEventArgs e) => _ui.Post(() => Tests.ReportStarted(path, e.Test));
        void OnFinished(object? sender, TestFinishedEventArgs e) => _ui.Post(() => Tests.ReportFinished(path, e.Result));

        session.TestStarted += OnStarted;
        session.TestFinished += OnFinished;
        try
        {
            var summary = await session.RunAsync(tests, cancellationToken);
            Tests.ReportFinished(path, summary);
            return summary;
        }
        finally
        {
            session.TestStarted -= OnStarted;
            session.TestFinished -= OnFinished;
        }
    }

    /// <summary>Writes the run's totals to the Output panel and the status bar, in Visual Studio's words.</summary>
    private void ReportTestRun(List<TestRunSummary> summaries, bool cancelled)
    {
        foreach (var message in summaries.SelectMany(s => s.Messages))
        {
            Output.WriteLine(message, OutputKind.Failure);
        }

        var total = summaries.Sum(s => s.Total);
        var passed = summaries.Sum(s => s.Passed);
        var failed = summaries.Sum(s => s.Failed + s.Errored);
        var skipped = summaries.Sum(s => s.Skipped);
        cancelled |= summaries.Exists(s => s.WasCancelled);
        var duration = TestDisplay.Duration(TimeSpan.FromTicks(summaries.Sum(s => s.Duration.Ticks)));
        Output.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"========== Test run {(cancelled ? "cancelled" : "finished")}: {total} Tests ({passed} Passed, {failed} Failed, {skipped} Skipped) run in {duration} =========="),
            failed > 0 || cancelled ? OutputKind.Failure : OutputKind.Success);

        StatusText = cancelled
            ? "Test run cancelled"
            : string.Create(CultureInfo.InvariantCulture, $"Test run finished: {passed} passed, {failed} failed, {skipped} skipped");
    }

    /// <summary>A loaded test session and the build it was loaded from.</summary>
    /// <param name="Session">The session.</param>
    /// <param name="Assemblies">The assemblies it was loaded from.</param>
    /// <param name="Packages">The package assemblies it was loaded with.</param>
    private sealed record LoadedTests(TestSession Session, List<CompiledAssembly> Assemblies, IReadOnlyList<string> Packages)
    {
        /// <summary>Whether a new build produced exactly these assemblies.</summary>
        public bool IsFor(List<CompiledAssembly> assemblies, IReadOnlyList<string> packages) =>
            Packages.SequenceEqual(packages, StringComparer.Ordinal)
            && Assemblies.Count == assemblies.Count
            && Assemblies.Zip(assemblies).All(pair => pair.First.Name == pair.Second.Name && pair.First.Image.AsSpan().SequenceEqual(pair.Second.Image));
    }
}
