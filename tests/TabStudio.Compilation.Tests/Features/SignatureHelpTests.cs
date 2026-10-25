using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class SignatureHelpTests
{
    private const string Library = """
        static class Greeter
        {
            /// <summary>Says hello.</summary>
            /// <param name="name">Who to greet.</param>
            public static string Greet(string name) => $"Hello, {name}";
        }

        static class Calc
        {
            public static int Add(int left, int right) => left + right;

            public static string Describe(int value) => value.ToString();

            public static string Describe(string name, int count) => name + count;
        }

        sealed class Point(int x, int y)
        {
            public int X { get; } = x;

            public int Y { get; } = y;
        }
        """;

    private static async Task<SignatureHelpResult?> HelpAsync(string call)
    {
        // The types live in their own file, so the unfinished call at the end of Program.cs parses on its own.
        using var workspace = new FeatureWorkspace()
            .Project("App", executable: true)
            .File("App/Program.cs", call)
            .File("App/Library.cs", Library)
            .Load();
        return await workspace.Features.SignatureHelp.GetSignatureHelpAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());
    }

    [Fact]
    public async Task Signature_help_shows_the_parameters_and_documentation_of_the_call()
    {
        var help = await HelpAsync("var text = Greeter.Greet($$");

        Assert.NotNull(help);
        var signature = Assert.Single(help.Signatures);
        Assert.Equal("string Greeter.Greet(string name)", signature.Label);
        Assert.Equal("Says hello.", signature.Documentation);
        Assert.Equal("Who to greet.", Assert.Single(signature.Parameters).Documentation);
        Assert.Equal(0, help.ActiveParameter);
        Assert.Contains(signature.Prefix, r => r.Text == "Greet" && r.Kind == TaggedTextKind.Method);
    }

    [Fact]
    public async Task Signature_help_moves_to_the_next_parameter_after_a_comma()
    {
        var help = await HelpAsync("var sum = Calc.Add(1, $$");

        Assert.NotNull(help);
        Assert.Equal(1, help.ArgumentIndex);
        Assert.Equal(1, help.ActiveParameter);
        Assert.Equal("right", help.Signatures[help.ActiveSignature].Parameters[help.ActiveParameter].Name);
    }

    [Fact]
    public async Task Signature_help_selects_the_overload_that_matches_the_arguments()
    {
        var twoArguments = await HelpAsync("var a = Calc.Describe(\"x\", $$");
        var oneArgument = await HelpAsync("var b = Calc.Describe(4$$2);");

        Assert.NotNull(twoArguments);
        Assert.Equal(2, twoArguments.Signatures.Count);
        Assert.Equal("string Calc.Describe(string name, int count)", twoArguments.Signatures[twoArguments.ActiveSignature].Label);
        Assert.Equal(1, twoArguments.ActiveParameter);

        Assert.NotNull(oneArgument);
        Assert.Equal("string Calc.Describe(int value)", oneArgument.Signatures[oneArgument.ActiveSignature].Label);
    }

    [Fact]
    public async Task Signature_help_follows_named_arguments()
    {
        var help = await HelpAsync("var c = Calc.Describe(count: 1, name: $$");

        Assert.NotNull(help);
        Assert.Equal("name", help.ArgumentName);
        Assert.Equal("string Calc.Describe(string name, int count)", help.Signatures[help.ActiveSignature].Label);
        Assert.Equal(0, help.ActiveParameter);
    }

    [Fact]
    public async Task Signature_help_lists_constructors()
    {
        var help = await HelpAsync("var point = new Point(1, $$");

        Assert.NotNull(help);
        Assert.Equal("Point(int x, int y)", Assert.Single(help.Signatures).Label);
        Assert.Equal(1, help.ActiveParameter);
    }

    [Fact]
    public async Task Signature_help_is_not_shown_outside_an_argument_list()
    {
        var help = await HelpAsync("var text = Greeter.Greet(\"x\");$$");

        Assert.Null(help);
    }
}
