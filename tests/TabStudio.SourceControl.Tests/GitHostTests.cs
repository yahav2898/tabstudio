using TabStudio.SourceControl.Git;
using TabStudio.SourceControl.GitHub;
using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

/// <summary>
/// Clone, commit, pull and submodules against a Git host that is not GitHub, served by real Git (<see cref="GitServer"/>),
/// and the <c>.git</c> of a clone kept in step with what Git Changes does, checked with Git itself.
/// </summary>
public sealed class GitHostTests : IDisposable
{
    private const string Repo = "team/demo";

    private readonly TempDirectory _temp = new();
    private readonly GitServer _server = new();
    private readonly HttpClient _http;

    public GitHostTests()
    {
        _http = new HttpClient(_server, disposeHandler: false);
        _server.CreateRepository(Repo, new Dictionary<string, string>
        {
            ["README.md"] = "# Demo\n",
            ["src/Program.cs"] = "Console.WriteLine(\"one\");\nConsole.WriteLine(\"two\");\n",
            ["src/deep/Util.cs"] = "class Util;\n",
            [".gitignore"] = "bin/\n",
        });
    }

    private string Folder => Path.Combine(_temp.Path, "work");

    private static GitRemoteAddress Address => Parse(GitServer.Address(Repo).ToString());

    [Fact]
    public async Task A_repository_on_a_host_other_than_GitHub_is_cloned_as_a_working_Git_clone()
    {
        // Only GitHub could be cloned: GitLab, Bitbucket, Azure DevOps and self-hosted addresses were refused.
        var sync = await GitHubSync.CloneAsync(Client(), Address, null, null, Folder);

        Assert.Equal("class Util;\n", Read("src/deep/Util.cs"));
        Assert.Equal("main", sync.Link.Branch);
        Assert.Equal(_server.Head(Repo), sync.Link.BaseCommit);
        Assert.Equal(GitServer.Address(Repo).ToString(), sync.Link.Remote);
        Assert.Equal(GitServer.Host, sync.Link.Host);
        Assert.Empty((await sync.GetStatusAsync(checkGitHub: true)).Changes);
        Assert.Equal(0, (await sync.GetStatusAsync(checkGitHub: true)).IncomingCommits);

        // Git in the Terminal sees the same clone.
        Assert.Equal(string.Empty, GitServer.Git(Folder, "status", "--porcelain"));
        Assert.Equal(_server.Head(Repo), GitServer.Git(Folder, "rev-parse", "HEAD").Trim());
        Assert.Equal("main", GitServer.Git(Folder, "rev-parse", "--abbrev-ref", "HEAD").Trim());
        Assert.Equal(GitServer.Address(Repo).ToString(), GitServer.Git(Folder, "config", "remote.origin.url").Trim());
        Assert.False(GitHubSync.IsIncompleteClone(Folder));
    }

    [Fact]
    public async Task A_server_that_speaks_only_the_original_protocol_is_cloned_too()
    {
        _server.OnlyVersion0 = true;

        var sync = await GitHubSync.CloneAsync(Client(), Address, null, null, Folder);

        Assert.Equal("# Demo\n", Read("README.md"));
        Assert.Equal(string.Empty, GitServer.Git(Folder, "status", "--porcelain"));
        Assert.Equal(_server.Head(Repo), sync.Link.BaseCommit);
    }

    [Fact]
    public async Task A_branch_or_tag_named_in_the_address_is_cloned()
    {
        var dev = _server.Commit(Repo, "Dev work", new Dictionary<string, string?> { ["dev.txt"] = "dev\n" }, branch: "dev");

        var sync = await GitHubSync.CloneAsync(Client(), Address, null, "dev", Folder);

        Assert.Equal("dev", sync.Link.Branch);
        Assert.Equal(dev, sync.Link.BaseCommit);
        Assert.Equal("dev\n", Read("dev.txt"));
    }

    [Fact]
    public async Task A_download_that_breaks_goes_on_where_it_stopped()
    {
        // An interrupted download started over from zero.
        var files = Enumerable.Range(0, 12).ToDictionary(i => $"data/file{i}.txt", i => (string?)Noise(i, 6000), StringComparer.Ordinal);
        _server.Commit(Repo, "Data", files);
        var batch = GitWorkingCopy.BlobBatch;
        GitWorkingCopy.BlobBatch = 4;
        try
        {
            // Fetch 1 brings the commit and its folders, then the files come four at a time; the third batch breaks.
            _server.BreakOnFetch = 3;
            _server.BreakFetchAfter = 9000;
            await Assert.ThrowsAsync<GitTransportException>(() => GitHubSync.CloneAsync(Client(), Address, null, null, Folder));
            Assert.True(GitHubSync.IsIncompleteClone(Folder));
            var before = _server.Wants.Count;

            var sync = await GitHubSync.CloneAsync(Client(), Address, null, null, Folder);

            var again = _server.Wants.Skip(before).Sum();
            Assert.True(again < 16, $"The second download asked for {again} objects again.");
            Assert.Equal(Noise(2, 6000), Read("data/file2.txt"));
            Assert.Equal(Noise(11, 6000), Read("data/file11.txt"));
            Assert.Equal(string.Empty, GitServer.Git(Folder, "status", "--porcelain"));
            Assert.Empty((await sync.GetStatusAsync()).Changes);
        }
        finally
        {
            GitWorkingCopy.BlobBatch = batch;
        }
    }

