using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TabStudio.App.Accessibility;

namespace TabStudio.App.Tests.Accessibility;

/// <summary>
/// The accessibility audit of every screenshot (docs/design/README.md, "Accessibility"). Each screenshot test checks
/// its window with <see cref="AccessibilityAudit"/> at the text size it was shown at, then shows the same window at the
/// largest text size (Very large, 150%), saves that as <c>largest/&lt;name&gt;.png</c> and checks that no text is
/// clipped there, and goes back. CI runs every screenshot test in the dark and the light theme, so every view is
/// checked in both, at the default and the largest text size.
/// </summary>
/// <remarks>
/// What is found goes to <c>accessibility/findings.tsv</c> beside the screenshots, and the views each screenshot showed
/// to <c>accessibility/views.tsv</c>, which <c>build/ci/accessibility-coverage.py</c> checks against every view of the IDE.
/// </remarks>
internal static class AccessibilityReport
{
    private static readonly Lock Gate = new();

    /// <summary>Whether a finding fails the test, rather than only being reported.</summary>
    public static bool Enforced => true;

    /// <summary>The folder of the reports, under the screenshots' folder.</summary>
    public static string FolderIn(string screenshots) => Directory.CreateDirectory(Path.Combine(screenshots, "accessibility")).FullName;

    /// <summary>Checks a screenshot's window, at its text size and at the largest, and records what was found.</summary>
    /// <param name="window">The window the screenshot is of.</param>
    /// <param name="screenshots">The screenshots' folder.</param>
    /// <param name="screenshot">The screenshot's name.</param>
    /// <returns>What was found.</returns>
    public static IReadOnlyList<AccessibilityFinding> Check(Window window, string screenshots, string screenshot)
    {
        var dark = Application.Current?.ActualThemeVariant != ThemeVariant.Light;
        var factor = TypeScale.Factor;
        var found = new List<AccessibilityFinding>(AccessibilityAudit.In(window, dark, clipping: factor >= TextSizes.Largest));
        var views = ViewsIn(window);
        if (factor < TextSizes.Largest)
        {
            // Nothing else runs while the text is larger: other tests' work waits on the dispatcher.
            TypeScale.Apply(TextSizes.Largest);
            try
            {
                Settle(window);
                found.AddRange(AccessibilityAudit.In(window, dark, clipping: true).Where(f => f.Kind == "clipped"));
                var largest = Directory.CreateDirectory(Path.Combine(screenshots, "largest")).FullName;
                RenderFrame();
                using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
                var file = Path.Combine(largest, screenshot + ".png");
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                frame.Save(file);
            }
            finally
            {
                TypeScale.Apply(factor);
                Settle(window);
            }
        }

        var folder = FolderIn(screenshots);
        lock (Gate)
        {
            File.AppendAllLines(Path.Combine(folder, "findings.tsv"), found.Select(f => $"{screenshot}\t{f.Kind}\t{f.Path}\t{f.Text}"));
            File.AppendAllLines(Path.Combine(folder, "views.tsv"), views.Select(v => $"{screenshot}\t{v}"));
        }

        return found;
    }

    /// <summary>The IDE's views a window shows: the user controls of TabStudio.App's views, by name.</summary>
    public static IReadOnlyList<string> ViewsIn(Visual root) =>
        [.. root.GetVisualDescendants().OfType<UserControl>()
            .Where(c => c.IsEffectivelyVisible && c.GetType().Namespace?.StartsWith("TabStudio.App.Views", StringComparison.Ordinal) == true)
            .Select(c => c.GetType().FullName!).Distinct().Order(StringComparer.Ordinal)];

    /// <summary>Fails with the findings, when there are any.</summary>
    public static void AssertNone(IReadOnlyList<AccessibilityFinding> found, string what)
    {
        Assert.True(found.Count == 0, $"{what} does not meet the accessibility rules (docs/design/README.md, \"Accessibility\"):\n"
            + string.Join("\n", found.Take(80).Select(f => $"{f.Kind}: {f.Path}: {f.Text}")));
    }

    private static void RenderFrame()
    {
        try
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        catch (AvaloniaEdit.Rendering.VisualLinesInvalidException)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    /// <summary>
    /// Lays the windows out again after the text size changed, as the next frames would: every window the app shows,
    /// since the text size is the app's, with their editors' lines; then the work layout posts (a bar folding its
    /// items), but not the other tests' work, which runs at a lower priority.
    /// </summary>
    private static void Settle(Window window)
    {
        var windows = TabStudio.App.Accessibility.AccessibilityFeatures.LiveWindows().Append(window).Distinct().ToList();
        for (var i = 0; i < 3; i++)
        {
            foreach (var shown in windows)
            {
                shown.UpdateLayout();
                foreach (var view in shown.GetVisualDescendants().OfType<AvaloniaEdit.Rendering.TextView>())
                {
                    try
                    {
                        view.EnsureVisualLines();
                    }
                    catch (InvalidOperationException)
                    {
                        // A text view that was never laid out has no lines to build.
                    }
                }
            }

            try
            {
                Dispatcher.UIThread.RunJobs(DispatcherPriority.Loaded);
            }
            catch (AvaloniaEdit.Rendering.VisualLinesInvalidException)
            {
                // An editor drew before its new lines were built: the next pass builds them and draws again.
            }

            // As the app does after its first layout at a new text size (AccessibilityFeatures).
            if (i == 0)
            {
                TabStudio.App.Accessibility.AccessibilityFeatures.RemeasureWindows();
            }
        }

        foreach (var shown in windows)
        {
            shown.UpdateLayout();
        }
    }
}
