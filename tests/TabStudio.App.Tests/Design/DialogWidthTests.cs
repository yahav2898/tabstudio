using System.Globalization;
using System.Xml.Linq;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// A dialog card fits a phone (docs/design/README.md, "Dialogs": the width less 16 on each side). A fixed Width of
/// 440 to 600 pixels is a minimum too, so on a 412-pixel phone the GitHub sign-in card lost 44 pixels on each side and
/// its Sign In button ended off the screen.
/// </summary>
public sealed class DialogWidthTests
{
    [Theory]
    [InlineData("Views/GitHubSignInView.axaml")]
    [InlineData("Views/GitDialogsView.axaml")]
    [InlineData("Views/Actions/ActionsDialogsView.axaml")]
    [InlineData("Views/Extensions/QuickPickView.axaml")]
    [InlineData("Views/PullRequests/PullRequestView.axaml")]
    [InlineData("Views/LinkView.axaml")]
    public void Dialog_cards_have_a_maximum_width_not_a_fixed_one(string file)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "TabStudio.slnx")))
        {
            root = root.Parent;
        }

        var document = XDocument.Load(Path.Combine(root!.FullName, "src", "TabStudio.App", file));
        var fixedCards = document.Descendants()
            .Where(e => e.Name.LocalName == "Border"
                && double.TryParse((string?)e.Attribute("Width"), NumberStyles.Float, CultureInfo.InvariantCulture, out var width) && width >= 300)
            .Select(e => e.ToString().Split('\n')[0])
            .ToList();

        Assert.True(fixedCards.Count == 0, string.Join(Environment.NewLine, fixedCards));
    }
}
