using System.Net;
using System.Text;
using TabStudio.SourceControl.GitHub;
using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

/// <summary>Pull requests, reviews and merging through <see cref="GitHubClient"/>, against <see cref="FakeGitHub"/>.</summary>
public sealed class PullRequestClientTests : IDisposable
{
    private const string Repo = "demo";
    private const string Owner = FakeGitHub.Login;
    private const string Branch = "feature/shout";
    private const string Program = "src/Program.cs";

    private readonly FakeGitHub _github = new();
    private readonly GitHubClient _client;
    private readonly int _number;

    public PullRequestClientTests()
    {
        _github.AddRepository(Repo, new Dictionary<string, string>
        {
            ["README.md"] = "# Demo\n",
            [Program] = "Console.WriteLine(\"one\");\nConsole.WriteLine(\"two\");\nConsole.WriteLine(\"three\");\n",
        });
        _client = _github.CreateClient();
        _client.CreateBranchAsync(Owner, Repo, Branch, _github.Head(Repo, "main")).GetAwaiter().GetResult();
        _github.Push(Repo, Branch, "Shout the second line", new Dictionary<string, string?>
        {
            [Program] = "Console.WriteLine(\"one\");\nConsole.WriteLine(\"TWO\");\nConsole.WriteLine(\"three\");\n",
            ["src/Loud.cs"] = "class Loud;\n",
        });
        _number = _github.AddPullRequest(Repo, "Shout", Branch, body: "Makes the second line **loud**.", reviewers: [FakeGitHub.Login]);
    }

    [Fact]
    public async Task Lists_pull_requests_with_their_branches_author_and_reviewers()
    {
        _github.AddPullRequest(Repo, "Mine", "main", "main", author: FakeGitHub.Login);

        var open = await _client.GetPullRequestsAsync(Owner, Repo);
        var shout = open.Single(p => p.Number == _number);

        Assert.Equal(2, open.Count);
        Assert.Equal("Shout", shout.Title);
        Assert.Equal(FakeGitHub.Reviewer, shout.Author);
        Assert.Equal(Branch, shout.Head.Name);
        Assert.Equal("main", shout.Base.Name);
        Assert.Equal(_github.Head(Repo, Branch), shout.Head.Sha);
        Assert.Equal(PullRequestState.Open, shout.State);
        Assert.Equal(new[] { FakeGitHub.Login }, shout.RequestedReviewers);
        Assert.False(shout.IsFromFork(Owner, Repo));
        Assert.Empty(await _client.GetPullRequestsAsync(Owner, Repo, PullRequestStateFilter.Closed));
        Assert.Single(await _client.GetPullRequestsAsync(Owner, Repo, headBranch: Branch));
    }

    [Fact]
    public async Task Reads_the_details_files_and_commits()
    {
        var details = await _client.GetPullRequestAsync(Owner, Repo, _number);
        var files = await _client.GetPullRequestFilesAsync(Owner, Repo, _number);
        var commits = await _client.GetPullRequestCommitsAsync(Owner, Repo, _number);

        Assert.Equal("Makes the second line **loud**.", details.Info.Body);
        Assert.Equal(MergeableState.Clean, details.Mergeable);
        Assert.True(details.IsMergeable);
        Assert.Equal((2, 1, 2, 1), (details.Additions, details.Deletions, details.ChangedFiles, details.Commits));

        Assert.Equal(new[] { "src/Loud.cs", Program }, files.Select(f => f.Path));
        Assert.Equal(PullRequestFileStatus.Added, files[0].Status);
        Assert.Equal("A", files[0].StatusLetter);
        Assert.Equal("M", files[1].StatusLetter);
        Assert.Equal((1, 1), (files[1].Additions, files[1].Deletions));
        Assert.Contains("+Console.WriteLine(\"TWO\");", files[1].Patch, StringComparison.Ordinal);

        Assert.Equal("Shout the second line", Assert.Single(commits).Summary);
    }

