using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Services.Ci;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.App.Views.Actions;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Screenshots of GitHub Actions in the IDE: the status bar's CI item while CI runs, fails and passes, the Actions
/// window's runs and a run's details, a job's log at its first error, CI's errors in the Error List, and the
/// "Run workflow" form.
/// </summary>
public sealed class ActionsScreenshotTests : IDisposable
{
    private const string Repository = "hello";
    private const string Program = "src/HelloTablet/Program.cs";
    private const string E = "\u001b";

    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();
    private readonly AppPaths _paths;
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public ActionsScreenshotTests()
    {
        _paths = new AppPaths(_temp.Path);
        _services = AppServices.Create(_paths, services => services.AddSingleton(_github.CreateAccount(_paths)));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Status_bar_while_CI_runs_then_fails_then_passes()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var actions = workspace.Actions;
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Actions);
        var head = _github.Head(Repository, "main");
        var ci = _github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        var run = _github.AddRun(Repository, ci, head, "main", "in_progress", null,
            new FakeJob { Name = "Build and test (libraries)" },
            new FakeJob { Name = "Build and test (the IDE)", Status = "in_progress", Conclusion = null },
            new FakeJob { Name = "Build Android APK", Status = "in_progress", Conclusion = null },
            new FakeJob { Name = "Run on an Android emulator", Status = "queued", Conclusion = null, StartedAt = null });
        _github.Change(() => _github.Actions(Repository).Statuses.Add(new FakeStatus(head, "codecov/project", "success", "87.2% of lines covered")));
        await actions.Monitor.RefreshAsync();
        await UiTestHelpers.WaitUntilAsync(() => actions.Runs.Count == 1, because: "the run");

        UiTestHelpers.SaveScreenshot(window, "actions-01-status-running");

        var item = window.GetVisualDescendants().OfType<CiStatusBarItem>().Single();
        Assert.True(item.IsEffectivelyVisible);
        Assert.Equal(("2/5", CiState.Running), (actions.CiText, actions.CiState));

        _github.Change(() =>
        {
            Finish(run.Jobs[1], "failure");
            Finish(run.Jobs[2], "success");
            Finish(run.Jobs[3], "skipped");
            run.Status = "completed";
            run.Conclusion = "failure";
        });
        await actions.Monitor.RefreshAsync();
        await UiTestHelpers.WaitUntilAsync(() => actions.HasNotice, because: "the notice of the finished run");

        UiTestHelpers.SaveScreenshot(window, "actions-02-status-failed");

        Assert.Equal("1 failed", actions.CiText);
        Assert.True(window.GetVisualDescendants().OfType<RunNoticeView>().Single().IsEffectivelyVisible);

