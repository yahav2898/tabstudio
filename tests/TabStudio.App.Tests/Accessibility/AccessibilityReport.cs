using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// The accessibility audit of every screenshot. Each screenshot test checks its window with
/// <see cref="AccessibilityAudit"/>: what is found goes to <c>accessibility/findings.tsv</c> beside the screenshots, and
/// the views it showed to <c>accessibility/views.tsv</c>, so CI can tell that every view was checked.
/// </summary>
internal static class AccessibilityReport
{
    private static readonly Lock Gate = new();

    /// <summary>Whether a finding fails the test, rather than only being reported.</summary>
    public static bool Enforced => false;

    /// <summary>The folder of the reports, under the screenshots' folder.</summary>
    public static string FolderIn(string screenshots) => Directory.CreateDirectory(Path.Combine(screenshots, "accessibility")).FullName;

    /// <summary>Checks a screenshot's window and records what was found, and the views it shows.</summary>
    /// <param name="window">The window the screenshot is of.</param>
    /// <param name="screenshots">The screenshots' folder.</param>
    /// <param name="screenshot">The screenshot's name.</param>
    /// <param name="clipping">Whether to check for clipped text too.</param>
    /// <returns>What was found.</returns>
    public static IReadOnlyList<AccessibilityFinding> Check(Window window, string screenshots, string screenshot, bool clipping = false)
    {
        var dark = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        var found = AccessibilityAudit.In(window, dark, clipping);
        var views = ViewsIn(window);
        var folder = FolderIn(screenshots);
        lock (Gate)
        {
            File.AppendAllLines(Path.Combine(folder, "findings.tsv"), found.Select(f => $"{screenshot}\t{f.Kind}\t{f.Path}\t{f.Text}"));
            File.AppendAllLines(Path.Combine(folder, "views.tsv"), views.Select(v => $"{screenshot}\t{v}"));
        }

        return found;
    }

    /// <summary>The IDE's views a window shows: the user controls of TabStudio.App, by name.</summary>
    public static IReadOnlyList<string> ViewsIn(Visual root) =>
        [.. root.GetVisualDescendants().OfType<UserControl>().Where(c => c.IsEffectivelyVisible && c.GetType().Namespace?.StartsWith("TabStudio.App.Views", StringComparison.Ordinal) == true)
            .Select(c => c.GetType().FullName!).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>Fails with the findings, when there are any.</summary>
    public static void AssertNone(IReadOnlyList<AccessibilityFinding> found, string what)
    {
        Assert.True(found.Count == 0, $"{what} does not meet the accessibility rules (docs/design/README.md, \"Accessibility\"):\n"
            + string.Join("\n", found.Take(80).Select(f => $"{f.Kind}: {f.Path}: {f.Text}")));
    }
}
