using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// Finds text that is stretched to a fixed height instead of centered in it (docs/design/README.md, "Type"). A text
/// block draws its line at its top, so a single line stretched into a taller pill, badge, header or row sits high; on
/// Android, whose font metrics differ from the headless renderer's, visibly so. The headless layout shows the stretch
/// itself: the text block is taller than its text.
/// </summary>
internal static class StretchedText
{
    /// <summary>How much taller than its text a line may be before it shows.</summary>
    private const double Tolerance = 1.5;

    /// <summary>Each single line of text under <paramref name="root"/> that is taller than its text, described.</summary>
    /// <param name="root">The window or control to look through.</param>
    public static IReadOnlyList<string> In(Visual root) =>
        [.. root.GetVisualDescendants().OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible && IsOneLine(text) && HasText(text))
            .Where(text => text.VerticalAlignment == VerticalAlignment.Stretch && Extra(text) > Tolerance)
            .Where(text => !InPillOrChip(text))
            .Select(Describe)];

    /// <summary>Fails with each stretched line of text under <paramref name="root"/>, when there are any.</summary>
    /// <param name="root">The window or control to look through.</param>
    public static void AssertNone(Visual root) => AssertNone(In(root));

    /// <summary>Fails with each stretched line of text found, when there are any.</summary>
    /// <param name="found">What <see cref="In"/> found.</param>
    public static void AssertNone(IEnumerable<string> found)
    {
        var all = found.Distinct().ToList();
        Assert.True(all.Count == 0, "Text stretched instead of centered:\n" + string.Join("\n", all));
    }

    private static bool IsOneLine(TextBlock text) => text.TextWrapping == TextWrapping.NoWrap && text.MaxLines <= 1;

    private static bool HasText(TextBlock text) => !string.IsNullOrEmpty(text.Text) || text.Inlines is { Count: > 0 };

    /// <summary>How much taller the text block is than its text.</summary>
    private static double Extra(TextBlock text) =>
        text.Bounds.Height - (text.DesiredSize.Height - text.Margin.Top - text.Margin.Bottom);

    /// <summary>
    /// Pills and chips center their content with a style of their own (Theme/Controls.axaml on main from 0.9), which this
    /// branch does not have yet.
    /// </summary>
    private static bool InPillOrChip(TextBlock text) =>
        text.GetVisualAncestors().OfType<Border>().FirstOrDefault() is { } border
        && (border.Classes.Contains("pill") || border.Classes.Contains("chip"));

    private static string Describe(TextBlock text)
    {
        var path = text.GetVisualAncestors().OfType<Control>().Take(4).Reverse().Select(Name).Append(Name(text));
        return $"{string.Join(" > ", path)} \"{text.Text}\" is {Extra(text):0.#} px taller than its text";
    }

    private static string Name(Control control)
    {
        var name = control.GetType().Name;
        if (!string.IsNullOrEmpty(control.Name))
        {
            name += "#" + control.Name;
        }

        if (control.Classes.Count > 0)
        {
            name += "." + string.Join(".", control.Classes.Where(item => !item.StartsWith(':')));
        }

        return name.TrimEnd('.');
    }
}
