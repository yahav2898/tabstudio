using System.Xml.Linq;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// Section headers are in sentence case (docs/design/README.md, "Type"): the GitHub Actions window's were in capitals,
/// "THIS COMMIT", which the spec names as what not to do.
/// </summary>
public sealed class SectionHeaderTests
{
    [Theory]
    [InlineData("Views/Actions/ActionsView.axaml")]
    [InlineData("Views/Live/LivePanelView.axaml")]
    [InlineData("Views/TestExplorerView.axaml")]
    public void Section_headers_are_not_in_capitals(string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TabStudio.slnx")))
        {
            root = root.Parent;
        }

        var document = XDocument.Load(Path.Combine(root!.FullName, "src", "TabStudio.App", file));
        var headers = document.Descendants()
            .Where(e => e.Name.LocalName == "TextBlock" && ((string?)e.Attribute("Classes"))?.Split(' ').Contains("section") == true)
            .Select(e => (string?)e.Attribute("Text") ?? string.Empty)
            .Where(text => text.Length > 1 && !text.StartsWith('{'))
            .ToList();

        Assert.NotEmpty(headers);
        Assert.All(headers, text => Assert.False(text.Where(char.IsLetter).All(char.IsUpper), $"\"{text}\" is in capitals."));
    }
}
