using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TabStudio.SourceControl.Tests;

/// <summary>
/// GitHub Actions and checks in the fake: workflows, runs with jobs and steps (each job is also its commit's
/// check run, as on GitHub), annotations, external commit statuses, job logs served from another host after a
/// redirect, re-runs, cancels and dispatches, and conditional requests with ETags.
/// </summary>
internal sealed partial class FakeGitHub
{
    /// <summary>The host job logs are downloaded from, as GitHub's blob storage would serve them.</summary>
    public const string LogHost = "logs.github.test";

    private readonly ConcurrentQueue<FakeRequest> _recorded = new();
    private readonly ConcurrentDictionary<string, FakeActions> _actions = new(StringComparer.OrdinalIgnoreCase);
    private long _nextId = 1000;

    /// <summary>Every request with the headers tests check, in order.</summary>
    public IReadOnlyList<FakeRequest> Recorded => [.. _recorded];

    /// <summary>How many requests were answered 304 Not Modified.</summary>
    public int NotModifiedCount { get; private set; }

    /// <summary>The Actions state of a repository.</summary>
    public FakeActions Actions(string name) => _actions.GetOrAdd($"{Login}/{name}", _ => new FakeActions());

    /// <summary>Adds a workflow to a repository.</summary>
    public FakeWorkflow AddWorkflow(string name, string workflowName, string path, string state = "active")
    {
        var workflow = new FakeWorkflow(Interlocked.Increment(ref _nextId), workflowName, path, state);
        lock (_gate)
        {
            Actions(name).Workflows.Add(workflow);
        }

        return workflow;
    }

    /// <summary>Adds a run of a workflow on a commit, with jobs.</summary>
    public FakeRun AddRun(string name, FakeWorkflow workflow, string sha, string branch, string status, string? conclusion, params FakeJob[] jobs)
    {
        lock (_gate)
        {
            var actions = Actions(name);
            var run = new FakeRun
            {
                Id = Interlocked.Increment(ref _nextId),
                Workflow = workflow,
                Number = actions.Runs.Count(r => r.Workflow == workflow) + 1,
                Title = HeadMessageOf(name, sha),
                Sha = sha,
                Branch = branch,
                Status = status,
                Conclusion = conclusion,
                CreatedAt = new DateTimeOffset(2026, 10, 25, 8, 0, 0, TimeSpan.Zero).AddMinutes(actions.Runs.Count),
            };
            run.UpdatedAt = run.CreatedAt.AddMinutes(3);
            foreach (var job in jobs)
            {
                job.Id = job.Id == 0 ? Interlocked.Increment(ref _nextId) : job.Id;
                run.Jobs.Add(job);
            }

            actions.Runs.Insert(0, run);
            return run;
        }
    }

    /// <summary>Changes the fake while holding its lock, so a change never races a request.</summary>
    public void Change(Action change)
    {
        lock (_gate)
        {
            change();
        }
    }

    private string HeadMessageOf(string name, string sha) =>
        _repos[$"{Login}/{name}"].Commits.TryGetValue(sha, out var commit) ? commit.Message.Split('\n')[0] : "A commit";