    [Fact]
    public async Task A_commit_and_a_pull_from_here_leave_git_status_agreeing_with_Git_Changes()
    {
        // After a commit or pull from the tablet, 'git status' in the Terminal disagreed with Git Changes.
        CloneWithGit(GitServer.Address(Repo).ToString());
        var sync = await GitHubSync.LinkCheckoutAsync(Client(), GitCheckout.Find(Folder)!);
        Write("src/Program.cs", "Console.WriteLine(\"edited\");\n");
        Write("NEW.md", "new\n");

        var commit = await sync.CommitAsync("Edit from the tablet");

        Assert.Equal(_server.Head(Repo), commit.Commit.Sha);
        Assert.Equal(string.Empty, GitServer.Git(Folder, "status", "--porcelain"));
        Assert.Equal(commit.Commit.Sha, GitServer.Git(Folder, "rev-parse", "HEAD").Trim());
        Assert.Equal(commit.Commit.Sha, GitServer.Git(Folder, "rev-parse", "origin/main").Trim());
        Assert.Equal("Edit from the tablet", GitServer.Git(Folder, "log", "-1", "--format=%s").Trim());
        Assert.Equal("Console.WriteLine(\"edited\");\n", GitServer.Git(_server.BarePath(Repo), "show", "main:src/Program.cs"));

        var theirs = _server.Commit(Repo, "From elsewhere", new Dictionary<string, string?> { ["README.md"] = "# Changed\n", ["src/deep/Util.cs"] = null });
        Write("NEW.md", "local edit\n");
        var pull = await sync.PullAsync();

        Assert.Equal(theirs, pull.Head);
        Assert.Equal("# Changed\n", Read("README.md"));
        Assert.False(File.Exists(Path.Combine(Folder, "src", "deep", "Util.cs")));
        Assert.Equal(theirs, GitServer.Git(Folder, "rev-parse", "HEAD").Trim());

        // The edit that was not committed shows in both, and nothing else.
        Assert.Equal(" M NEW.md", GitServer.Git(Folder, "status", "--porcelain").TrimEnd());
        Assert.Equal("NEW.md", Assert.Single((await sync.GetStatusAsync()).Changes).Path);
    }

    [Fact]
    public async Task A_GitHub_clone_commits_and_pulls_through_Git_so_its_git_folder_follows()
    {
        // A clone of a GitHub repository: its .git moves with commits and pulls from the tablet too.
        _server.PushCredentials = ("x-access-token", "test-token");
        CloneWithGit("https://github.com/team/demo.git");
        var client = new GitHubClient(_http, "test-token", new Uri("https://api.github.test/"), webAddress: new Uri($"https://{GitServer.Host}/"));
        var sync = await GitHubSync.LinkCheckoutAsync(client, GitCheckout.Find(Folder)!);
        Write("README.md", "# From the tablet\n");

        var commit = await sync.CommitAsync("Tablet commit");

        Assert.Equal(_server.Head(Repo), commit.Commit.Sha);
        Assert.Equal(string.Empty, GitServer.Git(Folder, "status", "--porcelain"));
        Assert.Equal(commit.Commit.Sha, GitServer.Git(Folder, "rev-parse", "HEAD").Trim());
        Assert.True(sync.Link.IsGitHub);
    }

