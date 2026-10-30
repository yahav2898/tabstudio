using TabStudio.Compilation.Features;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Compilation.Tests.Features;

/// <summary>IntelliSense in Visual Basic files, and across C# and Visual Basic projects that reference each other.</summary>
public sealed class VisualBasicFeatureTests
{
    private const string ProgramFile = "App/Program.vb";
    private const string GreeterFile = "Greetings/Greeter.vb";

    private const string Greeter = """
        ''' <summary>Greets people.</summary>
        Public Module Greeter
            ''' <summary>Says hello.</summary>
            ''' <param name="name">Who to greet.</param>
            Public Function Greet(name As String) As String
                Return "Hello, " & name
            End Function

            Public Function Repeat(name As String, count As Integer) As String
                Return String.Concat(Enumerable.Repeat(name, count))
            End Function

            ''' <summary>Writes a greeting.</summary>
            Public Sub Hello(name As String)
                Console.WriteLine(Greet(name))
            End Sub
        End Module
        """;

    /// <summary>A Visual Basic app, <c>App</c>, and the Visual Basic library it references, <c>Greetings</c>.</summary>
    private static FeatureWorkspace CreateSolution(string program) =>
        new FeatureWorkspace()
            .VisualBasicProject("Greetings")
            .VisualBasicProject("App", executable: true, references: ["Greetings"])
            .File(GreeterFile, Greeter)
            .File(ProgramFile, program)
            .Load();

    /// <summary>A program whose <c>Sub Main</c> runs one line of code.</summary>
    private static string ProgramWith(string line) => $"""
        Imports Greetings

        Module Program
            Sub Main()
                {line}
            End Sub
        End Module
        """;

    [Fact]
    public async Task Completion_after_a_dot_lists_members()
    {
        using var workspace = FeatureWorkspace.WithModule("""
            Module Program
                Sub Main()
                    Dim s = "x"
                    Console.WriteLine(s.$$)
                End Sub
            End Module
            """);

        var result = await workspace.Features.Completion.GetCompletionsAsync(
            workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), CompletionTrigger.Insertion('.'));