    /// <summary>Serves job logs from <see cref="LogHost"/>, which, like blob storage, refuses requests that carry a token.</summary>
    private HttpResponseMessage? HandleDownload(HttpRequestMessage request)
    {
        var uri = request.RequestUri!;
        if (!string.Equals(uri.Host, LogHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        _recorded.Enqueue(new FakeRequest(request.Method.Method, uri, request.Headers.Authorization is not null, null));
        if (request.Headers.Authorization is not null)
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("<Error>Authentication information is not given in the correct format.</Error>") };
        }

        lock (_gate)
        {
            var id = long.Parse(uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries)[^1], CultureInfo.InvariantCulture);
            var job = _actions.Values.SelectMany(a => a.Runs).SelectMany(r => r.Jobs).FirstOrDefault(j => j.Id == id);
            return job?.Log is { } log
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(log, Encoding.UTF8, "text/plain") }
                : new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }

    private HttpResponseMessage? HandleActions(string method, string rest, Dictionary<string, string> query, JsonNode? body, Repo repo)
    {
        var actions = Actions(repo.Name);
        var parts = rest.Split('/');
        return (method, parts) switch
        {
            ("GET", ["commits", var reference, "check-runs"]) => CheckRuns(repo, actions, reference),
            ("GET", ["commits", var reference, "status"]) => CombinedStatus(repo, actions, reference),
            ("GET", ["check-runs", var id, "annotations"]) => Annotations(actions, long.Parse(id, CultureInfo.InvariantCulture), query),
            ("GET", ["actions", "workflows"]) => Json(new JsonObject
            {
                ["total_count"] = actions.Workflows.Count,
                ["workflows"] = new JsonArray([.. actions.Workflows.Select(w => (JsonNode)WorkflowJson(repo, w))]),
            }),
            ("GET", ["actions", "runs"]) => Runs(repo, actions.Runs, query),
            ("GET", ["actions", "workflows", var id, "runs"]) => Runs(repo, actions.Runs.Where(r => r.Workflow.Id == long.Parse(id, CultureInfo.InvariantCulture)), query),
            ("GET", ["actions", "runs", var id]) => FindRun(actions, id) is { } run ? Json(RunJson(repo, run)) : Error(HttpStatusCode.NotFound, "Not Found"),
            ("GET", ["actions", "runs", var id, "jobs"]) => FindRun(actions, id) is { } run
                ? Json(new JsonObject { ["total_count"] = run.Jobs.Count, ["jobs"] = new JsonArray([.. run.Jobs.Select(j => (JsonNode)JobJson(repo, run, j))]) })
                : Error(HttpStatusCode.NotFound, "Not Found"),
            ("GET", ["actions", "jobs", var id, "logs"]) => JobLog(actions, long.Parse(id, CultureInfo.InvariantCulture)),
            ("POST", ["actions", "runs", var id, "rerun"]) => Rerun(actions, id, failedOnly: false),
            ("POST", ["actions", "runs", var id, "rerun-failed-jobs"]) => Rerun(actions, id, failedOnly: true),
            ("POST", ["actions", "runs", var id, "cancel"]) => Cancel(actions, id),
            ("POST", ["actions", "workflows", var id, "dispatches"]) => Dispatch(repo, actions, long.Parse(id, CultureInfo.InvariantCulture), body!),
            ("GET", ["contents", ..]) => Contents(repo, string.Join('/', parts.Skip(1)), query),
            _ => null,
        };
    }

    private static HttpResponseMessage CheckRuns(Repo repo, FakeActions actions, string reference)
    {
        var sha = Resolve(repo, reference) ?? reference;
        var checks = actions.Runs.Where(r => r.Sha == sha).SelectMany(r => r.Jobs.Select(j => CheckRunJson(repo, r, j)))
            .Concat(actions.ExternalChecks.Where(c => c.Sha == sha).Select(c => ExternalCheckJson(c)))
            .ToList();
        return Json(new JsonObject { ["total_count"] = checks.Count, ["check_runs"] = new JsonArray([.. checks]) });
    }

    private static HttpResponseMessage CombinedStatus(Repo repo, FakeActions actions, string reference)
    {
        var sha = Resolve(repo, reference) ?? reference;
        var statuses = actions.Statuses.Where(s => s.Sha == sha).ToList();
        var state = statuses.Count == 0 ? "pending"
            : statuses.Any(s => s.State is "failure" or "error") ? "failure"
            : statuses.Any(s => s.State == "pending") ? "pending"
            : "success";
        return Json(new JsonObject
        {
            ["state"] = state,
            ["total_count"] = statuses.Count,
            ["statuses"] = new JsonArray([.. statuses.Select(s => (JsonNode)new JsonObject
            {
                ["context"] = s.Context,
                ["state"] = s.State,
                ["description"] = s.Description,
                ["target_url"] = s.TargetUrl,
                ["updated_at"] = "2026-10-25T08:05:00Z",
            })]),
        });
    }

    private static HttpResponseMessage Annotations(FakeActions actions, long checkRunId, Dictionary<string, string> query)
    {
        var job = actions.Runs.SelectMany(r => r.Jobs).FirstOrDefault(j => j.Id == checkRunId);
        if (job is null)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var page = query.TryGetValue("page", out var text) ? int.Parse(text, CultureInfo.InvariantCulture) : 1;
        var perPage = query.TryGetValue("per_page", out var size) ? int.Parse(size, CultureInfo.InvariantCulture) : 30;
        return Json(new JsonArray([.. job.Annotations.Skip((page - 1) * perPage).Take(perPage).Select(a => (JsonNode)new JsonObject
        {
            ["path"] = a.Path,
            ["start_line"] = a.Line,
            ["end_line"] = a.Line,
            ["start_column"] = a.Column,
            ["end_column"] = a.Column,
            ["annotation_level"] = a.Level,
            ["title"] = a.Title ?? string.Empty,
            ["message"] = a.Message,
            ["raw_details"] = a.RawDetails ?? string.Empty,
        })]));
    }

    private static HttpResponseMessage Runs(Repo repo, IEnumerable<FakeRun> runs, Dictionary<string, string> query)
    {
        var matching = runs
            .Where(r => !query.TryGetValue("branch", out var branch) || r.Branch == branch)
            .Where(r => !query.TryGetValue("event", out var name) || r.Event == name)
            .Where(r => !query.TryGetValue("head_sha", out var sha) || r.Sha == sha)
            .Where(r => !query.TryGetValue("status", out var status) || r.Status == status || r.Conclusion == status)
            .ToList();
        var perPage = query.TryGetValue("per_page", out var size) ? int.Parse(size, CultureInfo.InvariantCulture) : 30;
        return Json(new JsonObject
        {
            ["total_count"] = matching.Count,
            ["workflow_runs"] = new JsonArray([.. matching.Take(perPage).Select(r => (JsonNode)RunJson(repo, r))]),
        });
    }

    private static FakeRun? FindRun(FakeActions actions, string id) =>
        actions.Runs.FirstOrDefault(r => r.Id == long.Parse(id, CultureInfo.InvariantCulture));

    private static HttpResponseMessage JobLog(FakeActions actions, long jobId)
    {
        var job = actions.Runs.SelectMany(r => r.Jobs).FirstOrDefault(j => j.Id == jobId);
        if (job?.Log is null)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var response = new HttpResponseMessage(HttpStatusCode.Found);
        response.Headers.Location = new Uri($"https://{LogHost}/jobs/{jobId.ToString(CultureInfo.InvariantCulture)}?sig=short-lived");
        return response;
    }

    private static HttpResponseMessage Rerun(FakeActions actions, string id, bool failedOnly)
    {
        if (FindRun(actions, id) is not { } run)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        if (run.Status != "completed")
        {
            return Error(HttpStatusCode.Forbidden, "This workflow is already running");
        }

        actions.Calls.Add($"{(failedOnly ? "rerun-failed-jobs" : "rerun")} {run.Id.ToString(CultureInfo.InvariantCulture)}");
        run.Attempt++;
        run.Status = "queued";
        run.Conclusion = null;
        foreach (var job in run.Jobs.Where(j => !failedOnly || j.Conclusion is "failure" or "cancelled" or "timed_out"))
        {
            job.Status = "queued";
            job.Conclusion = null;
        }

        return new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }

    private static HttpResponseMessage Cancel(FakeActions actions, string id)
    {
        if (FindRun(actions, id) is not { } run)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        if (run.Status == "completed")
        {
            return Error(HttpStatusCode.Conflict, "Cannot cancel a workflow run that is completed.");
        }

        actions.Calls.Add($"cancel {run.Id.ToString(CultureInfo.InvariantCulture)}");
        run.Status = "completed";
        run.Conclusion = "cancelled";
        foreach (var job in run.Jobs.Where(j => j.Status != "completed"))
        {
            job.Status = "completed";
            job.Conclusion = "cancelled";
        }

        return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }

    private HttpResponseMessage Dispatch(Repo repo, FakeActions actions, long workflowId, JsonNode body)
    {
        var workflow = actions.Workflows.FirstOrDefault(w => w.Id == workflowId);
        var reference = body["ref"]?.GetValue<string>();
        if (workflow is null || reference is null || Resolve(repo, reference) is not { } sha)
        {
            return Error(HttpStatusCode.UnprocessableEntity, "No ref found for: " + reference);
        }

        actions.Dispatches.Add(body.DeepClone());
        actions.Calls.Add($"dispatch {workflowId.ToString(CultureInfo.InvariantCulture)} {reference}");
        var run = new FakeRun
        {
            Id = Interlocked.Increment(ref _nextId),
            Workflow = workflow,
            Number = actions.Runs.Count(r => r.Workflow == workflow) + 1,
            Title = workflow.Name,
            Event = "workflow_dispatch",
            Sha = sha,
            Branch = reference,
            Status = "queued",
            Conclusion = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        run.UpdatedAt = run.CreatedAt;
        actions.Runs.Insert(0, run);
        return new HttpResponseMessage(HttpStatusCode.NoContent);
    }

    private static HttpResponseMessage Contents(Repo repo, string path, Dictionary<string, string> query)
    {
        var sha = Resolve(repo, query.TryGetValue("ref", out var reference) ? reference : repo.DefaultBranch);
        if (sha is null || !repo.Trees[repo.Commits[sha].Tree].TryGetValue(path, out var file))
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var content = repo.Blobs[file.Sha];
        var encoded = Convert.ToBase64String(content);
        // GitHub wraps the base64 text every 60 characters.
        var wrapped = string.Join('\n', Enumerable.Range(0, (encoded.Length + 59) / 60).Select(i => encoded.Substring(i * 60, Math.Min(60, encoded.Length - (i * 60)))));
        return Json(new JsonObject { ["type"] = "file", ["encoding"] = "base64", ["content"] = wrapped, ["size"] = content.Length, ["path"] = path, ["sha"] = file.Sha });
    }

    /// <summary>Adds an ETag to every successful GET and answers 304 when the client already has that version.</summary>
    private async Task<HttpResponseMessage> ApplyETagAsync(HttpRequestMessage request, HttpResponseMessage response)
    {
        var ifNoneMatch = request.Headers.IfNoneMatch.FirstOrDefault()?.Tag;
        _recorded.Enqueue(new FakeRequest(request.Method.Method, request.RequestUri!, request.Headers.Authorization is not null, ifNoneMatch));
        if (request.Method != HttpMethod.Get || response.StatusCode != HttpStatusCode.OK || response.Content is null)
        {
            return response;
        }

        var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        var etag = $"\"{Convert.ToHexStringLower(SHA256.HashData(body))[..20]}\"";
        if (ifNoneMatch == etag)
        {
            response.Dispose();
            NotModifiedCount++;
            var notModified = new HttpResponseMessage(HttpStatusCode.NotModified);
            notModified.Headers.ETag = new EntityTagHeaderValue(etag);
            AddRateLimit(notModified);
            return notModified;
        }

        response.Headers.ETag = new EntityTagHeaderValue(etag);
        AddRateLimit(response);
        return response;
    }

    private static void AddRateLimit(HttpResponseMessage response)
    {
        response.Headers.Add("x-ratelimit-limit", "5000");
        response.Headers.Add("x-ratelimit-remaining", "4321");
        response.Headers.Add("x-ratelimit-reset", "1792915200");
    }

    private static JsonObject WorkflowJson(Repo repo, FakeWorkflow workflow) => new()
    {
        ["id"] = workflow.Id,
        ["name"] = workflow.Name,
        ["path"] = workflow.Path,
        ["state"] = workflow.State,
        ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/blob/{repo.DefaultBranch}/{workflow.Path}",
    };

    private static JsonObject RunJson(Repo repo, FakeRun run) => new()
    {
        ["id"] = run.Id,
        ["workflow_id"] = run.Workflow.Id,
        ["name"] = run.Workflow.Name,
        ["display_title"] = run.Title,
        ["run_number"] = run.Number,
        ["run_attempt"] = run.Attempt,
        ["event"] = run.Event,
        ["status"] = run.Status,
        ["conclusion"] = run.Conclusion,
        ["head_branch"] = run.Branch,
        ["head_sha"] = run.Sha,
        ["created_at"] = run.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        ["run_started_at"] = run.CreatedAt.ToString("O", CultureInfo.InvariantCulture),
        ["updated_at"] = run.UpdatedAt.ToString("O", CultureInfo.InvariantCulture),
        ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/actions/runs/{run.Id}",
        ["actor"] = new JsonObject { ["login"] = run.Actor },
        ["triggering_actor"] = new JsonObject { ["login"] = run.Actor },
        ["head_commit"] = new JsonObject { ["message"] = run.Title },
        ["repository"] = new JsonObject { ["full_name"] = $"{repo.Owner}/{repo.Name}" },
    };

    private static JsonObject JobJson(Repo repo, FakeRun run, FakeJob job) => new()
    {
        ["id"] = job.Id,
        ["run_id"] = run.Id,
        ["name"] = job.Name,
        ["status"] = job.Status,
        ["conclusion"] = job.Conclusion,
        ["started_at"] = job.StartedAt?.ToString("O", CultureInfo.InvariantCulture),
        ["completed_at"] = job.Status == "completed" ? job.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) : null,
        ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/actions/runs/{run.Id}/job/{job.Id}",
        ["steps"] = new JsonArray([.. job.Steps.Select(s => (JsonNode)new JsonObject
        {
            ["number"] = s.Number,
            ["name"] = s.Name,
            ["status"] = s.Status,
            ["conclusion"] = s.Conclusion,
            ["started_at"] = s.StartedAt?.ToString("O", CultureInfo.InvariantCulture),
            ["completed_at"] = s.CompletedAt?.ToString("O", CultureInfo.InvariantCulture),
        })]),
    };

    private static JsonNode CheckRunJson(Repo repo, FakeRun run, FakeJob job) => new JsonObject
    {
        ["id"] = job.Id,
        ["name"] = job.Name,
        ["status"] = job.Status,
        ["conclusion"] = job.Conclusion,
        ["started_at"] = job.StartedAt?.ToString("O", CultureInfo.InvariantCulture),
        ["completed_at"] = job.Status == "completed" ? job.CompletedAt?.ToString("O", CultureInfo.InvariantCulture) : null,
        ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/actions/runs/{run.Id}/job/{job.Id}",
        ["details_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/actions/runs/{run.Id}/job/{job.Id}",
        ["output"] = new JsonObject { ["title"] = null, ["summary"] = null, ["annotations_count"] = job.Annotations.Count },
        ["app"] = new JsonObject { ["slug"] = "github-actions" },
    };

    private static JsonNode ExternalCheckJson(FakeExternalCheck check) => new JsonObject
    {
        ["id"] = check.Id,
        ["name"] = check.Name,
        ["status"] = check.Status,
        ["conclusion"] = check.Conclusion,
        ["started_at"] = "2026-10-25T08:00:00Z",
        ["completed_at"] = check.Status == "completed" ? "2026-10-25T08:02:00Z" : null,
        ["html_url"] = $"https://checks.test/{check.Id}",
        ["details_url"] = $"https://checks.test/{check.Id}",
        ["output"] = new JsonObject { ["title"] = check.Title, ["summary"] = null, ["annotations_count"] = 0 },
        ["app"] = new JsonObject { ["slug"] = check.App },
    };
}

