using TabStudio.App.Services;
using TabStudio.SourceControl;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class GitHubImporterTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();
    private readonly AppPaths _paths;
    private readonly GitHubAccount _account;

    public GitHubImporterTests()
    {
        _paths = new AppPaths(_temp.Path);
        _account = _github.CreateAccount(_paths);
        _github.AddRepository("demo", new Dictionary<string, string>
        {
            ["README.md"] = "# Demo\n",
            ["src/App.cs"] = "class App;\n",
        });
    }

    [Fact]
    public async Task Imports_a_repository_into_a_folder_linked_to_it()
    {
        var stages = new List<string>();

        var folder = await new GitHubImporter(_account, _paths).ImportAsync(
            new GitHubRepository(FakeGitHub.Login, "demo", null),
            new SynchronousProgress<ImportProgress>(p => stages.Add(p.Stage)));

        Assert.Equal(Path.Combine(_paths.Projects, "demo"), folder);
        Assert.Equal("class App;\n", await File.ReadAllTextAsync(Path.Combine(folder, "src", "App.cs")));
        Assert.Equal(folder, GitHubSync.FindRoot(Path.Combine(folder, "src", "App.cs")));
        var sync = GitHubSync.Open(_account.CreateClient(), folder);
        Assert.Equal("main", sync?.Link.Branch);
        Assert.Contains("Connecting to GitHub", stages);
    }

    [Fact]
    public async Task A_download_Android_ended_is_cleaned_up_and_the_next_one_takes_its_folder()
    {
        // Android ended TabStudio in the middle of a download: the half folder stayed for good, and the next download
        // of the repository went to demo-2.
        var leftover = Path.Combine(_paths.Projects, "demo");
        Directory.CreateDirectory(Path.Combine(leftover, SyncMetadata.FolderName));
        await File.WriteAllTextAsync(Path.Combine(leftover, "README.md"), "# De");
        await File.WriteAllTextAsync(Path.Combine(leftover, SyncMetadata.FolderName, "incomplete"), "0000000");
        var finished = Path.Combine(_paths.Projects, "mine");
        Directory.CreateDirectory(finished);
        await File.WriteAllTextAsync(Path.Combine(finished, "notes.txt"), "keep");
        Assert.True(GitHubSync.IsIncompleteClone(leftover));

        var folder = await new GitHubImporter(_account, _paths).ImportAsync(new GitHubRepository(FakeGitHub.Login, "demo", null));

        Assert.Equal(leftover, folder);
        Assert.Equal("# Demo\n", await File.ReadAllTextAsync(Path.Combine(folder, "README.md")));
        Assert.False(GitHubSync.IsIncompleteClone(folder));
        Assert.True(File.Exists(Path.Combine(finished, "notes.txt")));
    }

    [Fact]
    public async Task A_private_repository_needs_an_account_and_opens_once_signed_in()
    {
        _github.AddRepository("secret", new Dictionary<string, string> { ["a.txt"] = "a\n" }, isPrivate: true);
        var importer = new GitHubImporter(_account, _paths);
        var repository = new GitHubRepository(FakeGitHub.Login, "secret", null);

        var error = await Assert.ThrowsAsync<ImportException>(() => importer.ImportAsync(repository));
        Assert.Contains("Sign in to GitHub", error.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_paths.Projects, "secret")));

        await _account.SignInAsync(FakeGitHub.Token);
        var folder = await importer.ImportAsync(repository);
        Assert.Equal("a\n", await File.ReadAllTextAsync(Path.Combine(folder, "a.txt")));
    }

    [Fact]
    public async Task An_unknown_branch_is_reported_in_plain_words()
    {
        var error = await Assert.ThrowsAsync<ImportException>(() =>
            new GitHubImporter(_account, _paths).ImportAsync(new GitHubRepository(FakeGitHub.Login, "demo", "no-such-branch")));

        Assert.Contains("no branch, tag or commit 'no-such-branch'", error.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateDirectories(_paths.Projects));
    }

    [Fact]
    public async Task A_folder_address_opens_its_branch()
    {
        // github.com/owner/repo/tree/main/src: "main/src" is no branch, so the import failed.
        var folder = await new GitHubImporter(_account, _paths).ImportAsync(new GitHubRepository(FakeGitHub.Login, "demo", "main/src"));

        Assert.Equal("class App;\n", await File.ReadAllTextAsync(Path.Combine(folder, "src", "App.cs")));
        Assert.Equal("main", GitHubSync.Open(_account.CreateClient(), folder)?.Link.Branch);
        Assert.Single(Directory.EnumerateDirectories(_paths.Projects));
    }

    public void Dispose()
    {
        _github.Dispose();
        _temp.Dispose();
    }

    /// <summary>Reports on the calling thread, so a test sees every report before the import returns.</summary>
    private sealed class SynchronousProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
