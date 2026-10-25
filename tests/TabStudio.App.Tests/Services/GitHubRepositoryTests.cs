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
    public void Rejects_text_that_is_not_a_repository(string text)
    {
        Assert.False(GitHubRepository.TryParse(text, out _));
    }

    [Fact]
    public void Archive_address_keeps_slashes_in_branch_names()
    {
        var repository = new GitHubRepository("dotnet", "aspnetcore", "release/9.0");

        Assert.Equal("https://github.com/dotnet/aspnetcore/archive/release/9.0.zip", repository.ArchiveUri.ToString());
    }

    [Fact]
    public void Archive_address_uses_the_default_branch_when_none_is_given()
    {
        Assert.Equal("https://github.com/o/r/archive/HEAD.zip", new GitHubRepository("o", "r", null).ArchiveUri.ToString());
    }
}
