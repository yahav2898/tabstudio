using TabStudio.Compilation;
using TabStudio.Compilation.Features;
using TabStudio.Testing;

namespace TabStudio.Web.Tests.Apps;

/// <summary>
/// IntelliSense in Razor files, against a Blazor Web App restored from nuget.org: completion of components, their
/// parameters, directives and the C# of <c>@code</c>, and Go to Definition from a component's tag, an attribute and
/// C# in a Razor file, and into a Razor file from C#.
/// </summary>
[Collection(NuGetTestGroup.Name)]
public sealed class RazorEditorTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    private string HomePath => _temp.Resolve("Counters/Components/Home.razor");

    private string CounterPath => _temp.Resolve("Counters/Components/Counter.razor");

    [Fact]
    public async Task Completes_the_components_a_file_can_use_after_a_tag_opens()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var features = new LanguageFeatures(workspace.Host);
        var text = CountersApp.Home.Replace("<h1>Clicks</h1>", "<h1>Clicks</h1>\n<Cou", StringComparison.Ordinal);
        workspace.Host.UpdateText(HomePath, text);
        var caret = text.IndexOf("<Cou", StringComparison.Ordinal) + "<Cou".Length;

        Assert.True(features.Completion.ShouldTriggerCompletion(HomePath, caret - 3, 'C'));
        var list = await features.Completion.GetCompletionsAsync(HomePath, caret, CompletionTrigger.Invoke);

        var counter = Assert.Single(list.Items, i => i.DisplayText == "Counter");
        Assert.Equal("Counters.Components", counter.InlineDescription);
        Assert.Contains(list.Items, i => i.DisplayText == "PageTitle");
        Assert.Equal(caret - 3, list.SpanStart);
        var commit = await features.Completion.GetCompletionChangeAsync(HomePath, counter, null);
        Assert.Equal(new TextEdit(caret - 3, 3, "Counter"), Assert.Single(commit.Edits));
    }

    [Fact]
    public async Task Completes_a_components_parameters_inside_its_tag()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var features = new LanguageFeatures(workspace.Host);
        var text = CountersApp.Home.Replace("<Counter Label=\"Clicks\"", "<Counter  Label=\"Clicks\"", StringComparison.Ordinal);
        workspace.Host.UpdateText(HomePath, text);
        var caret = text.IndexOf("<Counter ", StringComparison.Ordinal) + "<Counter ".Length;

        Assert.True(features.Completion.ShouldTriggerCompletion(HomePath, caret, ' '));
        var list = await features.Completion.GetCompletionsAsync(HomePath, caret, CompletionTrigger.Insertion(' '));

        Assert.Equal(
            new[] { "@bind-Count", "@key", "@ref", "@rendermode", "Count", "CountChanged", "Label" },
            list.Items.Select(i => i.DisplayText).Order(StringComparer.Ordinal));
        Assert.Equal("EventCallback<int>", list.Items.Single(i => i.DisplayText == "CountChanged").InlineDescription);
    }

    [Fact]
    public async Task Completes_directives_at_the_start_of_a_line()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var features = new LanguageFeatures(workspace.Host);
        var text = "@pa\n" + CountersApp.Counter;
        workspace.Host.UpdateText(CounterPath, text);

        var list = await features.Completion.GetCompletionsAsync(CounterPath, 3, CompletionTrigger.Insertion('a'));

        Assert.Contains(list.Items, i => i.DisplayText == "@page");
        Assert.Contains(list.Items, i => i.DisplayText == "@inject");
        Assert.Contains(list.Items, i => i.DisplayText == "@code");
        Assert.DoesNotContain(list.Items, i => i.DisplayText == "@model");
        Assert.Equal(0, list.SpanStart);
    }

    [Fact]
    public async Task Completes_CSharp_in_a_Razor_file_from_the_generated_code()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var features = new LanguageFeatures(workspace.Host);
        // Typing the first letter of an expression opens the list, as in a C# file.
        var typed = CountersApp.Home.Replace("Total: @clicks", "Total: @c", StringComparison.Ordinal);
        workspace.Host.UpdateText(HomePath, typed);
        var caret = typed.IndexOf("@c", StringComparison.Ordinal) + "@c".Length;

        Assert.True(features.Completion.ShouldTriggerCompletion(HomePath, caret, 'c'));
        var list = await features.Completion.GetCompletionsAsync(HomePath, caret, CompletionTrigger.Insertion('c'));

        var clicks = Assert.Single(list.Items, i => i.DisplayText == "clicks");
        Assert.Equal(caret - 1, clicks.SpanStart);
        Assert.Contains(list.Items, i => i.DisplayText == "string");
        Assert.NotEmpty(await features.Completion.GetCompletionDescriptionAsync(clicks));

        // Ctrl+Space in the middle of a word lists the same, over the whole word.
        var invoked = CountersApp.Home.Replace("Total: @clicks", "Total: @cli", StringComparison.Ordinal);
        workspace.Host.UpdateText(HomePath, invoked);
        caret = invoked.IndexOf("@cli", StringComparison.Ordinal) + "@cli".Length;

        list = await features.Completion.GetCompletionsAsync(HomePath, caret, CompletionTrigger.Invoke);

        Assert.Equal(caret - 3, Assert.Single(list.Items, i => i.DisplayText == "clicks").SpanStart);
    }

    [Fact]
    public async Task Goes_from_a_components_tag_to_the_component()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var features = new LanguageFeatures(workspace.Host);
        var tag = CountersApp.Home.IndexOf("<Counter", StringComparison.Ordinal) + 3;

        var definition = await features.Navigation.FindDefinitionAsync(HomePath, tag);

        Assert.NotNull(definition);
        Assert.Equal(SymbolGlyph.Class, definition.Kind);
        var location = Assert.Single(definition.Locations);
        Assert.Equal((CounterPath, 1, 1), (location.FilePath, location.Line, location.Column));
    }

    [Fact]
    public async Task Goes_from_an_attribute_to_the_parameter_and_from_CSharp_to_its_declaration()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var features = new LanguageFeatures(workspace.Host);

        var label = await features.Navigation.FindDefinitionAsync(HomePath, CountersApp.Home.IndexOf("Label=", StringComparison.Ordinal) + 2);
        var clicks = await features.Navigation.FindDefinitionAsync(HomePath, CountersApp.Home.IndexOf("\"clicks\"", StringComparison.Ordinal) + 3);

        var parameter = Assert.Single(label!.Locations);
        Assert.Equal((CounterPath, 4), (parameter.FilePath, parameter.Line));
        var field = Assert.Single(clicks!.Locations);
        Assert.Equal((HomePath, 9), (field.FilePath, field.Line));
        Assert.Equal("clicks", CountersApp.Home.ReplaceLineEndings("\n").Substring(field.Start, "clicks".Length));
    }

    [Fact]
    public async Task Goes_from_CSharp_to_a_component_in_its_Razor_file()
    {
        using var workspace = await WebAppWorkspace.LoadAsync(nuget, CountersApp.Write(_temp));
        var features = new LanguageFeatures(workspace.Host);
        var program = _temp.Resolve("Counters/Program.cs");
        var source = File.ReadAllText(program);

        var definition = await features.Navigation.FindDefinitionAsync(program, source.IndexOf("<App>", StringComparison.Ordinal) + 2);

        var location = Assert.Single(definition!.Locations);
        Assert.Equal((_temp.Resolve("Counters/Components/App.razor"), 1), (location.FilePath, location.Line));
    }

    public void Dispose() => _temp.Dispose();
}