        Assert.Contains(result.Items, i => i.DisplayText == "Length" && i.Kind == SymbolGlyph.Property);
        Assert.Contains(result.Items, i => i.DisplayText == "Substring" && i.Kind == SymbolGlyph.Method);
    }

    [Fact]
    public async Task Completion_lists_locals_and_keywords()
    {
        using var workspace = FeatureWorkspace.WithModule("""
            Module Program
                Sub Main()
                    Dim count = 1
                    cou$$
                End Sub
            End Module
            """);

        var result = await workspace.Features.Completion.GetCompletionsAsync(
            workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), CompletionTrigger.Invoke);

        Assert.Contains(result.Items, i => i.DisplayText == "count" && i.Kind == SymbolGlyph.Local);
        Assert.Contains(result.Items, i => i.DisplayText == "If" && i.Kind == SymbolGlyph.Keyword);
        Assert.Equal(workspace.Caret(ProgramFile) - 3, result.SpanStart);
        Assert.Equal(3, result.SpanLength);
    }

    [Fact]
    public async Task Committing_an_item_replaces_the_typed_word()
    {
        using var workspace = FeatureWorkspace.WithModule("""
            Module Program
                Sub Main()
                    Console.Wri$$
                End Sub
            End Module
            """);
        var path = workspace.PathOf(ProgramFile);

        var result = await workspace.Features.Completion.GetCompletionsAsync(path, workspace.Caret(ProgramFile), CompletionTrigger.Invoke);
        var writeLine = Assert.Single(result.Items, i => i.DisplayText == "WriteLine");
        var commit = await workspace.Features.Completion.GetCompletionChangeAsync(path, writeLine);

        Assert.Contains("        Console.WriteLine\n", FeatureWorkspace.Apply(workspace.TextOf(ProgramFile), commit.Edits).ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_dot_triggers_completion()
    {
        using var workspace = FeatureWorkspace.WithModule("""
            Module Program
                Sub Main()
                    Console.$$
                End Sub
            End Module
            """);

        Assert.True(workspace.Features.Completion.ShouldTriggerCompletion(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), '.'));
    }

    [Fact]
    public async Task Quick_info_shows_the_signature_and_documentation_of_a_sub()
    {
        using var workspace = CreateSolution(ProgramWith("""Greeter.Hel$$lo("tablet")"""));

        var info = await workspace.Features.QuickInfo.GetQuickInfoAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.NotNull(info);
        var description = Assert.Single(info.Sections, s => s.Kind == QuickInfoSectionKind.Description);
        Assert.Contains("Hello(name As String)", description.Text, StringComparison.Ordinal);
        Assert.Contains(description.Runs, r => r.Text == "Sub" && r.Kind == TaggedTextKind.Keyword);
        var documentation = Assert.Single(info.Sections, s => s.Kind == QuickInfoSectionKind.Documentation);
        Assert.Equal("Writes a greeting.", documentation.Text.Trim());
        Assert.Equal("Hello", workspace.TextOf(ProgramFile).Substring(info.SpanStart, info.SpanLength));
        Assert.Equal(SymbolGlyph.Method, info.Kind);
    }

    [Fact]
    public async Task Signature_help_lists_the_overloads_of_a_framework_method()
    {
        using var workspace = CreateSolution(ProgramWith("Console.WriteLine($$)"));

        var help = await workspace.Features.SignatureHelp.GetSignatureHelpAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.NotNull(help);
        Assert.Contains(help.Signatures, s => s.Label == "Console.WriteLine(value As String)");
        Assert.True(help.Signatures.Count > 10);
        Assert.Equal(0, help.ArgumentIndex);
    }

    [Fact]
    public async Task Signature_help_shows_the_parameters_documentation_and_return_type()
    {
        using var workspace = CreateSolution(ProgramWith("Dim text = Greeter.Greet($$)"));

        var help = await workspace.Features.SignatureHelp.GetSignatureHelpAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.NotNull(help);
        var signature = Assert.Single(help.Signatures);
        Assert.Equal("Greeter.Greet(name As String) As String", signature.Label);
        Assert.Equal("Says hello.", signature.Documentation);
        Assert.Equal("Who to greet.", Assert.Single(signature.Parameters).Documentation);
        Assert.Contains(signature.Prefix, r => r.Text == "Greet" && r.Kind == TaggedTextKind.Method);
        Assert.Contains(signature.Suffix, r => r.Text == "As" && r.Kind == TaggedTextKind.Keyword);
        Assert.Equal(0, help.ActiveParameter);
    }

    [Fact]
    public async Task Signature_help_moves_to_the_next_parameter_after_a_comma()
    {
        using var workspace = CreateSolution(ProgramWith("""Dim text = Greeter.Repeat("a", $$)"""));

        var help = await workspace.Features.SignatureHelp.GetSignatureHelpAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.NotNull(help);
        Assert.Equal(1, help.ArgumentIndex);
        Assert.Equal(1, help.ActiveParameter);
        Assert.Equal("Greeter.Repeat(name As String, count As Integer) As String", help.Signatures[help.ActiveSignature].Label);
    }

    [Fact]
    public async Task Signature_help_matches_named_arguments_ignoring_case()
    {
        using var workspace = CreateSolution(ProgramWith("Dim text = Greeter.Repeat(COUNT:=$$)"));

        var help = await workspace.Features.SignatureHelp.GetSignatureHelpAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.NotNull(help);
        Assert.Equal("COUNT", help.ArgumentName);
        Assert.Equal(1, help.ActiveParameter);
    }

    [Fact]
    public async Task Signature_help_shows_constructors_with_new()
    {
        using var workspace = CreateSolution(ProgramWith("Dim builder = New System.Text.StringBuilder($$)"));

        var help = await workspace.Features.SignatureHelp.GetSignatureHelpAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.NotNull(help);
        Assert.Contains(help.Signatures, s => s.Label.StartsWith("New ", StringComparison.Ordinal)
            && s.Label.EndsWith("StringBuilder(capacity As Integer)", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Go_to_definition_finds_a_function_in_another_project()
    {
        using var workspace = CreateSolution(ProgramWith("""Console.WriteLine(Greeter.Gr$$eet("tablet"))"""));

        var definition = await workspace.Features.Navigation.FindDefinitionAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.NotNull(definition);
        Assert.False(definition.IsInMetadata);
        Assert.Equal("Greetings.Greeter.Greet(String)", definition.SymbolDisplay);
        Assert.Equal(SymbolGlyph.Method, definition.Kind);
        var location = Assert.Single(definition.Locations);
        Assert.Equal(workspace.PathOf(GreeterFile), location.FilePath);
        Assert.Equal(5, location.Line);
        Assert.Equal("Greet", workspace.TextOf(GreeterFile).Substring(location.Start, location.Length));
    }

    [Fact]
    public async Task Find_references_lists_the_declaration_and_every_use()
    {
        using var workspace = CreateSolution(ProgramWith("""Console.WriteLine(Greeter.Gr$$eet("tablet"))"""));

        var references = await workspace.Features.Navigation.FindReferencesAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        Assert.Equal(3, references.Count);
        Assert.Single(references, r => r.IsDefinition && r.FilePath == workspace.PathOf(GreeterFile));
        Assert.Single(references, r => !r.IsDefinition && r.FilePath == workspace.PathOf(GreeterFile));
        Assert.Single(references, r => !r.IsDefinition && r.FilePath == workspace.PathOf(ProgramFile));
    }

    [Fact]
    public async Task Highlights_mark_the_declaration_and_every_use_in_the_file()
    {
        using var workspace = FeatureWorkspace.WithModule("""
            Module Program
                Sub Main()
                    Dim total = 0
                    total += 2
                    Console.WriteLine(to$$tal)
                End Sub
            End Module
            """);

        var highlights = await workspace.Features.Highlights.GetDocumentHighlightsAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));

        var text = workspace.TextOf(ProgramFile);
        Assert.Equal(3, highlights.Count);
        Assert.All(highlights, h => Assert.Equal("total", text.Substring(h.Start, h.Length)));
        Assert.Equal(HighlightKind.Definition, highlights[0].Kind);
    }

    [Fact]
    public async Task Rename_changes_the_function_in_every_project()
    {
        using var workspace = CreateSolution(ProgramWith("""Console.WriteLine(Greeter.Gr$$eet("tablet"))"""));

        var target = await workspace.Features.Rename.CanRenameAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile));
        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), "Welcome");

        Assert.True(target.CanRename, target.Reason);
        Assert.Equal("Greet", target.CurrentName);
        Assert.True(result.Success, result.Error);
        Assert.Empty(result.Conflicts);
        var greeter = workspace.ApplyTo(GreeterFile, result.Edits);
        Assert.Contains("Public Function Welcome(name As String) As String", greeter, StringComparison.Ordinal);
        Assert.Contains("Console.WriteLine(Welcome(name))", greeter, StringComparison.Ordinal);
        Assert.Contains("Greeter.Welcome(\"tablet\")", workspace.ApplyTo(ProgramFile, result.Edits), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rename_may_change_only_the_case_of_a_name()
    {
        using var workspace = CreateSolution(ProgramWith("""Console.WriteLine(Greeter.Gr$$eet("tablet"))"""));

        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), "greet");

        Assert.True(result.Success, result.Error);
        Assert.Contains("Public Function greet(name As String) As String", workspace.ApplyTo(GreeterFile, result.Edits), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("1st", "is not a valid Visual Basic name")]
    [InlineData("two words", "is not a valid Visual Basic name")]
    [InlineData("Class", "'[Class]'")]
    [InlineData("end", "'[end]'")]
    public async Task Rename_rejects_names_that_are_not_visual_basic_identifiers(string newName, string expected)
    {
        using var workspace = CreateSolution(ProgramWith("""Console.WriteLine(Greeter.Gr$$eet("tablet"))"""));

        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), newName);

        Assert.False(result.Success);
        Assert.Contains(expected, result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rename_accepts_a_contextual_keyword()
    {
        using var workspace = CreateSolution(ProgramWith("""Console.WriteLine(Greeter.Gr$$eet("tablet"))"""));

        var result = await workspace.Features.Rename.GetRenameEditsAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), "From");

        Assert.True(result.Success, result.Error);
    }

    [Fact]
    public async Task Document_symbols_list_modules_classes_and_members_as_a_tree()
    {
        using var workspace = FeatureWorkspace.WithModule("""
            Namespace Shapes
                Public Module Geometry
                    Public Const Pi As Double = 3.14159
                    Private _count As Integer

                    Public Function Area(radius As Double) As Double
                        Return Pi * radius * radius
                    End Function

                    Sub Reset()
                        _count = 0
                    End Sub
                End Module

                Public Class Circle
                    Dim _radius As Double

                    Public Sub New(radius As Double)
                        _radius = radius
                    End Sub

                    Public Property Name As String = ""

                    Public Event Changed As EventHandler

                    Public Function Scale(ByRef factor As Double, ParamArray extra As Double()) As Double
                        Return _radius * fac$$tor
                    End Function

                    Public Shared Operator +(left As Circle, right As Circle) As Circle
                        Return New Circle(left._radius + right._radius)
                    End Operator

                    Public Enum Mood
                        Happy
                        Calm
                    End Enum
                End Class

                Friend Interface IShape
                    Function Area() As Double
                End Interface

                Public Structure Point
                    Public X As Integer
                End Structure

                Public Delegate Sub Notify(message As String)
            End Namespace
            """);
        var path = workspace.PathOf(ProgramFile);

        var symbols = await workspace.Features.Symbols.GetDocumentSymbolsAsync(path);
        var breadcrumb = await workspace.Features.Symbols.GetSymbolPathAsync(path, workspace.Caret(ProgramFile));

        var ns = Assert.Single(symbols);
        Assert.Equal("Shapes", ns.Name);
        Assert.Equal(SymbolGlyph.Namespace, ns.Kind);
        Assert.Equal(new[] { "Geometry", "Circle", "IShape", "Point", "Notify(String)" }, ns.Children.Select(c => c.DisplayName));
        Assert.Equal(
            new[] { SymbolGlyph.Class, SymbolGlyph.Class, SymbolGlyph.Interface, SymbolGlyph.Struct, SymbolGlyph.Delegate },
            ns.Children.Select(c => c.Kind));

        var geometry = ns.Children[0];
        Assert.Equal(SymbolAccessibility.Public, geometry.Accessibility);
        Assert.Equal(new[] { "Pi", "_count", "Area(Double)", "Reset()" }, geometry.Children.Select(c => c.DisplayName));
        Assert.Equal(
            new[] { SymbolGlyph.Constant, SymbolGlyph.Field, SymbolGlyph.Method, SymbolGlyph.Method },
            geometry.Children.Select(c => c.Kind));
        Assert.Equal(
            new[] { SymbolAccessibility.Public, SymbolAccessibility.Private, SymbolAccessibility.Public, SymbolAccessibility.Public },
            geometry.Children.Select(c => c.Accessibility));

        var circle = ns.Children[1];
        Assert.Equal(
            new[] { "_radius", "New(Double)", "Name", "Changed", "Scale(ByRef Double, ParamArray Double())", "Operator +(Circle, Circle)", "Mood" },
            circle.Children.Select(c => c.DisplayName));
        Assert.Equal(
            new[] { SymbolGlyph.Field, SymbolGlyph.Method, SymbolGlyph.Property, SymbolGlyph.Event, SymbolGlyph.Method, SymbolGlyph.Method, SymbolGlyph.Enum },
            circle.Children.Select(c => c.Kind));
        Assert.Equal(SymbolAccessibility.Private, circle.Children[0].Accessibility);
        Assert.Equal(new[] { "Happy", "Calm" }, circle.Children[6].Children.Select(c => c.Name));

        var shape = ns.Children[2];
        Assert.Equal(SymbolAccessibility.Internal, shape.Accessibility);
        var area = Assert.Single(shape.Children);
        Assert.Equal("Area()", area.DisplayName);
        Assert.Equal(SymbolAccessibility.Public, area.Accessibility);
        Assert.Equal(SymbolAccessibility.Public, Assert.Single(ns.Children[3].Children).Accessibility);

        var text = workspace.TextOf(ProgramFile);
        Assert.Equal("Circle", text.Substring(circle.NameStart, circle.NameLength));
        Assert.StartsWith("Public Class Circle", text[circle.Start..], StringComparison.Ordinal);
        Assert.EndsWith("End Class", text.Substring(circle.Start, circle.Length), StringComparison.Ordinal);
        Assert.Equal(new[] { "Shapes", "Circle", "Scale" }, breadcrumb.Select(s => s.Name));
    }

    [Fact]
    public async Task Add_imports_fixes_a_type_that_is_not_imported()
    {
        using var workspace = FeatureWorkspace.WithModule("""
            Module Program
                Sub Main()
                    Dim builder = New StringBuil$$der()
                    Console.WriteLine(builder.ToString())
                End Sub
            End Module
            """);

        var actions = await workspace.Features.CodeActions.GetCodeActionsAsync(workspace.PathOf(ProgramFile), workspace.Caret(ProgramFile), 0);
        var fix = actions.FirstOrDefault(a => a.Title == "Imports System.Text");
        Assert.True(fix is not null, "Offered: " + string.Join(" | ", actions.Select(a => a.Title)));
        var edits = await workspace.Features.CodeActions.GetCodeActionEditsAsync(fix.Id);
        var text = workspace.ApplyTo(ProgramFile, edits);

        Assert.Equal(CodeActionKind.QuickFix, fix.Kind);
        Assert.Equal("BC30002", fix.DiagnosticId);
        Assert.StartsWith("Imports System.Text", text, StringComparison.Ordinal);
        Assert.Contains("Dim builder = New StringBuilder()", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Code_actions_of_a_csharp_file_do_not_offer_visual_basic_fixes()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            var builder = new StringBuil$$der();
            Console.WriteLine(builder.ToString());
            """);

        var actions = await workspace.Features.CodeActions.GetCodeActionsAsync(workspace.PathOf("App/Program.cs"), workspace.Caret(), 0);

        Assert.Contains(actions, a => a.Title == "using System.Text;");
        Assert.DoesNotContain(actions, a => a.Title.StartsWith("Imports ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_csharp_app_sees_the_members_of_a_visual_basic_library()
    {
        using var workspace = new FeatureWorkspace()
            .VisualBasicProject("Greetings")
            .Project("App", executable: true, references: ["Greetings.vbproj"])
            .File(GreeterFile, Greeter)
            .File("App/Program.cs", "Console.WriteLine(Greetings.Greeter.$$);")
            .Load();
        var path = workspace.PathOf("App/Program.cs");

        var result = await workspace.Features.Completion.GetCompletionsAsync(path, workspace.Caret(), CompletionTrigger.Insertion('.'));
        workspace.Type("App/Program.cs", "Console.WriteLine(Greetings.Greeter.Gr$$eet(\"tablet\"));");
        var definition = await workspace.Features.Navigation.FindDefinitionAsync(path, workspace.Caret());
        var diagnostics = await workspace.Host.GetDocumentDiagnosticsAsync(path);

        Assert.Contains(result.Items, i => i.DisplayText == "Greet" && i.Kind == SymbolGlyph.Method);
        Assert.Contains(result.Items, i => i.DisplayText == "Repeat");
        Assert.NotNull(definition);
        Assert.Equal(workspace.PathOf(GreeterFile), Assert.Single(definition.Locations).FilePath);
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverityLevel.Error);
    }

    [Fact]
    public async Task A_visual_basic_app_sees_the_members_of_a_csharp_library()
    {
        using var workspace = new FeatureWorkspace()
            .Project("Shop")
            .VisualBasicProject("App", executable: true, references: ["Shop.csproj"])
            .File("Shop/Calculator.cs", """
                namespace Shop;

                public static class Calculator
                {
                    public static int Add(int left, int right) => left + right;
                }
                """)
            .File(ProgramFile, """
                Module Program
                    Sub Main()
                        Console.WriteLine(Shop.Calculator.$$)
                    End Sub
                End Module
                """)
            .Load();
        var path = workspace.PathOf(ProgramFile);

        var result = await workspace.Features.Completion.GetCompletionsAsync(path, workspace.Caret(ProgramFile), CompletionTrigger.Insertion('.'));
        workspace.Type(ProgramFile, """
            Module Program
                Sub Main()
                    Console.WriteLine(Shop.Calculator.Add(1, $$))
                End Sub
            End Module
            """);
        var help = await workspace.Features.SignatureHelp.GetSignatureHelpAsync(path, workspace.Caret(ProgramFile));

        Assert.Contains(result.Items, i => i.DisplayText == "Add" && i.Kind == SymbolGlyph.Method);
        Assert.NotNull(help);
        Assert.EndsWith("Add(left As Integer, right As Integer) As Integer", help.Signatures[help.ActiveSignature].Label, StringComparison.Ordinal);
        Assert.Equal(1, help.ActiveParameter);
    }
}
