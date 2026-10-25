using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using TabStudio.App.Editing;
using TabStudio.App.Services;

namespace TabStudio.App.Tests.Editing;

/// <summary>
/// TextMate colors a long file without tokenizing and building lines again on every frame of a scroll: once the
/// background tokenizer has colored the file, a scroll builds only the lines that come into view.
/// </summary>
public sealed class TextMateScrollTokenizingTests
{
    [AvaloniaFact]
    public async Task Scrolling_a_colored_file_builds_only_the_lines_that_come_into_view()
    {
        var editor = new TextEditor { Document = new TextDocument(LongSampleCode.Python()), ShowLineNumbers = true };
        var window = new Window { Width = 1000, Height = 700, Content = editor };
        window.Show();
        var installation = TextMateHighlighting.TryInstall(editor, ".py") ?? throw new InvalidOperationException("No Python grammar.");
        var textView = editor.TextArea.TextView;
        var takeover = TextMateScrollTokenizing.On(textView) ?? throw new InvalidOperationException("TextMate's scrolling was not taken over.");
        await UiTestHelpers.WaitUntilAsync(() => AllTokenized(installation, editor.Document), TimeSpan.FromSeconds(60), "the file to be tokenized");
        Render();

        var counter = new LineCounter();
        textView.LineTransformers.Add(counter);
        var scroll = editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        var ours = LinesBuiltWhileScrolling(scroll, counter, from: 0);
        Assert.Equal(0, takeover.ViewTokenizations);

        // AvaloniaEdit.TextMate's own handling tokenizes the first visible line again on each frame, and the editor builds it again.
        takeover.Uninstall();
        Assert.Null(TextMateScrollTokenizing.On(textView));
        var theirs = LinesBuiltWhileScrolling(scroll, counter, from: 3000);

        // 60 frames of 15 dip bring about 45 lines into view.
        Assert.InRange(ours, 20, 80);
        Assert.True(theirs >= ours + 40, $"{ours} lines built with the takeover, {theirs} without");
        installation.Dispose();
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_line_scrolled_into_view_before_it_was_tokenized_is_colored_at_once()
    {
        var editor = new TextEditor { Document = new TextDocument(LongSampleCode.Python()) };
        var window = new Window { Width = 1000, Height = 700, Content = editor };
        window.Show();
        var installation = TextMateHighlighting.TryInstall(editor, ".py") ?? throw new InvalidOperationException("No Python grammar.");
        var textView = editor.TextArea.TextView;
        var takeover = TextMateScrollTokenizing.On(textView) ?? throw new InvalidOperationException("TextMate's scrolling was not taken over.");
        await UiTestHelpers.WaitUntilAsync(() => AllTokenized(installation, editor.Document), TimeSpan.FromSeconds(60), "the file to be tokenized");

        // An edit at the top makes the lines below it stale until the background tokenizer comes by again.
        foreach (var line in Enumerable.Range(0, editor.Document.LineCount))
        {
            installation.EditorModel.Get(line).IsInvalid = true;
        }

        var scroll = editor.GetVisualDescendants().OfType<ScrollViewer>().First();
        scroll.Offset = new Vector(0, 20000);
        Render();
        Render();

        Assert.True(takeover.ViewTokenizations > 0, "the scroll tokenized the lines in view");
        var first = textView.VisualLines[0].FirstDocumentLine.LineNumber - 1;
        Assert.False(installation.EditorModel.Get(first).IsInvalid, "the first line in view has its tokens");
        installation.Dispose();
        window.Close();
    }

    private static bool AllTokenized(TextMate.Installation installation, TextDocument document)
    {
        for (var line = 0; line < document.LineCount; line++)
        {
            if (installation.EditorModel.Get(line) is not { IsInvalid: false, Tokens: not null })
            {
                return false;
            }
        }

        return true;
    }

    private static int LinesBuiltWhileScrolling(ScrollViewer scroll, LineCounter counter, double from)
    {
        scroll.Offset = new Vector(0, from);
        Render();
        var before = counter.Count;
        for (var frame = 1; frame <= 60; frame++)
        {
            scroll.Offset = new Vector(0, from + (15 * frame));
            Render();
        }

        return counter.Count - before;
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Counts the visual lines the editor builds: it transforms every line it builds once.</summary>
    private sealed class LineCounter : IVisualLineTransformer
    {
        public int Count { get; private set; }

        public void Transform(ITextRunConstructionContext context, IList<VisualLineElement> elements) => Count++;
    }
}
