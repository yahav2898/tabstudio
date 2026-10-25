using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using TabStudio.SourceControl.GitHub;
using TabStudio.SourceControl.Merging;

namespace TabStudio.App.Tests;

/// <summary>
/// The fake's pull requests: listing and reading them, their files, commits and conversation, review comments and
/// reviews (pending ones included), merging, file contents, and the search and GraphQL calls TabStudio makes for them.
/// </summary>
/// <remarks>
/// Requests are always made as <see cref="Login"/>; other people's pull requests, comments and reviews are added
/// with the helpers. Diffs are computed from the merge base, as GitHub's Files changed shows them.
/// </remarks>
internal sealed partial class FakeGitHub
{
    /// <summary>Another GitHub user, who opens and reviews pull requests in tests.</summary>
    public const string Reviewer = "octocat";

    private static readonly DateTimeOffset Epoch = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private long _nextId = 1000;
    private int _minutes;

    /// <summary>Opens a pull request, by default as <see cref="Reviewer"/>.</summary>
    /// <returns>Its number.</returns>
    public int AddPullRequest(string name, string title, string head, string baseBranch = "main", string author = Reviewer, string? body = null, bool draft = false, IReadOnlyList<string>? reviewers = null)
    {
        lock (_gate)
        {
            var repo = _repos[$"{Login}/{name}"];
            var pull = new PullRequest(repo.PullRequests.Count + 1, title, head, baseBranch, draft, author, Tick()) { Body = body };
            pull.RequestedReviewers.AddRange(reviewers ?? []);
            repo.PullRequests.Add(pull);
            return pull.Number;
        }
    }

    /// <summary>Adds a comment on a line of a pull request's diff, submitted in a review of its own as GitHub does.</summary>
    /// <param name="outdated">Whether the comment's lines changed since, so it no longer has a line in the diff.</param>
    /// <returns>The comment's id.</returns>
    public long AddReviewComment(string name, int number, string path, int line, string body, string author = Reviewer, DiffSide side = DiffSide.Right, long? inReplyTo = null, bool outdated = false, int? startLine = null)
    {
        lock (_gate)
        {
            var (repo, pull) = Find(name, number);
            var review = new ReviewRecord(NextId(), author, "COMMENTED", null, HeadSha(repo, pull), Tick());
            pull.Reviews.Add(review);
            var comment = NewComment(repo, pull, path, line, side == DiffSide.Left ? "LEFT" : "RIGHT", startLine, body, author, review.Id, inReplyTo);
            if (outdated)
            {
                comment.Line = null;
                comment.StartLine = null;
            }

            pull.Comments.Add(comment);
            return comment.Id;
        }
    }

    /// <summary>Adds a comment to a pull request's conversation.</summary>
    public void AddIssueComment(string name, int number, string body, string author = Reviewer)
    {
        lock (_gate)
        {
            Find(name, number).Pull.Conversation.Add(new IssueCommentRecord(NextId(), author, body, Tick()));
        }
    }

    /// <summary>Adds a submitted review: <c>APPROVED</c>, <c>CHANGES_REQUESTED</c> or <c>COMMENTED</c>.</summary>
    public void AddReview(string name, int number, string state, string? body, string author = Reviewer)
    {
        lock (_gate)
        {
            var (repo, pull) = Find(name, number);
            pull.Reviews.Add(new ReviewRecord(NextId(), author, state, body, HeadSha(repo, pull), Tick()));
        }
    }

    /// <summary>Sets what GitHub says about whether a pull request can be merged, such as <c>(false, "dirty")</c> for conflicts.</summary>
    public void SetMergeable(string name, int number, bool? mergeable, string state)
    {
        lock (_gate)
        {
            var pull = Find(name, number).Pull;
            pull.Mergeable = mergeable;
            pull.MergeableState = state;
        }
    }

    /// <summary>Sets which merge methods a repository allows, and whether it deletes merged branches itself.</summary>
    public void SetMergeSettings(string name, bool allowMerge, bool allowSquash, bool allowRebase, bool deleteBranchOnMerge = false)
    {
        lock (_gate)
        {
            _repos[$"{Login}/{name}"].Settings = new MergeSettingsRecord(allowMerge, allowSquash, allowRebase, deleteBranchOnMerge);
        }
    }

    /// <summary>A pull request's reviews, with their comments.</summary>
    public IReadOnlyList<FakeReview> Reviews(string name, int number)
    {
        lock (_gate)
        {
            var pull = Find(name, number).Pull;
            return [.. pull.Reviews.Select(r => new FakeReview(r.Id, r.State, r.Body, r.Author,
                [.. pull.Comments.Where(c => c.ReviewId == r.Id).Select(ToFakeComment)]))];
        }
    }

    /// <summary>A pull request's comments on its diff, pending ones included.</summary>
    public IReadOnlyList<FakeReviewComment> ReviewComments(string name, int number)
    {
        lock (_gate)
        {
            return [.. Find(name, number).Pull.Comments.Select(ToFakeComment)];
        }
    }

    /// <summary>The bodies of a pull request's conversation comments.</summary>
    public IReadOnlyList<string> IssueComments(string name, int number)
    {
        lock (_gate)
        {
            return [.. Find(name, number).Pull.Conversation.Select(c => c.Body)];
        }
    }

    /// <summary>Whether a pull request is merged, and how.</summary>
    public (bool Merged, string? Method, string State) MergeState(string name, int number)
    {
        lock (_gate)
        {
            var pull = Find(name, number).Pull;
            return (pull.MergedSha is not null, pull.MergeMethod, pull.State);
        }
    }

