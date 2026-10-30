using Avalonia.Headless.XUnit;
using TabStudio.App.Services.Ci;
using TabStudio.App.ViewModels.Actions;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// GitHub Actions in the IDE against <see cref="FakeGitHub"/>: the status bar's CI item, the Actions window's runs,
/// run details, re-runs, cancels and "Run workflow", CI's errors in the Error List, watched repositories and
/// notifications.
/// </summary>
public sealed class ActionsTests
{
    [Theory]
    [InlineData("dry-run", "Dry run")]
    [InlineData("node_version", "Node version")]
    [InlineData("releaseChannel", "Release channel")]
    [InlineData("version", "Version")]
    [InlineData("CI", "CI")]
    public void The_Run_workflow_form_labels_inputs_in_words(string name, string label)
    {
        // The form showed the raw names, "dry-run", in code font, and its switch said "true".
        Assert.Equal(label, DispatchInputViewModel.ToLabel(name));
    }

    private const string Repository = GitWorkspaceFixture.Repository;
    private const string Program = "src/HelloTablet/Program.cs";

    /// <summary>A workflow run by hand, with an input of each kind.</summary>
    internal const string ReleaseWorkflow = """
        name: Release
        on:
          workflow_dispatch:
            inputs:
              version:
                description: The version to publish, such as 1.4.0
                required: true
              channel:
                type: choice
                options: [stable, beta]
              dry-run:
                description: Build everything but publish nothing
                type: boolean
                default: true
              notes:
                description: Release notes
        jobs:
          publish:
            runs-on: ubuntu-latest
            steps:
              - run: echo publishing
        """;

    /// <summary>A workflow that runs on its triggers only.</summary>
    internal const string CiWorkflow = """
        name: CI
        on: [push, pull_request]
        jobs:
          build:
            runs-on: ubuntu-latest
            steps:
              - run: dotnet build
        """;

    [AvaloniaFact]
    public async Task The_status_bar_follows_the_commit_from_running_to_failed_and_a_notice_tells_when_the_run_ends()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var actions = fixture.Workspace.Actions;
        var github = fixture.GitHub;
        Assert.True(actions.IsCiVisible);

        var ci = github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        var run = github.AddRun(Repository, ci, github.Head(Repository, "main"), "main", "in_progress", null,
            new FakeJob { Name = "Build and test", Status = "in_progress", Conclusion = null },
            new FakeJob { Name = "Lint" });
        await actions.Monitor.RefreshAsync();

        Assert.Equal(CiState.Running, actions.CiState);
        Assert.Equal(RunVisual.Running, actions.CiVisual);
        Assert.Equal("1/2", actions.CiText);
        Assert.Equal("1 of 2 checks done", actions.ChecksSummary);
        Assert.Contains("Build and test: in progress", actions.CiToolTip, StringComparison.Ordinal);
        Assert.Null(actions.Notice);

        github.Change(() =>
        {
            run.Status = "completed";
            run.Conclusion = "failure";
            run.Jobs[0].Status = "completed";
            run.Jobs[0].Conclusion = "failure";
        });
        await actions.Monitor.RefreshAsync();

        Assert.Equal(CiState.Failed, actions.CiState);
        Assert.Equal(RunVisual.Failed, actions.CiVisual);
        Assert.Equal("1 failed", actions.CiText);
        Assert.Equal("1 of 2 checks failed", actions.ChecksSummary);
        Assert.Equal(("Build and test", RunVisual.Failed), (actions.Checks[0].Name, actions.Checks[0].Visual));
        var notice = Assert.IsType<RunNoticeViewModel>(actions.Notice);
        Assert.Equal("CI #1 failed", notice.Title);

        await actions.OpenNoticeCommand.ExecuteAsync(null);

