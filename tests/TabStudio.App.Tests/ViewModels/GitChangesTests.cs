using Avalonia.Headless.XUnit;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The Git Changes window against <see cref="FakeGitHub"/>: the whole round trip a tablet user makes.</summary>
public sealed class GitChangesTests
{
    private const string Program = "src/HelloTablet/Program.cs";
    private const string Greeter = "src/Greetings/Greeter.cs";

    [AvaloniaFact]
    public async Task A_saved_change_is_listed_and_commit_pushes_it_to_GitHub()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        Assert.Equal("tester/hello", git.RepositoryName);
        Assert.Equal("main", git.BranchName);
        Assert.True(git.IsClean);

        var program = fixture.Workspace.Documents.Single(d => d.FileName == "Program.cs");
        program.Document.Insert(0, "// Committed from a tablet\n");
        await fixture.Workspace.SaveAllCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => git.Changes.Count == 1, because: "the saved file to be listed");

        var change = git.Changes[0];
        Assert.Equal(Program, change.RelativePath);
        Assert.Equal(GitFileStatus.Modified, change.Status);
        Assert.Equal("M", change.StatusLetter);
        Assert.Equal("Commit All and Push", git.CommitButtonText);
        Assert.Contains(fixture.Workspace.Explorer.Rows, r => r.Name == "Program.cs" && r.GitGlyph == ExplorerGitGlyph.Modified);
        Assert.Contains(fixture.Workspace.Explorer.Rows, r => r.IsProject && r.Name == "HelloTablet" && r.GitGlyph == ExplorerGitGlyph.ContainsChanges);
        Assert.DoesNotContain(fixture.Workspace.Explorer.Rows, r => r.Name == "Greeter.cs" && r.GitGlyph != ExplorerGitGlyph.None);

        Assert.False(git.CommitCommand.CanExecute(null));
        git.CommitMessage = "Say where the code was written";
        await git.CommitCommand.ExecuteAsync(null);

        Assert.Null(git.ErrorText);
        Assert.Equal("Say where the code was written", fixture.GitHub.HeadMessage(GitWorkspaceFixture.Repository, "main"));
        Assert.StartsWith("// Committed from a tablet\n", fixture.GitHub.Files(GitWorkspaceFixture.Repository, "main")[Program], StringComparison.Ordinal);
        Assert.Empty(git.Changes);
        Assert.Equal(string.Empty, git.CommitMessage);
        Assert.Contains("pushed it to main", git.InfoText, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Workspace.Explorer.Rows, r => r.GitGlyph != ExplorerGitGlyph.None);
        Assert.False(git.CanCreatePullRequest);
    }

    [AvaloniaFact]
    public async Task Git_commit_shows_the_window_focuses_the_message_and_checks_GitHub()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var focused = false;
        fixture.Git.CommitMessageFocusRequested += (_, _) => focused = true;
        fixture.Workspace.IsSideBarVisible = false;
        Assert.Null(fixture.Git.IncomingCommits);

        fixture.Workspace.StartCommitCommand.Execute(null);

        Assert.True(fixture.Workspace.IsSideBarVisible);
        Assert.True(fixture.Workspace.IsGitChangesSelected);
        Assert.True(focused);
        await UiTestHelpers.WaitUntilAsync(() => fixture.Git.IncomingCommits is not null, because: "the window to ask GitHub when it shows");
        Assert.Equal("0 incoming", fixture.Git.IncomingText);
    }

    [AvaloniaFact]
    public async Task Checked_files_are_committed_alone()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        await fixture.WriteAsync(Greeter, "namespace Greetings;\n");
        await fixture.WriteAsync("NOTES.md", "# Notes\n");
        Assert.Equal(2, git.Changes.Count);

        git.Changes.Single(c => c.RelativePath == "NOTES.md").IsSelected = true;
        Assert.Equal("Commit Selected and Push", git.CommitButtonText);
        git.CommitMessage = "Add notes";
        await git.CommitCommand.ExecuteAsync(null);

        var files = fixture.GitHub.Files(GitWorkspaceFixture.Repository, "main");
        Assert.Equal("# Notes\n", files["NOTES.md"]);
        Assert.NotEqual("namespace Greetings;\n", files[Greeter]);
        Assert.Equal(Greeter, Assert.Single(git.Changes).RelativePath);
        Assert.False(git.Changes[0].IsSelected);
    }

    [AvaloniaFact]
    public async Task Pulling_a_conflicting_change_shows_it_under_merge_conflicts_until_resolved()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        var original = await File.ReadAllTextAsync(fixture.PathOf(Greeter));
        var mine = original.Replace("\"developer\"", "\"friend\"", StringComparison.Ordinal);
        var theirs = original.Replace("\"developer\"", "\"coder\"", StringComparison.Ordinal);
        await fixture.WriteAsync(Greeter, mine);
        fixture.GitHub.Push(GitWorkspaceFixture.Repository, "main", "Call people coders", new Dictionary<string, string?> { [Greeter] = theirs });

        await git.FetchCommand.ExecuteAsync(null);
        Assert.Equal(1, git.IncomingCommits);
        Assert.Contains("1 new commit", git.InfoText, StringComparison.Ordinal);

        await git.PullCommand.ExecuteAsync(null);

        var conflict = Assert.Single(git.Conflicts);
        Assert.Equal(Greeter, conflict.RelativePath);
        Assert.True(git.HasConflicts);
        Assert.DoesNotContain(git.Changes, c => c.RelativePath == Greeter);
        Assert.Equal(0, git.IncomingCommits);
        Assert.Contains("<<<<<<< Yours", await File.ReadAllTextAsync(fixture.PathOf(Greeter)), StringComparison.Ordinal);
        Assert.Contains(fixture.Workspace.Explorer.Rows, r => r.Name == "Greeter.cs" && r.GitGlyph == ExplorerGitGlyph.Conflict);

        await conflict.MarkResolvedCommand.ExecuteAsync(null);
        Assert.Contains("conflict markers", git.ErrorText, StringComparison.Ordinal);
        Assert.Single(git.Conflicts);

        await conflict.KeepMineCommand.ExecuteAsync(null);
        Assert.Empty(git.Conflicts);
        Assert.Equal(mine, await File.ReadAllTextAsync(fixture.PathOf(Greeter)));
        Assert.Equal(GitFileStatus.Modified, Assert.Single(git.Changes).Status);

        git.CommitMessage = "Call people friends";
        await git.CommitCommand.ExecuteAsync(null);
        Assert.Null(git.ErrorText);
        Assert.Equal(mine, fixture.GitHub.Files(GitWorkspaceFixture.Repository, "main")[Greeter]);
    }

    [AvaloniaFact]
    public async Task Committing_when_GitHub_has_new_commits_asks_to_pull_first()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        await fixture.WriteAsync("NOTES.md", "# Notes\n");
        fixture.GitHub.Push(GitWorkspaceFixture.Repository, "main", "Elsewhere", new Dictionary<string, string?> { ["README.md"] = "# Changed\n" });

        git.CommitMessage = "Add notes";
        await git.CommitCommand.ExecuteAsync(null);
        Assert.Contains("Pull them first", git.ErrorText, StringComparison.Ordinal);

        await git.SyncCommand.ExecuteAsync(null);

        Assert.Null(git.ErrorText);
        Assert.True(git.IsClean);
        Assert.Equal("Add notes", fixture.GitHub.HeadMessage(GitWorkspaceFixture.Repository, "main"));
        Assert.Equal("# Changed\n", await File.ReadAllTextAsync(fixture.PathOf("README.md")));
    }

    [AvaloniaFact]
    public async Task Undo_changes_asks_first_then_restores_the_file_and_its_open_tab()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        var program = fixture.Workspace.Documents.Single(d => d.FileName == "Program.cs");
        var original = program.Document.Text;
        program.Document.Insert(0, "// Oops\n");
        await fixture.Workspace.SaveAllCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => git.Changes.Count == 1, because: "the saved file to be listed");

        var undo = git.Changes[0].UndoChangesCommand.ExecuteAsync(null);
        Assert.True(fixture.Workspace.Prompt.IsOpen);
        Assert.True(fixture.Workspace.Prompt.IsDestructive);
        fixture.Workspace.Prompt.ConfirmCommand.Execute(null);
        await undo;

        Assert.Empty(git.Changes);
        Assert.Equal(original, await File.ReadAllTextAsync(fixture.PathOf(Program)));
        Assert.Equal(original, program.Document.Text);
        Assert.False(program.IsDirty);
    }

    [AvaloniaFact]
    public async Task Compare_with_unmodified_opens_a_read_only_diff_tab()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        var original = await File.ReadAllTextAsync(fixture.PathOf(Program));
        await fixture.WriteAsync(Program, original.Replace("// Welcome to TabStudio!", "// Welcome back!", StringComparison.Ordinal) + "// The end\n");

        await git.Changes.Single().CompareCommand.ExecuteAsync(null);

        var tab = fixture.Workspace.ActiveDocument!;
        Assert.True(tab.IsComparison);
        Assert.Equal("Program.cs vs. Unmodified", tab.Title);
        Assert.Equal("C#", tab.FileType.Badge);
        var diff = tab.Comparison!.Diff;
        Assert.Equal(2, diff.AddedCount);
        Assert.Equal(1, diff.RemovedCount);
        Assert.Equal(new DiffLine(DiffLineKind.Removed, "// Welcome to TabStudio!", 1, null), diff.Lines[0]);
        Assert.Equal(new DiffLine(DiffLineKind.Added, "// Welcome back!", null, 1), diff.Lines[1]);
        Assert.Equal(diff.Text, tab.Document.Text);

        var collapsed = diff.Lines.ToList().FindIndex(l => l.Kind == DiffLineKind.Collapsed);
        Assert.True(collapsed > 0);
        tab.ExpandComparison(collapsed);
        Assert.DoesNotContain(tab.Comparison.Diff.Lines, l => l.Kind == DiffLineKind.Collapsed);
        Assert.Equal(tab.Comparison.Diff.Text, tab.Document.Text);
        Assert.False(tab.IsDirty);

        // Saving a comparison tab writes nothing: it has no file of its own.
        await fixture.Workspace.SaveCommand.ExecuteAsync(null);
        Assert.False(File.Exists(tab.FilePath));

        var count = fixture.Workspace.Documents.Count;
        await git.Changes.Single().CompareCommand.ExecuteAsync(null);
        Assert.Equal(count, fixture.Workspace.Documents.Count);
        Assert.Contains(fixture.Workspace.Documents, d => d.FileName == "Program.cs" && !d.IsComparison);
    }

    [AvaloniaFact]
    public async Task Ignoring_a_new_file_adds_it_to_the_gitignore()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        await fixture.WriteAsync("scratch.txt", "todo\n");
        var added = Assert.Single(git.Changes);
        Assert.Equal(GitFileStatus.Added, added.Status);
        Assert.True(added.CanIgnore);

        await added.IgnoreCommand.ExecuteAsync(null);

        Assert.Equal("/scratch.txt\n", await File.ReadAllTextAsync(fixture.PathOf(".gitignore")));
        Assert.Equal(".gitignore", Assert.Single(git.Changes).RelativePath);
    }

    [AvaloniaFact]
    public async Task A_new_branch_gets_the_commit_and_offers_a_pull_request()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;

        var create = git.NewBranchCommand.ExecuteAsync(null);
        Assert.True(fixture.Workspace.Prompt.IsOpen);
        fixture.Workspace.Prompt.Text = "feature/warmer";
        fixture.Workspace.Prompt.ConfirmCommand.Execute(null);
        await create;
        Assert.Equal("feature/warmer", git.BranchName);
        Assert.True(fixture.GitHub.HasBranch(GitWorkspaceFixture.Repository, "feature/warmer"));

        await fixture.WriteAsync(Greeter, "namespace Greetings;\n");
        git.CommitMessage = "Greet more warmly\n\nAnd say less.";
        await git.CommitCommand.ExecuteAsync(null);
        Assert.Equal("Greet more warmly\n\nAnd say less.", fixture.GitHub.HeadMessage(GitWorkspaceFixture.Repository, "feature/warmer"));
        Assert.NotEqual("namespace Greetings;\n", fixture.GitHub.Files(GitWorkspaceFixture.Repository, "main")[Greeter]);
        Assert.True(git.CanCreatePullRequest);

        await git.OpenPullRequestFormCommand.ExecuteAsync(null);
        Assert.True(git.IsPullRequestFormOpen);
        Assert.Equal("Greet more warmly", git.PullRequestTitle);
        Assert.Equal("main", git.PullRequestBase);
        await git.CreatePullRequestCommand.ExecuteAsync(null);

        Assert.Equal(("Greet more warmly", "feature/warmer", "main"), Assert.Single(fixture.GitHub.PullRequests(GitWorkspaceFixture.Repository)));
        Assert.False(git.IsPullRequestFormOpen);
        Assert.False(git.CanCreatePullRequest);
        Assert.Equal("Pull request #1: Greet more warmly", git.PullRequestText);
        await git.OpenPullRequestCommand.ExecuteAsync(null);
        Assert.Equal("https://github.test/tester/hello/pull/1", Assert.Single(fixture.Browser.Opened).ToString());
    }

    [AvaloniaFact]
    public async Task A_folder_on_a_commit_cannot_commit_until_it_is_on_a_new_branch()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync(atCommit: true);
        var git = fixture.Git;
        Assert.True(git.IsDetached);
        Assert.Equal(fixture.GitHub.Head(GitWorkspaceFixture.Repository, "main")[..7], git.BranchName);

        await fixture.WriteAsync("NOTES.md", "# Notes\n");
        git.CommitMessage = "Add notes";
        Assert.False(git.CommitCommand.CanExecute(null));

        var create = git.NewBranchCommand.ExecuteAsync(null);
        fixture.Workspace.Prompt.Text = "notes";
        fixture.Workspace.Prompt.ConfirmCommand.Execute(null);
        await create;

        Assert.False(git.IsDetached);
        Assert.Equal("notes", git.BranchName);
        Assert.True(git.CommitCommand.CanExecute(null));
        await git.CommitCommand.ExecuteAsync(null);
        Assert.Equal("Add notes", fixture.GitHub.HeadMessage(GitWorkspaceFixture.Repository, "notes"));
    }

    [AvaloniaFact]
    public async Task Switching_branches_replaces_the_files_and_the_open_documents()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        var head = fixture.GitHub.Head(GitWorkspaceFixture.Repository, "main");
        await fixture.GitHub.CreateClient().CreateBranchAsync(FakeGitHub.Login, GitWorkspaceFixture.Repository, "experiment", head);
        fixture.GitHub.Push(GitWorkspaceFixture.Repository, "experiment", "Try something", new Dictionary<string, string?> { [Program] = "Console.WriteLine(\"Experiment\");\n" });

        await git.OpenBranchPickerCommand.ExecuteAsync(null);
        Assert.True(git.IsBranchPickerOpen);
        Assert.Equal(["main", "experiment"], git.Branches.Select(b => b.Name));
        Assert.True(git.Branches[0].IsCurrent);
        Assert.Equal("default", git.Branches[0].Note);

        await git.SwitchBranchCommand.ExecuteAsync(git.Branches[1]);

        Assert.False(git.IsBranchPickerOpen);
        Assert.Null(git.ErrorText);
        Assert.Equal("experiment", git.BranchName);
        var program = fixture.Workspace.Documents.Single(d => d.FileName == "Program.cs");
        Assert.Equal("Console.WriteLine(\"Experiment\");\n", program.Document.Text);
        Assert.False(program.IsDirty);
        Assert.True(git.IsClean);
    }

    [AvaloniaFact]
    public async Task Edits_typed_while_a_pull_runs_are_kept_and_merged_with_it()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        var program = fixture.Workspace.Documents.Single(d => d.FileName == "Program.cs");
        var original = program.Document.Text;
        var theirs = original.Replace("What's your name? ", "Who are you? ", StringComparison.Ordinal);
        fixture.GitHub.Push(GitWorkspaceFixture.Repository, "main", "Ask differently", new Dictionary<string, string?> { [Program] = theirs });
        const string Typed = "// Typed while the pull ran\n";

        await TypeWhileItRunsAsync(fixture, () => git.PullCommand.ExecuteAsync(null), () => program.Document.Insert(program.Document.TextLength, Typed));

        Assert.Null(git.ErrorText);
        Assert.Equal(theirs + Typed, program.Document.Text);
        Assert.True(program.IsDirty);
        Assert.Equal(theirs, await File.ReadAllTextAsync(fixture.PathOf(Program)));

        // One Undo goes back to exactly what the pull wrote, which is the saved state.
        program.Document.UndoStack.Undo();
        Assert.Equal(theirs, program.Document.Text);
        Assert.False(program.IsDirty);
    }

    [AvaloniaFact]
    public async Task Edits_typed_while_switching_branches_are_kept_even_where_they_conflict()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        var head = fixture.GitHub.Head(GitWorkspaceFixture.Repository, "main");
        await fixture.GitHub.CreateClient().CreateBranchAsync(FakeGitHub.Login, GitWorkspaceFixture.Repository, "experiment", head);
        fixture.GitHub.Push(GitWorkspaceFixture.Repository, "experiment", "Try something", new Dictionary<string, string?> { [Program] = "Console.WriteLine(\"Experiment\");\n" });
        await git.OpenBranchPickerCommand.ExecuteAsync(null);
        var program = fixture.Workspace.Documents.Single(d => d.FileName == "Program.cs");
        var question = program.Document.Text.IndexOf("What's your name? ", StringComparison.Ordinal);

        await TypeWhileItRunsAsync(fixture, () => git.SwitchBranchCommand.ExecuteAsync(git.Branches[1]), () => program.Document.Replace(question, "What's your name? ".Length, "Your name, please? "));

        Assert.Null(git.ErrorText);
        Assert.Equal("experiment", git.BranchName);
        Assert.Contains(fixture.Workspace.Documents, d => d == program);
        Assert.Contains("Your name, please? ", program.Document.Text, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(\"Experiment\");", program.Document.Text, StringComparison.Ordinal);
        Assert.Contains("<<<<<<< Yours", program.Document.Text, StringComparison.Ordinal);
        Assert.True(program.IsDirty);
        Assert.Contains("look for <<<<<<< Yours", git.InfoText, StringComparison.Ordinal);
        Assert.Equal("Console.WriteLine(\"Experiment\");\n", await File.ReadAllTextAsync(fixture.PathOf(Program)));
    }

    /// <summary>Starts a Git operation, holds it at its first request to GitHub, types, then lets it finish.</summary>
    private static async Task TypeWhileItRunsAsync(GitWorkspaceFixture fixture, Func<Task> operation, Action type)
    {
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.GitHub.BeforeRequest = () =>
        {
            fixture.GitHub.BeforeRequest = null;
            reached.TrySetResult();
            return resume.Task;
        };

        var running = operation();
        await reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        type();
        resume.SetResult();
        await running.WaitAsync(TimeSpan.FromSeconds(60));
    }

    [AvaloniaFact]
    public async Task History_lists_the_branch_newest_first_and_opens_commits_on_GitHub()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync();
        var git = fixture.Git;
        var sha = fixture.GitHub.Push(GitWorkspaceFixture.Repository, "main", "Second commit\n\nWith a body.", new Dictionary<string, string?> { ["README.md"] = "# Hello\n" });

        await git.OpenHistoryCommand.ExecuteAsync(null);

        Assert.True(git.IsHistoryOpen);
        Assert.Equal(["Second commit", "Initial commit"], git.History.Select(c => c.Summary));
        Assert.Equal("Test User", git.History[0].Author);
        await git.History[0].Open.ExecuteAsync(git.History[0]);
        Assert.Equal($"https://github.com/tester/hello/commit/{sha}", Assert.Single(fixture.Browser.Opened).ToString());
    }

    [AvaloniaFact]
    public async Task Committing_while_signed_out_asks_to_sign_in()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenLinkedSampleAsync(signIn: false);
        var git = fixture.Git;
        await fixture.WriteAsync("NOTES.md", "# Notes\n");

        git.CommitMessage = "Add notes";
        await git.CommitCommand.ExecuteAsync(null);

        Assert.Equal("Sign in to GitHub to commit.", git.ErrorText);
        Assert.True(fixture.AccountView.IsOpen);

        fixture.AccountView.Token = FakeGitHub.Token;
        await fixture.AccountView.SignInCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => git.ErrorText is null, because: "the window to notice the sign-in");
        await git.CommitCommand.ExecuteAsync(null);
        Assert.Equal("Add notes", fixture.GitHub.HeadMessage(GitWorkspaceFixture.Repository, "main"));
    }

    [AvaloniaFact]
    public async Task A_folder_Git_cloned_from_GitHub_offers_to_link_it_instead_of_publishing_it_again()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenGitCloneOfSampleAsync();
        var git = fixture.Git;
        Assert.True(git.IsLinkCheckoutVisible);
        Assert.False(git.IsPublishVisible);
        Assert.False(git.IsLinked);
        Assert.Contains("a Git clone of tester/hello on main", git.LinkCheckoutText, StringComparison.Ordinal);
        Assert.False(File.Exists(fixture.PathOf(".tabstudio/sync.json")));

        await git.LinkCheckoutCommand.ExecuteAsync(null);

        Assert.Null(git.ErrorText);
        Assert.True(git.IsLinked);
        Assert.False(git.IsPublishVisible);
        Assert.False(git.IsLinkCheckoutVisible);
        Assert.Equal("tester/hello", git.RepositoryName);
        Assert.Equal("main", git.BranchName);
        Assert.True(git.IsClean);

        await fixture.WriteAsync(Greeter, "// Edited in a clone\n");
        var change = Assert.Single(git.Changes);
        Assert.Equal(Greeter, change.RelativePath);
        Assert.Equal(GitFileStatus.Modified, change.Status);
    }

    [AvaloniaFact]
    public async Task Publishing_creates_the_repository_and_links_the_folder()
    {
        using var fixture = new GitWorkspaceFixture();
        await fixture.OpenUnlinkedSampleAsync();
        var git = fixture.Git;
        Assert.True(git.IsPublishVisible);
        Assert.False(git.IsLinked);
        Assert.Equal("HelloTablet", git.PublishName);

        await git.PublishCommand.ExecuteAsync(null);
        Assert.True(fixture.AccountView.IsOpen);

        fixture.AccountView.Token = FakeGitHub.Token;
        await fixture.AccountView.SignInCommand.ExecuteAsync(null);
        Assert.False(fixture.AccountView.IsOpen);
        git.PublishName = "hello-tablet";
        await git.PublishCommand.ExecuteAsync(null);

        Assert.Null(git.ErrorText);
        Assert.True(git.IsLinked);
        Assert.False(git.IsPublishVisible);
        Assert.Equal("tester/hello-tablet", git.RepositoryName);
        Assert.True(git.IsClean);
        var files = fixture.GitHub.Files("hello-tablet", "main");
        Assert.Contains("HelloTablet.slnx", files.Keys);
        Assert.Contains(Greeter, files.Keys);
        Assert.Equal(GitChangesViewModel.DefaultGitIgnore, files[".gitignore"]);
        Assert.DoesNotContain(files.Keys, k => k.StartsWith(".tabstudio", StringComparison.Ordinal));
        Assert.Contains("private repository", git.InfoText, StringComparison.Ordinal);
    }
}