/// <summary>A request the fake received, with the headers tests check.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The address.</param>
/// <param name="HasAuthorization">Whether it carried a token.</param>
/// <param name="IfNoneMatch">The ETag it sent, if any.</param>
internal sealed record FakeRequest(string Method, Uri Uri, bool HasAuthorization, string? IfNoneMatch);

/// <summary>A repository's Actions in the fake.</summary>
internal sealed class FakeActions
{
    public List<FakeWorkflow> Workflows { get; } = [];

    /// <summary>Runs, newest first.</summary>
    public List<FakeRun> Runs { get; } = [];

    public List<FakeExternalCheck> ExternalChecks { get; } = [];

    public List<FakeStatus> Statuses { get; } = [];

    /// <summary>Re-runs, cancels and dispatches, as "rerun 1234".</summary>
    public List<string> Calls { get; } = [];

    /// <summary>The bodies of dispatch requests.</summary>
    public List<JsonNode> Dispatches { get; } = [];
}

/// <summary>A workflow in the fake.</summary>
internal sealed record FakeWorkflow(long Id, string Name, string Path, string State);

/// <summary>A workflow run in the fake.</summary>
internal sealed class FakeRun
{
    public required long Id { get; init; }

    public required FakeWorkflow Workflow { get; init; }

    public int Number { get; init; }

