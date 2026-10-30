using TabStudio.App.Services;

namespace TabStudio.App.Tests.Services;

public sealed class GitHubRepositoryTests
{
    [Theory]
    [InlineData("https://github.com/yahav2898/tabstudio", "yahav2898", "tabstudio", null)]
    [InlineData("github.com/yahav2898/tabstudio", "yahav2898", "tabstudio", null)]
    [InlineData("yahav2898/tabstudio", "yahav2898", "tabstudio", null)]
    [InlineData("https://github.com/dotnet/runtime.git", "dotnet", "runtime", null)]
    [InlineData("git@github.com:dotnet/roslyn.git", "dotnet", "roslyn", null)]
    [InlineData("https://github.com/dotnet/aspnetcore/tree/release/9.0", "dotnet", "aspnetcore", "release/9.0")]
    [InlineData("https://github.com/AvaloniaUI/Avalonia/releases/tag/11.3.0", "AvaloniaUI", "Avalonia", "11.3.0")]
    [InlineData("  https://www.github.com/owner/my.repo/  ", "owner", "my.repo", null)]
    [InlineData("Github.com/owner/repo", "owner", "repo", null)]
    [InlineData("Https://GitHub.com/owner/repo", "owner", "repo", null)]
    [InlineData("ssh://git@github.com/owner/repo.git", "owner", "repo", null)]
    [InlineData("https://github.com/owner/repo/blob/main/README.md", "owner", "repo", "main")]
    [InlineData("https://github.com/owner/repo/pull/12", "owner", "repo", null)]
    [InlineData("https://github.com/owner/repo/issues", "owner", "repo", null)]
    [InlineData("https://github.com/owner/repo/commits/dev", "owner", "repo", "dev")]
    [InlineData("https://github.com/dotnet/runtime/tree/main/src/libraries", "dotnet", "runtime", "main/src/libraries")]
    public void Reads_the_forms_people_paste(string text, string owner, string name, string? reference)
    {
        Assert.True(GitHubRepository.TryParse(text, out var repository));
        Assert.Equal(new GitHubRepository(owner, name, reference), repository);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a repository")]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("https://github.com/owner")]
    [InlineData("https://github.com/owner/repo/tree/..%2F..%2Fother")]
    public void Rejects_text_that_is_not_a_repository(string text)
    {
        Assert.False(GitHubRepository.TryParse(text, out _));
    }

    [Fact]
    public void Web_address_and_display_name_leave_out_or_show_the_branch()
    {
        var repository = new GitHubRepository("dotnet", "aspnetcore", "release/9.0");

        Assert.Equal("https://github.com/dotnet/aspnetcore", repository.WebUri.ToString());
        Assert.Equal("dotnet/aspnetcore@release/9.0", repository.ToString());
        Assert.Equal("o/r", new GitHubRepository("o", "r", null).ToString());
    }
}
