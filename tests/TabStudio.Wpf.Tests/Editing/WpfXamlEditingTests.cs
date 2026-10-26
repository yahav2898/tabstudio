using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Compilation;
using TabStudio.Compilation.Wpf;
using TabStudio.Compilation.Wpf.Editing;
using TabStudio.Testing;
using TabStudio.Wpf.Templates;

namespace TabStudio.Wpf.Tests.Editing;

/// <summary>The XAML editor against the actual OpenSilver API, including edits that become buildable code.</summary>
[Collection(NuGetTestGroup.Name)]
public sealed class WpfXamlEditingTests(NuGetFixture nuget) : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public async Task Element_completion_replaces_the_whole_word_and_describes_the_type()
    {
        using var workspace = await OpenAsync("<Buton />");
        var features = new WpfXamlFeatures(workspace.Host);
        var text = File.ReadAllText(XamlPath);
        var position = text.IndexOf("Buton", StringComparison.Ordinal) + 2;

        var list = await features.GetCompletionsAsync(XamlPath, position);
        var button = Assert.Single(list.Items, i => i.DisplayText == "Button");
        var commit = await features.GetCompletionChangeAsync(XamlPath, button, null);
        var change = Assert.Single(commit.Edits);

        Assert.Equal("Buton", text.Substring(change.Start, change.Length));
        Assert.Equal("Button", change.NewText);
        Assert.NotEmpty(await features.GetCompletionDescriptionAsync(button));
        Assert.True(features.ShouldTriggerCompletion(XamlPath, position, 'u'));
    }

    [Fact]
    public async Task Attribute_completion_places_the_caret_inside_quotes_without_duplicating_existing_quotes()
    {
        using var workspace = await OpenAsync("<Button Horiz />");
        var features = new WpfXamlFeatures(workspace.Host);
        var text = File.ReadAllText(XamlPath);
        var position = text.IndexOf("Horiz", StringComparison.Ordinal) + 5;
        var list = await features.GetCompletionsAsync(XamlPath, position);
        var attribute = Assert.Single(list.Items, i => i.DisplayText == "HorizontalAlignment");

        var commit = await features.GetCompletionChangeAsync(XamlPath, attribute, null);

        Assert.Equal("HorizontalAlignment=\"\"", Assert.Single(commit.Edits).NewText);
        Assert.Equal(attribute.SpanStart + "HorizontalAlignment=\"".Length, commit.CaretPosition);
        workspace.Host.UpdateText(XamlPath, text.Replace("Horiz", "Horiz=\"Center\"", StringComparison.Ordinal));
        var existing = await features.GetCompletionChangeAsync(XamlPath, attribute, null);
        Assert.Equal("HorizontalAlignment", Assert.Single(existing.Edits).NewText);
    }

    [Fact]
    public async Task Completion_offers_enum_values_attached_properties_and_binding_paths()
    {
        using var workspace = await OpenAsync("""<Button HorizontalAlignment="" Grid.R="0" Content="{Binding }" />""");
        var features = new WpfXamlFeatures(workspace.Host);
        var text = File.ReadAllText(XamlPath);

        var values = await features.GetCompletionsAsync(XamlPath, text.IndexOf("HorizontalAlignment=\"", StringComparison.Ordinal) + "HorizontalAlignment=\"".Length);
        var attached = await features.GetCompletionsAsync(XamlPath, text.IndexOf("Grid.R", StringComparison.Ordinal) + 6);
        var binding = await features.GetCompletionsAsync(XamlPath, text.IndexOf("{Binding ", StringComparison.Ordinal) + 9);

        Assert.Contains(values.Items, i => i.DisplayText == "Center");
        Assert.Contains(attached.Items, i => i.DisplayText == "Grid.Row");
        Assert.Contains(binding.Items, i => i.DisplayText == "Title");
    }

    [Fact]
    public async Task Quick_info_and_definition_resolve_types_handlers_names_and_resources()
    {
        using var workspace = await OpenAsync("""
            <Grid>
                <Grid.Resources><SolidColorBrush x:Key="Accent" Color="Red" /></Grid.Resources>
                <Button x:Name="Go" Click="Go_Click" Background="{StaticResource Accent}" />
                <TextBlock Text="{Binding Content, ElementName=Go}" />
            </Grid>
            """);
        var features = new WpfXamlFeatures(workspace.Host);
        var text = File.ReadAllText(XamlPath);

        var info = await features.GetQuickInfoAsync(XamlPath, text.IndexOf("Button", StringComparison.Ordinal) + 2);
        var type = await features.FindDefinitionAsync(XamlPath, text.IndexOf("Button", StringComparison.Ordinal) + 2);
        var handler = await features.FindDefinitionAsync(XamlPath, text.IndexOf("Go_Click", StringComparison.Ordinal) + 2);
        var resource = await features.FindDefinitionAsync(XamlPath, text.IndexOf("StaticResource Accent", StringComparison.Ordinal) + 17);
        var name = await features.FindDefinitionAsync(XamlPath, text.IndexOf("ElementName=Go", StringComparison.Ordinal) + 13);

        Assert.NotNull(info);
        Assert.True(type?.IsInMetadata);
        Assert.Equal(CodePath, Assert.Single(handler!.Locations).FilePath);
        Assert.Equal(text.IndexOf("Accent", StringComparison.Ordinal), Assert.Single(resource!.Locations).Start);
        Assert.Equal(text.IndexOf("Go\"", StringComparison.Ordinal), Assert.Single(name!.Locations).Start);
    }

    [Theory]
    [InlineData("<Buton />", "Buton")]
    [InlineData("<Button Contnt=\"Go\" />", "Contnt")]
    [InlineData("<Button HorizontalAlignment=\"Middle\" />", "Middle")]
    [InlineData("<Button Click=\"Missing_Click\" />", "Missing_Click")]
    [InlineData("<Grid><Button x:Name=\"Go\" /><Button x:Name=\"Go\" /></Grid>", "Go")]
    public async Task Live_errors_point_to_the_invalid_name_or_value(string content, string name)
    {
        using var workspace = await OpenAsync(content);
        var features = new WpfXamlFeatures(workspace.Host);

        var diagnostics = await features.GetDiagnosticsAsync(XamlPath);

        Assert.Contains(diagnostics, d => d.Location?.FilePath == XamlPath
            && File.ReadAllText(XamlPath).Substring(d.Location.Start, d.Location.Length) == name);
    }

    [Fact]
    public async Task Missing_WPF_API_is_named_with_the_Windows_Bridge_in_XAML_and_CSharp()
    {
        using var workspace = await OpenAsync("<Viewport3D />");
        var features = new WpfXamlFeatures(workspace.Host);
        workspace.Host.UpdateText(CodePath, WpfTemplateTests.CodeBehind("private void Open() { DialogResult = true; }"));

        var xaml = await features.GetDiagnosticsAsync(XamlPath);
        var code = await workspace.Host.GetDocumentDiagnosticsAsync(CodePath);

        Assert.Contains(xaml, d => d.Id == WpfDiagnosticIds.UnsupportedApi && d.Message.Contains("Viewport3D", StringComparison.Ordinal) && d.Message.Contains("Windows Bridge", StringComparison.Ordinal));
        Assert.Contains(code, d => d.Id == WpfDiagnosticIds.UnsupportedApi && d.Message.Contains("Window.DialogResult", StringComparison.Ordinal) && d.Message.Contains("Windows Bridge", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task The_light_bulb_adds_a_typed_handler_inside_the_class_and_removes_the_live_error(bool crlf, bool compact)
    {
        using var workspace = await OpenAsync("<Button Click=\"Missing_Click\" />");
        var source = compact ? "namespace WpfApp1 { public partial class MainWindow : System.Windows.Window { } }" : File.ReadAllText(CodePath);
        if (crlf)
        {
            source = source.ReplaceLineEndings("\r\n");
        }

        workspace.Host.UpdateText(CodePath, source);
        var features = new WpfXamlFeatures(workspace.Host);
        var position = File.ReadAllText(XamlPath).IndexOf("Missing_Click", StringComparison.Ordinal) + 2;
        var action = Assert.Single(await features.GetCodeActionsAsync(XamlPath, position, 0));

        var edit = Assert.Single(await features.GetCodeActionEditsAsync(action.Id));
        foreach (var change in edit.Edits.OrderByDescending(e => e.Start))
        {
            source = source.Remove(change.Start, change.Length).Insert(change.Start, change.NewText);
        }

        Assert.Equal(CodePath, edit.FilePath);
        Assert.Contains("private void Missing_Click(object sender,", source, StringComparison.Ordinal);
        Assert.DoesNotContain(CSharpSyntaxTree.ParseText(source).GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        if (crlf)
        {
            Assert.DoesNotContain("\n", source.Replace("\r\n", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        }

        workspace.Host.UpdateText(CodePath, source);
        Assert.DoesNotContain(await features.GetDiagnosticsAsync(XamlPath), d => d.Id == WpfDiagnosticIds.MissingEventHandler);
        Assert.Empty(await features.GetCodeActionsAsync(XamlPath, position, 0));
    }

    [Fact]
    public async Task A_non_WPF_file_and_an_invalid_action_return_no_results()
    {
        using var workspace = await OpenAsync("<Grid />");
        var features = new WpfXamlFeatures(workspace.Host);

        Assert.False(features.AppliesTo(CodePath));
        Assert.Empty((await features.GetCompletionsAsync(CodePath, 0)).Items);
        Assert.Null(await features.GetQuickInfoAsync(CodePath, 0));
        Assert.Null(await features.FindDefinitionAsync(CodePath, 0));
        Assert.Empty(await features.GetCodeActionEditsAsync("wpf-xaml-handler|invalid"));
    }

    public void Dispose() => _temp.Dispose();

    private string XamlPath => _temp.Resolve("WpfApp1/MainWindow.xaml");

    private string CodePath => _temp.Resolve("WpfApp1/MainWindow.xaml.cs");

    private async Task<WpfAppWorkspace> OpenAsync(string content)
    {
        WpfTemplates.Create(_temp.Path, "WpfApp1");
        _temp.Write("WpfApp1/MainWindow.xaml", WpfTemplateTests.Window(content));
        _temp.Write("WpfApp1/MainWindow.xaml.cs", WpfTemplateTests.CodeBehind("""
            private void Bind() { DataContext = new EditorModel(); }
            private void Go_Click(object sender, RoutedEventArgs e) { }
            """) + "\nnamespace WpfApp1 { public class EditorModel { public string Title { get; set; } = string.Empty; } }");
        return await WpfAppWorkspace.LoadAsync(nuget, _temp.Path);
    }
}
