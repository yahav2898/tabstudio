using System.Xml.Linq;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// Text that trims must be measured with the room it has: inside a horizontal StackPanel a TextBlock gets all the
/// width it wants, so its TextTrimming never applies and it draws over its neighbour (a Locals name ran into its
/// value, "iteration3"). Read from the XAML, because a screenshot only shows it with a long enough name.
/// </summary>
public sealed class TrimmingLayoutTests
{
    private static readonly XNamespace Avalonia = "https://github.com/avaloniaui";
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Theory]
    [InlineData("Views/VariableListView.axaml", "NameText")]
    [InlineData("Views/WorkspaceView.axaml", "StatusMessageText")]
    [InlineData("Views/WorkspaceView.axaml", "OperationsText")]
    public void A_trimming_text_sits_in_a_star_column_not_a_stack(string file, string name)
    {
        var document = XDocument.Load(Path.Combine(SourceRoot(), file));
        var text = document.Descendants(Avalonia + "TextBlock").Single(t => (string?)t.Attribute(Xaml + "Name") == name);

        var parent = text.Parent!;
        Assert.NotEqual("StackPanel", parent.Name.LocalName);
        Assert.Equal("Grid", parent.Name.LocalName);
        var columns = ((string?)parent.Attribute("ColumnDefinitions") ?? string.Empty).Split(',');
        var column = int.Parse((string?)text.Attribute("Grid.Column") ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        Assert.EndsWith("*", columns[column].Trim(), StringComparison.Ordinal);
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TabStudio.slnx")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("The repository was not found."), "src", "TabStudio.App");
    }
}