        Assert.True(fixture.Workspace.IsActionsSelected);
        Assert.Equal<long?>(run.Id, actions.RunDetails?.Run.Id);
        Assert.Null(actions.Notice);
    }

    [AvaloniaFact]
    public async Task The_window_lists_runs_opens_one_and_re_runs_and_cancels_it_on_GitHub()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var actions = fixture.Workspace.Actions;
        var github = fixture.GitHub;
        var head = github.Head(Repository, "main");
        var ci = github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        github.AddRun(Repository, ci, head, "main", "completed", "success", new FakeJob { Name = "Build and test" });
        var failed = github.AddRun(Repository, ci, head, "main", "completed", "failure", FailedJob(), new FakeJob { Name = "Lint" });

        fixture.Workspace.ShowActionsCommand.Execute(null);
        Assert.True(actions.IsShown);
        await UiTestHelpers.WaitUntilAsync(() => actions.Runs.Count == 2 && actions.WorkflowFilters.Count == 2, because: "the runs and workflows to be listed");

        var item = actions.Runs[0];
        Assert.Equal((failed.Id, "CI #2", "push · main", RunVisual.Failed), (item.Run.Id, item.Workflow, item.Trigger, item.Visual));
        Assert.Equal(("main", "main"), (actions.BranchFilter?.Label, actions.BranchFilter?.Branch));
        Assert.Equal("All branches", actions.BranchFilters[^1].Label);
        Assert.Equal("CI", actions.WorkflowFilters[1].Label);

        await actions.OpenRunCommand.ExecuteAsync(item);
        var details = Assert.IsType<RunDetailsViewModel>(actions.RunDetails);
        Assert.Equal("CI #2", details.Header);
        Assert.Equal(2, details.Jobs.Count);
        var build = details.Jobs.Single(j => j.Name == "Build and test");
        Assert.True(build.IsExpanded);
        Assert.Equal(new[] { "Set up job", "Build", "Test" }, build.Steps.Select(s => s.Name));
        Assert.Equal(RunVisual.Failed, build.Steps[1].Visual);
        Assert.True(details.HasFailedJobs);
        Assert.True(details.RerunFailedJobsCommand.CanExecute(null));
        Assert.False(details.CancelCommand.CanExecute(null));

        await details.RerunFailedJobsCommand.ExecuteAsync(null);

        Assert.Null(details.ErrorText);
        Assert.Equal($"rerun-failed-jobs {failed.Id}", Assert.Single(github.Actions(Repository).Calls));
        Assert.Equal((2, GitHubRunStatus.Queued), (details.Run.Attempt, details.Run.Status));
        Assert.Equal("CI #2 · attempt 2", details.Header);
        Assert.True(actions.Monitor.HasRunsInProgress);
        Assert.True(details.CancelCommand.CanExecute(null));

        var cancelling = details.CancelCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.Prompt.IsOpen, because: "the confirmation");
        fixture.Workspace.Prompt.ConfirmCommand.Execute(null);
        await cancelling;

        Assert.Equal($"cancel {failed.Id}", github.Actions(Repository).Calls[^1]);
        Assert.Equal((GitHubRunStatus.Completed, GitHubConclusion.Cancelled), (details.Run.Status, details.Run.Conclusion));
        Assert.Contains("Cancelling", details.InfoText, StringComparison.Ordinal);

        await details.OpenOnGitHubCommand.ExecuteAsync(null);
        Assert.Equal(details.Run.WebUri, fixture.Browser.Opened[^1]);
        details.BackCommand.Execute(null);
        Assert.Null(actions.RunDetails);
    }

    [AvaloniaFact]
    public async Task Run_workflow_builds_its_form_from_the_dispatch_inputs_and_posts_them()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var actions = fixture.Workspace.Actions;
        var github = fixture.GitHub;
        github.Push(Repository, "main", "Add the workflows", new Dictionary<string, string?>
        {
            [".github/workflows/release.yml"] = ReleaseWorkflow,
            [".github/workflows/ci.yml"] = CiWorkflow,
        });
        await github.CreateClient().CreateBranchAsync(FakeGitHub.Login, Repository, "release/1.4", github.Head(Repository, "main"));
        var release = github.AddWorkflow(Repository, "Release", ".github/workflows/release.yml");
        github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");

        fixture.Workspace.ShowActionsCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => actions.Workflows.Count == 2 && actions.Workflows.All(w => w.Definition is not null), because: "the workflows and their files");
        var workflow = actions.Workflows.Single(w => w.Name == "Release");
        Assert.True(workflow.CanDispatch);
        Assert.Equal("Runs on its triggers only", actions.Workflows.Single(w => w.Name == "CI").Note);

        await actions.OpenDispatchFormCommand.ExecuteAsync(workflow);

        var form = Assert.IsType<DispatchFormViewModel>(actions.DispatchForm);
        Assert.Equal("Run Release", form.Title);
        Assert.Equal("main", form.Branch);
        Assert.Equal(new[] { "main", "release/1.4" }, form.Branches);
        Assert.Equal(new[] { "version", "channel", "dry-run", "notes" }, form.Inputs.Select(i => i.Name));
        var version = form.Inputs[0];
        var channel = form.Inputs[1];
        var dryRun = form.Inputs[2];
        Assert.True(version.IsText && version.IsRequired);
        Assert.True(channel.IsChoice);
        Assert.Equal(new[] { "stable", "beta" }, channel.Options);
        Assert.Equal("stable", channel.SelectedOption);
        Assert.True(dryRun.IsBoolean && dryRun.IsOn);

        await form.RunCommand.ExecuteAsync(null);
        Assert.Equal("version is required.", version.ErrorText);
        Assert.Empty(github.Actions(Repository).Dispatches);

        version.Text = "1.4.0";
        channel.SelectedOption = "beta";
        dryRun.IsOn = false;
        await form.RunCommand.ExecuteAsync(null);

        var body = Assert.Single(github.Actions(Repository).Dispatches);
        Assert.Equal("main", body["ref"]!.GetValue<string>());
        var inputs = body["inputs"]!.AsObject();
        Assert.Equal(new[] { "channel", "dry-run", "version" }, inputs.Select(p => p.Key).Order(StringComparer.Ordinal));
        Assert.Equal("1.4.0", inputs["version"]!.GetValue<string>());
        Assert.Equal("beta", inputs["channel"]!.GetValue<string>());
        Assert.Equal("false", inputs["dry-run"]!.GetValue<string>());
        Assert.Null(actions.DispatchForm);
        Assert.Contains("Started Release on main", actions.InfoText, StringComparison.Ordinal);
        Assert.True(actions.Monitor.HasRunsInProgress);
        Assert.Contains(actions.Runs, r => r.Run.Event == "workflow_dispatch" && r.Run.WorkflowId == release.Id);
    }

    [AvaloniaFact]
    public async Task CI_errors_are_listed_with_their_check_offer_Fix_with_Agent_and_follow_the_commit()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var workspace = fixture.Workspace;
        var errors = workspace.ErrorList;
        var github = fixture.GitHub;
        var ci = github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        var head = github.Head(Repository, "main");
        var build = FailedBuild(Program, 16, "'Greeter' does not contain a definition for 'Greet'");
        build.Annotations.Add(new FakeAnnotation(Program, 30, "warning", "The behavior of 'int.ToString()' could vary based on the current user's locale settings. (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1305)"));
        build.Annotations.Add(new FakeAnnotation(".github", 79, "failure", "Process completed with exit code 1."));
        github.AddRun(Repository, ci, head, "main", "completed", "failure", build);

        await workspace.Actions.Monitor.RefreshAsync();
        await UiTestHelpers.WaitUntilAsync(() => errors.HasCiDiagnostics, because: "CI's errors to reach the Error List");

        Assert.Equal("CI (2)", errors.CiLabel);
        var error = errors.Items.Single(i => i.IsFromCi && i.IsError);
        Assert.Equal(("Program.cs", "16", "HelloTablet"), (error.File, error.Line, error.Project));
        Assert.Equal($"Build and test · {head[..7]}", error.CiSource);
        Assert.Equal(fixture.PathOf(Program), error.Diagnostic.Location?.FilePath);
        Assert.Equal("CA1305", errors.Items.Single(i => i.IsFromCi && i.IsWarning).Code);
        Assert.DoesNotContain(errors.Items, i => i.Description.Contains("exit code", StringComparison.Ordinal));

        Assert.True(error.CanFixWithAgent);
        errors.FixWithAgentCommand.Execute(error);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Agent.InputText.Contains("CI reported it", StringComparison.Ordinal), because: "the request for the agent");
        Assert.Contains("'Greeter' does not contain a definition for 'Greet'", workspace.Agent.InputText, StringComparison.Ordinal);
        Assert.Contains($"the check \"Build and test\" on commit {head[..7]}", workspace.Agent.InputText, StringComparison.Ordinal);

        errors.ToggleCiCommand.Execute(null);
        Assert.DoesNotContain(errors.Items, i => i.IsFromCi);
        errors.ToggleCiCommand.Execute(null);
        Assert.Equal(2, errors.Items.Count(i => i.IsFromCi));

        // A new commit: its CI has not reported yet, so the previous commit's errors stay, labelled with that commit.
        var program = await File.ReadAllTextAsync(fixture.PathOf(Program));
        await fixture.WriteAsync(Program, program.Replace("greeter.Greet()", "greeter.Greet() + \"!\"", StringComparison.Ordinal));
        fixture.Git.CommitMessage = "Greet with feeling";
        await fixture.Git.CommitCommand.ExecuteAsync(null);
        var next = github.Head(Repository, "main");
        await UiTestHelpers.WaitUntilAsync(() => workspace.Actions.Monitor.Target?.Sha == next, because: "CI to follow the new commit");
        await workspace.Actions.Monitor.RefreshAsync();
        Assert.All(errors.Items.Where(i => i.IsFromCi), i => Assert.Equal($"Build and test · {head[..7]}", i.CiSource));

        var nextRun = github.AddRun(Repository, ci, next, "main", "completed", "failure", FailedBuild(Program, 12, "Something else is wrong"));
        await workspace.Actions.Monitor.RefreshAsync();
        await UiTestHelpers.WaitUntilAsync(() => errors.Items.Any(i => i.CiSource == $"Build and test · {next[..7]}"), because: "the new commit's errors");
        Assert.Single(errors.Items, i => i.IsFromCi);

        github.Change(() =>
        {
            nextRun.Conclusion = "success";
            nextRun.Jobs[0].Conclusion = "success";
        });
        await workspace.Actions.Monitor.RefreshAsync();

        Assert.False(errors.HasCiDiagnostics);
        Assert.DoesNotContain(errors.Items, i => i.IsFromCi);
    }

    [AvaloniaFact]
    public async Task A_watched_repository_shows_its_latest_run_and_opens_its_runs()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var actions = fixture.Workspace.Actions;
        var github = fixture.GitHub;
        github.AddRepository("platform", new Dictionary<string, string> { ["README.md"] = "# Platform\n" }, defaultBranch: "trunk");
        var deploy = github.AddWorkflow("platform", "Deploy", ".github/workflows/deploy.yml");
        var trunk = github.Head("platform", "trunk");
        github.AddRun("platform", deploy, trunk, "trunk", "completed", "success", new FakeJob { Name = "Deploy" });
        var latest = github.AddRun("platform", deploy, trunk, "trunk", "in_progress", null, new FakeJob { Name = "Deploy", Status = "in_progress", Conclusion = null });
        fixture.Workspace.ShowActionsCommand.Execute(null);

        actions.WatchText = "https://github.com/tester/platform";
        await actions.AddWatchedCommand.ExecuteAsync(null);

        Assert.Null(actions.WatchErrorText);
        var watched = Assert.Single(actions.Watched);
        Assert.Equal("tester/platform", watched.FullName);
        Assert.Equal(((long?)latest.Id, RunVisual.Running), (watched.LatestRun?.Id, watched.Visual));
        Assert.StartsWith("Deploy #2 · in progress", watched.Summary, StringComparison.Ordinal);
        Assert.Equal(new[] { "tester/platform" }, new ActionsSettingsStore(fixture.Paths).Load().WatchedRepositories);

        actions.WatchText = "tester/hello";
        await actions.AddWatchedCommand.ExecuteAsync(null);
        Assert.Contains("this workspace's repository", actions.WatchErrorText, StringComparison.Ordinal);

        await actions.OpenWatchedCommand.ExecuteAsync(watched);

        Assert.True(actions.IsWatchedScope);
        Assert.Equal("tester/platform", actions.ScopeName);
        Assert.Equal("trunk (default)", actions.BranchFilter?.Label);
        await UiTestHelpers.WaitUntilAsync(() => actions.Runs.Count == 2, because: "the watched repository's runs");
        Assert.Equal(latest.Id, actions.Runs[0].Run.Id);

        actions.BackToWorkspaceCommand.Execute(null);
        Assert.True(actions.IsWorkspaceScope);
        actions.RemoveWatchedCommand.Execute(watched);
        Assert.Empty(actions.Watched);
        Assert.Empty(new ActionsSettingsStore(fixture.Paths).Load().WatchedRepositories);
    }

    [AvaloniaFact]
    public async Task A_run_that_finishes_in_the_background_shows_a_system_notification_that_opens_it()
    {
        var notifications = new FakeNotifications();
        var activity = new FakeAppActivity();
        using var fixture = new GitWorkspaceFixture(notifications, activity);
        await fixture.OpenLinkedSampleAsync();
        var actions = fixture.Workspace.Actions;
        var github = fixture.GitHub;
        Assert.True(actions.AreSystemNotificationsSupported);
        Assert.False(actions.UseSystemNotifications);
        Assert.False(actions.Monitor.KeepsPollingInBackground);

        actions.UseSystemNotifications = true;
        await UiTestHelpers.WaitUntilAsync(() => actions.UseSystemNotifications, because: "Android's permission");

        Assert.Equal(1, notifications.PermissionRequests);
        Assert.True(actions.Monitor.KeepsPollingInBackground);
        Assert.True(new ActionsSettingsStore(fixture.Paths).Load().UseSystemNotifications);

        var ci = github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        var head = github.Head(Repository, "main");
        var inForeground = github.AddRun(Repository, ci, head, "main", "in_progress", null, new FakeJob { Name = "Build", Status = "in_progress", Conclusion = null });
        var inBackground = github.AddRun(Repository, ci, head, "main", "in_progress", null, new FakeJob { Name = "Build", Status = "in_progress", Conclusion = null });
        await actions.Monitor.RefreshAsync();

        github.Change(() => Finish(inForeground, "success"));
        await actions.Monitor.RefreshAsync();
        Assert.Equal("CI #1 succeeded", actions.Notice?.Title);
        Assert.Empty(notifications.Shown);

        activity.Set(inBackground: true);
        github.Change(() => Finish(inBackground, "failure"));
        await actions.Monitor.RefreshAsync();

        var shown = Assert.Single(notifications.Shown);
        Assert.Equal(("CI #2 failed", true), (shown.Title, shown.IsFailure));
        Assert.Equal($"run:{FakeGitHub.Login}/{Repository}/{inBackground.Id}", shown.Tag);

        activity.Set(inBackground: false);
        notifications.Tap(shown);
        await UiTestHelpers.WaitUntilAsync(() => actions.RunDetails?.Run.Id == inBackground.Id, because: "the notification to open its run");
        Assert.True(fixture.Workspace.IsActionsSelected);
    }

    /// <summary>A failed "Build and test" job with steps, as GitHub reports one.</summary>
    private static FakeJob FailedJob()
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-3);
        return new FakeJob
        {
            Name = "Build and test",
            Conclusion = "failure",
            StartedAt = start,
            CompletedAt = start.AddSeconds(95),
            Steps =
            {
                new FakeStep(1, "Set up job", "completed", "success", start, start.AddSeconds(2)),
                new FakeStep(2, "Build", "completed", "failure", start.AddSeconds(2), start.AddSeconds(95)),
                new FakeStep(3, "Test", "completed", "skipped", null, null),
            },
        };
    }

    /// <summary>A failed "Build and test" job with one error annotation.</summary>
    private static FakeJob FailedBuild(string path, int line, string message)
    {
        var job = new FakeJob { Name = "Build and test", Conclusion = "failure" };
        job.Annotations.Add(new FakeAnnotation(path, line, "failure", message));
        return job;
    }

    private static void Finish(FakeRun run, string conclusion)
    {
        run.Status = "completed";
        run.Conclusion = conclusion;
        foreach (var job in run.Jobs)
        {
            job.Status = "completed";
            job.Conclusion = conclusion;
        }
    }
}