    public int Attempt { get; set; } = 1;

    public string Title { get; set; } = string.Empty;

    public string Event { get; init; } = "push";

    public string Status { get; set; } = "completed";

    public string? Conclusion { get; set; }

    public string Branch { get; init; } = "main";

    public string Sha { get; init; } = string.Empty;

    public string Actor { get; init; } = FakeGitHub.Login;

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; set; }

    public List<FakeJob> Jobs { get; } = [];
}

/// <summary>A job of a run in the fake, which is also a check run of the run's commit.</summary>
internal sealed class FakeJob
{
    public long Id { get; set; }

    public required string Name { get; init; }

    public string Status { get; set; } = "completed";

    public string? Conclusion { get; set; } = "success";

    public DateTimeOffset? StartedAt { get; init; } = new DateTimeOffset(2026, 10, 25, 8, 0, 10, TimeSpan.Zero);

    public DateTimeOffset? CompletedAt { get; init; } = new DateTimeOffset(2026, 10, 25, 8, 2, 40, TimeSpan.Zero);

    public List<FakeStep> Steps { get; } = [];

    public List<FakeAnnotation> Annotations { get; } = [];

    /// <summary>The log, or <see langword="null"/> when GitHub has none for the job.</summary>
    public string? Log { get; set; }
}

/// <summary>A step of a job in the fake.</summary>
internal sealed record FakeStep(int Number, string Name, string Status, string? Conclusion, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt);

/// <summary>An annotation of a job in the fake.</summary>
internal sealed record FakeAnnotation(string Path, int Line, string Level, string Message, string? Title = null, string? RawDetails = null, int? Column = null);

/// <summary>A check run of another app in the fake.</summary>
internal sealed record FakeExternalCheck(long Id, string Sha, string Name, string Status, string? Conclusion, string App = "code-scanner", string? Title = null);

/// <summary>A commit status set by an external CI service in the fake.</summary>
internal sealed record FakeStatus(string Sha, string Context, string State, string? Description = null, string? TargetUrl = null);
