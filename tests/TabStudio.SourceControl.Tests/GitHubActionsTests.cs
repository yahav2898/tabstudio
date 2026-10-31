using System.Text;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl.Tests;

/// <summary>The checks, statuses and Actions parts of <see cref="GitHubClient"/> against <see cref="FakeGitHub"/>.</summary>
public sealed class GitHubActionsTests : IDisposable
{
    private const string Repository = "hello";

    private readonly FakeGitHub _github = new();
    private readonly GitHubClient _client;
    private readonly string _head;
    private readonly FakeWorkflow _ci;

    public GitHubActionsTests()
    {
        _github.AddRepository(Repository, new Dictionary<string, string> { ["README.md"] = "# Hello\n", ["src/App/Program.cs"] = "Console.WriteLine(x);\n" });
        _head = _github.Head(Repository, "main");
        _ci = _github.AddWorkflow(Repository, "CI", ".github/workflows/ci.yml");
        _client = _github.CreateClient();
    }

    [Fact]
    public async Task Runs_jobs_and_steps_are_read_with_their_states_and_times()
    {
        var build = new FakeJob { Name = "Build and test", Conclusion = "failure" };
        build.Steps.Add(new FakeStep(1, "Set up job", "completed", "success", At(8, 0, 10), At(8, 0, 12)));
        build.Steps.Add(new FakeStep(2, "Build", "completed", "failure", At(8, 0, 12), At(8, 2, 40)));
        build.Steps.Add(new FakeStep(3, "Publish", "completed", "skipped", null, null));
        var android = new FakeJob { Name = "Android", Status = "in_progress", Conclusion = null };
        var run = _github.AddRun(Repository, _ci, _head, "main", "in_progress", null, build, android);

        var page = await _client.GetWorkflowRunsAsync(FakeGitHub.Login, Repository, new GitHubRunQuery(Branch: "main", PerPage: 10));
        var listed = Assert.Single(page.Runs);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(run.Id, listed.Id);
        Assert.Equal(_ci.Id, listed.WorkflowId);
        Assert.Equal("CI", listed.WorkflowName);
        Assert.Equal("Initial commit", listed.Title);
        Assert.Equal(1, listed.RunNumber);
        Assert.Equal("push", listed.Event);
        Assert.Equal(GitHubRunStatus.InProgress, listed.Status);
        Assert.Null(listed.Conclusion);
        Assert.Equal("main", listed.HeadBranch);
        Assert.Equal(_head, listed.HeadSha);
        Assert.Equal(FakeGitHub.Login, listed.Actor);
        Assert.Contains($"/actions/runs/{run.Id}", listed.WebUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Contains(_github.Requests, r => r.EndsWith("/actions/runs?per_page=10&branch=main", StringComparison.Ordinal));

        var jobs = await _client.GetJobsAsync(FakeGitHub.Login, Repository, run.Id);
        Assert.Equal(new[] { "Build and test", "Android" }, jobs.Select(j => j.Name));
        var failed = jobs[0];
        Assert.True(failed.IsFailed);
        Assert.Equal(run.Id, failed.RunId);
        Assert.Equal(GitHubConclusion.Failure, failed.Conclusion);
        Assert.Equal(new[] { 1, 2, 3 }, failed.Steps.Select(s => s.Number));
        Assert.True(failed.Steps[1].IsFailed);
        Assert.Equal(GitHubConclusion.Skipped, failed.Steps[2].Conclusion);
        Assert.Equal(TimeSpan.FromSeconds(148), GitHubRunStates.Duration(failed.Steps[1].StartedAt, failed.Steps[1].CompletedAt, DateTimeOffset.UtcNow));
        Assert.Equal(GitHubRunStatus.InProgress, jobs[1].Status);
        Assert.False(jobs[1].IsCompleted);

        var workflows = await _client.GetWorkflowsAsync(FakeGitHub.Login, Repository);
        var workflow = Assert.Single(workflows);
        Assert.Equal(("CI", ".github/workflows/ci.yml", true), (workflow.Name, workflow.Path, workflow.IsActive));

        var byWorkflow = await _client.GetWorkflowRunsAsync(FakeGitHub.Login, Repository, new GitHubRunQuery(WorkflowId: _ci.Id));
        Assert.Single(byWorkflow.Runs);
        Assert.Contains(_github.Requests, r => r.Contains($"/actions/workflows/{_ci.Id}/runs", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Check_runs_and_external_statuses_of_a_commit_are_read()
    {
        _github.AddRun(Repository, _ci, _head, "main", "completed", "failure",
            new FakeJob { Name = "Build", Conclusion = "failure" },
            new FakeJob { Name = "Lint", Conclusion = "success" });
        _github.Change(() =>
        {
            _github.Actions(Repository).ExternalChecks.Add(new FakeExternalCheck(77, _head, "CodeQL", "completed", "neutral", "codeql", "No new alerts"));
            _github.Actions(Repository).Statuses.Add(new FakeStatus(_head, "ci/circleci: test", "pending", "Running tests", "https://circleci.test/1"));
        });

        var checks = await _client.GetCheckRunsAsync(FakeGitHub.Login, Repository, _head);
        Assert.Equal(new[] { "Build", "Lint", "CodeQL" }, checks.Select(c => c.Name));
        Assert.True(checks[0].IsFailed);
        Assert.True(checks[0].IsGitHubActionsJob);
        Assert.NotNull(checks[0].WorkflowRunId);
        Assert.Equal(GitHubConclusion.Neutral, checks[2].Conclusion);
        Assert.False(checks[2].IsGitHubActionsJob);
        Assert.Equal("No new alerts", checks[2].Title);

        var status = await _client.GetCombinedStatusAsync(FakeGitHub.Login, Repository, _head);
        Assert.True(status.HasStatuses);
        Assert.Equal(GitHubStatusState.Pending, status.State);
        var circle = Assert.Single(status.Statuses);
        Assert.Equal(("ci/circleci: test", GitHubStatusState.Pending, "Running tests"), (circle.Context, circle.State, circle.Description));
        Assert.Equal(new Uri("https://circleci.test/1"), circle.TargetUri);
    }

    [Fact]
    public async Task Polling_again_sends_the_ETag_and_reuses_the_answer_on_304()
    {
        _github.AddRun(Repository, _ci, _head, "main", "in_progress", null, new FakeJob { Name = "Build", Status = "in_progress", Conclusion = null });

        var first = await _client.GetCheckRunsAsync(FakeGitHub.Login, Repository, _head);
        var second = await _client.GetCheckRunsAsync(FakeGitHub.Login, Repository, _head);

        Assert.Equal(first, second);
        Assert.Equal(1, _github.NotModifiedCount);
        var polls = _github.Recorded.Where(r => r.Uri.AbsolutePath.EndsWith("/check-runs", StringComparison.Ordinal)).ToList();
        Assert.Null(polls[0].IfNoneMatch);
        Assert.NotNull(polls[1].IfNoneMatch);

        _github.Change(() => _github.Actions(Repository).Runs[0].Jobs[0].Status = "completed");
        var third = await _client.GetCheckRunsAsync(FakeGitHub.Login, Repository, _head);
        Assert.Equal(GitHubRunStatus.Completed, Assert.Single(third).Status);
        Assert.Equal(1, _github.NotModifiedCount);

        Assert.Equal(new GitHubRateLimit(5000, 4321, DateTimeOffset.FromUnixTimeSeconds(1792915200)), _client.RateLimit);
    }

    [Fact]
    public async Task A_job_log_follows_the_redirect_without_sending_the_token()
    {
        var job = new FakeJob { Name = "Build", Log = "2026-10-25T08:00:01.0000000Z ##[group]Run dotnet build\n2026-10-25T08:00:02.0000000Z ##[error]Build failed\n" };
        _github.AddRun(Repository, _ci, _head, "main", "completed", "failure", job);

        var log = await _client.GetJobLogAsync(FakeGitHub.Login, Repository, job.Id);

        Assert.True(log.IsComplete);
        Assert.Equal(job.Log, log.Head);
        var api = Assert.Single(_github.Recorded, r => r.Uri.AbsolutePath.EndsWith("/logs", StringComparison.Ordinal));
        Assert.True(api.HasAuthorization);
        var download = Assert.Single(_github.Recorded, r => r.Uri.Host == FakeGitHub.LogHost);
        Assert.False(download.HasAuthorization);
    }

    [Fact]
    public async Task A_missing_log_is_reported_as_not_found()
    {
        var job = new FakeJob { Name = "Build", Status = "in_progress", Conclusion = null };
        _github.AddRun(Repository, _ci, _head, "main", "in_progress", null, job);

        var failure = await Assert.ThrowsAsync<GitHubApiException>(() => _client.GetJobLogAsync(FakeGitHub.Login, Repository, job.Id));

        Assert.True(failure.IsNotFound);
    }

    [Fact]
    public async Task A_log_whose_connection_breaks_fails_as_a_GitHub_error()
    {
        // The dropped connection came out as an IOException the CI log panel did not catch, which ended the app.
        using var broken = new BrokenStream(Encoding.UTF8.GetBytes("line 1\nline 2\n"));

        var error = await Assert.ThrowsAsync<GitHubApiException>(() => GitHubClient.ReadLogAsync(broken, new GitHubJobLogLimits(1000, 1000), CancellationToken.None));

        Assert.IsType<IOException>(error.InnerException);
    }

    private sealed class BrokenStream(byte[] first) : MemoryStream(first)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Position < Length ? base.ReadAsync(buffer, cancellationToken) : throw new IOException("Connection reset by peer.");
    }

    [Fact]
    public async Task A_large_log_keeps_whole_lines_from_its_start_and_end()
    {
        var text = new StringBuilder();
        for (var i = 1; i <= 2000; i++)
        {
            text.Append("line ").Append(i).Append(" ü\n");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.ToString()));
        var log = await GitHubClient.ReadLogAsync(stream, new GitHubJobLogLimits(1000, 2000), CancellationToken.None);

        Assert.False(log.IsComplete);
        Assert.StartsWith("line 1 ü\n", log.Head, StringComparison.Ordinal);
        Assert.EndsWith("\n", log.Head, StringComparison.Ordinal);
        Assert.StartsWith("line ", log.Tail, StringComparison.Ordinal);
        Assert.EndsWith("line 2000 ü\n", log.Tail, StringComparison.Ordinal);
        Assert.Equal(stream.Length, log.TotalBytes);
        Assert.Equal(log.TotalBytes, Encoding.UTF8.GetByteCount(log.Head) + Encoding.UTF8.GetByteCount(log.Tail) + log.OmittedBytes);

        using var small = new MemoryStream(Encoding.UTF8.GetBytes("ü first\nsecond ü\n"));
        var whole = await GitHubClient.ReadLogAsync(small, new GitHubJobLogLimits(3, 100), CancellationToken.None);
        Assert.True(whole.IsComplete);
        Assert.Equal("ü first\nsecond ü\n", whole.Head);
    }

    [Fact]
    public async Task Re_run_re_run_failed_jobs_and_cancel_call_the_API()
    {
        var done = _github.AddRun(Repository, _ci, _head, "main", "completed", "failure", new FakeJob { Name = "Build", Conclusion = "failure" }, new FakeJob { Name = "Lint" });
        var running = _github.AddRun(Repository, _ci, _head, "main", "in_progress", null, new FakeJob { Name = "Build", Status = "in_progress", Conclusion = null });

        await _client.RerunFailedJobsAsync(FakeGitHub.Login, Repository, done.Id);
        await _client.CancelWorkflowRunAsync(FakeGitHub.Login, Repository, running.Id);
        await _client.RerunWorkflowAsync(FakeGitHub.Login, Repository, running.Id);

        Assert.Equal(new[] { $"rerun-failed-jobs {done.Id}", $"cancel {running.Id}", $"rerun {running.Id}" }, _github.Actions(Repository).Calls);
        Assert.Equal(2, done.Attempt);
        Assert.Equal("success", done.Jobs[1].Conclusion);

        var refused = await Assert.ThrowsAsync<GitHubApiException>(() => _client.CancelWorkflowRunAsync(FakeGitHub.Login, Repository, done.Id + 999));
        Assert.True(refused.IsNotFound);
    }

    [Fact]
    public async Task Dispatch_posts_the_branch_and_inputs()
    {
        await _client.DispatchWorkflowAsync(FakeGitHub.Login, Repository, _ci.Id, "main", new Dictionary<string, string> { ["environment"] = "staging", ["dryRun"] = "true" });

        var body = Assert.Single(_github.Actions(Repository).Dispatches);
        Assert.Equal("main", body["ref"]!.GetValue<string>());
        Assert.Equal("staging", body["inputs"]!["environment"]!.GetValue<string>());
        Assert.Equal("true", body["inputs"]!["dryRun"]!.GetValue<string>());

        await _client.DispatchWorkflowAsync(FakeGitHub.Login, Repository, _ci.Id, "main");
        Assert.Null(_github.Actions(Repository).Dispatches[1]["inputs"]);
    }

    [Fact]
    public async Task Annotations_are_read_with_their_compiler_codes()
    {
        var job = new FakeJob { Name = "Build", Conclusion = "failure" };
        job.Annotations.Add(new FakeAnnotation("src/App/Program.cs", 1, "failure", "error CS0103: The name 'x' does not exist in the current context", Column: 19));
        job.Annotations.Add(new FakeAnnotation("src/App/Program.cs", 3, "warning", "Do not use Enumerable methods on indexable collections. (https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca1826)"));
        job.Annotations.Add(new FakeAnnotation("src/App/Program.cs", 5, "failure", "Build failed", RawDetails: "src/App/Program.cs(5,9): error CS1002: ; expected [/home/runner/work/hello/src/App/App.csproj]"));
        job.Annotations.Add(new FakeAnnotation(".github", 12, "failure", "Process completed with exit code 1."));
        _github.AddRun(Repository, _ci, _head, "main", "completed", "failure", job);

        var annotations = await _client.GetCheckRunAnnotationsAsync(FakeGitHub.Login, Repository, job.Id);

        Assert.Equal(4, annotations.Count);
        var undefined = annotations[0];
        Assert.Equal(("src/App/Program.cs", 1, GitHubAnnotationLevel.Failure), (undefined.Path, undefined.StartLine, undefined.Level));
        Assert.Equal(19, undefined.StartColumn);
        Assert.Equal(("CS0103", "The name 'x' does not exist in the current context"), (undefined.Compiler.Code, undefined.Compiler.Message));
        Assert.Equal("error CS0103: The name 'x' does not exist in the current context", undefined.Compiler.Original);
        Assert.Equal("CA1826", annotations[1].Compiler.Code);
        Assert.Equal(GitHubAnnotationLevel.Warning, annotations[1].Level);
        Assert.Equal(("CS1002", "; expected"), (annotations[2].Compiler.Code, annotations[2].Compiler.Message));
        Assert.Equal(9, annotations[2].Compiler.Column);
        Assert.Null(annotations[3].Compiler.Code);
        Assert.Equal("Process completed with exit code 1.", annotations[3].Compiler.Message);
    }

    [Fact]
    public async Task A_workflow_definition_is_read_through_the_contents_API()
    {
        const string Yaml = """
            name: Deploy
            on:
              push:
                branches: [main]
              workflow_dispatch:
                inputs:
                  environment:
                    description: Where to deploy
                    type: choice
                    required: true
                    default: staging
                    options: [staging, production]
                  dryRun:
                    type: boolean
                    default: false
            jobs:
              deploy:
                runs-on: ubuntu-latest
                steps:
                  - run: echo deploying
            """;
        _github.Push(Repository, "main", "Add a deploy workflow", new Dictionary<string, string?> { [".github/workflows/deploy.yml"] = Yaml });
        var deploy = _github.AddWorkflow(Repository, "Deploy", ".github/workflows/deploy.yml");
        var workflow = (await _client.GetWorkflowsAsync(FakeGitHub.Login, Repository)).Single(w => w.Id == deploy.Id);

        var definition = await _client.GetWorkflowDefinitionAsync(FakeGitHub.Login, Repository, workflow, "main");

        Assert.Equal("Deploy", definition.Name);
        Assert.True(definition.CanDispatch);
        Assert.Equal(new[] { "environment", "dryRun" }, definition.Inputs.Select(i => i.Name));
        Assert.Equal(new WorkflowDispatchInput("environment", "Where to deploy", true, "staging", WorkflowInputType.Choice, definition.Inputs[0].Options), definition.Inputs[0]);
        Assert.Equal(new[] { "staging", "production" }, definition.Inputs[0].Options);
        Assert.Equal((WorkflowInputType.Boolean, "false", false), (definition.Inputs[1].Type, definition.Inputs[1].Default, definition.Inputs[1].IsRequired));
        Assert.Contains(_github.Requests, r => r.Contains("/contents/.github/workflows/deploy.yml?ref=main", StringComparison.Ordinal));
    }

    public void Dispose() => _github.Dispose();

    private static DateTimeOffset At(int hour, int minute, int second) => new(2026, 10, 25, hour, minute, second, TimeSpan.Zero);
}
