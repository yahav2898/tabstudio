using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class RenameTests
{
    private const string Greeter = """
        namespace Lib;

        public static class Greeter
        {
            public static string Greet(string name) => $"Hello, {name}";

            public static string Shout(string name) => Greet(name).ToUpperInvariant();
        }
        """;

    private static FeatureWorkspace CreateSolution(string program) =>
        new FeatureWorkspace()
            .Project("Lib")
            .Project("App", executable: true, references: ["Lib"])
            .File("Lib/Greeter.cs", Greeter)
            .File("App/Program.cs", program)
            .Load();

    [Fact]
    public async Task Rename_changes_the_symbol_in_every_project()
    {
        using var workspace = CreateSolution("""
            using Lib;
            Console.WriteLine(Greeter.Gre$$et("tablet"));
            """);

        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret(), "Welcome");

        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Conflicts);
        Assert.Equal(2, result.Edits.Count);
        var greeter = workspace.ApplyTo("Lib/Greeter.cs", result.Edits);
        Assert.Contains("public static string Welcome(string name)", greeter, StringComparison.Ordinal);
        Assert.Contains("=> Welcome(name).ToUpperInvariant()", greeter, StringComparison.Ordinal);
        Assert.Equal("using Lib;\nConsole.WriteLine(Greeter.Welcome(\"tablet\"));", workspace.ApplyTo("App/Program.cs", result.Edits).ReplaceLineEndings("\n"));
    }

    [Fact]
    public async Task Can_rename_returns_the_name_under_the_caret()
    {
        using var workspace = CreateSolution("""
            using Lib;
            Console.WriteLine(Greeter.Greet$$("tablet"));
            """);

        var target = await workspace.Features.Rename.CanRenameAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.True(target.CanRename, target.Reason);
        Assert.Equal("Greet", target.CurrentName);
        Assert.Equal(workspace.Caret() - 5, target.SpanStart);
        Assert.Equal(5, target.SpanLength);
    }

    [Fact]
    public async Task Framework_symbols_cannot_be_renamed()
    {
        using var workspace = CreateSolution("Con$$sole.WriteLine();");

        var target = await workspace.Features.Rename.CanRenameAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());
        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret(), "Terminal");

        Assert.False(target.CanRename);
        Assert.NotNull(target.Reason);
        Assert.False(result.Success);
        Assert.Empty(result.Edits);
    }

    [Theory]
    [InlineData("1st")]
    [InlineData("two words")]
    [InlineData("class")]
    [InlineData("")]
    public async Task Rename_rejects_names_that_are_not_identifiers(string newName)
    {
        using var workspace = CreateSolution("""
            var count = 1;
            Console.WriteLine(cou$$nt);
            """);

        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret(), newName);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task Rename_accepts_an_escaped_keyword()
    {
        using var workspace = CreateSolution("""
            var count = 1;
            Console.WriteLine(cou$$nt);
            """);

        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret(), "@class");

        Assert.True(result.Success, result.Error);
        Assert.Contains("Console.WriteLine(@class);", workspace.ApplyTo("App/Program.cs", result.Edits), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rename_reports_a_name_that_is_already_taken()
    {
        using var workspace = CreateSolution("""
            var first = 1;
            var second = 2;
            Console.WriteLine(fir$$st + second);
            """);

        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret(), "second");

        Assert.True(result.Success, result.Error);
        Assert.NotEmpty(result.Conflicts);
    }
}
