using TabStudio.App.ViewModels;
using TabStudio.Compilation.Features;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>The view models behind Parameter Info and the References panel, with results made up in the test.</summary>
public sealed class IntelliSenseViewModelTests
{
    [Fact]
    public void Parameter_info_shows_the_active_parameter_in_bold_with_its_documentation()
    {
        var help = new SignatureHelpViewModel();

        help.Show(Result(activeSignature: 0));

        Assert.True(help.IsOpen);
        Assert.Equal("1 of 2", help.Counter);
        Assert.True(help.HasOverloads);
        Assert.Equal("void Greet(string name)", string.Concat(help.Signature.Select(r => r.Text)));
        Assert.Contains(help.Signature, r => r.IsBold && r.Text == "string name");
        Assert.Equal("Says hello.", help.Documentation);
        Assert.Equal("name", help.ParameterName);
        Assert.Equal("Who to greet.", help.ParameterDocumentation);
    }

    [Fact]
    public void The_overload_picked_with_the_arrows_stays_while_typing_in_the_same_call()
    {
        var help = new SignatureHelpViewModel();
        help.Show(Result(activeSignature: 0));

        help.NextOverload();
        help.Show(Result(activeSignature: 0));

        Assert.Equal(1, help.SelectedIndex);
        Assert.Equal("2 of 2", help.Counter);
        Assert.Equal("void Greet(string name, int times)", string.Concat(help.Signature.Select(r => r.Text)));

        help.NextOverload();
        Assert.Equal(0, help.SelectedIndex);

        help.Close();
        Assert.False(help.IsOpen);
    }

    [Fact]
    public void References_are_grouped_by_file_with_the_reference_in_bold()
    {
        var references = new ReferencesViewModel((_, _, _) => Task.CompletedTask);
        var root = Path.Combine(Path.GetTempPath(), "Solution");
        var greeter = Path.Combine(root, "Lib", "Greeter.cs");
        var program = Path.Combine(root, "App", "Program.cs");

        references.Show(
            "'Greet' references",
            [
                new ReferenceItem(greeter, 40, 5, 3, 19, "    public string Greet() => \"Hi\";", IsDefinition: true),
                new ReferenceItem(program, 10, 5, 1, 9, "greeter.Greet();", IsDefinition: false),
                new ReferenceItem(program, 30, 5, 2, 9, "greeter.Greet();", IsDefinition: false),
            ],
            root);

        Assert.Equal("3 references in 2 files", references.Summary);
        var files = references.Rows.OfType<ReferenceFileRow>().ToList();
        Assert.Equal(new[] { "Greeter.cs", "Program.cs" }, files.Select(f => f.FileName));
        Assert.Equal("2 references", files[1].CountText);
        Assert.Equal("Lib", files[0].Folder);
        var definition = references.Rows.OfType<ReferenceLineRow>().First();
        Assert.True(definition.IsDefinition);
        Assert.Equal(new[] { "public string ", "Greet", "() => \"Hi\";" }, definition.Text.Select(r => r.Text));
        Assert.True(definition.Text[1].IsBold);
    }

    [Fact]
    public void An_empty_search_says_so()
    {
        var references = new ReferencesViewModel((_, _, _) => Task.CompletedTask);
        references.BeginSearch("'x' references");
        Assert.True(references.IsSearching);

        references.Show("'x' references", [], Path.GetTempPath());

        Assert.False(references.IsSearching);
        Assert.Empty(references.Rows);
        Assert.Equal("No references found.", references.Summary);
    }

    private static SignatureHelpResult Result(int activeSignature)
    {
        static TaggedRun Text(string text) => new(text, TaggedTextKind.Text);

        var name = new SignatureParameter("name", [Text("string name")], "Who to greet.", IsOptional: false);
        var times = new SignatureParameter("times", [Text("int times")], "How often.", IsOptional: false);
        SignatureItem[] signatures =
        [
            new([Text("void Greet(")], [name], [Text(")")], "Says hello.", ActiveParameter: 0, IsVariadic: false),
            new([Text("void Greet(")], [name, times], [Text(")")], "Says hello more than once.", ActiveParameter: 0, IsVariadic: false),
        ];
        return new SignatureHelpResult(signatures, activeSignature, ArgumentIndex: 0, ArgumentName: null, ApplicableSpanStart: 11, ApplicableSpanLength: 0);
    }
}
