using TabStudio.Testing;

namespace TabStudio.SourceControl.Tests;

/// <summary>Folders that Git cloned from GitHub: read without Git and linked without downloading them again.</summary>
public sealed class GitCheckoutTests : IDisposable
{
    private const string Repo = "demo";

    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal)
    {
        ["README.md"] = "# Demo\n",
        ["src/Program.cs"] = "Console.WriteLine(\"one\");\n",
    };

    public GitCheckoutTests() => _github.AddRepository(Repo, _files);

    private string Folder => Path.Combine(_temp.Path, "clone");

    [Theory]
    [InlineData("https://github.com/octo/hello.git", "octo", "hello")]
    [InlineData("https://github.com/octo/hello", "octo", "hello")]
    [InlineData("https://token@github.com/octo/hello.git/", "octo", "hello")]
    [InlineData("git@github.com:octo/hello.js.git", "octo", "hello.js")]
    [InlineData("ssh://git@github.com/octo/hello.git", "octo", "hello")]
    [InlineData("https://gitlab.com/octo/hello.git", null, null)]
    [InlineData("../local", null, null)]
    public void Remote_addresses_on_GitHub_name_their_repository(string address, string? owner, string? repository)
    {
        var parsed = GitCheckout.ParseRemote(address);

        Assert.Equal(owner, parsed?.Owner);
        Assert.Equal(repository, parsed?.Repository);
    }

    [Fact]
    public void A_clone_is_read_with_its_branch_commit_and_origin_from_a_subfolder()
    {
        var head = _github.Head(Repo, "main");
        WriteClone(head, packed: true);

        var checkout = GitCheckout.Find(Path.Combine(Folder, "src", "Program.cs"));

        Assert.NotNull(checkout);
        Assert.Equal(Folder, checkout.Root);
        Assert.Equal($"{FakeGitHub.Login}/{Repo}", checkout.FullName);
        Assert.Equal("main", checkout.Branch);
        Assert.Equal(head, checkout.Commit);
    }

    [Fact]
    public void The_branch_remote_wins_over_origin_and_a_remote_off_GitHub_is_not_a_link()
    {
        const string config = "[remote \"origin\"]\n\turl = https://github.com/fork/demo.git\n[remote \"upstream\"]\n\turl = git@github.com:main/demo.git\n[branch \"dev\"]\n\tremote = upstream\n";

        Assert.Equal("main", GitCheckout.RemoteRepository(config, "dev")?.Owner);
        Assert.Equal("fork", GitCheckout.RemoteRepository(config, "other")?.Owner);
        Assert.Null(GitCheckout.RemoteRepository("[remote \"origin\"]\n\turl = https://example.com/x.git\n", "main"));
    }

    [Fact]
    public async Task Linking_a_clone_shows_its_edits_without_downloading_it_again()
    {
        WriteClone(_github.Head(Repo, "main"));
        Write("src/Program.cs", "Console.WriteLine(\"edited\");\n");
        var before = _github.Requests.Count;

        var sync = await GitHubSync.LinkCheckoutAsync(_github.CreateClient(signedIn: false), GitCheckout.Find(Folder)!);

        Assert.Equal(new GitHubLink(FakeGitHub.Login, Repo, "main", _github.Head(Repo, "main")), sync.Link);
        Assert.DoesNotContain(_github.Requests.Skip(before), r => r.Contains("/zipball/", StringComparison.Ordinal) || r.Contains("/git/blobs/", StringComparison.Ordinal));
        var change = Assert.Single((await sync.GetStatusAsync()).Changes);
        Assert.Equal("src/Program.cs", change.Path);
        Assert.Equal(ChangeKind.Modified, change.Kind);
        Assert.Contains(".tabstudio/", await File.ReadAllTextAsync(Path.Combine(Folder, ".git", "info", "exclude")), StringComparison.Ordinal);
        Assert.Equal(Folder, GitHubSync.FindRoot(Folder));
    }

    [Fact]
    public async Task A_clone_with_a_commit_GitHub_lacks_compares_with_the_branch_on_GitHub()
    {
        WriteClone(new string('a', 40));

        var sync = await GitHubSync.LinkCheckoutAsync(_github.CreateClient(signedIn: false), GitCheckout.Find(Folder)!);

        Assert.Equal(_github.Head(Repo, "main"), sync.Link.BaseCommit);
        Assert.Equal("main", sync.Link.Branch);
        Assert.Empty((await sync.GetStatusAsync()).Changes);
    }

    [Fact]
    public async Task A_branch_GitHub_lacks_is_linked_as_a_commit_so_commits_never_land_on_another_branch()
    {
        WriteClone(_github.Head(Repo, "main"), branch: "local-only");

        var sync = await GitHubSync.LinkCheckoutAsync(_github.CreateClient(signedIn: false), GitCheckout.Find(Folder)!);

        Assert.Null(sync.Link.Branch);
        Assert.True(sync.Link.IsDetached);
    }

    public void Dispose()
    {
        _github.Dispose();
        _temp.Dispose();
    }

    /// <summary>Writes the repository's files and a <c>.git</c> folder as <c>git clone</c> leaves them.</summary>
    private void WriteClone(string commit, bool packed = false, string branch = "main")
    {
        foreach (var (path, text) in _files)
        {
            Write(path, text);
        }

        var git = Path.Combine(Folder, ".git");
        Directory.CreateDirectory(Path.Combine(git, "refs", "heads"));
        File.WriteAllText(Path.Combine(git, "HEAD"), $"ref: refs/heads/{branch}\n");
        File.WriteAllText(Path.Combine(git, "config"), $"[core]\n\trepositoryformatversion = 0\n[remote \"origin\"]\n\turl = https://github.com/{FakeGitHub.Login}/{Repo}.git\n\tfetch = +refs/heads/*:refs/remotes/origin/*\n[branch \"{branch}\"]\n\tremote = origin\n\tmerge = refs/heads/{branch}\n");
        if (packed)
        {
            File.WriteAllText(Path.Combine(git, "packed-refs"), $"# pack-refs with: peeled fully-peeled sorted\n{commit} refs/heads/{branch}\n");
        }
        else
        {
            File.WriteAllText(Path.Combine(git, "refs", "heads", branch), commit + "\n");
        }
    }

    private void Write(string path, string content)
    {
        var full = Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }
}
