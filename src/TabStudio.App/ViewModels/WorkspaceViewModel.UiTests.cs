using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Swarm;
using TabStudio.App.Swarm.Recording;
using TabStudio.Core.Workspaces;
using TabStudio.TestRunner;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Recorded UI tests: Test &gt; Record UI Test and the Swarm window's recorder write them into the solution's test
/// project; the Test Explorer shows each one once for every screen of the swarm, the app preview and the virtual
/// devices, and running them replays them on those screens, with a picture of the screen when one fails. In CI they run
/// with <c>dotnet test</c>, on Avalonia's headless platform.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<IAppScreen, ScreenProfile> _uiTestScreens = [];
    private CancellationTokenSource? _uiTestScan;

    /// <summary>
    /// Whether recorded UI tests can replay while the app runs: they need it running on their screens, and take nothing
    /// from the tablet's console, so the Test Explorer runs them while the app runs; the other tests wait for it to stop.
    /// </summary>
    private bool CanReplayUiTestsNow => _swarmServices is not null && State is IdeState.Running or IdeState.Debugging && Tests.HasScreenTests;

    /// <summary>The tests a run could not run because the app runs: they run in-process once it stops.</summary>
    private void ReportTestsWaitingForTheApp(TestRunRequest rest)
    {
        if (rest.Projects.Count > 0)
        {
            Output.WriteLine("The other tests of the run did not run: they run once the app stops. Recorded UI tests replay while it runs.", OutputKind.Info);
        }
    }

    /// <summary>Test &gt; Record UI Test: opens the Swarm window's recorder and starts recording.</summary>
    [RelayCommand]
    private void RecordUiTest()
    {
        if (Swarm is not { } swarm)
        {
            return;
        }

        SelectSideBarPanel(SideBarPanel.Swarm);
        swarm.Recorder.Open(start: true);
    }

    /// <summary>Test &gt; Run UI Tests on Every Screen: replays every recorded UI test on every screen, in the Test Explorer.</summary>
    [RelayCommand]
    private async Task RunUiTestsOnEveryScreenAsync()
    {
        var selections = Tests.ScreenTestSelections();
        if (selections.Count == 0)
        {
            StatusText = "There are no recorded UI tests yet: record one in the Swarm window";
            return;
        }

        await Tests.RunAsync(new TestRunRequest("recorded UI tests on every screen", selections));
    }

    /// <summary>Starts following the screens and the workspace's test projects. Called once, by <see cref="InitializeSwarm"/>.</summary>
    private void InitializeUiTests(SwarmServices swarm)
    {
        swarm.Registry.Screens.CollectionChanged += OnScreensChangedForUiTests;
        PropertyChanged += OnPropertyChangedForUiTests;
        WatchUiTestScreens();
    }

    private void DisposeUiTests()
    {
        if (_swarmServices is { } swarm)
        {
            swarm.Registry.Screens.CollectionChanged -= OnScreensChangedForUiTests;
        }

        PropertyChanged -= OnPropertyChangedForUiTests;
        foreach (var screen in _uiTestScreens.Keys)
        {
            screen.Changed -= OnScreenChangedForUiTests;
        }

        _uiTestScreens.Clear();
        _uiTestScan?.Cancel();
        _uiTestScan?.Dispose();
        _uiTestScan = null;
    }

    private void OnPropertyChangedForUiTests(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Workspace))
        {
            ScanUiTests();
        }
    }

    private void OnScreensChangedForUiTests(object? sender, NotifyCollectionChangedEventArgs e)
    {
        WatchUiTestScreens();
        ScanUiTests();
    }

    private void OnScreenChangedForUiTests(object? sender, EventArgs e)
    {
        if (!_ui.CheckAccess())
        {
            _ui.Post(() => OnScreenChangedForUiTests(sender, e));
            return;
        }

        // A screen's name and size show in its rows; whether the app runs there does not.
        if (sender is IAppScreen screen && _uiTestScreens.TryGetValue(screen, out var shown) && shown != screen.Profile)
        {
            _uiTestScreens[screen] = screen.Profile;
            ScanUiTests();
        }
    }

    /// <summary>A saved test file: its recorded tests are read again.</summary>
    private void OnDocumentSavedForUiTests(DocumentViewModel document)
    {
        if (document.FileType.IsCSharp && Workspace?.FindProjectForFile(document.FilePath) is { IsTestProject: true })
        {
            ScanUiTests();
        }
    }

    private void WatchUiTestScreens()
    {
        var screens = _swarmServices?.Registry.Screens.ToList() ?? [];
        foreach (var gone in _uiTestScreens.Keys.Where(s => !screens.Contains(s)).ToList())
        {
            gone.Changed -= OnScreenChangedForUiTests;
            _uiTestScreens.Remove(gone);
        }

        foreach (var screen in screens.Where(s => _uiTestScreens.TryAdd(s, s.Profile)))
        {
            screen.Changed += OnScreenChangedForUiTests;
        }
    }

    /// <summary>Reads the recorded UI tests of every test project again, for the Test Explorer's rows.</summary>
    private void ScanUiTests()
    {
        if (_swarmServices is not { } swarm)
        {
            return;
        }

        _uiTestScan?.Cancel();
        _uiTestScan?.Dispose();
        _uiTestScan = new CancellationTokenSource();
        _ = ScanUiTestsAsync(TestProjectsOf(Workspace), [.. swarm.Registry.Screens], _uiTestScan.Token);
    }

    private async Task ScanUiTestsAsync(List<ProjectInfo> projects, List<IAppScreen> screens, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var project in projects)
            {
                var tests = await Task.Run(() => UiTestReader.ReadDirectory(project.DirectoryPath), cancellationToken);
                if (cancellationToken.IsCancellationRequested || _disposed)
                {
                    return;
                }

                Tests.SetScreenTests(project.FilePath, project.Name, tests, screens);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer scan replaced this one.
        }
    }

    /// <summary>The recorder's Save: writes the test into the app's test project, which is created when there is none.</summary>
    /// <returns>Why it could not be saved, or <see langword="null"/>.</returns>
    private async Task<string?> SaveUiTestAsync(UiTestRecording recording)
    {
        if (Workspace is not { } workspace)
        {
            return "Open the app's solution first.";
        }

        var app = new[] { _swarmProject, StartupProject }.FirstOrDefault(p => p is not null && IsAvaloniaApp(p))
            ?? workspace.Projects.FirstOrDefault(IsAvaloniaApp);
        if (app is null)
        {
            return "Recorded UI tests test an Avalonia app, and this solution has none.";
        }

        UiTestSaved saved;
        try
        {
            saved = await Task.Run(() => UiTestProjects.Save(workspace, app, recording));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            return $"The test could not be written: {ex.Message}";
        }

        var file = Path.GetRelativePath(workspace.RootDirectory, saved.TestFilePath);
        Output.WriteLine(
            saved.CreatedProject
                ? $"Recorded \"{recording.Name}\" into {file}, in the new test project {Path.GetFileNameWithoutExtension(saved.ProjectFilePath)}, which is now part of the solution."
                : $"Recorded \"{recording.Name}\" into {file}.",
            OutputKind.Success);
        // The test project may have new packages (xUnit, Avalonia's headless platform): restore them as a saved
        // project file would, so the editor and the Error List see them.
        await ReloadWorkspaceAsync();
        if (Workspace is { } reloaded)
        {
            StartPackageRestore(reloaded);
        }

        await OpenDocumentAsync(saved.TestFilePath);
        ScanUiTests();
        return null;
    }

    /// <summary>
    /// Replays the recorded UI tests a run selects on their screens, reporting each screen's result to the Test
    /// Explorer, and returns the rest of the run for the test runner.
    /// </summary>
    private async Task<TestRunRequest> RunUiTestsOnScreensAsync(TestRunRequest request)
    {
        var (rows, rest) = Tests.SplitScreenTests(request);
        if (rows.Count == 0 || _swarmServices is not { } swarm)
        {
            return request;
        }

        SelectSideBarPanel(SideBarPanel.TestExplorer);
        using var run = new CancellationTokenSource();
        _testRun = run;
        try
        {
            foreach (var project in rows.GroupBy(r => r.ProjectFilePath, StringComparer.Ordinal))
            {
                Tests.ReportQueued(project.Key, [.. project.Select(TestExplorerViewModel.CaseOf)]);
            }

            var screens = rows.Select(r => Tests.ScreenTestOf(r)?.Screen).Distinct().Count();
            Output.WriteLine(
                string.Create(CultureInfo.InvariantCulture, $"========== Replaying recorded UI tests on {screens} {(screens == 1 ? "screen" : "screens")} =========="),
                OutputKind.Info);

            // Every screen replays its tests one after another; the screens replay at the same time.
            var results = await Task.WhenAll(rows
                .GroupBy(r => Tests.ScreenTestOf(r)?.Screen)
                .Select(group => ReplayOnScreenAsync(swarm, [.. group], run.Token)));
            var all = results.SelectMany(r => r).ToList();
            foreach (var project in all.GroupBy(r => r.Project, StringComparer.Ordinal))
            {
                var summary = new TestRunSummary
                {
                    Total = project.Count(),
                    Results = [.. project.Select(r => r.Result)],
                    Duration = TimeSpan.FromTicks(project.Sum(r => r.Result.Duration.Ticks)),
                    WasCancelled = run.IsCancellationRequested,
                };
                Tests.ReportFinished(project.Key, summary);
            }

            var failed = all.Count(r => r.Result.Outcome == TestOutcome.Failed);
            Output.WriteLine(
                string.Create(CultureInfo.InvariantCulture, $"========== UI tests replayed: {all.Count(r => r.Result.Outcome == TestOutcome.Passed)} passed, {failed} failed, {all.Count(r => r.Result.Outcome == TestOutcome.Skipped)} skipped =========="),
                failed > 0 ? OutputKind.Failure : OutputKind.Success);
        }
        finally
        {
            _testRun = null;
        }

        return rest;
    }

    private async Task<List<(string Project, TestResult Result)>> ReplayOnScreenAsync(SwarmServices swarm, List<TestEntry> rows, CancellationToken cancellationToken)
    {
        List<(string, TestResult)> results = [];
        foreach (var row in rows)
        {
            if (cancellationToken.IsCancellationRequested || Tests.ScreenTestOf(row) is not { } target)
            {
                break;
            }

            var test = TestExplorerViewModel.CaseOf(row);
            Tests.ReportStarted(row.ProjectFilePath, test);
            UiTestReplayResult replay;
            if (target.Screen is not { } screen)
            {
                replay = new UiTestReplayResult(
                    UiTestReplayOutcome.Skipped,
                    "No screen runs the app. Run it on the swarm, in the app preview or on a virtual device, then run the test again; it replays on every one of them.",
                    string.Empty,
                    null,
                    TimeSpan.Zero);
            }
            else
            {
                try
                {
                    replay = await UiTestReplayer.ReplayAsync(target.Test, screen, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            var screenshot = replay.Screenshot is { } png ? await SaveScreenshotAsync(swarm, row, target.Screen, png) : null;
            var result = new TestResult
            {
                Test = test,
                Outcome = replay.Outcome switch
                {
                    UiTestReplayOutcome.Passed => TestOutcome.Passed,
                    UiTestReplayOutcome.Failed => TestOutcome.Failed,
                    _ => TestOutcome.Skipped,
                },
                Duration = replay.Duration,
                Message = screenshot is null ? replay.Message : $"{replay.Message}\nA picture of the screen is below, and in {screenshot}.",
                Output = replay.Output,
                FailureLocation = replay.Outcome == UiTestReplayOutcome.Failed ? row.Location : null,
            };
            Tests.SetScreenshot(row.ProjectFilePath, row.Id, screenshot);
            Tests.ReportFinished(row.ProjectFilePath, result);
            results.Add((row.ProjectFilePath, result));
        }

        return results;
    }

    private static async Task<string?> SaveScreenshotAsync(SwarmServices swarm, TestEntry row, IAppScreen? screen, byte[] png)
    {
        try
        {
            Directory.CreateDirectory(swarm.ScreenshotDirectory);
            var name = string.Concat($"{row.MethodName}-{screen?.Profile.Name}".Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-'));
            var path = Path.Combine(swarm.ScreenshotDirectory, string.Create(CultureInfo.InvariantCulture, $"{name}-{DateTime.Now:yyyyMMdd-HHmmss}.png"));
            await File.WriteAllBytesAsync(path, png);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