        actions.DismissNoticeCommand.Execute(null);
        _github.Change(() =>
        {
            run.Attempt = 2;
            run.Conclusion = "success";
            foreach (var job in run.Jobs)
            {
                Finish(job, "success");
            }
        });
        await actions.Monitor.RefreshAsync();
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "actions-03-status-passed");

        Assert.Equal(("Passed", CiState.Passed), (actions.CiText, actions.CiState));
    }

    [AvaloniaFact]
    public async Task Actions_window_with_runs_and_a_run_s_details()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var actions = workspace.Actions;
        var head = _github.Head(Repository, "main");
        var ci = _github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        var release = _github.AddWorkflow(Repository, "Release", ".github/workflows/release.yml");
        _github.Push(Repository, "main", "Add the workflows", new Dictionary<string, string?>
        {
            [".github/workflows/ci.yml"] = ActionsTests.CiWorkflow,
            [".github/workflows/release.yml"] = ActionsTests.ReleaseWorkflow,
        });
        // Earlier commits' runs, this commit's failed run, and a run of a commit someone pushed since.
        Aged(_github.AddRun(Repository, ci, "1f0a9c3e5b7d2468ace013579bdf2468ace01357", "main", "completed", "success", new FakeJob { Name = "Build and test" }), "Add the languages table", hours: 26);
        Aged(_github.AddRun(Repository, release, "2e1b8d4f6a0c3579bdf124680ace13579bdf1246", "main", "completed", "success", new FakeJob { Name = "Publish" }), "Release 1.3.0", hours: 5, "workflow_dispatch");
        var failed = _github.AddRun(Repository, ci, head, "main", "completed", "failure", BuildJob(), new FakeJob { Name = "Lint" });
        Aged(failed, "Greet in the evening too", hours: 0);
        _github.AddRun(Repository, ci, "3d2c7e5a9b1f4680ace235791bdf3579ace24680", "main", "in_progress", null,
            new FakeJob { Name = "Build and test", Status = "in_progress", Conclusion = null, StartedAt = DateTimeOffset.UtcNow.AddSeconds(-40) }).Title = "Say good night";
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Actions);
        await UiTestHelpers.WaitUntilAsync(() => actions.Runs.Count == 4 && actions.Workflows.Count == 2 && actions.Workflows.All(w => w.Definition is not null), because: "the runs and workflows");

        UiTestHelpers.SaveScreenshot(window, "actions-04-runs");

        Assert.True(window.GetVisualDescendants().OfType<ActionsView>().Single().IsEffectivelyVisible);

        await actions.OpenRunCommand.ExecuteAsync(actions.Runs.Single(r => r.Run.Id == failed.Id));
        await UiTestHelpers.WaitUntilAsync(() => window.GetVisualDescendants().OfType<RunDetailsView>().Any(v => v.IsEffectivelyVisible), because: "the run's details");

        UiTestHelpers.SaveScreenshot(window, "actions-05-run-details");

        Assert.Equal(2, actions.RunDetails?.Jobs.Count);
    }

    [AvaloniaFact]
    public async Task A_job_log_opens_at_its_first_error_and_F8_moves_between_errors()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var actions = workspace.Actions;
        var ci = _github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        _github.AddRun(Repository, ci, _github.Head(Repository, "main"), "main", "completed", "failure", BuildJob());
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Actions);
        await UiTestHelpers.WaitUntilAsync(() => actions.Runs.Count == 1, because: "the run");
        await actions.OpenRunCommand.ExecuteAsync(actions.Runs[0]);
        await actions.RunDetails!.Jobs.Single(j => j.Name == "Build and test").OpenLogCommand.ExecuteAsync(null);

        var document = workspace.ActiveDocument!;
        Assert.True(document.IsCiLog);
        var log = document.CiLog!;
        await UiTestHelpers.WaitUntilAsync(() => log.HasLog && window.GetVisualDescendants().OfType<CiLogView>().Any(v => v.IsEffectivelyVisible), because: "the log tab");
        var view = window.GetVisualDescendants().OfType<CiLogView>().Single(v => v.IsEffectivelyVisible);
        var readable = log.Log!;
        var errors = readable.ErrorLines;
        Assert.Equal(2, errors.Count);
        await UiTestHelpers.WaitUntilAsync(() => view.Editor.TextArea.Caret.Line == errors[0], because: "the log to open at its first error");

        UiTestHelpers.SaveScreenshot(window, "actions-06-log");

        var text = view.Editor.Document.Text;
        Assert.DoesNotContain("##[", text, StringComparison.Ordinal);
        Assert.DoesNotContain(E, text, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-10-25T", text, StringComparison.Ordinal);
        Assert.Contains("error CS1061", view.Editor.Document.GetText(view.Editor.Document.GetLineByNumber(errors[0])), StringComparison.Ordinal);
        Assert.Equal(CiLogLineKind.Error, readable.KindOf(errors[0]));
        var build = readable.Steps.Single(s => s.Name == "Build");
        var setUp = readable.Steps.Single(s => s.Name == "Set up job");
        Assert.All(FoldsAt(view, setUp.Line), f => Assert.True(f.IsFolded));
        Assert.All(FoldsAt(view, build.Line), f => Assert.False(f.IsFolded));
        Assert.Equal("2 errors", log.ErrorCountText);

        view.Editor.TextArea.Focus();
        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.F8, RawInputModifiers.None);
        Assert.Equal(errors[1], view.Editor.TextArea.Caret.Line);

        window.KeyPressQwerty(PhysicalKey.F8, RawInputModifiers.Shift);
        window.KeyReleaseQwerty(PhysicalKey.F8, RawInputModifiers.Shift);
        Assert.Equal(errors[0], view.Editor.TextArea.Caret.Line);

        await log.ToggleTimestampsCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => log.Log?.PrefixLength > 0, because: "the timestamps");
        Assert.Matches(@"^\d\d:\d\d:\d\d\.\d{3}  ", view.Editor.Document.GetText(view.Editor.Document.GetLineByNumber(log.Log!.ErrorLines[0])));
    }

    [AvaloniaFact]
    public async Task Error_List_with_the_errors_CI_reported()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var ci = _github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        var job = new FakeJob { Name = "Build and test", Conclusion = "failure" };
        job.Annotations.Add(new FakeAnnotation(Program, 16, "failure", "'Greeter' does not contain a definition for 'Greet' and no accessible extension method 'Greet' accepting a first argument of type 'Greeter' could be found"));
        job.Annotations.Add(new FakeAnnotation(Program, 30, "warning", "The behavior of 'int.ToString()' could vary based on the current user's locale settings. (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1305)"));
        job.Annotations.Add(new FakeAnnotation(".github", 79, "failure", "Process completed with exit code 1."));
        _github.AddRun(Repository, ci, _github.Head(Repository, "main"), "main", "completed", "failure", job);
        workspace.SelectPanelCommand.Execute(BottomPanel.ErrorList);
        await workspace.Actions.Monitor.RefreshAsync();
        await UiTestHelpers.WaitUntilAsync(() => workspace.ErrorList.HasCiDiagnostics, because: "CI's errors");

        UiTestHelpers.SaveScreenshot(window, "actions-07-error-list");

        var list = window.GetVisualDescendants().OfType<ErrorListView>().Single();
        Assert.True(list.FindControl<Button>("CiFilterButton")?.IsEffectivelyVisible);
        Assert.Equal(2, workspace.ErrorList.Items.Count(i => i.IsFromCi));
    }

    [AvaloniaFact]
    public async Task Run_workflow_form_built_from_the_dispatch_inputs()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var actions = workspace.Actions;
        _github.Push(Repository, "main", "Add the release workflow", new Dictionary<string, string?> { [".github/workflows/release.yml"] = ActionsTests.ReleaseWorkflow });
        await _github.CreateClient().CreateBranchAsync(FakeGitHub.Login, Repository, "release/1.4", _github.Head(Repository, "main"));
        _github.AddWorkflow(Repository, "Release", ".github/workflows/release.yml");
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Actions);
        await UiTestHelpers.WaitUntilAsync(() => actions.Workflows.Count == 1 && actions.Workflows[0].CanDispatch, because: "the workflow's file");

        await actions.OpenDispatchFormCommand.ExecuteAsync(actions.Workflows[0]);
        var form = actions.DispatchForm!;
        form.Inputs[0].Text = "1.4.0";
        await UiTestHelpers.WaitUntilAsync(() => window.GetVisualDescendants().OfType<ActionsDialogsView>().Any(v => v.IsEffectivelyVisible), because: "the form");

        UiTestHelpers.SaveScreenshot(window, "actions-08-dispatch-form");

        Assert.Equal(4, form.Inputs.Count);
        Assert.Equal(2, form.Branches.Count);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _github.Dispose();
        _temp.Dispose();
    }

    /// <summary>A failed "Build and test" job with its steps and a log like dotnet's on GitHub's runners.</summary>
    private static FakeJob BuildJob()
    {
        var start = new DateTimeOffset(2026, 10, 25, 8, 0, 1, TimeSpan.Zero);
        return new FakeJob
        {
            Name = "Build and test",
            Conclusion = "failure",
            StartedAt = start,
            CompletedAt = start.AddSeconds(33),
            Log = BuildLog,
            Steps =
            {
                new FakeStep(1, "Set up job", "completed", "success", start, start.AddSeconds(2)),
                new FakeStep(2, "Run actions/checkout@v4", "completed", "success", start.AddSeconds(2), start.AddSeconds(8)),
                new FakeStep(3, "Build", "completed", "failure", start.AddSeconds(9), start.AddSeconds(30)),
                new FakeStep(4, "Test", "completed", "skipped", null, null),
                new FakeStep(5, "Complete job", "completed", "success", start.AddSeconds(31), start.AddSeconds(31)),
            },
        };
    }

    private const string BuildLog = $$"""
        2026-10-25T08:00:01.1000000Z Current runner version: '2.329.0'
        2026-10-25T08:00:01.2000000Z ##[group]Operating System
        2026-10-25T08:00:01.2000000Z Ubuntu
        2026-10-25T08:00:01.2000000Z 24.04.3
        2026-10-25T08:00:01.2000000Z LTS
        2026-10-25T08:00:01.3000000Z ##[endgroup]
        2026-10-25T08:00:01.4000000Z ##[group]Runner Image
        2026-10-25T08:00:01.4000000Z Image: ubuntu-24.04
        2026-10-25T08:00:01.4000000Z Version: 20261019.1.0
        2026-10-25T08:00:01.5000000Z ##[endgroup]
        2026-10-25T08:00:02.0000000Z Prepare all required actions
        2026-10-25T08:00:03.1000000Z ##[group]Run actions/checkout@v4
        2026-10-25T08:00:03.1000000Z with:
        2026-10-25T08:00:03.1000000Z   repository: tester/hello
        2026-10-25T08:00:03.1000000Z   fetch-depth: 1
        2026-10-25T08:00:03.2000000Z ##[endgroup]
        2026-10-25T08:00:04.0000000Z Syncing repository: tester/hello
        2026-10-25T08:00:06.0000000Z [command]/usr/bin/git checkout --progress --force -B main refs/remotes/origin/main
        2026-10-25T08:00:06.5000000Z Switched to a new branch 'main'
        2026-10-25T08:00:10.0000000Z ##[group]Run dotnet build --configuration Release
        2026-10-25T08:00:10.0000000Z {{E}}[36;1mdotnet build --configuration Release{{E}}[0m
        2026-10-25T08:00:10.0000000Z shell: /usr/bin/bash -e {0}
        2026-10-25T08:00:10.1000000Z ##[endgroup]
        2026-10-25T08:00:12.0000000Z   Determining projects to restore...
        2026-10-25T08:00:19.0000000Z   Restored /home/runner/work/hello/hello/src/Greetings/Greetings.csproj (in 1.1 sec).
        2026-10-25T08:00:19.2000000Z   Restored /home/runner/work/hello/hello/src/HelloTablet/HelloTablet.csproj (in 1.2 sec).
        2026-10-25T08:00:24.0000000Z   Greetings -> /home/runner/work/hello/hello/src/Greetings/bin/Release/net10.0/Greetings.dll
        2026-10-25T08:00:30.0000000Z /home/runner/work/hello/hello/src/HelloTablet/Program.cs(16,27): error CS1061: 'Greeter' does not contain a definition for 'Greet' [/home/runner/work/hello/hello/src/HelloTablet/HelloTablet.csproj]
        2026-10-25T08:00:30.1000000Z /home/runner/work/hello/hello/src/HelloTablet/Program.cs(30,50): warning CA1305: The behavior of 'int.ToString()' could vary based on the current user's locale settings [/home/runner/work/hello/hello/src/HelloTablet/HelloTablet.csproj]
        2026-10-25T08:00:30.5000000Z
        2026-10-25T08:00:30.5000000Z {{E}}[31;1mBuild FAILED.{{E}}[0m
        2026-10-25T08:00:30.6000000Z     1 Warning(s)
        2026-10-25T08:00:30.6000000Z     1 Error(s)
        2026-10-25T08:00:30.7000000Z
        2026-10-25T08:00:30.7000000Z Time Elapsed 00:00:20.31
        2026-10-25T08:00:31.0000000Z ##[error]Process completed with exit code 1.
        2026-10-25T08:00:32.0000000Z Cleaning up orphan processes
        """;

    private static void Aged(FakeRun run, string title, int hours, string trigger = "push")
    {
        run.Title = title;
        run.Event = trigger;
        run.CreatedAt = DateTimeOffset.UtcNow.AddHours(-hours).AddMinutes(-6);
        run.UpdatedAt = run.CreatedAt.AddMinutes(3).AddSeconds(12);
    }

    private static void Finish(FakeJob job, string conclusion)
    {
        job.Status = "completed";
        job.Conclusion = conclusion;
        job.StartedAt ??= DateTimeOffset.UtcNow.AddMinutes(-1);
        job.CompletedAt = conclusion == "skipped" ? job.StartedAt : DateTimeOffset.UtcNow;
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<AvaloniaEdit.Folding.FoldingSection> FoldsAt(CiLogView view, int line)
    {
        var offset = view.Editor.Document.GetLineByNumber(line).EndOffset;
        return view.Folding.GetFoldingsAt(offset);
    }

    private MainWindow Show(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Signs in, puts the sample on the fake GitHub and opens it from there, as the start screen does.</summary>
    private async Task<WorkspaceViewModel> OpenLinkedSampleAsync()
    {
        var account = _services.GetRequiredService<GitHubAccount>();
        await account.SignInAsync(FakeGitHub.Token);
        _github.AddRepository(Repository, await GitWorkspaceFixture.SampleFilesAsync(_paths));
        var folder = await _services.GetRequiredService<GitHubImporter>().ImportAsync(new GitHubRepository(FakeGitHub.Login, Repository, null));
        await _main.OpenWorkspaceAsync(Path.Combine(folder, "HelloTablet.slnx"));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.GitChanges.IsLinked && workspace.Actions.IsLinked, because: "the link to GitHub");
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");
        return workspace;
    }
}