    /// <summary>Whether a pull request is a draft.</summary>
    public bool IsDraft(string name, int number)
    {
        lock (_gate)
        {
            return Find(name, number).Pull.Draft;
        }
    }

    /// <summary>The people asked to review a pull request.</summary>
    public IReadOnlyList<string> RequestedReviewers(string name, int number)
    {
        lock (_gate)
        {
            return [.. Find(name, number).Pull.RequestedReviewers];
        }
    }

    /// <summary>Whether the thread a comment started is resolved.</summary>
    public bool IsResolved(string name, int number, long rootId)
    {
        lock (_gate)
        {
            return Find(name, number).Pull.Comments.Single(c => c.Id == rootId).Resolved;
        }
    }

    private (Repo Repo, PullRequest Pull) Find(string name, int number)
    {
        var repo = _repos[$"{Login}/{name}"];
        return (repo, repo.PullRequests.Single(p => p.Number == number));
    }

    /// <summary>The endpoints outside a repository's path: search and GraphQL.</summary>
    private HttpResponseMessage? HandleOutsideRepositories(string method, string path, Dictionary<string, string> query, JsonNode? body) => (method, path) switch
    {
        ("GET", "search/issues") => SearchIssues(query["q"]),
        ("POST", "graphql") => GraphQL(body!),
        _ => null,
    };

    private static HttpResponseMessage ListPullRequests(Repo repo, Dictionary<string, string> query)
    {
        var state = query.GetValueOrDefault("state", "open");
        var list = repo.PullRequests
            .Where(p => state == "all" || p.State == state)
            .Where(p => !query.TryGetValue("head", out var head) || head == $"{repo.Owner}:{p.Head}")
            .Where(p => !query.TryGetValue("base", out var baseBranch) || baseBranch == p.Base)
            .OrderByDescending(p => p.Updated)
            .Select(p => (JsonNode)PullRequestJson(repo, p, details: false));
        return Json(new JsonArray([.. list]));
    }

    private HttpResponseMessage CreatePullRequest(Repo repo, JsonNode body)
    {
        var pull = new PullRequest(repo.PullRequests.Count + 1, body["title"]!.GetValue<string>(), body["head"]!.GetValue<string>(), body["base"]!.GetValue<string>(), body["draft"]?.GetValue<bool>() ?? false, Login, Tick())
        {
            Body = body["body"]?.GetValue<string>(),
        };
        if (!repo.Branches.ContainsKey(pull.Head) || !repo.Branches.ContainsKey(pull.Base))
        {
            return Error(HttpStatusCode.UnprocessableEntity, "Validation Failed");
        }

        repo.PullRequests.Add(pull);
        return Json(PullRequestJson(repo, pull, details: false), HttpStatusCode.Created);
    }

    /// <summary>Everything under a repository's path that has to do with pull requests; <see langword="null"/> for anything else.</summary>
    private HttpResponseMessage? HandlePullRequests(Repo repo, string method, string rest, Dictionary<string, string> query, JsonNode? body)
    {
        var parts = rest.Split('/');
        switch (parts)
        {
            case ["contents", ..] when method == "GET":
                return Contents(repo, string.Join('/', parts.Skip(1)), query.GetValueOrDefault("ref") ?? repo.DefaultBranch);
            case ["git", "refs", "heads", ..] when method == "DELETE":
                return DeleteBranch(repo, string.Join('/', parts.Skip(3)));
            case ["pulls", "comments", var id]:
                return EditComment(repo, method, long.Parse(id, CultureInfo.InvariantCulture), body);
            case ["issues", var issue, "comments"] when repo.PullRequests.FirstOrDefault(p => p.Number.ToString(CultureInfo.InvariantCulture) == issue) is { } discussed:
                return method == "POST" ? AddConversationComment(discussed, body!) : Page(query, discussed.Conversation.Select(c => (JsonNode)IssueCommentJson(c)));
            case ["pulls", var number, .. var sub] when repo.PullRequests.FirstOrDefault(p => p.Number.ToString(CultureInfo.InvariantCulture) == number) is { } pull:
                return HandlePullRequest(repo, pull, method, sub, query, body);
            default:
                return null;
        }
    }

    private HttpResponseMessage? HandlePullRequest(Repo repo, PullRequest pull, string method, string[] sub, Dictionary<string, string> query, JsonNode? body) => (method, sub) switch
    {
        ("GET", []) => Json(PullRequestJson(repo, pull, details: true)),
        ("GET", ["files"]) => Page(query, ChangedFiles(repo, pull).Select(f => (JsonNode)FileJson(f))),
        ("GET", ["commits"]) => Page(query, PullCommits(repo, pull).Select(c => (JsonNode)RepositoryCommitJson(repo.Commits[c]))),
        ("GET", ["comments"]) => Page(query, pull.Comments.Where(c => !IsPending(pull, c)).Select(c => (JsonNode)CommentJson(repo, pull, c))),
        ("POST", ["comments", var id, "replies"]) => Reply(repo, pull, long.Parse(id, CultureInfo.InvariantCulture), body!),
        ("GET", ["reviews"]) => Page(query, pull.Reviews.Where(r => r.State != "PENDING" || r.Author == Login).Select(r => (JsonNode)ReviewJson(repo, pull, r))),
        ("POST", ["reviews"]) => CreateReview(repo, pull, body!),
        ("DELETE", ["reviews", var id]) => DeleteReview(repo, pull, long.Parse(id, CultureInfo.InvariantCulture)),
        ("GET", ["reviews", var id, "comments"]) => Page(query, pull.Comments.Where(c => c.ReviewId == long.Parse(id, CultureInfo.InvariantCulture)).Select(c => (JsonNode)CommentJson(repo, pull, c))),
        ("POST", ["reviews", var id, "events"]) => SubmitReview(repo, pull, long.Parse(id, CultureInfo.InvariantCulture), body!),
        ("PUT", ["merge"]) => Merge(repo, pull, body!),
        ("POST", ["requested_reviewers"]) => RequestReviewers(repo, pull, body!),
        _ => null,
    };

