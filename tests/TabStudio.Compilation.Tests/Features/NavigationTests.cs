using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class NavigationTests
{
    private const string Greeter = """
        namespace Lib;

        public static class Greeter
        {
            public static string Greet(string name) => $"Hello, {name}";
        }
        """;

    private const string Shapes = """
        namespace Lib;

        public interface IShape
        {
            double Area();
        }

        public sealed class Square(double side) : IShape
        {
            public double Area() => side * side;
        }

        public sealed class Circle(double radius) : IShape
        {
            public double Area() => Math.PI * radius * radius;
        }
        """;

    /// <summary>An app and the library it references, the way most solutions are laid out.</summary>
    private static FeatureWorkspace CreateSolution(string program, string? greeter = null) =>
        new FeatureWorkspace()
            .Project("Lib")
            .Project("App", executable: true, references: ["Lib"])
            .File("Lib/Greeter.cs", greeter ?? Greeter)
            .File("Lib/Shapes.cs", Shapes)
            .File("App/Program.cs", program)
            .Load();

    [Fact]
    public async Task Go_to_definition_finds_a_method_in_another_project()
    {
        using var workspace = CreateSolution("""
            using Lib;
            Console.WriteLine(Greeter.Gr$$eet("tablet"));
            """);

        var definition = await workspace.Features.Navigation.FindDefinitionAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.NotNull(definition);
        Assert.False(definition.IsInMetadata);
        Assert.Equal("Lib.Greeter.Greet(string)", definition.SymbolDisplay);
        Assert.Equal(SymbolGlyph.Method, definition.Kind);
        var location = Assert.Single(definition.Locations);
        Assert.Equal(workspace.PathOf("Lib/Greeter.cs"), location.FilePath);
        Assert.Equal(Greeter.IndexOf("Greet(", StringComparison.Ordinal), location.Start);
        Assert.Equal(5, location.Length);
        Assert.Equal(5, location.Line);
    }

    [Fact]
    public async Task Go_to_definition_of_a_framework_type_reports_metadata()
    {
        using var workspace = CreateSolution("Con$$sole.WriteLine();");

        var definition = await workspace.Features.Navigation.FindDefinitionAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.NotNull(definition);
        Assert.True(definition.IsInMetadata);
        Assert.Equal("System.Console", definition.SymbolDisplay);
        Assert.Equal("System.Console", definition.AssemblyName);
    }

    [Fact]
    public async Task Go_to_definition_finds_a_local_variable()
    {
        using var workspace = CreateSolution("""
            var total = 1;
            Console.WriteLine(to$$tal);
            """);

        var definition = await workspace.Features.Navigation.FindDefinitionAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.NotNull(definition);
        Assert.Equal(SymbolGlyph.Local, definition.Kind);
        Assert.Equal(4, Assert.Single(definition.Locations).Start);
    }

    [Fact]
    public async Task Find_all_references_lists_uses_in_every_project()
    {
        using var workspace = CreateSolution("""
            using Lib;
            Console.WriteLine(Greeter.Greet("tablet"));
            Console.WriteLine(Greeter.Greet("phone"));
            """, Greeter.Replace("Greet(string", "Gr$$eet(string", StringComparison.Ordinal));

        var references = await workspace.Features.Navigation.FindReferencesAsync(workspace.PathOf("Lib/Greeter.cs"), workspace.Caret("Lib/Greeter.cs"));

        var definition = Assert.Single(references, r => r.IsDefinition);
        Assert.Equal(workspace.PathOf("Lib/Greeter.cs"), definition.FilePath);
        var uses = references.Where(r => !r.IsDefinition).ToList();
        Assert.Equal(2, uses.Count);
        Assert.All(uses, r => Assert.Equal(workspace.PathOf("App/Program.cs"), r.FilePath));
        Assert.Equal("Console.WriteLine(Greeter.Greet(\"tablet\"));", uses[0].LineText);
        Assert.Equal(2, uses[0].Line);
        Assert.Equal(27, uses[0].Column);
    }

    [Fact]
    public async Task Go_to_implementation_finds_the_classes_that_implement_an_interface()
    {
        using var workspace = CreateSolution("""
            using Lib;
            I$$Shape shape = new Square(2);
            """);

        var implementations = await workspace.Features.Navigation.FindImplementationsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.Equal(new[] { "Lib.Circle", "Lib.Square" }, implementations.Select(i => i.SymbolDisplay).Order(StringComparer.Ordinal));
        Assert.All(implementations, i => Assert.Equal(workspace.PathOf("Lib/Shapes.cs"), Assert.Single(i.Locations).FilePath));
    }

    [Fact]
    public async Task Go_to_implementation_of_an_interface_member_finds_its_implementations()
    {
        using var workspace = CreateSolution("""
            using Lib;
            IShape shape = new Square(2);
            Console.WriteLine(shape.Ar$$ea());
            """);

        var implementations = await workspace.Features.Navigation.FindImplementationsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret());

        Assert.Equal(new[] { "Lib.Circle.Area()", "Lib.Square.Area()" }, implementations.Select(i => i.SymbolDisplay).Order(StringComparer.Ordinal));
    }
}
