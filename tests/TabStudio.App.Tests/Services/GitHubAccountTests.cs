using TabStudio.App.Services;
using TabStudio.SourceControl.GitHub;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Services;

public sealed class GitHubAccountTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private readonly FakeGitHub _github = new();
    private readonly AppPaths _paths;

    public GitHubAccountTests()
    {
        _paths = new AppPaths(_temp.Path);
    }

    [Fact]
    public async Task Signing_in_checks_the_token_with_GitHub_and_remembers_it()
    {
        var account = _github.CreateAccount(_paths);
        var changes = 0;
        account.Changed += (_, _) => changes++;

        var user = await account.SignInAsync($"  {FakeGitHub.Token}\n");

        Assert.Equal(FakeGitHub.Login, user.Login);
        Assert.True(account.IsSignedIn);
        Assert.True(account.CreateClient().IsSignedIn);
        Assert.Equal(1, changes);

        var later = _github.CreateAccount(_paths);
        Assert.True(later.IsSignedIn);
        Assert.Equal(FakeGitHub.Login, later.User?.Login);
        Assert.Equal("Test User", later.User?.Name);
    }

    [Fact]
    public async Task The_token_file_is_readable_by_the_user_only()
    {
        await _github.CreateAccount(_paths).SignInAsync(FakeGitHub.Token);

        var file = Assert.Single(Directory.EnumerateFiles(_paths.State, "github*"));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(file));
        }
    }

    [Fact]
    public async Task A_token_GitHub_refuses_is_not_remembered()
    {
        var account = _github.CreateAccount(_paths);

        var error = await Assert.ThrowsAsync<GitHubApiException>(() => account.SignInAsync("wrong-token"));

        Assert.True(error.IsUnauthorized);
        Assert.False(account.IsSignedIn);
        Assert.False(_github.CreateAccount(_paths).IsSignedIn);
    }

    [Fact]
    public async Task Signing_out_forgets_the_token()
    {
        var account = _github.CreateAccount(_paths);
        await account.SignInAsync(FakeGitHub.Token);

        account.SignOut();

        Assert.False(account.IsSignedIn);
        Assert.Null(account.User);
        Assert.False(account.CreateClient().IsSignedIn);
        Assert.False(_github.CreateAccount(_paths).IsSignedIn);
    }

    public void Dispose()
    {
        _github.Dispose();
        _temp.Dispose();
    }
}
