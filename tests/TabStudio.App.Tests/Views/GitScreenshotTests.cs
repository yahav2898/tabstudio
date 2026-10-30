using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Views;

/// <summary>Screenshots of the Git experience: the Git Changes window, the diff view, signing in and publishing.</summary>
public sealed class GitScreenshotTests : IDisposable
{
    private const string Repository = "hello";
    private const string Program = "src/HelloTablet/Program.cs";
    private const string Greeter = "src/Greetings/Greeter.cs";

    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();
    private readonly AppPaths _paths;
    private readonly ServiceProvider _services;
    private readonly MainViewModel _main;

    public GitScreenshotTests()
    {
        _paths = new AppPaths(_temp.Path);
        _services = AppServices.Create(_paths, services => services.AddSingleton(_github.CreateAccount(_paths)));
        _main = _services.GetRequiredService<MainViewModel>();
    }

    [AvaloniaFact]
    public async Task Git_changes_with_changes_and_a_conflict()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var git = workspace.GitChanges;
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.GitChanges);
        await UiTestHelpers.WaitUntilAsync(() => git.IncomingCommits is not null, because: "the first fetch");

        var greeter = await File.ReadAllTextAsync(PathOf(Greeter));
        await WriteAsync(Greeter, greeter.Replace("\"developer\"", "\"friend\"", StringComparison.Ordinal));
        _github.Push(Repository, "main", "Call people coders", new Dictionary<string, string?> { [Greeter] = greeter.Replace("\"developer\"", "\"coder\"", StringComparison.Ordinal) });
        await git.PullCommand.ExecuteAsync(null);

        var program = await File.ReadAllTextAsync(PathOf(Program));
        await WriteAsync(Program, program.Replace("What's your name? ", "Who's there? ", StringComparison.Ordinal));
        await WriteAsync("docs/NOTES.md", "# Notes\n");
        git.Changes.Single(c => c.RelativePath == Program).IsSelected = true;
        git.CommitMessage = "Ask who is there";
        await UiTestHelpers.WaitUntilAsync(() => !git.IsBusy);

        UiTestHelpers.SaveScreenshot(window, "07-git-changes");

        Assert.Single(git.Conflicts);
        Assert.Equal(2, git.Changes.Count);
        Assert.True(window.GetVisualDescendants().OfType<GitChangesView>().Single().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Diff_view_and_Solution_Explorer_glyphs()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var git = workspace.GitChanges;
        var program = await File.ReadAllTextAsync(PathOf(Program));
        await WriteAsync(Program, program
            .Replace("What's your name? ", "Who's there? ", StringComparison.Ordinal)
            .Replace("    new Language(\"Rust\", 2015, IsStaticallyTyped: true),\n", "    new Language(\"Rust\", 2015, IsStaticallyTyped: true),\n    new Language(\"Go\", 2009, IsStaticallyTyped: true),\n", StringComparison.Ordinal)
            .Replace("return 0;", "return languages.Length;", StringComparison.Ordinal));
        await WriteAsync("docs/NOTES.md", "# Notes\n");

        await git.Changes.Single(c => c.RelativePath == Program).CompareCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => window.GetVisualDescendants().OfType<DiffView>().Any(v => v.IsEffectivelyVisible), because: "the diff tab");

        UiTestHelpers.SaveScreenshot(window, "08-diff");

        Assert.True(workspace.ActiveDocument?.IsComparison);
        Assert.Contains(workspace.Explorer.Rows, r => r.Name == "Program.cs" && r.GitGlyph == ExplorerGitGlyph.Modified);
    }

    [AvaloniaFact]
    public async Task Branches_pull_request_and_history_dialogs()
    {
        var window = Show(1280, 800);
        var workspace = await OpenLinkedSampleAsync();
        var git = workspace.GitChanges;
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.GitChanges);
        var head = _github.Head(Repository, "main");
        await _github.CreateClient().CreateBranchAsync(FakeGitHub.Login, Repository, "release/1.0", head);
        await _github.CreateClient().CreateBranchAsync(FakeGitHub.Login, Repository, "feature/greetings", head);
        _github.Push(Repository, "main", "Explain how to run the sample", new Dictionary<string, string?> { ["README.md"] = "# HelloTablet\n\nPress F5.\n" });
        _github.Push(Repository, "main", "Greet in the evening too", new Dictionary<string, string?> { ["docs/NOTES.md"] = "# Notes\n" });

        await git.OpenBranchPickerCommand.ExecuteAsync(null);
        UiTestHelpers.SaveScreenshot(window, "11-git-branches");
        Assert.Equal(3, git.Branches.Count);

        await git.SwitchBranchCommand.ExecuteAsync(git.Branches.Single(b => b.Name == "feature/greetings"));
        await WriteAsync(Greeter, (await File.ReadAllTextAsync(PathOf(Greeter))).Replace("\"developer\"", "\"friend\"", StringComparison.Ordinal));
        git.CommitMessage = "Call people friends";
        await git.CommitCommand.ExecuteAsync(null);
        await git.OpenPullRequestFormCommand.ExecuteAsync(null);
        git.PullRequestDescription = "Friendlier greetings for everyone who runs the sample.";
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "12-git-pull-request");
        Assert.True(git.IsPullRequestFormOpen);

        await git.CreatePullRequestCommand.ExecuteAsync(null);
        await git.OpenHistoryCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => true);
        UiTestHelpers.SaveScreenshot(window, "13-git-history");
        Assert.Equal(2, git.History.Count);
        Assert.True(git.HasPullRequest);

        // Escape closes a dialog, wherever the keyboard focus was left, and so does a tap on the scrim beside it.
        window.KeyPressQwerty(Avalonia.Input.PhysicalKey.Escape, Avalonia.Input.RawInputModifiers.None);
        Assert.False(git.IsHistoryOpen);
        await git.OpenBranchPickerCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => true);
        var scrim = window.GetVisualDescendants().OfType<Avalonia.Controls.Panel>().Single(p => p.Name == "BranchPickerScrim");
        var corner = scrim.TranslatePoint(new Avalonia.Point(8, 8), window)!.Value;
        window.MouseDown(corner, Avalonia.Input.MouseButton.Left);
        window.MouseUp(corner, Avalonia.Input.MouseButton.Left);
        Assert.False(git.IsBranchPickerOpen);
    }

    [AvaloniaFact]
    public async Task Signing_in_to_GitHub()
    {
        var window = Show(1280, 800);
        _main.Start.SelectCommand.Execute(StartAction.GitHub);
        _main.GitHubAccount.OpenCommand.Execute(null);
        _main.GitHubAccount.Token = "ghp_example";
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "09-github-sign-in");

        Assert.True(_main.GitHubAccount.IsOpen);
        Assert.True(window.GetVisualDescendants().OfType<GitHubSignInView>().Single().IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public async Task Publishing_a_folder_to_GitHub()
    {
        var window = Show(1280, 800);
        await _services.GetRequiredService<GitHubAccount>().SignInAsync(FakeGitHub.Token);
        var solution = await _services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        await _main.OpenWorkspaceAsync(solution);
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.GitChanges.HasWorkspace, because: "the Git Changes window");
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.GitChanges);
        await UiTestHelpers.WaitUntilAsync(() => true);

        UiTestHelpers.SaveScreenshot(window, "10-git-publish");

        Assert.True(workspace.GitChanges.IsPublishVisible);
    }

    public void Dispose()
    {
        _main.Workspace?.Dispose();
        _services.Dispose();
        _github.Dispose();
        _temp.Dispose();
    }

    private string PathOf(string relativePath) =>
        Path.Combine(_main.Workspace!.Workspace!.RootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));

    private async Task WriteAsync(string relativePath, string content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
        await _main.Workspace!.GitChanges.RefreshAsync();
    }

    private MainWindow Show(double width, double height)
    {
        var window = new MainWindow { DataContext = _main, Width = width, Height = height };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>Signs in, puts the sample on the fake GitHub and opens it from there, as the start screen does.</summary>
    private async Task<WorkspaceViewModel> OpenLinkedSampleAsync()
    {
        var account = _services.GetRequiredService<GitHubAccount>();
        await account.SignInAsync(FakeGitHub.Token);
        _github.AddRepository(Repository, await GitWorkspaceFixture.SampleFilesAsync(_paths));
        var folder = await _services.GetRequiredService<GitHubImporter>().ImportAsync(new GitHubRepository(FakeGitHub.Login, Repository, null));
        await _main.OpenWorkspaceAsync(Path.Combine(folder, "HelloTablet.slnx"));
        var workspace = _main.Workspace!;
        await UiTestHelpers.WaitUntilAsync(() => workspace.GitChanges.IsLinked, because: "the link to GitHub");
        var document = workspace.ActiveDocument!;
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 0, because: "syntax colors");
        return workspace;
    }
}
