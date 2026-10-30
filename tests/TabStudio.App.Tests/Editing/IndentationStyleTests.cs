using TabStudio.App.Editing;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Editing;

/// <summary>
/// Tab indents as the file needs: it always inserted four spaces, which broke Makefiles ("missing separator") and
/// tab-indented Python (TabError), and ignored the repository's .editorconfig.
/// </summary>
public sealed class IndentationStyleTests
{
    [Fact]
    public void Makefiles_go_and_tab_indented_files_get_tabs_and_others_four_spaces()
    {
        Assert.True(IndentationStyle.For("/src/Makefile", "all:\n\tcc main.c\n").UsesTabs);
        Assert.True(IndentationStyle.For("/src/main.go", "package main\n").UsesTabs);
        Assert.True(IndentationStyle.For("/src/app.py", "def f():\n\treturn 1\n").UsesTabs);
        Assert.Equal(IndentationStyle.Default, IndentationStyle.For("/src/app.py", "def f():\n    return 1\n"));
        Assert.Equal(IndentationStyle.Default, IndentationStyle.For("/src/Program.cs", "class C {}\n"));
    }

    [Fact]
    public void The_repositorys_editorconfig_wins_nearest_file_last()
    {
        using var temp = new TempDirectory();
        temp.Write(".editorconfig", "root = true\n\n[*]\nindent_style = space\nindent_size = 4\n\n[*.{js,ts}]\nindent_size = 2\n\n[Makefile]\nindent_style = tab\n");
        temp.Write("src/.editorconfig", "[*.cs]\nindent_style = tab\ntab_width = 8\n");

        Assert.Equal(new IndentationStyle(false, 2), IndentationStyle.For(temp.Resolve("web/app.ts"), "x\n"));
        Assert.Equal(new IndentationStyle(true, 4), IndentationStyle.For(temp.Resolve("src/Program.cs"), "class C {}\n"));
        Assert.Equal(new IndentationStyle(false, 4), IndentationStyle.For(temp.Resolve("tools/main.go"), "package main\n"));
        Assert.True(IndentationStyle.For(temp.Resolve("Makefile"), "all:\n").UsesTabs);
    }

    [Theory]
    [InlineData("*.cs", "src/A.cs", true)]
    [InlineData("*.{js,ts}", "web/app.ts", true)]
    [InlineData("src/**.cs", "src/a/b/C.cs", true)]
    [InlineData("/Makefile", "sub/Makefile", false)]
    [InlineData("*.cs", "src/A.csx", false)]
    public void Section_globs_match_as_EditorConfig_defines_them(string glob, string path, bool matches) =>
        Assert.Equal(matches, IndentationStyle.Matches(glob, path));
}
