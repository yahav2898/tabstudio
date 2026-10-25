using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class DocumentSymbolTests
{
    private const string Source = """
        namespace Lib;

        public sealed class Greeter
        {
            private const string Prefix = "Hello";
            private readonly int _count;

            public Greeter(int count) => _count = count;

            public string Name { get; set; } = "";

            public event EventHandler? Greeted;

            public string Greet(string name, ref int calls)
            {
                calls++;
                return $"{Prefix}, {na$$me}";
            }

            public enum Mood
            {
                Happy,
                Calm,
            }
        }

        internal interface IGreeter
        {
            string Greet(string name, ref int calls);
        }
        """;

    [Fact]
    public async Task Document_symbols_list_types_and_members_as_a_tree()
    {
        using var workspace = FeatureWorkspace.WithProgram(Source);

        var symbols = await workspace.Features.Symbols.GetDocumentSymbolsAsync(workspace.PathOf("App/Program.cs"));

        var ns = Assert.Single(symbols);
        Assert.Equal("Lib", ns.Name);
        Assert.Equal(SymbolGlyph.Namespace, ns.Kind);
        Assert.Equal(new[] { "Greeter", "IGreeter" }, ns.Children.Select(c => c.Name));

        var greeter = ns.Children[0];
        Assert.Equal(SymbolGlyph.Class, greeter.Kind);
        Assert.Equal(SymbolAccessibility.Public, greeter.Accessibility);
        Assert.Equal(
            new[] { "Prefix", "_count", "Greeter(int)", "Name", "Greeted", "Greet(string, ref int)", "Mood" },
            greeter.Children.Select(c => c.DisplayName));
        Assert.Equal(
            new[] { SymbolGlyph.Constant, SymbolGlyph.Field, SymbolGlyph.Method, SymbolGlyph.Property, SymbolGlyph.Event, SymbolGlyph.Method, SymbolGlyph.Enum },
            greeter.Children.Select(c => c.Kind));
        Assert.Equal(SymbolAccessibility.Private, greeter.Children[0].Accessibility);
        Assert.Equal(new[] { "Happy", "Calm" }, greeter.Children[^1].Children.Select(c => c.Name));

        var text = workspace.TextOf("App/Program.cs");
        var greet = greeter.Children[5];
        Assert.Equal("Greet", text.Substring(greet.NameStart, greet.NameLength));
        Assert.StartsWith("public string Greet(", text.Substring(greet.Start, greet.Length), StringComparison.Ordinal);

        var contract = ns.Children[1];
        Assert.Equal(SymbolGlyph.Interface, contract.Kind);
        Assert.Equal(SymbolAccessibility.Internal, contract.Accessibility);
        Assert.Equal(SymbolAccessibility.Public, Assert.Single(contract.Children).Accessibility);
    }

    [Fact]
    public async Task Symbol_path_goes_from_namespace_to_member()
    {
        using var workspace = FeatureWorkspace.WithProgram(Source);

        var path = await workspace.Features.Symbols.GetSymbolPathAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.Equal(new[] { "Lib", "Greeter", "Greet" }, path.Select(s => s.Name));
    }

    [Fact]
    public async Task Symbol_path_is_empty_outside_every_declaration()
    {
        using var workspace = FeatureWorkspace.WithProgram("$$Console.WriteLine();");

        var path = await workspace.Features.Symbols.GetSymbolPathAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.Empty(path);
    }
}
