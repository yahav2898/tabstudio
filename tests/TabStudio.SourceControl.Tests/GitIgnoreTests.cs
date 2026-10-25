namespace TabStudio.SourceControl.Tests;

public sealed class GitIgnoreTests
{
    private static GitIgnore Rules(string content, string folder = "")
    {
        var rules = new GitIgnore();
        rules.Add(content, folder);
        return rules;
    }

    [Theory]
    [InlineData("bin/", "bin", true, true)]
    [InlineData("bin/", "src/App/bin", true, true)]
    [InlineData("bin/", "bin", false, false)]
    [InlineData("*.user", "src/App/App.csproj.user", false, true)]
    [InlineData("*.user", "src/App/App.csproj", false, false)]
    [InlineData("/build.log", "build.log", false, true)]
    [InlineData("/build.log", "src/build.log", false, false)]
    [InlineData("docs/*.md", "docs/a.md", false, true)]
    [InlineData("docs/*.md", "docs/deep/a.md", false, false)]
    [InlineData("docs/**/*.md", "docs/deep/er/a.md", false, true)]
    [InlineData("**/temp", "a/b/temp", true, true)]
    [InlineData("log?.txt", "log1.txt", false, true)]
    [InlineData("log?.txt", "log10.txt", false, false)]
    [InlineData("[ab].txt", "a.txt", false, true)]
    [InlineData("[!ab].txt", "a.txt", false, false)]
    [InlineData("# a comment", "# a comment", false, false)]
    [InlineData("\\#hash", "#hash", false, true)]
    public void Matches_like_git(string pattern, string path, bool isDirectory, bool ignored) =>
        Assert.Equal(ignored, Rules(pattern).IsIgnored(path, isDirectory));

    [Fact]
    public void The_last_matching_rule_wins_and_negation_brings_files_back()
    {
        var rules = Rules("*.log\n!keep.log\n");

        Assert.True(rules.IsIgnored("debug.log", isDirectory: false));
        Assert.False(rules.IsIgnored("keep.log", isDirectory: false));
        Assert.False(rules.IsIgnored("src/keep.log", isDirectory: false));
    }

    [Fact]
    public void Rules_from_a_subfolder_apply_only_below_it()
    {
        var rules = Rules("generated/\n/local.txt\n", "src/App");

        Assert.True(rules.IsIgnored("src/App/generated", isDirectory: true));
        Assert.True(rules.IsIgnored("src/App/deep/generated", isDirectory: true));
        Assert.False(rules.IsIgnored("generated", isDirectory: true));
        Assert.True(rules.IsIgnored("src/App/local.txt", isDirectory: false));
        Assert.False(rules.IsIgnored("src/App/deep/local.txt", isDirectory: false));
        Assert.False(rules.IsIgnored("local.txt", isDirectory: false));
    }

    [Fact]
    public void Windows_line_endings_and_trailing_spaces_are_ignored()
    {
        var rules = Rules("obj/  \r\n*.tmp\r\n");

        Assert.True(rules.IsIgnored("obj", isDirectory: true));
        Assert.True(rules.IsIgnored("a.tmp", isDirectory: false));
    }
}