    [Fact]
    public async Task Reads_both_ends_of_a_file_from_the_merge_base_and_the_head()
    {
        _github.Push(Repo, "main", "Change the readme", new Dictionary<string, string?> { ["README.md"] = "# Changed\n" });
        var pull = (await _client.GetPullRequestAsync(Owner, Repo, _number)).Info;

        var mergeBase = await _client.GetMergeBaseAsync(Owner, Repo, pull.Base.Sha, pull.Head.Sha);
        var before = await _client.GetFileContentAsync(Owner, Repo, Program, mergeBase);
        var after = await _client.GetFileContentAsync(Owner, Repo, Program, pull.Head.Sha);

        Assert.NotEqual(pull.Base.Sha, mergeBase);
        Assert.Contains("\"two\"", Encoding.UTF8.GetString(before!), StringComparison.Ordinal);
        Assert.Contains("\"TWO\"", Encoding.UTF8.GetString(after!), StringComparison.Ordinal);
        Assert.Null(await _client.GetFileContentAsync(Owner, Repo, "src/Loud.cs", mergeBase));
    }

    [Fact]
    public async Task Conversation_comments_are_listed_added_and_counted()
    {
        _github.AddIssueComment(Repo, _number, "Looks fine to me.");

        await _client.CreateIssueCommentAsync(Owner, Repo, _number, "Thanks!");
        var comments = await _client.GetIssueCommentsAsync(Owner, Repo, _number);
        var counts = await _client.GetPullRequestCommentCountsAsync(Owner, Repo, PullRequestStateFilter.Open);

        Assert.Equal(new[] { "Looks fine to me.", "Thanks!" }, comments.Select(c => c.Body));
        Assert.Equal(new[] { FakeGitHub.Reviewer, FakeGitHub.Login }, comments.Select(c => c.Author));
        Assert.Equal(2, counts[_number]);
    }

    [Fact]
    public async Task A_pending_review_holds_its_comments_until_it_is_submitted()
    {
        var pending = await _client.CreateReviewAsync(Owner, Repo, _number, null, null, null, [new NewReviewComment(Program, 2, DiffSide.Right, "Why so loud?")]);
        await _client.AddPendingReviewCommentAsync(pending.NodeId, new NewReviewComment(Program, 3, DiffSide.Right, "And this one?", StartLine: 1, StartSide: DiffSide.Right));

        Assert.Equal(ReviewState.Pending, pending.State);
        Assert.Empty(await _client.GetReviewCommentsAsync(Owner, Repo, _number));
        var held = await _client.GetReviewCommentsOfReviewAsync(Owner, Repo, _number, pending.Id);
        Assert.Equal(new[] { "Why so loud?", "And this one?" }, held.Select(c => c.Body));
        Assert.Equal(((int?)3, (int?)1), (held[1].Line, held[1].StartLine));

        var submitted = await _client.SubmitReviewAsync(Owner, Repo, _number, pending.Id, ReviewEvent.Approve, "Fine, ship it.");

        Assert.Equal(ReviewState.Approved, submitted.State);
        Assert.Equal(2, (await _client.GetReviewCommentsAsync(Owner, Repo, _number)).Count);
        var review = Assert.Single(_github.Reviews(Repo, _number));
        Assert.Equal(("APPROVED", "Fine, ship it."), (review.State, review.Body));
        Assert.Equal(2, review.Comments.Count);
    }

    [Fact]
    public async Task A_pending_review_can_be_discarded_and_comments_must_be_on_the_diff()
    {
        var pending = await _client.CreateReviewAsync(Owner, Repo, _number, null, null, null, [new NewReviewComment(Program, 1, DiffSide.Left, "Context on the left")]);
        var refused = await Assert.ThrowsAsync<GitHubApiException>(() => _client.CreateReviewAsync(Owner, Repo, _number, null, "Again", ReviewEvent.Comment, [new NewReviewComment("README.md", 1, DiffSide.Right, "Not changed")]));

        await _client.DeletePendingReviewAsync(Owner, Repo, _number, pending.Id);

        Assert.True(refused.IsRejected);
        Assert.Empty(_github.Reviews(Repo, _number));
        Assert.Empty(_github.ReviewComments(Repo, _number));
    }

