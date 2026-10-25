using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.TextMate;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.LanguageServers.Hosting;
using TabStudio.LiveTesting.Results;
using TabStudio.Testing;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// Scrolls long files in the real editor, frame by frame, as a drag and as a fling move them, with everything a
/// tablet user has on: coloring, squiggles, folding, the breakpoint margin with breakpoints and the Live Unit Testing
/// margin with markers. Writes the frame statistics, where the time goes and how a finger scrolls to
/// <c>editor-scrolling-csharp.md</c> and <c>editor-scrolling-python.md</c> beside the screenshots.
/// </summary>
/// <remarks>
/// The benchmarks run one at a time after the parallel tests (the Extensions group), so nothing else competes for the
/// processor while frames are timed.
/// </remarks>
[Collection(ExtensionsTestGroup.Name)]
public sealed class EditorScrollingBenchmarkTests
{
    [AvaloniaFact]
    public async Task Scrolling_a_long_CSharp_file()
    {
        using var fixture = new ExtensionsFixture();
        using (var package = new MemoryStream(CSharpConfigurationExtension.Build()))
        {
            await fixture.Service.InstallFileAsync(package, CSharpConfigurationExtension.FileName);
        }

        var solution = await new SampleLibrary(fixture.Paths).EnsureExtractedAsync();
        var path = Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings", "Operations.cs");
        await File.WriteAllTextAsync(path, CodeCorpus.CSharp());
        await fixture.Workspace.LoadAsync(solution);
        var window = fixture.Show(bottomPanel: false);
        var document = await fixture.Workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The file did not open.");
        var view = ActiveEditor(window);

        await UiTestHelpers.WaitUntilAsync(
            () => document.Diagnostics.Any(d => d.Id == "CS0219") && document.Classifications.Count > 5000 && view.Folding?.AllFoldings.Any() == true,
            TimeSpan.FromMinutes(2),
            "Roslyn's colors and squiggles and the folds");
        foreach (var line in new[] { 60, 230, 410, 700, 950, 1300, 1720, 2100 })
        {
            fixture.Workspace.Breakpoints.Toggle(path, line);
        }

        document.ShowLiveTestCoverage(Coverage(path, document.Document.Text));
        Dispatcher.UIThread.RunJobs();

        using var bench = new EditorScrollBench(window, view);
        var report = new StringBuilder();
        report.AppendLine("# Scrolling a long C# file").AppendLine();
        report.Append(Setup(view, document));
        report.Append(MeasureAll(bench, view));

        bench.ScrollViewer.Offset = new Avalonia.Vector(0, bench.MaxOffset / 2);
        bench.Settle();
        UiTestHelpers.SaveScreenshot(window, "perf-01-csharp-scrolled");

        var touch = TouchScrollProbe.Measure(window, view, bench);
        report.AppendLine("## Scrolling with a finger").AppendLine();
        report.AppendLine(ScrollRun.Header).AppendLine(touch.Drag.Row()).AppendLine();
        report.Append(touch.Report()).AppendLine();
        Write("editor-scrolling-csharp.md", report.ToString());

        Assert.True(touch.GlideDistance > 0, "the editor glides after a flick");
        window.Close();
    }

    [RealServerAvaloniaFact("pyright-langserver")]
    public async Task Scrolling_a_long_Python_file()
    {
        using var temp = new TempDirectory();
        await using var services = AppServices.Create(new AppPaths(temp.Resolve("app")));
        var main = services.GetRequiredService<MainViewModel>();
        var folder = temp.Resolve("python");
        var path = temp.Write("python/shop.py", CodeCorpus.Python());
        await main.OpenWorkspaceAsync(folder);
        var workspace = main.Workspace!;
        workspace.IsBottomPanelVisible = false;
        var window = new MainWindow { DataContext = main, Width = 1280, Height = 800 };
        window.Show();
        var document = await workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The file did not open.");
        var view = ActiveEditor(window);

        await UiTestHelpers.WaitUntilAsync(
            () => document.LanguageServer?.ServerState is LanguageServerState.Running or LanguageServerState.Failed,
            TimeSpan.FromSeconds(90),
            "Pyright to start");
        await UiTestHelpers.WaitUntilAsync(
            () => document.Diagnostics.Count > 0 && document.FoldingRanges.Count > 0 && view.Folding?.AllFoldings.Any() == true,
            TimeSpan.FromSeconds(90),
            "Pyright's squiggles and folds");
        foreach (var line in new[] { 50, 260, 520, 900, 1400, 1900 })
        {
            workspace.Breakpoints.Toggle(path, line);
        }

        Dispatcher.UIThread.RunJobs();
        using var bench = new EditorScrollBench(window, view);
        var report = new StringBuilder();
        report.AppendLine("# Scrolling a long Python file").AppendLine();
        report.Append(Setup(view, document));
        report.Append(MeasureAll(bench, view));

        bench.ScrollViewer.Offset = new Avalonia.Vector(0, bench.MaxOffset / 3);
        bench.Settle();
        UiTestHelpers.SaveScreenshot(window, "perf-02-python-scrolled");
        Write("editor-scrolling-python.md", report.ToString());
        window.Close();
        main.Workspace?.Dispose();
    }

