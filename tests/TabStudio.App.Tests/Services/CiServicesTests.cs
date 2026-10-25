using TabStudio.App.Services.Ci;
using TabStudio.Core.Diagnostics;
using TabStudio.SourceControl.GitHub;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

/// <summary>The CI services without a UI: aggregation, the polling schedule, log parsing and Error List mapping.</summary>
public sealed class CiServicesTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Checks_and_statuses_add_up_to_one_state()
    {
        Assert.Equal(CiState.None, CiSummary.From("abc", [], null).State);
        Assert.Equal(CiState.None, CiSummary.From("abc", [], new GitHubCombinedStatus(GitHubStatusState.Pending, [])).State);
        Assert.Equal(CiState.Pending, CiSummary.From("abc", [Check("Build", GitHubRunStatus.Queued, null)], null).State);

        var running = CiSummary.From("abc", [Check("Build", GitHubRunStatus.InProgress, null), Check("Lint", GitHubRunStatus.Completed, GitHubConclusion.Failure), Check("Docs", GitHubRunStatus.Queued, null)], null);
        Assert.Equal(CiState.Running, running.State);
        Assert.Equal((3, 1, 1), (running.Total, running.Done, running.FailedCount));
        Assert.Equal("Lint", running.Checks[0].Name);

        var failed = CiSummary.From("abc", [Check("Build", GitHubRunStatus.Completed, GitHubConclusion.Success), Check("Lint", GitHubRunStatus.Completed, GitHubConclusion.TimedOut)], null);
        Assert.Equal(CiState.Failed, failed.State);

        var cancelled = CiSummary.From("abc", [Check("Build", GitHubRunStatus.Completed, GitHubConclusion.Cancelled), Check("Lint", GitHubRunStatus.Completed, GitHubConclusion.Skipped)], null);
        Assert.Equal(CiState.Cancelled, cancelled.State);

        var passed = CiSummary.From("abc", [Check("Build", GitHubRunStatus.Completed, GitHubConclusion.Success), Check("Old", GitHubRunStatus.Completed, GitHubConclusion.Stale)],
            new GitHubCombinedStatus(GitHubStatusState.Success, [new GitHubCommitStatus("ci/circleci", GitHubStatusState.Success, "Tests passed", null, Now)]));
        Assert.Equal(CiState.Passed, passed.State);
        Assert.Equal(2, passed.Total);
        Assert.Contains(passed.Checks, c => c.Kind == CiCheckKind.Status && c.Name == "ci/circleci");

        var external = CiSummary.From("abc", [], new GitHubCombinedStatus(GitHubStatusState.Failure, [new GitHubCommitStatus("jenkins", GitHubStatusState.Error, "Crashed", null, Now)]));
        Assert.Equal(CiState.Failed, external.State);
    }

    [Fact]
    public void Polling_is_often_while_running_rare_when_idle_and_waits_for_the_limit_to_reset()
    {
        Assert.Equal(CiPollSchedule.Active, CiPollSchedule.Next(isActive: true, isSignedIn: true, null, lastPollFailed: false, Now));
        Assert.Equal(CiPollSchedule.Idle, CiPollSchedule.Next(isActive: false, isSignedIn: true, null, lastPollFailed: false, Now));
        Assert.Equal(CiPollSchedule.ActiveSignedOut, CiPollSchedule.Next(isActive: true, isSignedIn: false, null, lastPollFailed: false, Now));
        Assert.Equal(CiPollSchedule.IdleSignedOut, CiPollSchedule.Next(isActive: false, isSignedIn: false, null, lastPollFailed: false, Now));
        Assert.Equal(CiPollSchedule.AfterError, CiPollSchedule.Next(isActive: true, isSignedIn: true, null, lastPollFailed: true, Now));
        Assert.Equal(CiPollSchedule.Active * 4, CiPollSchedule.Next(isActive: true, isSignedIn: true, new GitHubRateLimit(5000, 300, Now.AddMinutes(30)), lastPollFailed: false, Now));
        Assert.Equal(TimeSpan.FromMinutes(20) + TimeSpan.FromSeconds(5), CiPollSchedule.Next(isActive: true, isSignedIn: true, new GitHubRateLimit(5000, 0, Now.AddMinutes(20)), lastPollFailed: false, Now));
    }

    [Fact]
    public void A_log_folds_steps_and_groups_and_marks_its_errors()
    {
        const string E = "\u001b";
        const string Raw = $"""
            2026-10-25T08:00:10.1000000Z Current runner version: '2.329.0'
            2026-10-25T08:00:12.2000000Z ##[group]Run actions/checkout@v4
            2026-10-25T08:00:12.3000000Z with:
            2026-10-25T08:00:12.4000000Z ##[endgroup]
            2026-10-25T08:00:13.0000000Z Syncing repository: octocat/hello
            2026-10-25T08:00:20.5000000Z ##[group]Run dotnet build
            2026-10-25T08:00:20.6000000Z [command]/usr/bin/dotnet build
            2026-10-25T08:00:20.7000000Z ##[endgroup]
            2026-10-25T08:00:40.0000000Z src/App/Program.cs(3,19): error CS0103: The name 'x' does not exist in the current context [/home/runner/work/hello/src/App/App.csproj]
            2026-10-25T08:00:41.0000000Z {E}[32mBuild{E}[0m finished with {E}[1;31merrors{E}[0m.
            2026-10-25T08:00:41.5000000Z ##[warning]Node.js 20 is deprecated.
            2026-10-25T08:00:42.0000000Z ##[error]Process completed with exit code 1.
            """;
        var steps = new[]
        {
            new GitHubJobStep(1, "Set up job", GitHubRunStatus.Completed, GitHubConclusion.Success, At(8, 0, 10), At(8, 0, 12)),
            new GitHubJobStep(2, "Run actions/checkout@v4", GitHubRunStatus.Completed, GitHubConclusion.Success, At(8, 0, 12), At(8, 0, 20)),
            new GitHubJobStep(3, "Build", GitHubRunStatus.Completed, GitHubConclusion.Failure, At(8, 0, 20), At(8, 0, 42)),
            new GitHubJobStep(4, "Publish", GitHubRunStatus.Completed, GitHubConclusion.Skipped, null, null),
        };

        var log = CiLogParser.Parse(new GitHubJobLog(Raw, string.Empty, Raw.Length, 0), steps, showTimestamps: false, jobFailed: true);
        var lines = log.Text.Split('\n');

        Assert.Equal(new[] { 1, 2, 3 }, log.Steps.Select(s => s.Number));
        Assert.Equal("Set up job  ·  2s", lines[log.Steps[0].Line - 1]);
        Assert.Equal("Build  ·  22s  ·  failed", lines[log.Steps[2].Line - 1]);
        Assert.Equal(CiLogLineKind.Step, log.KindOf(log.Steps[2].Line));
        Assert.Equal("Run dotnet build", lines[log.Steps[2].Line]);
        Assert.Equal(CiLogLineKind.Group, log.KindOf(log.Steps[2].Line + 1));
        Assert.DoesNotContain(lines, l => l.Contains("##[", StringComparison.Ordinal) || l.Contains('\u001b', StringComparison.Ordinal));
        Assert.Contains("Error: Process completed with exit code 1.", lines);
        Assert.Contains("Warning: Node.js 20 is deprecated.", lines);
        Assert.Equal(CiLogLineKind.Command, log.KindOf(Array.IndexOf(lines, "/usr/bin/dotnet build") + 1));

        Assert.Equal(2, log.ErrorLines.Count);
        Assert.StartsWith("src/App/Program.cs(3,19): error CS0103", lines[log.FirstErrorLine!.Value - 1], StringComparison.Ordinal);
        Assert.Equal(CiLogLineKind.Warning, log.KindOf(Array.IndexOf(lines, "Warning: Node.js 20 is deprecated.") + 1));

        var buildStep = Assert.Single(log.Folds, f => f.IsStep && f.StartLine == log.Steps[2].Line);
        Assert.False(buildStep.StartsFolded);
        Assert.True(Assert.Single(log.Folds, f => f.IsStep && f.StartLine == log.Steps[1].Line).StartsFolded);
        Assert.All(log.Folds.Where(f => !f.IsStep), f => Assert.True(f.StartsFolded));
        Assert.Equal(log.Folds.OrderBy(f => f.StartLine).Select(f => f.StartLine), log.Folds.Select(f => f.StartLine));

        var colored = Array.IndexOf(lines, "Build finished with errors.") + 1;
        var spans = log.Colors[colored];
        Assert.Contains(spans, s => s is { Start: 0, Length: 5, Color: CiLogColor.Green });
        Assert.Contains(spans, s => s is { Color: CiLogColor.Red, IsBold: true, Length: 6 });

        var stamped = CiLogParser.Parse(new GitHubJobLog(Raw, string.Empty, Raw.Length, 0), steps, showTimestamps: true, jobFailed: true);
        Assert.Equal(14, stamped.PrefixLength);
        Assert.Matches(@"^\d\d:\d\d:\d\d\.\d{3}  Current runner version", stamped.Text.Split('\n')[1]);
    }

    [Fact]
    public void A_partial_log_says_how_much_is_left_out()
    {
        var log = CiLogParser.Parse(new GitHubJobLog("first\n", "last\n", 40L * 1024 * 1024, 39L * 1024 * 1024), [], showTimestamps: false, jobFailed: false);

        var lines = log.Text.Split('\n');
        Assert.Equal("first", lines[0]);
        Assert.Equal(CiLogLineKind.Omitted, log.KindOf(2));
        Assert.Contains("39.0 MB", lines[1], StringComparison.Ordinal);
        Assert.Equal("last", lines[2]);
    }

    [Fact]
    public void Annotations_map_to_files_of_the_workspace_below_the_repository_root()
    {
        using var temp = new TempDirectory();
        var workspace = Path.Combine(temp.Path, "app");
        Directory.CreateDirectory(Path.Combine(workspace, "src"));
        File.WriteAllText(Path.Combine(workspace, "src", "Program.cs"), "using System;\r\nConsole.WriteLine(x);\r\n");
        Directory.CreateDirectory(Path.Combine(temp.Path, "docs"));
        File.WriteAllText(Path.Combine(temp.Path, "docs", "Notes.cs"), "x\n");
        var check = new GitHubCheckRun(7, "Build and test", GitHubRunStatus.Completed, GitHubConclusion.Failure, Now, Now, null, new Uri("https://github.test/o/r/actions/runs/1/job/7"), null, null, 4, "github-actions");
        CiAnnotation Annotation(string path, int line, int? column, GitHubAnnotationLevel level, string message) =>
            new(check, new GitHubCheckAnnotation(path, line, line, column, null, level, null, message, null));

        var mapped = CiDiagnostics.Map(
            [
                Annotation("app/src/Program.cs", 2, 19, GitHubAnnotationLevel.Failure, "error CS0103: The name 'x' does not exist in the current context"),
                Annotation("app/src/Program.cs", 2, 19, GitHubAnnotationLevel.Failure, "error CS0103: The name 'x' does not exist in the current context"),
                Annotation("app/src/Program.cs", 1, null, GitHubAnnotationLevel.Warning, "Do not use it. (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1826)"),
                Annotation("docs/Notes.cs", 1, null, GitHubAnnotationLevel.Failure, "Outside the workspace"),
                Annotation(".github", 12, null, GitHubAnnotationLevel.Failure, "Process completed with exit code 1."),
                Annotation("app/src/Program.cs", 1, null, GitHubAnnotationLevel.Notice, "Just so you know"),
                Annotation("../escape.cs", 1, null, GitHubAnnotationLevel.Failure, "Not in the repository"),
            ],
            "0123456789abcdef",
            temp.Path,
            workspace,
            _ => "App");

        Assert.Equal(2, mapped.Count);
        var error = mapped[0];
        Assert.Equal(("CS0103", DiagnosticSeverityLevel.Error, "The name 'x' does not exist in the current context"), (error.Diagnostic.Id, error.Diagnostic.Severity, error.Diagnostic.Message));
        var location = error.Diagnostic.Location!;
        Assert.Equal(Path.Combine(workspace, "src", "Program.cs"), location.FilePath);
        Assert.Equal((2, 19, "using System;\r\n".Length + 18), (location.Line, location.Column, location.Start));
        Assert.Equal(("Build and test", "0123456", "App"), (error.CheckName, error.ShortSha, error.Diagnostic.ProjectName));
        Assert.Equal(("CA1826", DiagnosticSeverityLevel.Warning), (mapped[1].Diagnostic.Id, mapped[1].Diagnostic.Severity));
    }

    [Fact]
    public void Durations_times_and_events_read_as_on_GitHub()
    {
        Assert.Equal("45s", ActionsFormat.Duration(TimeSpan.FromSeconds(45)));
        Assert.Equal("3m 12s", ActionsFormat.Duration(TimeSpan.FromSeconds(192)));
        Assert.Equal("1h 4m", ActionsFormat.Duration(TimeSpan.FromMinutes(64)));
        Assert.Equal("just now", ActionsFormat.Ago(Now.AddSeconds(-20), Now));
        Assert.Equal("5 min ago", ActionsFormat.Ago(Now.AddMinutes(-5), Now));
        Assert.Equal("3 h ago", ActionsFormat.Ago(Now.AddHours(-3), Now));
        Assert.Equal("yesterday", ActionsFormat.Ago(Now.AddHours(-30), Now));
        Assert.Equal("manual", ActionsFormat.Event("workflow_dispatch"));
        Assert.Equal("pull request", ActionsFormat.Event("pull_request"));
        Assert.Equal("timed out", ActionsFormat.State(GitHubRunStatus.Completed, GitHubConclusion.TimedOut));
    }

    [Fact]
    public async Task The_monitor_reports_a_run_that_finishes_once_and_follows_re_runs()
    {
        using var github = new FakeGitHub();
        github.AddRepository("hello", new Dictionary<string, string> { ["README.md"] = "# Hello\n" });
        var head = github.Head("hello", "main");
        var ci = github.AddWorkflow("hello", "CI", ".github/workflows/ci.yml");
        var old = github.AddRun("hello", ci, head, "main", "completed", "success", new FakeJob { Name = "Build" });
        var running = github.AddRun("hello", ci, head, "main", "in_progress", null, new FakeJob { Name = "Build", Status = "in_progress", Conclusion = null });
        var client = github.CreateClient();
        using var monitor = new CiMonitor(() => client, new FakeAppActivity());
        var finished = new List<CiRunFinished>();
        monitor.RunFinished += (_, run) => finished.Add(run);

        monitor.SetTarget(new CiTarget(FakeGitHub.Login, "hello", "main", head));
        await monitor.RefreshAsync();
        Assert.Equal(CiState.Running, monitor.Summary!.State);
        Assert.Empty(finished);

        github.Change(() =>
        {
            running.Status = "completed";
            running.Conclusion = "failure";
            running.Jobs[0].Status = "completed";
            running.Jobs[0].Conclusion = "failure";
        });
        await monitor.RefreshAsync();
        await monitor.RefreshAsync();

        var reported = Assert.Single(finished);
        Assert.Equal((running.Id, false, GitHubConclusion.Failure), (reported.Run.Id, reported.StartedHere, reported.Run.Conclusion));
        Assert.Equal(CiState.Failed, monitor.Summary.State);

        var before = await client.GetWorkflowRunAsync(FakeGitHub.Login, "hello", old.Id);
        await client.RerunWorkflowAsync(FakeGitHub.Login, "hello", old.Id);
        monitor.Watch(FakeGitHub.Login, "hello", before, isRerun: true);
        await monitor.RefreshAsync();
        Assert.Single(finished);
        github.Change(() =>
        {
            old.Status = "completed";
            old.Conclusion = "success";
        });
        await monitor.RefreshAsync();

        Assert.Equal(2, finished.Count);
        Assert.True(finished[1].StartedHere);
        Assert.Equal(2, finished[1].Run.Attempt);
        Assert.False(monitor.HasRunsInProgress);
    }

    private static GitHubCheckRun Check(string name, GitHubRunStatus status, GitHubConclusion? conclusion) =>
        new(name.Length, name, status, conclusion, Now, null, null, null, null, null, 0, "github-actions");

    private static DateTimeOffset At(int hour, int minute, int second) => new(2026, 10, 25, hour, minute, second, TimeSpan.Zero);
}