    [Fact]
    public async Task Approving_your_own_pull_request_is_refused()
    {
        var mine = _github.AddPullRequest(Repo, "Mine", Branch, author: FakeGitHub.Login);

        var error = await Assert.ThrowsAsync<GitHubApiException>(() => _client.CreateReviewAsync(Owner, Repo, mine, null, null, ReviewEvent.Approve, []));

        Assert.True(error.IsRejected);
        Assert.Contains("own pull request", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Replies_join_their_thread_and_comments_can_be_edited_and_deleted()
    {
        var root = _github.AddReviewComment(Repo, _number, Program, 2, "Shouting?");

        var reply = await _client.ReplyToReviewCommentAsync(Owner, Repo, _number, root, "On purpose.");
        var edited = await _client.UpdateReviewCommentAsync(Owner, Repo, reply.Id, "On purpose, yes.");
        var thread = Assert.Single(ReviewThread.Group(await _client.GetReviewCommentsAsync(Owner, Repo, _number)));

        Assert.Equal(root, reply.InReplyToId);
        Assert.Equal("On purpose, yes.", edited.Body);
        Assert.Equal(root, thread.Root.Id);
        Assert.Equal((Program, (int?)2, DiffSide.Right), (thread.Path, thread.Line, thread.Side));
        Assert.Equal("Console.WriteLine(\"TWO\");", thread.Root.CommentedLineText);
        Assert.Equal(FakeGitHub.Login, Assert.Single(thread.Replies).Author);

        await _client.DeleteReviewCommentAsync(Owner, Repo, reply.Id);
        Assert.Single(_github.ReviewComments(Repo, _number));
    }

    [Fact]
    public async Task Threads_are_resolved_through_GraphQL()
    {
        var root = _github.AddReviewComment(Repo, _number, Program, 2, "Shouting?");

        var before = await _client.GetReviewThreadStatesAsync(Owner, Repo, _number);
        await _client.SetReviewThreadResolvedAsync(before[root].ThreadId, resolved: true);
        var after = await _client.GetReviewThreadStatesAsync(Owner, Repo, _number);

        Assert.False(before[root].IsResolved);
        Assert.True(before[root].CanResolve);
        Assert.True(after[root].IsResolved);
        Assert.True(_github.IsResolved(Repo, _number, root));
    }

    [Fact]
    public async Task A_pending_reply_waits_in_the_review()
    {
        var root = _github.AddReviewComment(Repo, _number, Program, 2, "Shouting?");
        var rootNode = (await _client.GetReviewCommentsAsync(Owner, Repo, _number)).Single().NodeId;
        var pending = await _client.CreateReviewAsync(Owner, Repo, _number, null, null, null, [new NewReviewComment(Program, 3, DiffSide.Right, "Also here")]);

        await _client.AddPendingReplyAsync(pending.NodeId, rootNode, "Agreed.");

        Assert.Single(await _client.GetReviewCommentsAsync(Owner, Repo, _number));
        var held = await _client.GetReviewCommentsOfReviewAsync(Owner, Repo, _number, pending.Id);
        Assert.Equal(root, held.Single(c => c.Body == "Agreed.").InReplyToId);
    }

    [Fact]
    public async Task Squash_merging_puts_the_changes_on_the_base_and_the_branch_can_be_deleted()
    {
        _github.SetMergeSettings(Repo, allowMerge: false, allowSquash: true, allowRebase: false);
        var settings = await _client.GetMergeSettingsAsync(Owner, Repo);
        var pull = (await _client.GetPullRequestAsync(Owner, Repo, _number)).Info;

        var result = await _client.MergePullRequestAsync(Owner, Repo, _number, MergeMethod.Squash, "Shout (#1)", "Loud.", pull.Head.Sha);
        await _client.DeleteBranchAsync(Owner, Repo, Branch);
        var merged = await _client.GetPullRequestAsync(Owner, Repo, _number);

        Assert.Equal(new[] { MergeMethod.Squash }, settings.AllowedMethods);
        Assert.Equal(_github.Head(Repo, "main"), result.Sha);
        Assert.Equal("Shout (#1)\n\nLoud.", _github.HeadMessage(Repo, "main"));
        Assert.Contains("\"TWO\"", _github.Files(Repo, "main")[Program], StringComparison.Ordinal);
        Assert.Equal((true, "squash", "closed"), _github.MergeState(Repo, _number));
        Assert.False(_github.HasBranch(Repo, Branch));
        Assert.Equal(PullRequestState.Merged, merged.Info.State);
        Assert.Equal(result.Sha, merged.MergeCommitSha);
    }

    [Fact]
    public async Task GitHub_refuses_to_merge_conflicts_a_moved_head_and_methods_the_repository_does_not_allow()
    {
        _github.SetMergeSettings(Repo, allowMerge: true, allowSquash: false, allowRebase: true);
        var squash = await Assert.ThrowsAsync<GitHubApiException>(() => _client.MergePullRequestAsync(Owner, Repo, _number, MergeMethod.Squash, null, null, null));
        var moved = await Assert.ThrowsAsync<GitHubApiException>(() => _client.MergePullRequestAsync(Owner, Repo, _number, MergeMethod.Merge, null, null, new string('0', 40)));
        _github.SetMergeable(Repo, _number, false, "dirty");
        var conflicts = await Assert.ThrowsAsync<GitHubApiException>(() => _client.MergePullRequestAsync(Owner, Repo, _number, MergeMethod.Merge, null, null, null));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, squash.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, moved.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, conflicts.StatusCode);
        Assert.Equal(MergeableState.Conflicts, (await _client.GetPullRequestAsync(Owner, Repo, _number)).Mergeable);
    }

    [Fact]
    public async Task A_draft_becomes_ready_for_review_and_reviewers_can_be_requested()
    {
        var draft = _github.AddPullRequest(Repo, "Draft", Branch, draft: true);
        var info = (await _client.GetPullRequestAsync(Owner, Repo, draft)).Info;

        await _client.MarkReadyForReviewAsync(info.NodeId);
        var requested = await _client.RequestReviewersAsync(Owner, Repo, draft, ["hubot"]);

        Assert.True(info.IsDraft);
        Assert.False(_github.IsDraft(Repo, draft));
        Assert.Equal(new[] { "hubot" }, requested.RequestedReviewers);
    }

    [Fact]
    public async Task GraphQL_errors_become_exceptions()
    {
        var error = await Assert.ThrowsAsync<GitHubApiException>(() => _client.AddPendingReviewCommentAsync("PRR_1", new NewReviewComment(Program, 2, DiffSide.Right, "Late")));

        Assert.True(error.IsNotFound);
        Assert.Contains("PRR_1", error.Message, StringComparison.Ordinal);
    }

    public void Dispose() => _github.Dispose();
}

/// <summary>Checking out a pull request's branch into a linked folder.</summary>
public sealed class PullRequestCheckOutTests : IDisposable
{
    private const string Repo = "demo";
    private const string Branch = "feature/shout";

    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();

