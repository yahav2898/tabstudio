using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class CodeActionTests
{
    private const string ProgramFile = "App/Program.cs";

    /// <summary>Lists the actions at the caret, applies the one with the given title and returns the new text.</summary>
    private static async Task<string> ApplyAsync(FeatureWorkspace workspace, Func<CodeActionItem, bool> choose)
    {
        var actions = await workspace.Features.CodeActions.GetCodeActionsAsync(workspace.PathOf(ProgramFile), workspace.Caret(), 0);
        var action = actions.FirstOrDefault(choose);
        Assert.True(action is not null, "Offered: " + string.Join(" | ", actions.Select(a => a.Title)));

        var edits = await workspace.Features.CodeActions.GetCodeActionEditsAsync(action.Id);
        return workspace.ApplyTo(ProgramFile, edits);
    }

    [Fact]
    public async Task Add_using_fixes_a_generic_list_without_implicit_usings()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            namespace Demo;

            public class Holder
            {
                public Li$$st<int> Items { get; } = new();
            }
            """, implicitUsings: false);

        var text = await ApplyAsync(workspace, a => a.Title == "using System.Collections.Generic;");

        Assert.StartsWith("using System.Collections.Generic;", text, StringComparison.Ordinal);
        Assert.Contains("public List<int> Items", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Add_using_fixes_a_string_builder()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            var builder = new StringBuil$$der();
            Console.WriteLine(builder.ToString());
            """);

        var actions = await workspace.Features.CodeActions.GetCodeActionsAsync(workspace.PathOf(ProgramFile), workspace.Caret(), 0);
        var text = await ApplyAsync(workspace, a => a.Title == "using System.Text;");

        var fix = Assert.Single(actions, a => a.Title == "using System.Text;");
        Assert.Equal(CodeActionKind.QuickFix, fix.Kind);
        Assert.Equal("CS0246", fix.DiagnosticId);
        Assert.StartsWith("using System.Text;", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Implement_interface_adds_the_missing_members()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            namespace Demo;

            public interface IShape
            {
                double Area();
            }

            public sealed class Square : ISh$$ape
            {
            }
            """);

        var text = await ApplyAsync(workspace, a => a.Title == "Implement interface");

        Assert.Contains("public double Area()", text, StringComparison.Ordinal);
        Assert.Contains("NotImplementedException", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_method_creates_a_stub_for_an_unknown_call()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            namespace Demo;

            public sealed class Calculator
            {
                public int Run() => Comp$$ute(1, 2);
            }
            """);

        var text = await ApplyAsync(workspace, a => a.Title.StartsWith("Generate method", StringComparison.Ordinal));

        Assert.Contains("private int Compute(int", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remove_unnecessary_usings_is_offered_on_an_unused_using()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            using System.Te$$xt;

            namespace Demo;

            public sealed class Empty
            {
            }
            """);

        var text = await ApplyAsync(workspace, a => a.Title == "Remove unnecessary usings");

        Assert.DoesNotContain("using System.Text;", text, StringComparison.Ordinal);
        Assert.Contains("public sealed class Empty", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refactorings_are_offered_without_a_diagnostic()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            namespace Demo;

            public static class Status
            {
                public static void Print(bool ready)
                {
                    i$$f (ready)
                    {
                        Console.WriteLine("ready");
                    }
                    else
                    {
                        Console.WriteLine("waiting");
                    }
                }
            }
            """);

        var actions = await workspace.Features.CodeActions.GetCodeActionsAsync(workspace.PathOf(ProgramFile), workspace.Caret(), 0);
        var text = await ApplyAsync(workspace, a => a.Title == "Invert if");

        Assert.Equal(CodeActionKind.Refactoring, Assert.Single(actions, a => a.Title == "Invert if").Kind);
        Assert.Contains("if (!ready)", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_action_changes_nothing()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.WriteLine();");

        var edits = await workspace.Features.CodeActions.GetCodeActionEditsAsync("0:0");

        Assert.Empty(edits);
    }
}