    [Fact]
    public async Task A_pull_that_moves_a_submodule_pin_moves_its_folder()
    {
        // Pull ignored submodules: a commit that moved a submodule's pin left its folder at the old commit.
        _server.CreateRepository("team/lib", new Dictionary<string, string> { ["lib.h"] = "int lib(void);\n" });
        var pinned = _server.Head("team/lib");
        _server.Commit(Repo, "Add lib", new Dictionary<string, string?> { [".gitmodules"] = "[submodule \"lib\"]\n\tpath = external/lib\n\turl = ../lib.git\n" });
        _server.PinSubmodule(Repo, "external/lib", pinned);
        var sync = await GitHubSync.CloneAsync(Client(), Address, null, null, Folder);
        Assert.Equal("int lib(void);\n", Read("external/lib/lib.h"));

        var moved = _server.Commit("team/lib", "Newer", new Dictionary<string, string?> { ["lib.h"] = "int lib(int);\n" });
        _server.PinSubmodule(Repo, "external/lib", moved);
        var pull = await sync.PullAsync();

        Assert.Contains("external/lib", pull.Updated);
        Assert.Equal("int lib(int);\n", Read("external/lib/lib.h"));
        Assert.Equal(moved, SyncMetadata.Load(Path.Combine(Folder, "external", "lib"))!.Link.BaseCommit);
        Assert.Empty((await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task A_push_the_host_refuses_for_new_commits_asks_for_a_pull()
    {
        var sync = await GitHubSync.CloneAsync(Client(), Address, null, null, Folder);
        _server.Commit(Repo, "Elsewhere", new Dictionary<string, string?> { ["other.txt"] = "x\n" });
        Write("README.md", "# Mine\n");

        var error = await Assert.ThrowsAsync<SyncException>(() => sync.CommitAsync("Mine"));

        Assert.Equal(SyncFailure.BehindRemote, error.Failure);
        await sync.PullAsync();
        var commit = await sync.CommitAsync("Mine");
        Assert.Equal(_server.Head(Repo), commit.Commit.Sha);
    }

    [Theory]
    [InlineData("https://gitlab.com/group/sub/project.git", "https://gitlab.com/group/sub/project.git", "group/sub", "project", null)]
    [InlineData("https://gitlab.com/group/project/-/tree/feature/x", "https://gitlab.com/group/project.git", "group", "project", "feature")]
    [InlineData("git@gitlab.com:group/project.git", "https://gitlab.com/group/project.git", "group", "project", null)]
    [InlineData("https://bitbucket.org/team/repo/src/main/", "https://bitbucket.org/team/repo.git", "team", "repo", "main")]
    [InlineData("https://dev.azure.com/org/project/_git/repo", "https://dev.azure.com/org/project/_git/repo", "org/project", "repo", null)]
    [InlineData("git@ssh.dev.azure.com:v3/org/project/repo", "https://dev.azure.com/org/project/_git/repo", "org/project", "repo", null)]
    [InlineData("https://codeberg.org/forgejo/forgejo/src/branch/forgejo", "https://codeberg.org/forgejo/forgejo.git", "forgejo", "forgejo", "forgejo")]
    [InlineData("http://git.local:3000/me/tool.git", "http://git.local:3000/me/tool.git", "me", "tool", null)]
    [InlineData("gitlab.com/group/project", "https://gitlab.com/group/project.git", "group", "project", null)]
    public void Addresses_of_every_host_name_their_repository(string text, string address, string owner, string name, string? reference)
    {
        Assert.True(GitRemoteAddress.TryParse(text, out var parsed, out var parsedReference, out _));
        Assert.Equal(address, parsed.Address.ToString());
        Assert.Equal(owner, parsed.Owner);
        Assert.Equal(name, parsed.Name);
        Assert.Equal(reference, parsedReference);
    }

    [Fact]
    public void A_sign_in_written_into_the_address_is_read_and_left_out_of_it()
    {
        Assert.True(GitRemoteAddress.TryParse("https://oauth2:secret%40token@gitlab.com/group/project.git", out var address, out _, out var credentials));
        Assert.Equal("https://gitlab.com/group/project.git", address.Address.ToString());
        Assert.Equal(new GitCredentials("oauth2", "secret@token"), credentials);
        Assert.False(GitRemoteAddress.TryParse("not an address", out _, out _, out _));
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
        _temp.Dispose();
    }

    private GitHubClient Client() => new(_http, null, new Uri("https://api.github.test/"), webAddress: new Uri("https://github.test/"));

    private static GitRemoteAddress Parse(string text) =>
        GitRemoteAddress.TryParse(text, out var address, out _, out _) ? address : throw new InvalidOperationException(text);

    /// <summary>Clones the server's repository with Git, then points its remote at an HTTPS address, as a clone from that host has it.</summary>
    private void CloneWithGit(string remote)
    {
        GitServer.Git(_temp.Path, "clone", "--quiet", _server.BarePath(Repo), Folder);
        GitServer.Configure(Folder);
        GitServer.Git(Folder, "remote", "set-url", "origin", remote);
        GitServer.Git(Folder, "fetch", "--quiet", _server.BarePath(Repo), "+refs/heads/*:refs/remotes/origin/*");
    }

    /// <summary>Text that does not compress much, so a pack of it is as large as it.</summary>
    private static string Noise(int seed, int length)
    {
        var random = new Random(seed);
        return string.Concat(Enumerable.Range(0, length).Select(_ => (char)('!' + random.Next(90))));
    }

    private string Read(string path) => File.ReadAllText(Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar)));

    private void Write(string path, string content)
    {
        var full = Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
