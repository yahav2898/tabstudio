using System.Diagnostics;
using System.Globalization;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views.Scrolling;

/// <summary>
/// Types in a 2,500-line C# file colored by Roslyn, and measures what moving its colors through each edit costs:
/// before 0.20.2 every keystroke copied all the ranges (about 0.6 MB of large-object allocation per edit, and over
/// 1 GB for a Format Document's 2,000 edits). Writes the measurements to <c>editor-typing-csharp.md</c> beside the
/// screenshots.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class EditorTypingBenchmarkTests
{
    private const int Keystrokes = 200;
    private const int FormatEdits = 2_000;

    [AvaloniaFact]
    public async Task Typing_in_a_long_CSharp_file_does_not_copy_its_colors()
    {
        using var fixture = new ExtensionsFixture();
        var solution = await new SampleLibrary(fixture.Paths).EnsureExtractedAsync();
        var path = Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings", "Operations.cs");
        await File.WriteAllTextAsync(path, LongSampleCode.CSharp(2500));
        await fixture.Workspace.LoadAsync(solution);
        var window = fixture.Show(bottomPanel: false);
        var document = await fixture.Workspace.OpenDocumentAsync(path) ?? throw new InvalidOperationException("The file did not open.");
        var view = ActiveEditor(window);
        await UiTestHelpers.WaitUntilAsync(() => document.Classifications.Count > 5000, TimeSpan.FromMinutes(2), "Roslyn's colors");
        Dispatcher.UIThread.RunJobs();
        var colorizer = view.Colorizer ?? throw new InvalidOperationException("The C# editor has no Roslyn coloring.");
        var classifications = document.Classifications.Select(c => (c.Start, c.Length, c.Classification)).ToList();

        // The same edits for both: a keystroke at a time in the middle of the file, and a Format Document's small
        // whitespace changes spread over the whole file.
        var length = document.Document.TextLength;
        var typing = Enumerable.Range(0, Keystrokes).Select(i => new DocumentChangeEventArgs(length / 2 + i, string.Empty, "x")).ToArray();
        var format = Enumerable.Range(0, FormatEdits).Select(i => new DocumentChangeEventArgs((int)((long)length * i / FormatEdits), " ", "  ")).ToArray();

        var probe = new ClassificationColorizer();
        probe.SetClassifications(classifications);
        var ranges = probe.Count;
        var before = probe.Snapshot();
        var typingBefore = Measure(typing, change => before = TrackedRanges.Apply(before, change));
        before = probe.Snapshot();
        var formatBefore = Measure(format, change => before = TrackedRanges.Apply(before, change));
        var typingAfter = Measure(typing, probe.OnDocumentChanged);
        probe.SetClassifications(classifications);
        var formatAfter = Measure(format, probe.OnDocumentChanged);

        // And the whole keystroke in the real editor, with every other listener of the document.
        view.Editor.CaretOffset = length / 2;
        var keystroke = Measure(typing, _ => view.Editor.TextArea.PerformTextInput("x"));
        Dispatcher.UIThread.RunJobs();
        Assert.True(colorizer.Count > 5000, "the editor keeps its colors while typing");

        var culture = CultureInfo.InvariantCulture;
        var report = new StringBuilder();
        report.AppendLine("# Typing in a long C# file").AppendLine();
        report.AppendLine(culture, $"- {document.Document.LineCount} lines, {ranges} colored ranges from Roslyn.");
        report.AppendLine(culture, $"- {Keystrokes} keystrokes in the middle of the file; a Format Document of {FormatEdits} edits spread over it.").AppendLine();
        report.AppendLine("| Moving the colors through | Before (copy per edit) | After (in place) |");
        report.AppendLine("| --- | --- | --- |");
        report.AppendLine(culture, $"| A keystroke | {typingBefore.PerEdit} | {typingAfter.PerEdit} |");
        report.AppendLine(culture, $"| Format Document, in all | {formatBefore.InAll} | {formatAfter.InAll} |").AppendLine();
        report.AppendLine(culture, $"A whole keystroke in the editor, every listener included: {keystroke.PerEdit}.");
        Write("editor-typing-csharp.md", report.ToString());

        Assert.True(typingBefore.Bytes / Keystrokes > 100_000, $"the file has enough colors to measure: {typingBefore.PerEdit}");
        Assert.True(typingAfter.Bytes / Keystrokes < 1024, $"a keystroke allocates nothing to move the colors: {typingAfter.PerEdit}");
        Assert.True(formatAfter.Bytes < 1024 * 1024, $"a Format Document allocates nothing to move the colors: {formatAfter.InAll}");
        window.Close();
    }

    private static Result Measure(DocumentChangeEventArgs[] edits, Action<DocumentChangeEventArgs> apply)
    {
        var bytes = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        foreach (var edit in edits)
        {
            apply(edit);
        }

        clock.Stop();
        return new Result(GC.GetAllocatedBytesForCurrentThread() - bytes, clock.Elapsed, edits.Length);
    }

    private static void Write(string name, string text)
    {
        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_SCREENSHOTS") ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, name), text);
    }

    private sealed record Result(long Bytes, TimeSpan Time, int Edits)
    {
        public string PerEdit => string.Create(CultureInfo.InvariantCulture, $"{Bytes / (double)Edits / 1024:0.0} KB and {Time.TotalMicroseconds / Edits:0} µs per edit");

        public string InAll => string.Create(CultureInfo.InvariantCulture, $"{Bytes / 1048576.0:0.0} MB and {Time.TotalMilliseconds:0} ms");
    }
}