    private HttpResponseMessage CreateReview(Repo repo, PullRequest pull, JsonNode body)
    {
        var reviewEvent = body["event"]?.GetValue<string>();
        var text = body["body"]?.GetValue<string>();
        var comments = body["comments"]?.AsArray() ?? new JsonArray();
        if (reviewEvent is null && pull.Reviews.Any(r => r.State == "PENDING" && r.Author == Login))
        {
            return Error(HttpStatusCode.UnprocessableEntity, "User can only have one pending review per pull request");
        }

        if (Refusal(pull, reviewEvent, text, comments.Count) is { } refusal)
        {
            return refusal;
        }

        var review = new ReviewRecord(NextId(), Login, reviewEvent is null ? "PENDING" : ReviewState(reviewEvent), text, body["commit_id"]?.GetValue<string>() ?? HeadSha(repo, pull), reviewEvent is null ? null : Tick());
        foreach (var comment in comments)
        {
            var path = comment!["path"]!.GetValue<string>();
            var line = comment["line"]!.GetValue<int>();
            var side = comment["side"]?.GetValue<string>() ?? "RIGHT";
            if (!InDiff(repo, pull, path, line, side))
            {
                return Error(HttpStatusCode.UnprocessableEntity, "pull_request_review_thread.line must be part of the diff");
            }

            pull.Comments.Add(NewComment(repo, pull, path, line, side, comment["start_line"]?.GetValue<int>(), comment["body"]!.GetValue<string>(), Login, review.Id, inReplyTo: null));
        }

        pull.Reviews.Add(review);
        return Json(ReviewJson(repo, pull, review));
    }

    private HttpResponseMessage SubmitReview(Repo repo, PullRequest pull, long id, JsonNode body)
    {
        if (pull.Reviews.FirstOrDefault(r => r.Id == id && r.State == "PENDING" && r.Author == Login) is not { } review)
        {
            return Error(HttpStatusCode.UnprocessableEntity, "Can not submit a review that is not pending");
        }

        var reviewEvent = body["event"]!.GetValue<string>();
        var text = body["body"]?.GetValue<string>();
        if (Refusal(pull, reviewEvent, text, pull.Comments.Count(c => c.ReviewId == id)) is { } refusal)
        {
            return refusal;
        }

        review.State = ReviewState(reviewEvent);
        review.Body = text;
        review.Submitted = Tick();
        pull.RequestedReviewers.Remove(Login);
        return Json(ReviewJson(repo, pull, review));
    }

    /// <summary>GitHub's rules for submitting a review.</summary>
    private static HttpResponseMessage? Refusal(PullRequest pull, string? reviewEvent, string? text, int comments) => reviewEvent switch
    {
        "APPROVE" when pull.Author == Login => Error(HttpStatusCode.UnprocessableEntity, "Can not approve your own pull request"),
        "REQUEST_CHANGES" when pull.Author == Login => Error(HttpStatusCode.UnprocessableEntity, "Can not request changes on your own pull request"),
        "REQUEST_CHANGES" when string.IsNullOrWhiteSpace(text) => Error(HttpStatusCode.UnprocessableEntity, "Review body is required"),
        "COMMENT" when string.IsNullOrWhiteSpace(text) && comments == 0 => Error(HttpStatusCode.UnprocessableEntity, "Review body is required"),
        _ => null,
    };

    private static HttpResponseMessage DeleteReview(Repo repo, PullRequest pull, long id)
    {
        if (pull.Reviews.FirstOrDefault(r => r.Id == id) is not { State: "PENDING" } review)
        {
            return Error(HttpStatusCode.UnprocessableEntity, "Can not delete a non-pending pull request review");
        }

        pull.Reviews.Remove(review);
        pull.Comments.RemoveAll(c => c.ReviewId == id);
        return Json(ReviewJson(repo, pull, review));
    }

    private HttpResponseMessage Reply(Repo repo, PullRequest pull, long id, JsonNode body)
    {
        if (pull.Comments.FirstOrDefault(c => c.Id == id) is not { } replied)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var root = replied;
        while (root.InReplyTo is { } parent && pull.Comments.FirstOrDefault(c => c.Id == parent) is { } next)
        {
            root = next;
        }

        var review = new ReviewRecord(NextId(), Login, "COMMENTED", null, HeadSha(repo, pull), Tick());
        pull.Reviews.Add(review);
        var reply = new ReviewCommentRecord(NextId(), root.Path, root.Side, Login, body["body"]!.GetValue<string>(), Tick(), review.Id, root.CommitId, root.DiffHunk)
        {
            Line = root.Line,
            StartLine = root.StartLine,
            OriginalLine = root.OriginalLine,
            InReplyTo = root.Id,
        };
        pull.Comments.Add(reply);
        return Json(CommentJson(repo, pull, reply), HttpStatusCode.Created);
    }