    /// <summary>The main runs, the runs with one part of the editor taken away at a time, and the layout profile.</summary>
    private static string MeasureAll(EditorScrollBench bench, CodeEditorView view)
    {
        var max = bench.MaxOffset;
        var drag = EditorScrollBench.Drag(0, Math.Min(max, 4500), 15);
        var flings = EditorScrollBench.Flings(Math.Min(max, 4500), max, 5000, 4, EditorScrollBench.Frame60);
        var fast = EditorScrollBench.Flings(0, max, 8000, 2, EditorScrollBench.Frame60);

        // Once through everything first, so the timed runs measure warm code rather than the JIT.
        bench.Run("warm-up", [.. drag, .. flings, .. fast]);
        bench.ScrollViewer.Offset = default;
        bench.Settle();

        var runs = new List<ScrollRun>
        {
            bench.Run("drag, 15 dip per frame (900 dip/s at 60 Hz)", drag),
            bench.Run("4 flings at 5000 dip/s", flings),
            bench.Run("2 flings at 8000 dip/s", fast),
        };

        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"Avalonia frame metrics: {(bench.Probe.HasAvaloniaMetrics ? "on" : "off (times are stopwatch times)")}.").AppendLine();
        text.AppendLine("## Frames").AppendLine();
        text.AppendLine(ScrollRun.Header);
        foreach (var run in runs)
        {
            text.AppendLine(run.Row());
        }

        text.AppendLine();
        text.AppendLine("## Without one part of the editor at a time").AppendLine();
        text.AppendLine("The same drag and two 5000 dip/s flings, with one part of the editor taken out.").AppendLine();
        text.AppendLine(ScrollRun.Header);
        var sample = drag.Take(150).Concat(EditorScrollBench.Flings(drag[Math.Min(149, drag.Count - 1)], max, 5000, 2, EditorScrollBench.Frame60)).ToList();
        text.AppendLine(bench.Run("everything", sample).Row());
        var editor = view.Editor;
        var textView = editor.TextArea.TextView;
        var parts = new List<(string Name, Func<IDisposable> Remove)>
        {
            ("without line numbers", () => WithoutMargins(editor.TextArea, m => m is LineNumberMargin || DottedLineMargin.IsDottedLineMargin(m))),
            ("without the folding margin", () => WithoutMargins(editor.TextArea, m => m is FoldingMargin)),
            ("without the breakpoint margin", () => WithoutMargins(editor.TextArea, m => m is BreakpointMargin)),
            ("without the Live Unit Testing margin", () => WithoutMargins(editor.TextArea, m => m is LiveTestingMargin)),
            ("without coloring", () => WithoutTransformers(textView, t => t is ClassificationColorizer or TextMateColoringTransformer)),
            ("without squiggles", () => WithoutRenderers(textView, r => r is DiagnosticRenderer)),
            ("without the floating touch bar", () => WithoutTouchBar(view)),
        };
        foreach (var (name, remove) in parts)
        {
            using (remove())
            {
                bench.Settle();
                text.AppendLine(bench.Run(name, sample).Row());
            }

            bench.Settle();
        }

        using (WithoutMargins(editor.TextArea, m => m is not null))
        using (WithoutTransformers(textView, t => t is ClassificationColorizer or TextMateColoringTransformer))
        using (WithoutRenderers(textView, r => r is DiagnosticRenderer))
        using (WithoutTouchBar(view))
        {
            bench.Settle();
            text.AppendLine(bench.Run("bare editor: no margins, coloring, squiggles or touch bar", sample).Row());
        }

