using System.Text;
using TabStudio.SourceControl.GitHub;
using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

/// <summary>Clone, status, commit, pull, branches, pull requests and publishing, against <see cref="FakeGitHub"/>.</summary>
public sealed class GitHubSyncTests : IDisposable
{
    private const string Repo = "demo";

    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();

    public GitHubSyncTests()
    {
        _github.AddRepository(Repo, new Dictionary<string, string>
        {
            ["README.md"] = "# Demo\n",
            ["src/Program.cs"] = "Console.WriteLine(\"one\");\nConsole.WriteLine(\"two\");\nConsole.WriteLine(\"three\");\n",
            ["src/Util.cs"] = "class Util;\n",
            [".gitignore"] = "bin/\nobj/\n",
        });
    }

    private string Folder => Path.Combine(_temp.Path, "work");

    [Fact]
    public async Task Clone_writes_the_files_and_links_the_folder()
    {
        var sync = await CloneAsync();

        Assert.Equal("# Demo\n", Read("README.md"));
        Assert.Equal("class Util;\n", Read("src/Util.cs"));
        Assert.Equal(new GitHubLink(FakeGitHub.Login, Repo, "main", _github.Head(Repo, "main")), sync.Link);
        Assert.True(SyncMetadata.Exists(Folder));
        Assert.Equal(Folder, GitHubSync.FindRoot(Path.Combine(Folder, "src", "Program.cs")));

        var status = await sync.GetStatusAsync(checkGitHub: true);
        Assert.Empty(status.Changes);
        Assert.Equal(0, status.IncomingCommits);
    }

