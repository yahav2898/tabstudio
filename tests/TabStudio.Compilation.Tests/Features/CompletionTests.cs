using TabStudio.Compilation.Features;

namespace TabStudio.Compilation.Tests.Features;

public sealed class CompletionTests
{
    [Fact]
    public async Task Completion_after_a_dot_lists_members()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.$$");

        var result = await workspace.Features.Completion.GetCompletionsAsync(
            workspace.PathOf("App/Program.cs"), workspace.Caret(), CompletionTrigger.Insertion('.'));

        var writeLine = Assert.Single(result.Items, i => i.DisplayText == "WriteLine");
        Assert.Equal(SymbolGlyph.Method, writeLine.Kind);
        Assert.Contains(result.Items, i => i.DisplayText == "Out" && i.Kind == SymbolGlyph.Property);
        Assert.DoesNotContain(result.Items, i => i.DisplayText == "while");
    }

    [Fact]
    public async Task Completion_lists_locals_and_keywords()
    {
        using var workspace = FeatureWorkspace.WithProgram("""
            var count = 1;
            cou$$
            """);

        var result = await workspace.Features.Completion.GetCompletionsAsync(
            workspace.PathOf("App/Program.cs"), workspace.Caret(), CompletionTrigger.Invoke);

        Assert.Contains(result.Items, i => i.DisplayText == "count" && i.Kind == SymbolGlyph.Local);
        Assert.Contains(result.Items, i => i.DisplayText == "while" && i.Kind == SymbolGlyph.Keyword);
        Assert.Equal(workspace.Caret() - 3, result.SpanStart);
        Assert.Equal(3, result.SpanLength);
    }

    [Fact]
    public async Task Committing_an_item_replaces_the_typed_word()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.Wri$$");
        var path = workspace.PathOf("App/Program.cs");

        var result = await workspace.Features.Completion.GetCompletionsAsync(path, workspace.Caret(), CompletionTrigger.Invoke);
        var writeLine = Assert.Single(result.Items, i => i.DisplayText == "WriteLine");
        var commit = await workspace.Features.Completion.GetCompletionChangeAsync(path, writeLine);

        Assert.Equal("Console.WriteLine", FeatureWorkspace.Apply(workspace.TextOf("App/Program.cs"), commit.Edits));
    }

    [Fact]
    public async Task Committing_after_typing_more_replaces_the_filter_text_too()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.$$");
        var path = workspace.PathOf("App/Program.cs");
        var result = await workspace.Features.Completion.GetCompletionsAsync(path, workspace.Caret(), CompletionTrigger.Insertion('.'));
        var writeLine = Assert.Single(result.Items, i => i.DisplayText == "WriteLine");

        workspace.Type("App/Program.cs", "Console.WrL$$");
        var commit = await workspace.Features.Completion.GetCompletionChangeAsync(path, writeLine);

        Assert.Equal("Console.WriteLine", FeatureWorkspace.Apply(workspace.TextOf("App/Program.cs"), commit.Edits));
    }

    [Fact]
    public async Task Completion_descriptions_show_the_signature()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.$$");

        var result = await workspace.Features.Completion.GetCompletionsAsync(
            workspace.PathOf("App/Program.cs"), workspace.Caret(), CompletionTrigger.Insertion('.'));
        var description = await workspace.Features.Completion.GetCompletionDescriptionAsync(
            Assert.Single(result.Items, i => i.DisplayText == "WriteLine"));

        var text = string.Concat(description.Select(r => r.Text));
        Assert.Contains("Console.WriteLine(", text, StringComparison.Ordinal);
        Assert.Contains(description, r => r.Text == "WriteLine" && r.Kind == TaggedTextKind.Method);
    }

    [Fact]
    public async Task Completion_offers_types_from_namespaces_that_are_not_imported()
    {
        using var workspace = FeatureWorkspace.WithProgram("var builder = new StringBuil$$");
        var path = workspace.PathOf("App/Program.cs");

        var result = await workspace.Features.Completion.GetCompletionsAsync(path, workspace.Caret(), CompletionTrigger.Invoke);
        var builder = Assert.Single(result.Items, i => i.DisplayText == "StringBuilder" && i.InlineDescription == "System.Text");
        var commit = await workspace.Features.Completion.GetCompletionChangeAsync(path, builder);

        var text = FeatureWorkspace.Apply(workspace.TextOf("App/Program.cs"), commit.Edits);
        Assert.Contains("using System.Text;", text, StringComparison.Ordinal);
        Assert.EndsWith("new StringBuilder", text, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dot_triggers_completion_and_a_space_does_not()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.$$");
        var path = workspace.PathOf("App/Program.cs");

        Assert.True(workspace.Features.Completion.ShouldTriggerCompletion(path, workspace.Caret(), '.'));
        Assert.False(workspace.Features.Completion.ShouldTriggerCompletion(path, workspace.Caret(), ' '));
    }

    [Fact]
    public async Task A_file_outside_the_workspace_has_no_completions()
    {
        using var workspace = FeatureWorkspace.WithProgram("Console.$$");

        var result = await workspace.Features.Completion.GetCompletionsAsync("/nowhere/File.cs", 0, CompletionTrigger.Invoke);

        Assert.Empty(result.Items);
    }
}