    private static HttpResponseMessage EditComment(Repo repo, string method, long id, JsonNode? body)
    {
        if (repo.PullRequests.FirstOrDefault(p => p.Comments.Any(c => c.Id == id)) is not { } pull)
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var comment = pull.Comments.Single(c => c.Id == id);
        if (method == "DELETE")
        {
            pull.Comments.Remove(comment);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        comment.Body = body!["body"]!.GetValue<string>();
        return Json(CommentJson(repo, pull, comment));
    }

    private HttpResponseMessage AddConversationComment(PullRequest pull, JsonNode body)
    {
        var comment = new IssueCommentRecord(NextId(), Login, body["body"]!.GetValue<string>(), Tick());
        pull.Conversation.Add(comment);
        return Json(IssueCommentJson(comment), HttpStatusCode.Created);
    }

    private static HttpResponseMessage RequestReviewers(Repo repo, PullRequest pull, JsonNode body)
    {
        foreach (var login in body["reviewers"]!.AsArray().Select(r => r!.GetValue<string>()))
        {
            if (login == pull.Author)
            {
                return Error(HttpStatusCode.UnprocessableEntity, "Review cannot be requested from pull request author.");
            }

            if (!pull.RequestedReviewers.Contains(login))
            {
                pull.RequestedReviewers.Add(login);
            }
        }

        return Json(PullRequestJson(repo, pull, details: false), HttpStatusCode.Created);
    }

    private HttpResponseMessage Merge(Repo repo, PullRequest pull, JsonNode body)
    {
        var method = body["merge_method"]?.GetValue<string>() ?? "merge";
        var settings = repo.Settings;
        var allowed = method switch
        {
            "squash" => settings.AllowSquash,
            "rebase" => settings.AllowRebase,
            _ => settings.AllowMerge,
        };
        if (pull.State != "open" || pull.Mergeable == false)
        {
            return Error(HttpStatusCode.MethodNotAllowed, "Pull Request is not mergeable");
        }

        if (pull.Draft)
        {
            return Error(HttpStatusCode.MethodNotAllowed, "Pull Request is still a draft");
        }

        if (pull.MergeableState == "blocked")
        {
            return Error(HttpStatusCode.MethodNotAllowed, "At least 1 approving review is required by reviewers with write access.");
        }

        if (!allowed)
        {
            return Error(HttpStatusCode.MethodNotAllowed, $"{char.ToUpperInvariant(method[0])}{method[1..]} merges are not allowed on this repository.");
        }

        var head = HeadSha(repo, pull);
        if (body["sha"]?.GetValue<string>() is { } expected && expected != head)
        {
            return Error(HttpStatusCode.Conflict, "Head branch was modified. Review and try the merge again.");
        }

        // The base's files with the head's changes since the merge base: no conflicts, which the fake does not model.
        var baseHead = repo.Branches[pull.Base];
        var mergeBase = MergeBase(repo, baseHead, head);
        var files = new Dictionary<string, (string Mode, string Sha)>(repo.Trees[repo.Commits[baseHead].Tree], StringComparer.Ordinal);
        var before = repo.Trees[repo.Commits[mergeBase].Tree];
        var after = repo.Trees[repo.Commits[head].Tree];
        foreach (var path in before.Keys.Union(after.Keys))
        {
            if (!after.TryGetValue(path, out var changed))
            {
                files.Remove(path);
            }
            else if (!before.TryGetValue(path, out var original) || original.Sha != changed.Sha)
            {
                files[path] = changed;
            }
        }

        var title = body["commit_title"]?.GetValue<string>() ?? $"Merge pull request #{pull.Number} from {repo.Owner}/{pull.Head}";
        var message = body["commit_message"]?.GetValue<string>();
        var commit = StoreCommit(repo, StoreTree(repo, files), string.IsNullOrEmpty(message) ? title : $"{title}\n\n{message}", method == "merge" ? [baseHead, head] : [baseHead]);
        repo.Branches[pull.Base] = commit;
        pull.MergedHead = head;
        pull.MergedBase = mergeBase;
        pull.MergedSha = commit;
        pull.MergeMethod = method;
        pull.MergedAt = Tick();
        pull.State = "closed";
        if (settings.DeleteBranchOnMerge)
        {
            repo.Branches.Remove(pull.Head);
        }

        return Json(new JsonObject { ["sha"] = commit, ["merged"] = true, ["message"] = "Pull Request successfully merged" });
    }

    private static HttpResponseMessage DeleteBranch(Repo repo, string branch) =>
        repo.Branches.Remove(branch)
            ? new HttpResponseMessage(HttpStatusCode.NoContent)
            : Error(HttpStatusCode.UnprocessableEntity, "Reference does not exist");

    private static HttpResponseMessage Contents(Repo repo, string path, string reference)
    {
        if (Resolve(repo, reference) is not { } sha || !repo.Trees[repo.Commits[sha].Tree].TryGetValue(path, out var file))
        {
            return Error(HttpStatusCode.NotFound, "Not Found");
        }

        var content = repo.Blobs[file.Sha];
        return Json(new JsonObject
        {
            ["type"] = "file",
            ["encoding"] = "base64",
            ["content"] = Base64Lines(content),
            ["sha"] = file.Sha,
            ["size"] = content.Length,
            ["path"] = path,
        });
    }

    private HttpResponseMessage SearchIssues(string query)
    {
        var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var name = terms.Single(t => t.StartsWith("repo:", StringComparison.Ordinal))["repo:".Length..];
        var state = terms.Contains("is:open") ? "open" : terms.Contains("is:closed") ? "closed" : null;
        var items = _repos.TryGetValue(name, out var repo)
            ? repo.PullRequests.Where(p => state is null || p.State == state).Select(p => (JsonNode)new JsonObject { ["number"] = p.Number, ["comments"] = p.Conversation.Count })
            : Enumerable.Empty<JsonNode>();
        var list = new JsonArray([.. items]);
        return Json(new JsonObject { ["total_count"] = list.Count, ["incomplete_results"] = false, ["items"] = list });
    }

    /// <summary>The GraphQL operations TabStudio sends, recognized by the field they use.</summary>
    private HttpResponseMessage GraphQL(JsonNode body)
    {
        var query = body["query"]!.GetValue<string>();
        var variables = body["variables"]!;
        if (query.Contains("reviewThreads", StringComparison.Ordinal))
        {
            var pull = Find(variables["name"]!.GetValue<string>(), variables["number"]!.GetValue<int>()).Pull;
            var threads = pull.Comments.Where(c => c.InReplyTo is null && !IsPending(pull, c)).Select(c => (JsonNode)new JsonObject
            {
                ["id"] = $"PRRT_{c.Id}",
                ["isResolved"] = c.Resolved,
                ["viewerCanResolve"] = true,
                ["viewerCanUnresolve"] = true,
                ["comments"] = new JsonObject { ["nodes"] = new JsonArray(new JsonObject { ["databaseId"] = c.Id }) },
            });
            return Data(new JsonObject
            {
                ["repository"] = new JsonObject
                {
                    ["pullRequest"] = new JsonObject
                    {
                        ["reviewThreads"] = new JsonObject
                        {
                            ["pageInfo"] = new JsonObject { ["hasNextPage"] = false, ["endCursor"] = null },
                            ["nodes"] = new JsonArray([.. threads]),
                        },
                    },
                },
            });
        }

        var id = variables["id"]?.GetValue<string>();
        if (query.Contains("unresolveReviewThread", StringComparison.Ordinal) || query.Contains("resolveReviewThread", StringComparison.Ordinal))
        {
            var resolved = !query.Contains("unresolveReviewThread", StringComparison.Ordinal);
            var rootId = long.Parse(id!["PRRT_".Length..], CultureInfo.InvariantCulture);
            var comment = _repos.Values.SelectMany(r => r.PullRequests).SelectMany(p => p.Comments).Single(c => c.Id == rootId);
            comment.Resolved = resolved;
            return Data(new JsonObject { ["thread"] = new JsonObject { ["id"] = id, ["isResolved"] = resolved } });
        }

        if (query.Contains("markPullRequestReadyForReview", StringComparison.Ordinal))
        {
            var pull = _repos.Values.SelectMany(r => r.PullRequests.Select(p => (Repo: r, Pull: p))).Single(x => NodeId(x.Repo, x.Pull) == id).Pull;
            pull.Draft = false;
            return Data(new JsonObject { ["markPullRequestReadyForReview"] = new JsonObject { ["pullRequest"] = new JsonObject { ["isDraft"] = false } } });
        }

        var reviewNode = variables["review"]!.GetValue<string>();
        var reviewId = long.Parse(reviewNode["PRR_".Length..], CultureInfo.InvariantCulture);
        if (_repos.Values.SelectMany(r => r.PullRequests.Select(p => (Repo: r, Pull: p))).FirstOrDefault(x => x.Pull.Reviews.Any(r => r.Id == reviewId)) is not ({ } targetRepo, { } target))
        {
            return GraphQLError("NOT_FOUND", $"Could not resolve to a node with the global id of '{reviewNode}'.");
        }

        if (target.Reviews.Single(r => r.Id == reviewId).State != "PENDING")
        {
            return GraphQLError("UNPROCESSABLE", "The review is not pending.");
        }

        if (query.Contains("addPullRequestReviewThread", StringComparison.Ordinal))
        {
            var path = variables["path"]!.GetValue<string>();
            var line = variables["line"]!.GetValue<int>();
            var side = variables["side"]!.GetValue<string>();
            if (!InDiff(targetRepo, target, path, line, side))
            {
                return GraphQLError("UNPROCESSABLE", "pull_request_review_thread.line must be part of the diff");
            }

            var comment = NewComment(targetRepo, target, path, line, side, variables["startLine"]?.GetValue<int>(), variables["body"]!.GetValue<string>(), Login, reviewId, inReplyTo: null);
            target.Comments.Add(comment);
            return Data(new JsonObject { ["addPullRequestReviewThread"] = new JsonObject { ["thread"] = new JsonObject { ["id"] = $"PRRT_{comment.Id}" } } });
        }

        var replyTo = long.Parse(variables["replyTo"]!.GetValue<string>()["PRRC_".Length..], CultureInfo.InvariantCulture);
        var root = target.Comments.Single(c => c.Id == replyTo);
        var reply = new ReviewCommentRecord(NextId(), root.Path, root.Side, Login, variables["body"]!.GetValue<string>(), Tick(), reviewId, root.CommitId, root.DiffHunk)
        {
            Line = root.Line,
            OriginalLine = root.OriginalLine,
            InReplyTo = root.Id,
        };
        target.Comments.Add(reply);
        return Data(new JsonObject { ["addPullRequestReviewComment"] = new JsonObject { ["comment"] = new JsonObject { ["id"] = $"PRRC_{reply.Id}" } } });
    }

    private static HttpResponseMessage Data(JsonObject data) => Json(new JsonObject { ["data"] = data });

    private static HttpResponseMessage GraphQLError(string type, string message) =>
        Json(new JsonObject { ["errors"] = new JsonArray(new JsonObject { ["type"] = type, ["message"] = message }) });

    /// <summary>A list as one page: GitHub's later pages are empty because the fake's lists are short.</summary>
    private static HttpResponseMessage Page(Dictionary<string, string> query, IEnumerable<JsonNode> items) =>
        Json(query.TryGetValue("page", out var page) && page != "1" ? new JsonArray() : new JsonArray([.. items]));

    private ReviewCommentRecord NewComment(Repo repo, PullRequest pull, string path, int line, string side, int? startLine, string body, string author, long reviewId, long? inReplyTo)
    {
        var file = ChangedFiles(repo, pull).FirstOrDefault(f => f.Path == path);
        return new ReviewCommentRecord(NextId(), path, side, author, body, Tick(), reviewId, HeadSha(repo, pull), DiffHunk(file?.Patch, line, side))
        {
            Line = line,
            StartLine = startLine,
            OriginalLine = line,
            InReplyTo = inReplyTo,
        };
    }

    private static bool InDiff(Repo repo, PullRequest pull, string path, int line, string side) =>
        ChangedFiles(repo, pull).FirstOrDefault(f => f.Path == path) is { } file
        && DiffPatch.Parse(file.Patch).Contains(side == "LEFT" ? DiffSide.Left : DiffSide.Right, line);

    private static bool IsPending(PullRequest pull, ReviewCommentRecord comment) =>
        pull.Reviews.FirstOrDefault(r => r.Id == comment.ReviewId)?.State == "PENDING";

    private static string HeadSha(Repo repo, PullRequest pull) =>
        pull.MergedHead ?? repo.Branches.GetValueOrDefault(pull.Head) ?? string.Empty;

    /// <summary>The commit the head branch left the base at: the newest ancestor of the head that the base also has.</summary>
    private static string MergeBase(Repo repo, string baseSha, string headSha)
    {
        var baseAncestors = Ancestors(repo, baseSha);
        var pending = new Queue<string>([headSha]);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var sha))
        {
            if (baseAncestors.Contains(sha))
            {
                return sha;
            }

            if (seen.Add(sha) && repo.Commits.TryGetValue(sha, out var commit))
            {
                foreach (var parent in commit.Parents)
                {
                    pending.Enqueue(parent);
                }
            }
        }

