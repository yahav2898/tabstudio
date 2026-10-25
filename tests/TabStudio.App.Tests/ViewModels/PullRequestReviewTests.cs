using System.Text.Json.Nodes;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services.Agent;
using TabStudio.App.ViewModels.PullRequests;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Pull request review against <see cref="FakeGitHub"/>: the Pull Requests window, a pull request's document, comments
/// on its diff, finishing a review, replies, merging, checking out, the editor's threads and Claude's drafts.
/// </summary>
public sealed class PullRequestReviewTests
{
    private const string Greeter = PullRequestScenario.GreeterPath;

    [AvaloniaFact]
    public async Task The_Pull_Requests_window_lists_them_by_filter()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var list = scenario.Workspace.PullRequests;

        scenario.Workspace.ShowPullRequestsCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => list.Items.Count == 2 && !list.IsLoading, because: "the open pull requests");

        Assert.True(scenario.Workspace.IsPullRequestsSelected);
        Assert.Equal("tester/hello", list.RepositoryName);
        var friendly = list.Items.Single(i => i.Number == 1);
        Assert.Equal(PullRequestScenario.Title, friendly.Title);
        Assert.Equal("#1", friendly.NumberText);
        Assert.Equal(FakeGitHub.Reviewer, friendly.Author);
        Assert.Equal($"{PullRequestScenario.Branch} → main", friendly.BranchText);
        Assert.StartsWith("opened ", friendly.Age, StringComparison.Ordinal);
        Assert.True(friendly.IsReviewRequested);
        Assert.Equal(1, friendly.CommentCount);
        Assert.False(friendly.IsDraft);
        var draft = list.Items.Single(i => i.Number == 2);
        Assert.True(draft.IsDraft);
        Assert.False(draft.HasComments);

        await list.SelectFilterCommand.ExecuteAsync(PullRequestFilter.Mine);
        Assert.Equal(2, Assert.Single(list.Items).Number);

        await list.SelectFilterCommand.ExecuteAsync(PullRequestFilter.ReviewRequested);
        Assert.Equal(1, Assert.Single(list.Items).Number);

        await list.SelectFilterCommand.ExecuteAsync(PullRequestFilter.Closed);
        Assert.Empty(list.Items);
        Assert.True(list.IsEmpty);
        Assert.Equal("No pull requests were merged or closed yet.", list.EmptyText);
    }

    [AvaloniaFact]
    public async Task A_pull_request_opens_in_a_document_of_its_own()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var list = scenario.Workspace.PullRequests;
        await list.LoadAsync();

        await list.OpenCommand.ExecuteAsync(list.Items.Single(i => i.Number == 1));
        var document = scenario.Workspace.ActiveDocument!;
        Assert.True(document.IsPullRequest);
        var pullRequest = document.PullRequest!;
        await UiTestHelpers.WaitUntilAsync(() => pullRequest.IsLoaded, because: "the pull request to load");

        Assert.Equal("Pull Request #1", document.FileName);
        Assert.Equal(PullRequestScenario.Title, pullRequest.Title);
        Assert.Equal(PullRequestBadge.Open, pullRequest.Badge);
        Assert.Equal($"octocat wants to merge 1 commit into main from {PullRequestScenario.Branch}", pullRequest.SummaryText);
        Assert.Equal(MergeabilityKind.Good, pullRequest.MergeabilityKind);
        Assert.True(pullRequest.HasDescription);
        Assert.False(pullRequest.IsMine);
        Assert.True(pullRequest.CanApprove);

        Assert.Contains(pullRequest.Reviewers, r => r.Login == "hubot" && r.State == ReviewerState.Approved);
        Assert.Contains(pullRequest.Reviewers, r => r.Login == PullRequestScenario.Colleague && r.State == ReviewerState.Commented);
        Assert.Contains(pullRequest.Reviewers, r => r.Login == FakeGitHub.Login && r.State == ReviewerState.Requested);

        Assert.Equal(new[] { TimelineKind.Opened, TimelineKind.Comment, TimelineKind.Approved }, pullRequest.Timeline.Select(t => t.Kind));
        Assert.Equal("Conversation (2)", pullRequest.ConversationTabText);

        var file = Assert.Single(pullRequest.Files);
        Assert.Equal(Greeter, file.Path);
        Assert.Equal("M", file.StatusLetter);
        Assert.Equal(1, file.ThreadCount);
        Assert.Equal("Files changed (1)", pullRequest.FilesTabText);
        Assert.Equal("Greet friends and say goodbye", Assert.Single(pullRequest.Commits).Summary);

        var thread = Assert.Single(pullRequest.Threads);
        Assert.Equal(PullRequestScenario.ThreadLine, thread.Line);
        Assert.Equal(new[] { PullRequestScenario.Colleague, FakeGitHub.Reviewer }, thread.Comments.Select(c => c.Author));

        // Opening it again shows the same tab.
        await scenario.Workspace.ShowPullRequestAsync(1);
        Assert.Single(scenario.Workspace.Documents, d => d.IsPullRequest);
        Assert.Same(document, scenario.Workspace.ActiveDocument);
    }

    [AvaloniaFact]
    public async Task A_files_diff_puts_each_conversation_under_its_line()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var pullRequest = await scenario.OpenPullRequestAsync(1);

        var review = await scenario.OpenFileAsync(pullRequest, Greeter);

        var document = scenario.Workspace.ActiveDocument!;
        Assert.True(document.IsComparison);
        Assert.Equal("Greeter.cs (#1)", document.Comparison!.Title);
        var anchor = Assert.Single(review.Anchors);
        Assert.Equal(scenario.DiffLineOf(PullRequestScenario.ThreadLine), anchor.Line);
        Assert.True(anchor.IsExpanded);
        Assert.Same(Assert.Single(pullRequest.Threads), Assert.Single(anchor.Items));
        Assert.Equal("1 conversation", review.ThreadsText);
        Assert.True(review.CanCommentOn(scenario.DiffLineOf(PullRequestScenario.FarewellLine)));
        Assert.False(review.CanCommentOn(1));
        Assert.NotNull(review.StartComment(1, 1));
    }

    [AvaloniaFact]
    public async Task Comments_wait_in_a_pending_review_until_it_is_finished_as_an_approval()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        var review = await scenario.OpenFileAsync(pullRequest, Greeter);

        var farewell = scenario.DiffLineOf(PullRequestScenario.FarewellLine);
        Assert.Null(review.StartComment(farewell, farewell));
        var first = Composer(review);
        Assert.Equal("Start a Review", first.SubmitText);
        Assert.Equal($"Comment on line {PullRequestScenario.FarewellLine}", first.LocationText);
        first.Text = "Could the farewell use the part of the day too?";
        await first.SubmitCommand.ExecuteAsync(null);

        Assert.Null(first.ErrorText);
        Assert.True(pullRequest.HasPendingReview);
        Assert.Equal(1, pullRequest.PendingCount);
        var pending = Assert.Single(pullRequest.Threads, t => t.IsPending);
        Assert.Equal(PullRequestScenario.FarewellLine, pending.Line);
        var stored = Assert.Single(scenario.GitHub.Reviews(PullRequestScenario.Repository, 1), r => r.Author == FakeGitHub.Login);
        Assert.Equal("PENDING", stored.State);

        // A range on the new side of the diff goes in the same review.
        Assert.Null(review.StartComment(scenario.DiffLineOf(PullRequestScenario.GreetingLine - 2), scenario.DiffLineOf(PullRequestScenario.GreetingLine)));
        var second = Composer(review);
        Assert.Equal("Add Review Comment", second.SubmitText);
        Assert.Equal($"Comment on lines {PullRequestScenario.GreetingLine - 2}–{PullRequestScenario.GreetingLine}", second.LocationText);
        second.Text = "Nice and short.";
        await second.SubmitCommand.ExecuteAsync(null);
        Assert.Equal(2, pullRequest.PendingCount);
        Assert.Equal(2, pullRequest.Files.Single().PendingCount);

        review.FinishReviewCommand.Execute(null);
        Assert.Same(pullRequest, scenario.Workspace.ActiveDocument?.PullRequest);
        Assert.True(pullRequest.IsFinishReviewOpen);
        Assert.Equal("Your 2 pending comments are sent with the review.", pullRequest.FinishReviewNote);
        pullRequest.ReviewSummary = "Thanks, this reads much better.";
        await pullRequest.SubmitReviewCommand.ExecuteAsync(ReviewEvent.Approve);

        Assert.Null(pullRequest.ReviewErrorText);
        Assert.False(pullRequest.IsFinishReviewOpen);
        Assert.False(pullRequest.HasPendingReview);
        Assert.Equal(0, pullRequest.PendingCount);
        Assert.Equal("Approved pull request #1.", pullRequest.InfoText);
        var approved = Assert.Single(scenario.GitHub.Reviews(PullRequestScenario.Repository, 1), r => r.Author == FakeGitHub.Login);
        Assert.Equal("APPROVED", approved.State);
        Assert.Equal("Thanks, this reads much better.", approved.Body);
        Assert.Equal(new[] { "Could the farewell use the part of the day too?", "Nice and short." }, approved.Comments.Select(c => c.Body).Order(StringComparer.Ordinal));
        Assert.Equal((int?)(PullRequestScenario.GreetingLine - 2), approved.Comments.Single(c => c.Body == "Nice and short.").StartLine);
        Assert.Contains(pullRequest.Reviewers, r => r.Login == FakeGitHub.Login && r.State == ReviewerState.Approved);
        Assert.Contains(pullRequest.Timeline, t => t.Kind == TimelineKind.Approved && t.Author == FakeGitHub.Login && t.Note == "2 comments on files");
    }

    [AvaloniaFact]
    public async Task A_reply_is_sent_at_once_or_joins_the_pending_review()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        var thread = Assert.Single(pullRequest.Threads);

        thread.StartReplyCommand.Execute(null);
        Assert.True(thread.IsReplying);
        Assert.Equal("Reply", thread.SendReplyText);
        thread.ReplyText = "A follow-up works for me.";
        await thread.SendReplyCommand.ExecuteAsync(null);

        Assert.Null(thread.ErrorText);
        Assert.False(thread.IsReplying);
        var reply = Assert.Single(scenario.GitHub.ReviewComments(PullRequestScenario.Repository, 1), c => c.Author == FakeGitHub.Login);
        Assert.Equal(scenario.ThreadId, reply.InReplyTo);
        Assert.Equal("A follow-up works for me.", reply.Body);
        Assert.Equal(3, Assert.Single(pullRequest.Threads).Comments.Count);
        Assert.False(pullRequest.HasPendingReview);

        // With a review in progress, a reply waits in it, as on GitHub.
        var review = await scenario.OpenFileAsync(pullRequest, Greeter);
        var farewell = scenario.DiffLineOf(PullRequestScenario.FarewellLine);
        review.StartComment(farewell, farewell);
        var comment = Composer(review);
        comment.Text = "Maybe \"Goodbye\"?";
        await comment.SubmitCommand.ExecuteAsync(null);
        var published = pullRequest.Threads.Single(t => !t.IsPending);
        published.StartReplyCommand.Execute(null);
        Assert.Equal("Add to Review", published.SendReplyText);
        published.ReplyText = "Also: a setting would need a default.";
        await published.SendReplyCommand.ExecuteAsync(null);

        Assert.Equal(2, pullRequest.PendingCount);
        var pendingReview = Assert.Single(scenario.GitHub.Reviews(PullRequestScenario.Repository, 1), r => r.Author == FakeGitHub.Login && r.State == "PENDING");
        Assert.Contains(pendingReview.Comments, c => c.InReplyTo == scenario.ThreadId && c.Body == "Also: a setting would need a default.");
        Assert.Contains(pullRequest.Threads.Single(t => t.Thread.Root.Id == scenario.ThreadId).Comments, c => c.IsPending);
    }

    [AvaloniaFact]
    public async Task Squash_and_merge_deletes_the_branch()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        scenario.GitHub.SetMergeSettings(PullRequestScenario.Repository, allowMerge: false, allowSquash: true, allowRebase: true);
        var pullRequest = await scenario.OpenPullRequestAsync(1);

        Assert.Equal(new[] { MergeMethod.Squash, MergeMethod.Rebase }, pullRequest.MergeMethods.Select(m => m.Method));
        Assert.Equal(MergeMethod.Squash, pullRequest.SelectedMergeMethod?.Method);
        Assert.Equal("Squash and Merge", pullRequest.MergeButtonText);
        Assert.Equal("Greet friends and say goodbye (#1)", pullRequest.MergeTitle);
        Assert.True(pullRequest.CanDeleteBranch);
        Assert.True(pullRequest.DeleteBranchAfterMerge);

        pullRequest.MergeCommand.Execute(null);
        Assert.True(pullRequest.IsConfirmingMerge);
        Assert.Equal((false, (string?)null, "open"), scenario.GitHub.MergeState(PullRequestScenario.Repository, 1));
        await pullRequest.ConfirmMergeCommand.ExecuteAsync(null);

        Assert.Null(pullRequest.MergeErrorText);
        Assert.Equal((true, (string?)"squash", "closed"), scenario.GitHub.MergeState(PullRequestScenario.Repository, 1));
        Assert.False(scenario.GitHub.HasBranch(PullRequestScenario.Repository, PullRequestScenario.Branch));
        Assert.StartsWith("Greet friends and say goodbye (#1)", scenario.GitHub.HeadMessage(PullRequestScenario.Repository, "main"), StringComparison.Ordinal);
        Assert.Contains("See you soon", scenario.GitHub.Files(PullRequestScenario.Repository, "main")[Greeter], StringComparison.Ordinal);
        Assert.True(pullRequest.IsMerged);
        Assert.Equal(PullRequestBadge.Merged, pullRequest.Badge);
        Assert.False(pullRequest.ShowsMergePanel);
        Assert.Contains($"Deleted {PullRequestScenario.Branch}.", pullRequest.InfoText, StringComparison.Ordinal);
        Assert.StartsWith("Merged into main as ", pullRequest.MergedText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Merging_explains_what_stands_in_the_way()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        scenario.GitHub.SetMergeable(PullRequestScenario.Repository, 1, mergeable: false, state: "dirty");
        var conflicted = await scenario.OpenPullRequestAsync(1);

        Assert.Equal(MergeabilityKind.Blocked, conflicted.MergeabilityKind);
        Assert.Equal("This branch has conflicts with main that must be resolved before merging.", conflicted.MergeabilityText);
        Assert.NotNull(conflicted.MergeBlockedText);
        Assert.False(conflicted.MergeCommand.CanExecute(null));

        // The tester's own draft: no approving it, and it is marked ready before it can be merged.
        var mine = await scenario.OpenPullRequestAsync(2);
        Assert.True(mine.IsMine);
        Assert.False(mine.CanApprove);
        Assert.NotNull(mine.OwnPullRequestNote);
        Assert.True(mine.IsDraft);
        Assert.False(mine.MergeCommand.CanExecute(null));
        await mine.ReadyForReviewCommand.ExecuteAsync(null);
        Assert.False(scenario.GitHub.IsDraft(PullRequestScenario.Repository, 2));
        Assert.False(mine.IsDraft);
        Assert.True(mine.MergeCommand.CanExecute(null));

        mine.OpenFinishReviewCommand.Execute(null);
        mine.ReviewSummary = "Ready now.";
        await mine.SubmitReviewCommand.ExecuteAsync(ReviewEvent.Approve);
        Assert.Equal(mine.OwnPullRequestNote, mine.ReviewErrorText);
        Assert.DoesNotContain(scenario.GitHub.Reviews(PullRequestScenario.Repository, 2), r => r.State == "APPROVED");
    }

    [AvaloniaFact]
    public async Task Check_out_switches_the_folder_to_the_branch_unless_changes_would_be_lost()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        var program = scenario.Fixture.PathOf("src/HelloTablet/Program.cs");
        var before = await File.ReadAllTextAsync(program);
        await scenario.Fixture.WriteAsync("src/HelloTablet/Program.cs", "// Not committed yet\n" + before);

        await pullRequest.CheckOutCommand.ExecuteAsync(null);
        Assert.Contains("not committed", pullRequest.ErrorText, StringComparison.Ordinal);
        Assert.Equal("main", scenario.Fixture.Git.BranchName);
        Assert.False(pullRequest.IsCheckedOut);

        await scenario.Fixture.WriteAsync("src/HelloTablet/Program.cs", before);
        await pullRequest.CheckOutCommand.ExecuteAsync(null);

        Assert.Null(pullRequest.ErrorText);
        Assert.Equal(PullRequestScenario.Branch, scenario.Fixture.Git.BranchName);
        Assert.True(pullRequest.IsCheckedOut);
        Assert.False(pullRequest.CheckOutCommand.CanExecute(null));
        Assert.Contains("F5", pullRequest.CheckedOutText, StringComparison.Ordinal);
        Assert.Contains("See you soon", await File.ReadAllTextAsync(scenario.Fixture.PathOf(Greeter)), StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task On_the_pull_requests_branch_the_editor_shows_its_conversations()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        await pullRequest.CheckOutCommand.ExecuteAsync(null);

        var document = await scenario.Workspace.OpenDocumentAsync(scenario.Fixture.PathOf(Greeter)) ?? throw new InvalidOperationException("The greeter did not open.");
        await UiTestHelpers.WaitUntilAsync(() => document.EditorReview is { Anchors.Count: > 0 }, because: "the editor's conversations");

        var anchor = Assert.Single(document.EditorReview!.Anchors);
        Assert.Equal(PullRequestScenario.ThreadLine, anchor.Line);
        Assert.False(anchor.IsExpanded);
        Assert.Equal(2, anchor.CommentCount);
        Assert.False(document.EditorReview.CanComment);

        // The conversation follows its line when lines are added above it.
        document.Document.Insert(0, "// A new first line\n");
        Assert.Equal(PullRequestScenario.ThreadLine + 1, Assert.Single(document.EditorReview.Anchors).Line);
    }

    [AvaloniaFact]
    public async Task Claudes_draft_comments_are_kept_edited_or_dismissed()
    {
        var claude = new FakeClaude().ToolCall("toolu_1", "report_review", new JsonObject
        {
            ["summary"] = "A small, clear change. Two notes on the new text.",
            ["findings"] = new JsonArray(
                new JsonObject { ["path"] = Greeter, ["line"] = PullRequestScenario.FarewellLine, ["side"] = "RIGHT", ["severity"] = "suggestion", ["body"] = "Consider greeting by part of day here too." },
                new JsonObject { ["path"] = Greeter, ["line"] = PullRequestScenario.GreetingLine, ["side"] = "RIGHT", ["severity"] = "warning", ["body"] = "The README still quotes the old greeting." }),
        });
        using var http = new HttpClient(claude);
        using var scenario = new PullRequestScenario(http);
        await scenario.OpenAsync();
        new AgentSettingsStore(scenario.Fixture.Paths).Save(AgentSettings.Default with { ApiKey = "sk-ant-test" });
        var pullRequest = await scenario.OpenPullRequestAsync(1);

        await pullRequest.ReviewWithClaudeCommand.ExecuteAsync(null);

        Assert.Null(pullRequest.ErrorText);
        Assert.Equal("sk-ant-test", Assert.Single(claude.ApiKeys));
        Assert.Contains("report_review", claude.Requests[0].ToJsonString(), StringComparison.Ordinal);
        Assert.Equal(2, pullRequest.Drafts.Count);
        Assert.Equal(PullRequestSection.Files, pullRequest.Section);
        Assert.Equal("A small, clear change. Two notes on the new text.", pullRequest.ClaudeSummary);
        Assert.Equal(2, pullRequest.Files.Single().DraftCount);
        Assert.False(pullRequest.HasPendingReview);

        var review = await scenario.OpenFileAsync(pullRequest, Greeter);
        var drafts = review.Anchors.SelectMany(a => a.Items).OfType<DraftCommentViewModel>().ToList();
        Assert.Equal(2, drafts.Count);
        Assert.Equal("2 Claude drafts", review.DraftsText);

        var kept = drafts.Single(d => d.Line == PullRequestScenario.FarewellLine);
        kept.EditCommand.Execute(null);
        kept.Body = "Consider greeting by the part of the day here too, like Greet does.";
        kept.FinishEditingCommand.Execute(null);
        await kept.KeepCommand.ExecuteAsync(null);
        drafts.Single(d => d.Line == PullRequestScenario.GreetingLine).DismissCommand.Execute(null);

        Assert.Empty(pullRequest.Drafts);
        Assert.Equal(1, pullRequest.PendingCount);
        var comment = Assert.Single(scenario.GitHub.ReviewComments(PullRequestScenario.Repository, 1), c => c.Author == FakeGitHub.Login);
        Assert.Equal("Consider greeting by the part of the day here too, like Greet does.", comment.Body);
        Assert.Equal(PullRequestScenario.FarewellLine, comment.Line);

        // Nothing is posted until the review is finished.
        Assert.Equal("PENDING", Assert.Single(scenario.GitHub.Reviews(PullRequestScenario.Repository, 1), r => r.Author == FakeGitHub.Login).State);
    }

    [AvaloniaFact]
    public async Task Without_an_API_key_Claude_explains_how_to_add_one()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var pullRequest = await scenario.OpenPullRequestAsync(1);

        await pullRequest.ReviewWithClaudeCommand.ExecuteAsync(null);

        Assert.True(pullRequest.NeedsClaudeKey);
        Assert.Empty(pullRequest.Drafts);
        pullRequest.OpenClaudeSettingsCommand.Execute(null);
        Assert.False(pullRequest.NeedsClaudeKey);
        Assert.True(scenario.Workspace.IsAgentSelected);
        Assert.True(scenario.Workspace.Agent.IsSettingsOpen);
    }

    private static NewCommentViewModel Composer(FileReviewViewModel review) =>
        review.Anchors.SelectMany(a => a.Items).OfType<NewCommentViewModel>().Single();
}
