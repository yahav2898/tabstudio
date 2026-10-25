using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// Puts AvaloniaEdit's own parts back into an editor in place of the faster ones, as the editor was before them: the
/// text layer, the line numbers, the folding margin and TextMate's tokenizing on every scroll. For comparing pictures
/// and frame times in the same run. There is no way back: use it last on an editor.
/// </summary>
internal static class AvaloniaEditParts
{
    /// <summary>Puts AvaloniaEdit's parts back, and says which.</summary>
    /// <param name="view">The editor.</param>
    /// <returns>The parts put back, for a report.</returns>
    public static string PutBack(CodeEditorView view)
    {
        var editor = view.Editor;
        var textView = editor.TextArea.TextView;
        var parts = new List<string>();
        if (view.TextLayer is { } layer)
        {
            layer.Uninstall();
            parts.Add("text layer");
        }

        var margins = editor.TextArea.LeftMargins;
        if (margins.OfType<CachedLineNumberMargin>().FirstOrDefault() is { } numbers)
        {
            var stock = new LineNumberMargin();
            stock.Bind(TextElement.ForegroundProperty, editor.GetBindingObservable(TextEditor.LineNumbersForegroundProperty));
            margins[margins.IndexOf(numbers)] = stock;
            parts.Add("line numbers");
        }

        if (margins.OfType<DrawnFoldingMargin>().FirstOrDefault() is { } folding && view.Folding is { } manager)
        {
            margins[margins.IndexOf(folding)] = new FoldingMargin { FoldingManager = manager };

            // AvaloniaEdit's margin takes its line colors when the editor's folding brushes change, as when folding starts.
            foreach (var brush in new[] { FoldingMargin.FoldingMarkerBrushProperty, FoldingMargin.SelectedFoldingMarkerBrushProperty })
            {
                var color = editor.GetValue(brush);
                editor.SetValue(brush, Brushes.Transparent);
                editor.SetValue(brush, color);
            }

            parts.Add("folding margin");
        }

        if (TextMateScrollTokenizing.On(textView) is { } tokenizing)
        {
            tokenizing.Uninstall();
            parts.Add("TextMate's tokenizing on every scroll");
        }

        textView.Redraw();
        return string.Join(", ", parts);
    }

    /// <summary>Waits until the language service's colors, squiggles and hints have not changed for a while.</summary>
    /// <param name="document">The document.</param>
    /// <param name="quiet">How long nothing may change.</param>
    /// <param name="timeout">How long to wait at most.</param>
    public static async Task UntilSettledAsync(DocumentViewModel document, TimeSpan quiet, TimeSpan timeout)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        object[] Snapshot() => [document.Classifications, document.Diagnostics, document.InlayHints, document.FoldingRanges];
        var last = Snapshot();
        var since = clock.Elapsed;
        while (clock.Elapsed - since < quiet && clock.Elapsed < timeout)
        {
            await Task.Delay(100);
            var now = Snapshot();
            if (!now.Zip(last).All(pair => ReferenceEquals(pair.First, pair.Second)))
            {
                (last, since) = (now, clock.Elapsed);
            }
        }
    }
}