        return baseSha;
    }

    /// <summary>The head's commits that the base lacks, oldest first.</summary>
    private static List<string> PullCommits(Repo repo, PullRequest pull)
    {
        var head = HeadSha(repo, pull);
        var stop = pull.MergedBase ?? MergeBase(repo, repo.Branches[pull.Base], head);
        var commits = new List<string>();
        for (var sha = head; sha != stop && repo.Commits.TryGetValue(sha, out var commit); sha = commit.Parents is [var parent, ..] ? parent : stop)
        {
            commits.Add(sha);
        }

        commits.Reverse();
        return commits;
    }

    /// <summary>The files the pull request changes, from the merge base to the head, with GitHub's patches.</summary>
    private static List<ChangedFile> ChangedFiles(Repo repo, PullRequest pull)
    {
        var head = HeadSha(repo, pull);
        if (head.Length == 0)
        {
            return [];
        }

        var mergeBase = pull.MergedBase ?? MergeBase(repo, repo.Branches[pull.Base], head);
        var before = repo.Trees[repo.Commits[mergeBase].Tree];
        var after = repo.Trees[repo.Commits[head].Tree];
        var files = new List<ChangedFile>();
        foreach (var path in before.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
        {
            before.TryGetValue(path, out var old);
            after.TryGetValue(path, out var current);
            if (old.Sha == current.Sha)
            {
                continue;
            }

            var oldText = old.Sha is null ? string.Empty : Encoding.UTF8.GetString(repo.Blobs[old.Sha]);
            var newText = current.Sha is null ? string.Empty : Encoding.UTF8.GetString(repo.Blobs[current.Sha]);
            var (patch, additions, deletions) = UnifiedPatch(oldText, newText);
            var status = old.Sha is null ? "added" : current.Sha is null ? "removed" : "modified";
            files.Add(new ChangedFile(path, status, additions, deletions, patch, current.Sha));
        }

        return files;
    }

    /// <summary>A unified diff with three lines of context, as GitHub's file patches have.</summary>
    private static (string Patch, int Additions, int Deletions) UnifiedPatch(string oldText, string newText)
    {
        const int Context = 3;
        var oldLines = LineDiff.SplitLines(oldText).Select(l => l.TrimEnd('\n').TrimEnd('\r')).ToList();
        var newLines = LineDiff.SplitLines(newText).Select(l => l.TrimEnd('\n').TrimEnd('\r')).ToList();
        var hunks = LineDiff.Compute(oldLines, newLines);
        var patch = new StringBuilder();
        var index = 0;
        while (index < hunks.Count)
        {
            // Changes closer than twice the context share one hunk.
            var last = index;
            while (last + 1 < hunks.Count && hunks[last + 1].OldStart - hunks[last].OldEnd <= 2 * Context)
            {
                last++;
            }

            var oldStart = Math.Max(0, hunks[index].OldStart - Context);
            var newStart = hunks[index].NewStart - (hunks[index].OldStart - oldStart);
            var oldEnd = Math.Min(oldLines.Count, hunks[last].OldEnd + Context);
            var newEnd = hunks[last].NewEnd + (oldEnd - hunks[last].OldEnd);
            patch.Append(CultureInfo.InvariantCulture, $"@@ -{(oldEnd > oldStart ? oldStart + 1 : oldStart)},{oldEnd - oldStart} +{(newEnd > newStart ? newStart + 1 : newStart)},{newEnd - newStart} @@\n");
            var o = oldStart;
            var n = newStart;
            for (var h = index; h <= last; h++)
            {
                for (; o < hunks[h].OldStart; o++, n++)
                {
                    patch.Append(' ').Append(oldLines[o]).Append('\n');
                }

                for (; o < hunks[h].OldEnd; o++)
                {
                    patch.Append('-').Append(oldLines[o]).Append('\n');
                }

                for (; n < hunks[h].NewEnd; n++)
                {
                    patch.Append('+').Append(newLines[n]).Append('\n');
                }
            }

            for (; o < oldEnd; o++, n++)
            {
                patch.Append(' ').Append(oldLines[o]).Append('\n');
            }

            index = last + 1;
        }

        return (patch.ToString().TrimEnd('\n'), hunks.Sum(h => h.NewLength), hunks.Sum(h => h.OldLength));
    }

    /// <summary>The part of a patch a comment shows: its hunk, up to the commented line.</summary>
    private static string DiffHunk(string? patch, int line, string side)
    {
        var lines = (patch ?? string.Empty).Split('\n');
        var header = 0;
        var parsed = DiffPatch.Parse(patch);
        var target = parsed.Lines.Select((l, i) => (Line: l, Index: i)).FirstOrDefault(x => side == "LEFT" ? x.Line.OldLine == line && x.Line.Kind != PatchLineKind.Added : x.Line.NewLine == line && x.Line.Kind != PatchLineKind.Removed);
        if (target.Line is null)
        {
            return $"@@ -{line},1 +{line},1 @@";
        }

        // Find the patch's text line for the parsed line: skip headers while counting body lines.
        var body = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("@@", StringComparison.Ordinal))
            {
                header = i;
                continue;
            }

            if (++body == target.Index)
            {
                return string.Join('\n', lines[header..(i + 1)]);
            }
        }

        return string.Join('\n', lines);
    }

    private static string Base64Lines(byte[] content)
    {
        var text = Convert.ToBase64String(content);
        var lines = new StringBuilder();
        for (var i = 0; i < text.Length; i += 60)
        {
            lines.Append(text, i, Math.Min(60, text.Length - i)).Append('\n');
        }

        return lines.ToString();
    }

    private static string NodeId(Repo repo, PullRequest pull) => $"PR_{repo.Name}_{pull.Number}";

    private static string ReviewState(string reviewEvent) => reviewEvent switch
    {
        "APPROVE" => "APPROVED",
        "REQUEST_CHANGES" => "CHANGES_REQUESTED",
        _ => "COMMENTED",
    };

    private DateTimeOffset Tick() => Epoch.AddMinutes(Interlocked.Increment(ref _minutes));

    private long NextId() => Interlocked.Increment(ref _nextId);

    private static JsonObject PullRequestJson(Repo repo, PullRequest pull, bool details)
    {
        var json = new JsonObject
        {
            ["number"] = pull.Number,
            ["node_id"] = NodeId(repo, pull),
            ["title"] = pull.Title,
            ["body"] = pull.Body,
            ["state"] = pull.State,
            ["draft"] = pull.Draft,
            ["user"] = User(pull.Author),
            ["head"] = new JsonObject { ["ref"] = pull.Head, ["sha"] = HeadSha(repo, pull), ["repo"] = new JsonObject { ["full_name"] = $"{repo.Owner}/{repo.Name}" } },
            ["base"] = new JsonObject { ["ref"] = pull.Base, ["sha"] = repo.Branches.GetValueOrDefault(pull.Base), ["repo"] = new JsonObject { ["full_name"] = $"{repo.Owner}/{repo.Name}" } },
            ["created_at"] = pull.Created,
            ["updated_at"] = pull.Updated,
            ["merged_at"] = pull.MergedAt,
            ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/pull/{pull.Number}",
            ["requested_reviewers"] = new JsonArray([.. pull.RequestedReviewers.Select(r => (JsonNode)User(r))]),
        };

        if (details)
        {
            var files = ChangedFiles(repo, pull);
            json["mergeable"] = pull.State == "open" ? pull.Mergeable : null;
            json["mergeable_state"] = pull.Draft ? "draft" : pull.MergeableState;
            json["merged"] = pull.MergedSha is not null;
            json["additions"] = files.Sum(f => f.Additions);
            json["deletions"] = files.Sum(f => f.Deletions);
            json["changed_files"] = files.Count;
            json["commits"] = PullCommits(repo, pull).Count;
            json["merge_commit_sha"] = pull.MergedSha;
            json["merged_by"] = pull.MergedSha is null ? null : User(Login);
        }

        return json;
    }

    private static JsonObject FileJson(ChangedFile file) => new()
    {
        ["filename"] = file.Path,
        ["status"] = file.Status,
        ["additions"] = file.Additions,
        ["deletions"] = file.Deletions,
        ["changes"] = file.Additions + file.Deletions,
        ["patch"] = file.Patch,
        ["sha"] = file.Sha,
    };

    private static JsonObject CommentJson(Repo repo, PullRequest pull, ReviewCommentRecord comment) => new()
    {
        ["id"] = comment.Id,
        ["node_id"] = $"PRRC_{comment.Id}",
        ["path"] = comment.Path,
        ["line"] = comment.Line,
        ["side"] = comment.Side,
        ["start_line"] = comment.StartLine,
        ["start_side"] = comment.StartLine is null ? null : comment.Side,
        ["original_line"] = comment.OriginalLine,
        ["original_start_line"] = comment.StartLine,
        ["in_reply_to_id"] = comment.InReplyTo,
        ["user"] = User(comment.Author),
        ["body"] = comment.Body,
        ["created_at"] = comment.Created,
        ["updated_at"] = comment.Created,
        ["diff_hunk"] = comment.DiffHunk,
        ["commit_id"] = comment.CommitId,
        ["original_commit_id"] = comment.CommitId,
        ["pull_request_review_id"] = comment.ReviewId,
        ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/pull/{pull.Number}#discussion_r{comment.Id}",
    };

    private static JsonObject ReviewJson(Repo repo, PullRequest pull, ReviewRecord review) => new()
    {
        ["id"] = review.Id,
        ["node_id"] = $"PRR_{review.Id}",
        ["user"] = User(review.Author),
        ["state"] = review.State,
        ["body"] = review.Body ?? string.Empty,
        ["submitted_at"] = review.Submitted,
        ["commit_id"] = review.CommitId,
        ["html_url"] = $"https://github.test/{repo.Owner}/{repo.Name}/pull/{pull.Number}#pullrequestreview-{review.Id}",
    };

    private static JsonObject IssueCommentJson(IssueCommentRecord comment) => new()
    {
        ["id"] = comment.Id,
        ["user"] = User(comment.Author),
        ["body"] = comment.Body,
        ["created_at"] = comment.Created,
        ["html_url"] = $"https://github.test/comments/{comment.Id}",
    };

    private static JsonObject User(string login) => new() { ["login"] = login };

    private static JsonObject WithMergeSettings(Repo repo, JsonObject json)
    {
        json["allow_merge_commit"] = repo.Settings.AllowMerge;
        json["allow_squash_merge"] = repo.Settings.AllowSquash;
        json["allow_rebase_merge"] = repo.Settings.AllowRebase;
        json["delete_branch_on_merge"] = repo.Settings.DeleteBranchOnMerge;
        return json;
    }

    private static FakeReviewComment ToFakeComment(ReviewCommentRecord c) => new(c.Id, c.Path, c.Line, c.Side, c.StartLine, c.Body, c.InReplyTo, c.Author, c.ReviewId);

    /// <summary>A review as tests see it.</summary>
    internal sealed record FakeReview(long Id, string State, string? Body, string Author, IReadOnlyList<FakeReviewComment> Comments);

    /// <summary>A comment on the diff as tests see it.</summary>
    internal sealed record FakeReviewComment(long Id, string Path, int? Line, string Side, int? StartLine, string Body, long? InReplyTo, string Author, long? ReviewId);

    private sealed record ChangedFile(string Path, string Status, int Additions, int Deletions, string Patch, string? Sha);

    private sealed record MergeSettingsRecord(bool AllowMerge, bool AllowSquash, bool AllowRebase, bool DeleteBranchOnMerge);

    private sealed class PullRequest(int number, string title, string head, string baseBranch, bool draft, string author, DateTimeOffset created)
    {
        public int Number { get; } = number;

        public string Title { get; } = title;

        public string Head { get; } = head;

        public string Base { get; } = baseBranch;

        public bool Draft { get; set; } = draft;

        public string Author { get; } = author;

        public string? Body { get; init; }

        public string State { get; set; } = "open";

        public DateTimeOffset Created { get; } = created;

        public DateTimeOffset Updated => new[] { Created, MergedAt ?? Created }.Concat(Comments.Select(c => c.Created)).Concat(Conversation.Select(c => c.Created)).Max();

        public DateTimeOffset? MergedAt { get; set; }

        public string? MergedSha { get; set; }

        public string? MergedHead { get; set; }

        public string? MergedBase { get; set; }

        public string? MergeMethod { get; set; }

        public bool? Mergeable { get; set; } = true;

        public string MergeableState { get; set; } = "clean";

        public List<string> RequestedReviewers { get; } = [];

        public List<ReviewCommentRecord> Comments { get; } = [];

        public List<ReviewRecord> Reviews { get; } = [];

        public List<IssueCommentRecord> Conversation { get; } = [];
    }

    private sealed class ReviewCommentRecord(long id, string path, string side, string author, string body, DateTimeOffset created, long? reviewId, string commitId, string diffHunk)
    {
        public long Id { get; } = id;

        public string Path { get; } = path;

        public string Side { get; } = side;

        public string Author { get; } = author;

        public string Body { get; set; } = body;

        public DateTimeOffset Created { get; } = created;

        public long? ReviewId { get; } = reviewId;

        public string CommitId { get; } = commitId;

        public string DiffHunk { get; } = diffHunk;

        public int? Line { get; set; }

        public int? StartLine { get; set; }

        public int? OriginalLine { get; init; }

        public long? InReplyTo { get; init; }

        public bool Resolved { get; set; }
    }

    private sealed class ReviewRecord(long id, string author, string state, string? body, string commitId, DateTimeOffset? submitted)
    {
        public long Id { get; } = id;

        public string Author { get; } = author;

        public string State { get; set; } = state;

        public string? Body { get; set; } = body;

        public string CommitId { get; } = commitId;

        public DateTimeOffset? Submitted { get; set; } = submitted;
    }

    private sealed record IssueCommentRecord(long Id, string Author, string Body, DateTimeOffset Created);
}