    [Fact]
    public async Task Clone_of_a_large_repository_uses_one_archive_and_downloads_only_what_it_lacks()
    {
        var files = Enumerable.Range(0, 60).ToDictionary(i => $"src/File{i:00}.cs", i => $"class File{i};\n");
        _github.AddRepository("big", files);
        _github.ArchiveOmits.Add("src/File07.cs");
        _github.ArchiveAlters.Add("src/File42.cs");

        var sync = await GitHubSync.CloneAsync(_github.CreateClient(signedIn: false), FakeGitHub.Login, "big", null, Folder);

        Assert.All(files, f => Assert.Equal(f.Value, Read(f.Key)));
        Assert.Single(_github.Requests, r => r.Contains("/zipball/", StringComparison.Ordinal));
        Assert.Equal(2, _github.Requests.Count(r => r.Contains("/git/blobs/", StringComparison.Ordinal)));
        Assert.Empty((await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task Clone_reads_huge_trees_folder_by_folder()
    {
        _github.TruncateRecursiveTrees = true;

        await CloneAsync();

        Assert.Equal("class Util;\n", Read("src/Util.cs"));
        Assert.Equal("# Demo\n", Read("README.md"));
    }

    [Fact]
    public async Task Clone_of_a_commit_is_detached_and_cannot_commit()
    {
        var first = _github.Head(Repo, "main");
        _github.Push(Repo, "main", "Second", new Dictionary<string, string?> { ["README.md"] = "# Changed\n" });

        var sync = await GitHubSync.CloneAsync(_github.CreateClient(), FakeGitHub.Login, Repo, first[..7], Folder);
        Write("README.md", "# Mine\n");

        Assert.Equal(first, sync.Link.BaseCommit);
        Assert.True(sync.Link.IsDetached);
        var error = await Assert.ThrowsAsync<SyncException>(() => sync.CommitAsync("Try"));
        Assert.Equal(SyncFailure.Detached, error.Failure);
    }

    [Fact]
    public async Task Status_lists_changes_and_leaves_out_ignored_files()
    {
        var sync = await CloneAsync();
        Write("src/Program.cs", "Console.WriteLine(\"changed\");\n");
        Write("src/New.cs", "class New;\n");
        Write("bin/Debug/app.dll", "binary");
        File.Delete(Path.Combine(Folder, "src", "Util.cs"));

        var status = await sync.GetStatusAsync();

        Assert.Equal(
            new[] { new FileChange("src/New.cs", ChangeKind.Added), new FileChange("src/Program.cs", ChangeKind.Modified), new FileChange("src/Util.cs", ChangeKind.Deleted) },
            status.Changes);
    }

    [Fact]
    public async Task Commit_creates_the_commit_on_GitHub_and_moves_the_branch()
    {
        var sync = await CloneAsync();
        var before = _github.Head(Repo, "main");
        Write("src/Program.cs", "Console.WriteLine(\"changed\");\n");
        Write("docs/notes.md", "Notes\n");
        File.Delete(Path.Combine(Folder, "src", "Util.cs"));

        var result = await sync.CommitAsync("Change things");

        Assert.Equal(3, result.Files);
        Assert.Equal(result.Commit.Sha, _github.Head(Repo, "main"));
        Assert.Equal(new[] { before }, _github.HeadParents(Repo, "main"));
        Assert.Equal("Change things", _github.HeadMessage(Repo, "main"));
        var files = _github.Files(Repo, "main");
        Assert.Equal("Console.WriteLine(\"changed\");\n", files["src/Program.cs"]);
        Assert.Equal("Notes\n", files["docs/notes.md"]);
        Assert.False(files.ContainsKey("src/Util.cs"));
        Assert.True(files.ContainsKey("README.md"));
        Assert.Empty((await sync.GetStatusAsync()).Changes);
        Assert.Equal(result.Commit.Sha, GitHubSync.Open(_github.CreateClient(), Folder)!.Link.BaseCommit);
    }

    [Fact]
    public async Task Commit_can_include_only_some_files()
    {
        var sync = await CloneAsync();
        Write("README.md", "# Committed\n");
        Write("src/Util.cs", "class Util { }\n");

        await sync.CommitAsync("Only the readme", ["README.md"]);

        Assert.Equal("# Committed\n", _github.Files(Repo, "main")["README.md"]);
        Assert.Equal("class Util;\n", _github.Files(Repo, "main")["src/Util.cs"]);
        Assert.Equal(new[] { new FileChange("src/Util.cs", ChangeKind.Modified) }, (await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task Commit_refuses_when_there_is_nothing_to_commit_or_no_account()
    {
        var sync = await CloneAsync();
        var nothing = await Assert.ThrowsAsync<SyncException>(() => sync.CommitAsync("Empty"));
        Assert.Equal(SyncFailure.NothingToCommit, nothing.Failure);

        Write("README.md", "# Changed\n");
        var signedOut = GitHubSync.Open(_github.CreateClient(signedIn: false), Folder)!;
        var error = await Assert.ThrowsAsync<SyncException>(() => signedOut.CommitAsync("Change"));
        Assert.Equal(SyncFailure.NotSignedIn, error.Failure);
    }

    [Fact]
    public async Task Commit_asks_for_a_pull_when_GitHub_has_new_commits()
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Someone else", new Dictionary<string, string?> { ["other.txt"] = "x\n" });
        Write("README.md", "# Mine\n");

        var error = await Assert.ThrowsAsync<SyncException>(() => sync.CommitAsync("Mine"));

        Assert.Equal(SyncFailure.BehindRemote, error.Failure);
        Assert.Equal(1, (await sync.GetStatusAsync(checkGitHub: true)).IncomingCommits);
    }

    [Fact]
    public async Task A_protected_branch_says_so_instead_of_asking_for_a_pull()
    {
        // GitHub refuses an update to a protected branch as it refuses one that is behind, and "pull first" was
        // answered by "up to date", round and round.
        var sync = await CloneAsync();
        _github.ProtectedBranches.Add("main");
        Write("README.md", "# Mine\n");

        var error = await Assert.ThrowsAsync<SyncException>(() => sync.CommitAsync("Mine"));

        Assert.NotEqual(SyncFailure.BehindRemote, error.Failure);
        Assert.Contains("Protected branch update failed", error.Message, StringComparison.Ordinal);
        Assert.Contains("open a pull request", error.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { new FileChange("README.md", ChangeKind.Modified) }, (await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task A_push_that_races_another_one_is_refused_and_nothing_is_lost()
    {
        var sync = await CloneAsync();
        Write("README.md", "# Mine\n");
        _github.BeforeBranchUpdate = () => _github.Push(Repo, "main", "Racing", new Dictionary<string, string?> { ["race.txt"] = "x\n" });

        var error = await Assert.ThrowsAsync<SyncException>(() => sync.CommitAsync("Mine"));

        Assert.Equal(SyncFailure.BehindRemote, error.Failure);
        Assert.Equal("Racing", _github.HeadMessage(Repo, "main"));
        Assert.Equal(new[] { new FileChange("README.md", ChangeKind.Modified) }, (await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task Pull_brings_in_changes_from_GitHub_and_keeps_local_ones()
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Theirs", new Dictionary<string, string?>
        {
            ["README.md"] = "# Updated on GitHub\n",
            ["src/Added.cs"] = "class Added;\n",
            ["src/Util.cs"] = null,
        });
        Write("docs/local.md", "Mine\n");

        var result = await sync.PullAsync();

        Assert.Equal(new[] { "README.md", "src/Added.cs", "src/Util.cs" }, result.Updated);
        Assert.Empty(result.Conflicts);
        Assert.Equal("# Updated on GitHub\n", Read("README.md"));
        Assert.Equal("class Added;\n", Read("src/Added.cs"));
        Assert.False(File.Exists(Path.Combine(Folder, "src", "Util.cs")));
        Assert.Equal("Mine\n", Read("docs/local.md"));
        Assert.Equal(_github.Head(Repo, "main"), sync.Link.BaseCommit);
        Assert.Equal(new[] { new FileChange("docs/local.md", ChangeKind.Added) }, (await sync.GetStatusAsync()).Changes);
        Assert.True((await sync.PullAsync()).WasUpToDate);
    }

    [Fact]
    public async Task Pull_merges_a_file_changed_on_both_sides_in_different_places()
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Theirs", new Dictionary<string, string?>
        {
            ["src/Program.cs"] = "Console.WriteLine(\"one\");\nConsole.WriteLine(\"two\");\nConsole.WriteLine(\"THREE\");\n",
        });
        Write("src/Program.cs", "Console.WriteLine(\"ONE\");\nConsole.WriteLine(\"two\");\nConsole.WriteLine(\"three\");\n");

        var result = await sync.PullAsync();

        Assert.Empty(result.Conflicts);
        Assert.Equal("Console.WriteLine(\"ONE\");\nConsole.WriteLine(\"two\");\nConsole.WriteLine(\"THREE\");\n", Read("src/Program.cs"));

        await sync.CommitAsync("Merged");
        Assert.Equal(Read("src/Program.cs"), _github.Files(Repo, "main")["src/Program.cs"]);
    }

    [Fact]
    public async Task A_conflicting_pull_writes_markers_and_blocks_the_commit_until_resolved()
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Theirs", new Dictionary<string, string?> { ["README.md"] = "# Theirs\n" });
        Write("README.md", "# Mine\n");

        var result = await sync.PullAsync();

        Assert.Equal(new[] { "README.md" }, result.Conflicts);
        Assert.Equal("<<<<<<< Yours\n# Mine\n=======\n# Theirs\n>>>>>>> GitHub\n", Read("README.md"));
        Assert.Equal(new[] { "README.md" }, sync.Metadata.Conflicts);
        var blocked = await Assert.ThrowsAsync<SyncException>(() => sync.CommitAsync("Too early"));
        Assert.Equal(SyncFailure.UnresolvedConflicts, blocked.Failure);

        Write("README.md", "# Both\n");
        await sync.CommitAsync("Resolved");

        Assert.Equal("# Both\n", _github.Files(Repo, "main")["README.md"]);
        Assert.Empty(sync.Metadata.Conflicts);
    }

    [Theory]
    [InlineData(ConflictResolution.KeepMine, "# Mine\n")]
    [InlineData(ConflictResolution.TakeTheirs, "# Theirs\n")]
    public async Task A_conflict_can_be_settled_by_choosing_a_side(ConflictResolution resolution, string expected)
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Theirs", new Dictionary<string, string?> { ["README.md"] = "# Theirs\n" });
        Write("README.md", "# Mine\n");
        await sync.PullAsync();

        await sync.ResolveAsync("README.md", resolution);

        Assert.Equal(expected, Read("README.md"));
        Assert.Empty(sync.Metadata.Conflicts);
    }

    [Fact]
    public async Task A_file_deleted_on_GitHub_but_changed_here_is_kept_as_a_conflict()
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Delete", new Dictionary<string, string?> { ["src/Util.cs"] = null });
        Write("src/Util.cs", "class Util { int x; }\n");

        var result = await sync.PullAsync();

        Assert.Equal(new[] { "src/Util.cs" }, result.Conflicts);
        Assert.Equal("class Util { int x; }\n", Read("src/Util.cs"));
        Assert.Equal(new[] { new FileChange("src/Util.cs", ChangeKind.Added) }, (await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task The_same_change_on_both_sides_is_not_a_conflict()
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Same", new Dictionary<string, string?> { ["README.md"] = "# Same\n" });
        Write("README.md", "# Same\n");

        var result = await sync.PullAsync();

        Assert.Empty(result.Conflicts);
        Assert.Empty((await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task A_new_branch_takes_the_next_commit_and_a_pull_request_can_be_opened()
    {
        var sync = await CloneAsync();
        var main = _github.Head(Repo, "main");
        Write("README.md", "# Feature\n");

        await sync.CreateBranchAsync("feature/readme");
        await sync.CommitAsync("Feature work");
        var pull = await sync.CreatePullRequestAsync("Improve the readme", "Details");

        Assert.Equal(main, _github.Head(Repo, "main"));
        Assert.Equal("# Feature\n", _github.Files(Repo, "feature/readme")["README.md"]);
        Assert.Equal("feature/readme", sync.Link.Branch);
        Assert.Equal(("Improve the readme", "feature/readme", "main"), Assert.Single(_github.PullRequests(Repo)));
        Assert.Equal(1, pull.Number);
        Assert.Equal("main", pull.BaseBranch);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("a..b")]
    [InlineData("-dash")]
    [InlineData("ends/")]
    [InlineData("x.lock")]
    [InlineData("feature/.hidden")]
    [InlineData("what?")]
    public void Invalid_branch_names_are_caught(string name) => Assert.False(BranchNames.IsValid(name, out _));

    [Theory]
    [InlineData("main")]
    [InlineData("feature/login-page")]
    [InlineData("release-1.2")]
    public void Valid_branch_names_pass(string name) => Assert.True(BranchNames.IsValid(name, out _));

    [Fact]
    public async Task Switching_branches_replaces_the_files_but_not_over_uncommitted_changes()
    {
        var sync = await CloneAsync();
        await sync.CreateBranchAsync("other");
        _github.Push(Repo, "other", "Other", new Dictionary<string, string?> { ["only-on-other.txt"] = "other\n", ["README.md"] = "# Other\n" });
        await sync.PullAsync();
        Assert.Equal("# Other\n", Read("README.md"));

        await sync.SwitchBranchAsync("main");
        Assert.Equal("# Demo\n", Read("README.md"));
        Assert.False(File.Exists(Path.Combine(Folder, "only-on-other.txt")));
        Assert.Equal("main", sync.Link.Branch);

        Write("README.md", "# Unsaved work\n");
        var error = await Assert.ThrowsAsync<SyncException>(() => sync.SwitchBranchAsync("other"));
        Assert.Equal(SyncFailure.UncommittedChanges, error.Failure);
        Assert.Equal("# Unsaved work\n", Read("README.md"));
    }

    [Fact]
    public async Task Discard_restores_changed_and_deleted_files_and_removes_new_ones()
    {
        var sync = await CloneAsync();
        Write("README.md", "# Oops\n");
        Write("src/Scratch.cs", "class Scratch;\n");
        File.Delete(Path.Combine(Folder, "src", "Util.cs"));

        await sync.DiscardAsync(["README.md", "src/Scratch.cs", "src/Util.cs"]);

        Assert.Equal("# Demo\n", Read("README.md"));
        Assert.Equal("class Util;\n", Read("src/Util.cs"));
        Assert.False(File.Exists(Path.Combine(Folder, "src", "Scratch.cs")));
        Assert.Empty((await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task Publish_creates_a_repository_holding_exactly_the_folders_files()
    {
        Write("App/Program.cs", "return 0;\n");
        Write("App/App.csproj", "<Project />\n");
        Write(".gitignore", "bin/\n");
        Write("App/bin/App.dll", "binary");

        var sync = await GitHubSync.PublishAsync(_github.CreateClient(), Folder, "published", isPrivate: true);

        Assert.True(_github.HasRepository("published"));
        Assert.Equal(new[] { ".gitignore", "App/App.csproj", "App/Program.cs" }, _github.Files("published", "main").Keys.Order(StringComparer.Ordinal));
        Assert.Equal("main", sync.Link.Branch);
        Assert.Empty((await sync.GetStatusAsync()).Changes);
        var again = await Assert.ThrowsAsync<SyncException>(() => GitHubSync.PublishAsync(_github.CreateClient(), Folder, "again", isPrivate: false));
        Assert.Contains("already linked", again.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task History_and_branches_come_from_GitHub()
    {
        var sync = await CloneAsync();
        _github.Push(Repo, "main", "Second\n\nWith a body", new Dictionary<string, string?> { ["a.txt"] = "a\n" });

        var history = await sync.GetHistoryAsync();
        var branches = await sync.GetBranchesAsync();

        Assert.Equal(new[] { "Second", "Initial commit" }, history.Select(c => c.Summary));
        Assert.Equal("main", Assert.Single(branches).Name);
    }

    [Fact]
    public async Task Failures_are_described_for_people()
    {
        var error = await Assert.ThrowsAsync<GitHubApiException>(() =>
            GitHubSync.CloneAsync(_github.CreateClient(), FakeGitHub.Login, "missing", null, Folder));

        Assert.True(error.IsNotFound);
        Assert.Contains("could not find", SyncErrors.Describe(error), StringComparison.Ordinal);
        Assert.Contains("token", SyncErrors.Describe(new GitHubApiException(System.Net.HttpStatusCode.Unauthorized, "Bad credentials")), StringComparison.Ordinal);
        var limited = new GitHubApiException(System.Net.HttpStatusCode.Forbidden, "API rate limit exceeded", DateTimeOffset.UtcNow.AddMinutes(30));
        Assert.Contains("limit", SyncErrors.Describe(limited), StringComparison.Ordinal);
    }

    public void Dispose() => _temp.Dispose();

    private Task<GitHubSync> CloneAsync() =>
        GitHubSync.CloneAsync(_github.CreateClient(), FakeGitHub.Login, Repo, null, Folder);

    private string Read(string path) => File.ReadAllText(Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar)), Encoding.UTF8);

    private void Write(string path, string content)
    {
        var full = Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