    public PullRequestCheckOutTests()
    {
        _github.AddRepository(Repo, new Dictionary<string, string> { ["src/Program.cs"] = "Console.WriteLine(\"one\");\n" });
        _github.CreateClient().CreateBranchAsync(FakeGitHub.Login, Repo, Branch, _github.Head(Repo, "main")).GetAwaiter().GetResult();
        _github.Push(Repo, Branch, "Shout", new Dictionary<string, string?> { ["src/Program.cs"] = "Console.WriteLine(\"ONE\");\n" });
        _github.AddPullRequest(Repo, "Shout", Branch);
    }

    private string Folder => Path.Combine(_temp.Path, "work");

    [Fact]
    public async Task Checking_out_switches_the_folder_to_the_pull_request_branch()
    {
        var client = _github.CreateClient();
        var sync = await GitHubSync.CloneAsync(client, FakeGitHub.Login, Repo, null, Folder);
        var pull = (await client.GetPullRequestsAsync(FakeGitHub.Login, Repo)).Single();

        await sync.CheckOutPullRequestAsync(pull);

        Assert.Equal(Branch, sync.Link.Branch);
        Assert.Equal("Console.WriteLine(\"ONE\");\n", await File.ReadAllTextAsync(Path.Combine(Folder, "src", "Program.cs")));
    }

    [Fact]
    public async Task Uncommitted_changes_and_forks_are_refused_politely()
    {
        var client = _github.CreateClient();
        var sync = await GitHubSync.CloneAsync(client, FakeGitHub.Login, Repo, null, Folder);
        var pull = (await client.GetPullRequestsAsync(FakeGitHub.Login, Repo)).Single();
        await File.WriteAllTextAsync(Path.Combine(Folder, "src", "Program.cs"), "// mine\n");

        var changes = await Assert.ThrowsAsync<SyncException>(() => sync.CheckOutPullRequestAsync(pull));
        var fork = await Assert.ThrowsAsync<SyncException>(() => sync.CheckOutPullRequestAsync(pull with { Head = pull.Head with { RepositoryFullName = "someone/demo" } }));

        Assert.Equal(SyncFailure.UncommittedChanges, changes.Failure);
        Assert.Contains("Commit or undo them", changes.Message, StringComparison.Ordinal);
        Assert.Contains("the fork someone/demo", fork.Message, StringComparison.Ordinal);
        Assert.Equal("main", sync.Link.Branch);
    }

    public void Dispose()
    {
        _github.Dispose();
        _temp.Dispose();
    }
}
