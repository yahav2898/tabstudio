using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using TabStudio.App.Services.Agent;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels.PullRequests;
using TabStudio.App.Views;
using TabStudio.App.Views.PullRequests;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// Pull request review in the real views, with screenshots: the Pull Requests window, a pull request's page, a diff
/// with its conversations and a pending comment, Finish Review, merging, the editor's threads and Claude's drafts.
/// </summary>
public sealed class PullRequestScreenshotTests
{
    private const string Greeter = PullRequestScenario.GreeterPath;

    [AvaloniaFact]
    public async Task The_Pull_Requests_window_and_a_pull_requests_page()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var window = Show(scenario);

        scenario.Workspace.ShowPullRequestsCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => scenario.Workspace.PullRequests.Items.Count == 2, because: "the pull requests");
        UiTestHelpers.SaveScreenshot(window, "pr-01-list");

        var list = window.GetVisualDescendants().OfType<PullRequestListView>().Single();
        Assert.True(list.IsEffectivelyVisible);
        Assert.Equal(2, list.FindControl<ItemsControl>("PullRequestList")!.GetRealizedContainers().Count());

        var pullRequest = await scenario.OpenPullRequestAsync(1);
        var page = await VisiblePageAsync(window);
        UiTestHelpers.SaveScreenshot(window, "pr-02-overview");

        Assert.Same(pullRequest, page.DataContext);
        Assert.True(page.FindControl<WrapPanel>("StatusLine")!.IsEffectivelyVisible);
        Assert.True(page.FindControl<Button>("CheckOutButton")!.IsEffectivelyVisible);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_diff_with_its_conversation_a_pending_comment_and_Finish_Review()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var window = Show(scenario);
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        var review = await scenario.OpenFileAsync(pullRequest, Greeter);
        var diff = await VisibleDiffAsync(window);
        var support = diff.ReviewSupport!;
        var threadLine = scenario.DiffLineOf(PullRequestScenario.ThreadLine);

        // The conversation sits under its line, in room the line makes for it.
        await UiTestHelpers.WaitUntilAsync(
            () => CardHeight(diff, threadLine) > 40 && Math.Abs(CardHeight(diff, threadLine) - support.Adorner.Views.Single().DesiredSize.Height) < 1,
            because: "room under the commented line as tall as its card");
        var card = support.Adorner.Views.Single();
        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == PullRequestScenario.Colleague);

        // Tapping the margin of a changed line opens a comment box there; Cancel closes it.
        var greeting = scenario.DiffLineOf(PullRequestScenario.GreetingLine);
        Tap(window, support.Adorner.Margin, diff.Editor, greeting);
        var tapped = Assert.Single(review.Anchors.SelectMany(a => a.Items).OfType<NewCommentViewModel>());
        Assert.Equal(PullRequestScenario.GreetingLine, tapped.Line);
        tapped.CancelCommand.Execute(null);

        // Selecting lines and pressing Ctrl+Shift+Enter comments on the range: the unchanged lines after the change.
        const int First = PullRequestScenario.ThreadLine + 2;
        const int Last = PullRequestScenario.ThreadLine + 3;
        var editor = diff.Editor;
        var document = editor.Document;
        var start = document.GetLineByNumber(scenario.DiffLineOf(First));
        var end = document.GetLineByNumber(scenario.DiffLineOf(Last));
        editor.Select(start.Offset, end.EndOffset - start.Offset);
        editor.TextArea.Focus();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Control | RawInputModifiers.Shift);
        var composer = Assert.Single(review.Anchors.SelectMany(a => a.Items).OfType<NewCommentViewModel>());
        Assert.Equal(Last, composer.Line);
        Assert.Equal(First, composer.StartLine);
        composer.Text = "Could `Greet` take the time as a parameter? Then a test can check the **evening** greeting.";
        await composer.SubmitCommand.ExecuteAsync(null);
        var pendingLine = scenario.DiffLineOf(Last);
        await UiTestHelpers.WaitUntilAsync(() => support.Adorner.Views.Count == 2 && CardHeight(diff, pendingLine) > 40, because: "the pending comment's card");
        await ScrollToTopAsync(editor, threadLine - 2);
        UiTestHelpers.SaveScreenshot(window, "pr-03-diff-review");

        Assert.Equal(1, pullRequest.PendingCount);
        Assert.Equal(First, Assert.Single(scenario.GitHub.ReviewComments(PullRequestScenario.Repository, 1), c => c.Author == FakeGitHub.Login).StartLine);

        // Finish Review, from the diff's bar.
        review.FinishReviewCommand.Execute(null);
        var page = await VisiblePageAsync(window);
        pullRequest.ReviewSummary = "Thanks! One question on testing the greeting, otherwise good to go.";
        await UiTestHelpers.WaitUntilAsync(() => page.FindControl<Border>("FinishReviewPanel")!.IsEffectivelyVisible, because: "the Finish Review panel");
        UiTestHelpers.SaveScreenshot(window, "pr-04-finish-review");

        Assert.True(page.FindControl<Button>("ApproveButton")!.IsEffectivelyEnabled);
        page.FindControl<Button>("ApproveButton")!.Command!.Execute(ReviewEvent.Approve);
        await UiTestHelpers.WaitUntilAsync(() => !pullRequest.IsFinishReviewOpen && !pullRequest.IsSubmittingReview, because: "the review to be sent");
        var approved = Assert.Single(scenario.GitHub.Reviews(PullRequestScenario.Repository, 1), r => r.Author == FakeGitHub.Login);
        Assert.Equal("APPROVED", approved.State);
        Assert.Single(approved.Comments);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_merge_panel()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        scenario.GitHub.SetMergeSettings(PullRequestScenario.Repository, allowMerge: true, allowSquash: true, allowRebase: false);
        var window = Show(scenario);
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        var page = await VisiblePageAsync(window);

        pullRequest.SelectedMergeMethod = pullRequest.MergeMethods.Single(m => m.Method == MergeMethod.Squash);
        pullRequest.MergeCommand.Execute(null);
        var panel = page.FindControl<Border>("MergePanel")!;
        await UiTestHelpers.WaitUntilAsync(() => page.FindControl<Button>("ConfirmMergeButton")!.IsEffectivelyVisible, because: "the confirmation");
        page.FindControl<ScrollViewer>("SectionScroller")!.ScrollToEnd();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        UiTestHelpers.SaveScreenshot(window, "pr-05-merge");

        Assert.True(panel.IsEffectivelyVisible);
        Assert.Equal("Greet friends and say goodbye (#1)", page.FindControl<TextBox>("MergeTitleBox")!.Text);
        Assert.True(page.FindControl<CheckBox>("DeleteBranchBox")!.IsChecked);
        await pullRequest.ConfirmMergeCommand.ExecuteAsync(null);
        Assert.True(pullRequest.IsMerged);
        Assert.False(scenario.GitHub.HasBranch(PullRequestScenario.Repository, PullRequestScenario.Branch));
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_editor_shows_the_pull_requests_conversation_on_its_branch()
    {
        using var scenario = new PullRequestScenario();
        await scenario.OpenAsync();
        var window = Show(scenario);
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        await pullRequest.CheckOutCommand.ExecuteAsync(null);
        var document = await scenario.Workspace.OpenDocumentAsync(scenario.Fixture.PathOf(Greeter)) ?? throw new InvalidOperationException("The greeter did not open.");
        await UiTestHelpers.WaitUntilAsync(() => document.EditorReview is { Anchors.Count: > 0 }, because: "the editor's conversations");
        var editor = await VisibleEditorAsync(window);
        await UiTestHelpers.WaitUntilAsync(() => editor.ReviewThreads is not null, because: "the review margin");

        var threads = editor.ReviewThreads!;
        Assert.Empty(threads.Views);
        Assert.True(threads.Toggle(PullRequestScenario.ThreadLine));
        await UiTestHelpers.WaitUntilAsync(() => CardHeight(editor.Editor, PullRequestScenario.ThreadLine) > 40, because: "the conversation under its line");
        await ScrollToTopAsync(editor.Editor, PullRequestScenario.ThreadLine - 5);
        UiTestHelpers.SaveScreenshot(window, "pr-06-editor-comments");

        var card = threads.Views.Single();
        Assert.True(card.IsEffectivelyVisible);
        Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == FakeGitHub.Reviewer);

        // Typing a reply never reaches the code.
        var text = document.Document.Text;
        var thread = pullRequest.Threads.Single();
        thread.StartReplyCommand.Execute(null);
        await UiTestHelpers.WaitUntilAsync(() => card.GetVisualDescendants().OfType<TextBox>().Any(b => b.IsEffectivelyVisible), because: "the reply box");
        var box = card.GetVisualDescendants().OfType<TextBox>().First(b => b.IsEffectivelyVisible);
        box.Focus();
        window.KeyTextInput("Thanks!");
        Assert.Equal("Thanks!", thread.ReplyText);
        Assert.Equal(text, document.Document.Text);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Claudes_draft_comments_in_the_files_list_and_the_diff()
    {
        var claude = new FakeClaude().ToolCall("toolu_1", "report_review", new JsonObject
        {
            ["summary"] = "A small, clear change that makes the greeting friendlier. Two notes on the new text.",
            ["findings"] = new JsonArray(
                new JsonObject { ["path"] = Greeter, ["line"] = PullRequestScenario.FarewellLine, ["side"] = "RIGHT", ["severity"] = "suggestion", ["body"] = "`Farewell` could greet by the part of the day too, like `Greet` does." },
                new JsonObject { ["path"] = Greeter, ["line"] = PullRequestScenario.GreetingLine, ["side"] = "RIGHT", ["severity"] = "warning", ["body"] = "The README still quotes the old greeting; update it in this pull request." }),
        });
        using var http = new HttpClient(claude);
        using var scenario = new PullRequestScenario(http);
        await scenario.OpenAsync();
        new AgentSettingsStore(scenario.Fixture.Paths).Save(AgentSettings.Default with { ApiKey = "sk-ant-test" });
        var window = Show(scenario);
        var pullRequest = await scenario.OpenPullRequestAsync(1);
        var page = await VisiblePageAsync(window);

        await pullRequest.ReviewWithClaudeCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => page.FindControl<Border>("ClaudeSummary")!.IsEffectivelyVisible, because: "Claude's summary");
        UiTestHelpers.SaveScreenshot(window, "pr-07-claude-files");
        Assert.Equal(2, pullRequest.Files.Single().DraftCount);

        await scenario.OpenFileAsync(pullRequest, Greeter);
        var diff = await VisibleDiffAsync(window);
        var farewell = scenario.DiffLineOf(PullRequestScenario.FarewellLine);
        var greeting = scenario.DiffLineOf(PullRequestScenario.GreetingLine);
        await UiTestHelpers.WaitUntilAsync(() => CardHeight(diff, greeting) > 40 && CardHeight(diff, farewell) > 40, because: "the drafts' cards");
        await ScrollToTopAsync(diff.Editor, greeting - 2);
        UiTestHelpers.SaveScreenshot(window, "pr-08-claude-drafts");

        var cards = diff.ReviewSupport!.Adorner.Views;
        Assert.Contains(cards, c => c.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Claude"));
        var draft = pullRequest.Drafts.Single(d => d.Line == PullRequestScenario.FarewellLine);
        await draft.KeepCommand.ExecuteAsync(null);
        Assert.Equal(1, pullRequest.PendingCount);
        Assert.Single(pullRequest.Drafts);
        window.Close();
    }

    /// <summary>Shows the workspace without the bottom panel, so the pages and diffs have room.</summary>
    private static Window Show(PullRequestScenario scenario)
    {
        scenario.Workspace.IsBottomPanelVisible = false;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = scenario.Workspace } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static async Task<PullRequestView> VisiblePageAsync(Window window)
    {
        PullRequestView? page = null;
        await UiTestHelpers.WaitUntilAsync(() => (page = window.GetVisualDescendants().OfType<PullRequestView>().FirstOrDefault(v => v.IsEffectivelyVisible)) is not null, because: "the pull request's page");
        return page!;
    }

    private static async Task<DiffView> VisibleDiffAsync(Window window)
    {
        DiffView? diff = null;
        await UiTestHelpers.WaitUntilAsync(() => (diff = window.GetVisualDescendants().OfType<DiffView>().FirstOrDefault(v => v.IsEffectivelyVisible && v.ReviewSupport is not null)) is not null, because: "the file's diff");
        return diff!;
    }

    /// <summary>How much room a line of a diff has under its text, for a card.</summary>
    private static double CardHeight(DiffView diff, int line) => CardHeight(diff.Editor, line);

    private static double CardHeight(AvaloniaEdit.TextEditor editor, int line)
    {
        // Lines scrolled out of view are laid out on demand, with the room they would have.
        var textView = editor.TextArea.TextView;
        return textView.VisualLinesValid ? textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(line)).Height - textView.DefaultLineHeight : 0;
    }

    /// <summary>Scrolls an editor so a line is at its top.</summary>
    private static async Task ScrollToTopAsync(AvaloniaEdit.TextEditor editor, int line)
    {
        // The caret goes there first, as the editor keeps the caret in view; the scroll is repeated until layout keeps it.
        var textView = editor.TextArea.TextView;
        editor.TextArea.ClearSelection();
        editor.TextArea.Caret.Line = line;
        var target = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(line)).VisualTop;
        var settled = 0;
        await UiTestHelpers.WaitUntilAsync(
            () =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                if (Math.Abs(textView.VerticalOffset - target) > 1)
                {
                    settled = 0;
                    editor.ScrollToVerticalOffset(target);
                    return false;
                }

                return ++settled > 2;
            },
            because: "the editor to scroll");
    }

    /// <summary>Taps the review margin beside a line, as a finger would.</summary>
    private static void Tap(Window window, Control margin, AvaloniaEdit.TextEditor editor, int line)
    {
        editor.ScrollToLine(line);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var textView = editor.TextArea.TextView;
        var visual = textView.GetOrConstructVisualLine(editor.Document.GetLineByNumber(line));
        var y = visual.VisualTop - textView.VerticalOffset + (textView.DefaultLineHeight / 2);
        var point = margin.TranslatePoint(new Point(margin.Bounds.Width / 2, y), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static async Task<CodeEditorView> VisibleEditorAsync(Window window)
    {
        CodeEditorView? editor = null;
        await UiTestHelpers.WaitUntilAsync(() => (editor = window.GetVisualDescendants().OfType<CodeEditorView>().FirstOrDefault(v => v.IsEffectivelyVisible)) is not null, because: "the code editor");
        return editor!;
    }
}