        bench.Settle();
        text.AppendLine();
        text.AppendLine("## Where layout time goes").AppendLine();
        text.AppendLine("Exclusive time of Avalonia's layout and style activities by control type, while dragging (profiling slows everything down).").AppendLine();
        text.Append(bench.ProfileLines(drag.Take(200))).AppendLine();
        return text.ToString();
    }

    private static string Setup(CodeEditorView view, DocumentViewModel document)
    {
        var editor = view.Editor;
        var textView = editor.TextArea.TextView;
        var text = new StringBuilder();
        var culture = CultureInfo.InvariantCulture;
        text.AppendLine(culture, $"- {editor.Document.LineCount} lines; {textView.VisualLines.Count} visible lines; viewport {textView.Bounds.Width:0} × {textView.Bounds.Height:0} dip.");
        text.AppendLine(culture, $"- {document.Classifications.Count} classified spans, {document.Diagnostics.Count} diagnostics, {view.Folding?.AllFoldings.Count() ?? 0} folds, {document.Breakpoints.Count} breakpoints, {document.LiveTestMarkers?.Count ?? 0} Live Unit Testing markers.");
        text.AppendLine(culture, $"- Margins: {string.Join(", ", editor.TextArea.LeftMargins.Select(m => m.GetType().Name))}.");
        text.AppendLine(culture, $"- Line transformers: {string.Join(", ", textView.LineTransformers.Select(t => t.GetType().Name))}.");
        text.AppendLine(culture, $"- Background renderers: {string.Join(", ", textView.BackgroundRenderers.Select(r => r.GetType().Name))}.");
        text.AppendLine(culture, $"- Element generators: {string.Join(", ", textView.ElementGenerators.Select(g => g.GetType().Name))}.");
        text.AppendLine(culture, $"- Gesture recognizers: {string.Join(", ", Recognizers(editor))}.");
        text.AppendLine(culture, $"- {Environment.ProcessorCount} processors, {Environment.OSVersion}, .NET {Environment.Version}.");
        text.AppendLine();
        return text.ToString();
    }

    private static IEnumerable<string> Recognizers(AvaloniaEdit.TextEditor editor) =>
        Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(editor)
            .OfType<Avalonia.Input.InputElement>()
            .SelectMany(e => e.GestureRecognizers.Select(r => $"{r.GetType().Name} on {e.GetType().Name}"))
            .Distinct();

    /// <summary>Live Unit Testing results for every line with code: mostly covered, some failing, some not covered.</summary>
    private static FileCoverage Coverage(string path, string text)
    {
        var source = SourceText.From(text);
        var lines = new List<LineCoverage>();
        foreach (var line in source.Lines)
        {
            var content = line.ToString().Trim();
            if (content.Length == 0 || content.StartsWith("//", StringComparison.Ordinal) || content is "{" or "}" || content.StartsWith('#'))
            {
                continue;
            }

            var number = line.LineNumber + 1;
            var state = (number % 17) switch
            {
                0 => LineCoverageState.Failing,
                3 or 9 => LineCoverageState.NotCovered,
                _ => LineCoverageState.Covered,
            };
            lines.Add(new LineCoverage(number, state, IsStale: number % 41 == 0, []));
        }

        return new FileCoverage(path, source, lines);
    }

    private static IDisposable WithoutMargins(TextArea area, Func<Control, bool> which)
    {
        var removed = area.LeftMargins.Select((margin, index) => (margin, index)).Where(m => which(m.margin)).ToList();
        foreach (var (margin, _) in removed)
        {
            area.LeftMargins.Remove(margin);
        }

        return new Restore(() =>
        {
            foreach (var (margin, index) in removed)
            {
                area.LeftMargins.Insert(Math.Min(index, area.LeftMargins.Count), margin);
            }
        });
    }

    private static IDisposable WithoutTransformers(TextView textView, Func<IVisualLineTransformer, bool> which)
    {
        var removed = textView.LineTransformers.Select((transformer, index) => (transformer, index)).Where(t => which(t.transformer)).ToList();
        foreach (var (transformer, _) in removed)
        {
            textView.LineTransformers.Remove(transformer);
        }

        textView.Redraw();
        return new Restore(() =>
        {
            foreach (var (transformer, index) in removed)
            {
                textView.LineTransformers.Insert(Math.Min(index, textView.LineTransformers.Count), transformer);
            }

            textView.Redraw();
        });
    }

    private static IDisposable WithoutRenderers(TextView textView, Func<IBackgroundRenderer, bool> which)
    {
        var removed = textView.BackgroundRenderers.Where(which).ToList();
        foreach (var renderer in removed)
        {
            textView.BackgroundRenderers.Remove(renderer);
        }

        return new Restore(() =>
        {
            foreach (var renderer in removed)
            {
                textView.BackgroundRenderers.Add(renderer);
            }
        });
    }

    private static IDisposable WithoutTouchBar(CodeEditorView view)
    {
        var workspace = Avalonia.VisualTree.VisualExtensions.GetVisualAncestors(view).OfType<Control>().Select(c => c.DataContext).OfType<WorkspaceViewModel>().FirstOrDefault();
        if (workspace is null || workspace.IsTouchBarCollapsed)
        {
            return new Restore(static () => { });
        }

        workspace.IsTouchBarCollapsed = true;
        return new Restore(() => workspace.IsTouchBarCollapsed = false);
    }

    private static void Write(string name, string text)
    {
        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), text);
    }

    private sealed class Restore(Action action) : IDisposable
    {
        public void Dispose() => action();
    }
}
