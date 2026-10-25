using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl.Tests;

/// <summary>Reading GitHub's patches, grouping review comments into threads, and GitHub's default merge messages.</summary>
public sealed class PullRequestModelTests
{
    private const string Patch = """
        @@ -1,4 +1,5 @@
         using System;
        -Console.WriteLine("Hi");
        +Console.WriteLine("Hello");
        +Console.WriteLine("World");
         return 0;
         // end
        @@ -20,2 +21,2 @@ class Program
        -    int x;
        +    int y;
         }
        \ No newline at end of file
        """;

    [Fact]
    public void A_patch_lists_its_lines_with_the_numbers_of_both_versions()
    {
        var patch = DiffPatch.Parse(Patch);

        Assert.Equal(9, patch.Lines.Count);
        Assert.Equal(new PatchLine(PatchLineKind.Context, 1, 1, "using System;"), patch.Lines[0]);
        Assert.Equal(new PatchLine(PatchLineKind.Removed, 2, null, "Console.WriteLine(\"Hi\");"), patch.Lines[1]);
        Assert.Equal(new PatchLine(PatchLineKind.Added, null, 3, "Console.WriteLine(\"World\");"), patch.Lines[3]);
        Assert.Equal(new PatchLine(PatchLineKind.Context, 4, 5, "// end"), patch.Lines[5]);
        Assert.Equal(new PatchLine(PatchLineKind.Added, null, 21, "    int y;"), patch.Lines[7]);
        Assert.Equal(new PatchLine(PatchLineKind.Context, 21, 22, "}"), patch.Lines[8]);
    }

    [Theory]
    [InlineData(DiffSide.Right, 3, true)]
    [InlineData(DiffSide.Right, 5, true)]
    [InlineData(DiffSide.Right, 10, false)]
    [InlineData(DiffSide.Left, 2, true)]
    [InlineData(DiffSide.Left, 20, true)]
    [InlineData(DiffSide.Left, 5, false)]
    public void Comments_can_go_on_the_lines_of_the_diff_on_their_side(DiffSide side, int line, bool commentable) =>
        Assert.Equal(commentable, DiffPatch.Parse(Patch).Contains(side, line));

    [Fact]
    public void The_nearest_line_of_the_diff_is_found_within_a_distance()
    {
        var patch = DiffPatch.Parse(Patch);

        Assert.Equal(5, patch.Nearest(DiffSide.Right, 7, 3));
        Assert.Equal(21, patch.Nearest(DiffSide.Right, 19, 2));
        Assert.Null(patch.Nearest(DiffSide.Right, 12, 3));
        Assert.Empty(DiffPatch.Parse(null).Lines);
    }

    [Fact]
    public void Replies_are_grouped_under_the_comment_that_started_their_thread()
    {
        var root = Comment(1, "src/A.cs", 12, null);
        var reply = Comment(2, "src/A.cs", 12, 1, minutes: 5);
        var replyToReply = Comment(3, "src/A.cs", 12, 2, minutes: 9);
        var other = Comment(4, "src/A.cs", 3, null);
        var orphan = Comment(5, "src/B.cs", 7, 99);

        var threads = ReviewThread.Group([replyToReply, other, root, reply, orphan, root]);

        Assert.Equal(new long[] { 4, 1, 5 }, threads.Select(t => t.Root.Id));
        Assert.Equal(new long[] { 2, 3 }, threads[1].Replies.Select(c => c.Id));
        Assert.Equal(3, threads[1].Comments.Count);
    }

    [Fact]
    public void An_outdated_comment_keeps_the_line_it_was_written_on()
    {
        var outdated = Comment(1, "src/A.cs", null, null) with { OriginalLine = 8, OriginalStartLine = 6, DiffHunk = "@@ -1,2 +1,2 @@\n context\n+    return value;" };

        var thread = Assert.Single(ReviewThread.Group([outdated]));

        Assert.True(thread.IsOutdated);
        Assert.Equal(8, thread.Line);
        Assert.Equal(6, thread.StartLine);
        Assert.Equal("    return value;", outdated.CommentedLineText);
    }

    [Fact]
    public void Merge_messages_follow_GitHub_defaults()
    {
        var pull = PullRequest();
        var one = new[] { Commit("Fix the greeting\n\nIt said hi twice.") };
        var two = new[] { Commit("Fix the greeting"), Commit("Add a test") };
        var settings = RepositoryMergeSettings.Default;

        Assert.Equal(new MergeCommitText("Merge pull request #7 from octocat/feature/greet", "Greet properly"), MergeMessages.Default(MergeMethod.Merge, pull, two, settings));
        Assert.Equal(new MergeCommitText("Fix the greeting (#7)", "It said hi twice."), MergeMessages.Default(MergeMethod.Squash, pull, one, settings));
        Assert.Equal(new MergeCommitText("Greet properly (#7)", "* Fix the greeting\n\n* Add a test"), MergeMessages.Default(MergeMethod.Squash, pull, two, settings));
        Assert.Equal(new MergeCommitText(string.Empty, string.Empty), MergeMessages.Default(MergeMethod.Rebase, pull, two, settings));
    }

    [Fact]
    public void Merge_messages_follow_the_repository_settings()
    {
        var pull = PullRequest();
        var settings = RepositoryMergeSettings.Default with { MergeCommitTitle = "PR_TITLE", MergeCommitMessage = "PR_BODY", SquashCommitTitle = "PR_TITLE", SquashCommitMessage = "BLANK" };
        var one = new[] { Commit("Fix the greeting") };

        Assert.Equal(new MergeCommitText("Greet properly (#7)", "Says hello once."), MergeMessages.Default(MergeMethod.Merge, pull, one, settings));
        Assert.Equal(new MergeCommitText("Greet properly (#7)", string.Empty), MergeMessages.Default(MergeMethod.Squash, pull, one, settings));
        Assert.Equal(new[] { MergeMethod.Merge, MergeMethod.Squash, MergeMethod.Rebase }, settings.AllowedMethods);
    }

    private static ReviewComment Comment(long id, string path, int? line, long? inReplyTo, int minutes = 0) => new(
        id, $"PRRC_{id}", path, line, DiffSide.Right, null, null, line, null, inReplyTo, "octocat", $"Comment {id}",
        new DateTimeOffset(2026, 10, 20, 9, minutes, 0, TimeSpan.Zero), new DateTimeOffset(2026, 10, 20, 9, minutes, 0, TimeSpan.Zero),
        "@@ -1 +1 @@\n+line", "abc", 1, null);

    private static PullRequestInfo PullRequest() => new(
        7, "Greet properly", "Says hello once.", PullRequestState.Open, false, "octocat",
        new PullRequestBranch("feature/greet", "head", "octocat/demo"), new PullRequestBranch("main", "base", "tester/demo"),
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, null, new Uri("https://github.test/tester/demo/pull/7"), [], "PR_7");

    private static GitCommit Commit(string message) => new("sha", "tree", message, [], null);
}
