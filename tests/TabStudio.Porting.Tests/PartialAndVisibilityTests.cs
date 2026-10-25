using System.Xml.Linq;

namespace TabStudio.Porting.Tests;

public sealed class PartialAndVisibilityTests
{
    [Theory]
    [InlineData("Form", "AppForm")]
    [InlineData("UserControl", "UserControl")]
    public void Designer_root_uses_the_base_class_from_the_other_partial_file(string baseClass, string rootClass)
    {
        var result = Port(PortSourceKind.WindowsForms, new Dictionary<string, string>
        {
            ["View.cs"] = $"namespace Example; public partial class View : {baseClass} {{ public View() {{ InitializeComponent(); }} }}",
            ["View.Designer.cs"] = "namespace Example; partial class View { private void InitializeComponent() { ClientSize = new Size(300, 200); } }",
        });
        var xaml = XDocument.Parse(result.Files.Single(f => f.Path == "View.axaml").Text!);
        Assert.Equal(rootClass, xaml.Root!.Name.LocalName);
    }

    [Theory]
    [InlineData("<TextBlock Visibility=\"Hidden\" />", "Visibility.Hidden")]
    [InlineData("<Window.Resources><Style TargetType=\"Button\"><Setter Property=\"Visibility\" Value=\"Hidden\" /></Style></Window.Resources><Button />", "Visibility.Hidden setter")]
    public void Hidden_visibility_approximations_have_reviewable_notes(string content, string expected)
    {
        var result = Port(PortSourceKind.Wpf, new Dictionary<string, string>
        {
            ["MainWindow.xaml"] = "<Window xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" x:Class=\"Example.MainWindow\">" + content + "</Window>",
        });
        var note = Assert.Single(result.Notes, n => n.What == expected);
        Assert.Contains("focus", note.Why, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TODO(port)", result.Files.Single(f => f.Path == note.File).Text!.Split('\n')[note.Line - 1], StringComparison.Ordinal);
    }

    private static PortResult Port(PortSourceKind kind, Dictionary<string, string> files) => AvaloniaPorter.Port(
        new PortSource { Name = "Example", Kind = kind, Files = files },
        new PortOptions("Converted", Path.Combine(Path.GetTempPath(), "port-partial-rules"), SampleProjects.AvaloniaVersion));
}
