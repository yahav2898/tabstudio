using System.Globalization;
using System.Reflection;
using System.Text;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// What one keystroke costs in a 2,500-line C# file colored by Roslyn, and which part of the editor costs it: the
/// keystroke is typed with each listener of the document and the editor taken out in turn. A keystroke that allocates
/// the size of the file feeds the garbage collector a large object every time a key is pressed, and on a tablet that
/// becomes pauses while typing. Writes <c>smoothness-keystroke.md</c>.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class KeystrokeSmoothnessTests
{
    private const int Keystrokes = 100;

    private static readonly string[] DocumentEvents = ["Changing", "Changed", "TextChanged", "TextLengthChanged", "LineCountChanged", "UpdateStarted", "UpdateFinished"];

    [AvaloniaFact]
    public async Task A_keystroke_in_a_long_file_costs_little_and_nothing_the_size_of_the_file()
    {
        if (Smoothness.IsLightPass)
        {
            return;
        }

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
        var editor = view.Editor;
        editor.CaretOffset = editor.Document.TextLength / 2;
        var textBytes = editor.Document.TextLength * 2L;

        long PerKeystroke(string key = "x")
        {
            Dispatcher.UIThread.RunJobs();
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < Keystrokes; i++)
            {
                editor.TextArea.PerformTextInput(key);
            }

            return (GC.GetAllocatedBytesForCurrentThread() - before) / Keystrokes;
        }

        PerKeystroke();
        var whole = PerKeystroke();

        // A space never starts a completion list, so every one is a keystroke with the list closed.
        var space = PerKeystroke(" ");

        // The same space with the whole text sent to Roslyn as a new string, as every such keystroke did before 0.21.11.
        var language = Assert.IsType<LocalLanguageService>(fixture.Workspace.LanguageService);
        Dispatcher.UIThread.RunJobs();
        var oldBefore = GC.GetAllocatedBytesForCurrentThread();
        var oldClock = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < Keystrokes; i++)
        {
            editor.TextArea.PerformTextInput(" ");
            language.UpdateText(path, editor.Document.Text);
        }

        oldClock.Stop();
        var oldSpace = (GC.GetAllocatedBytesForCurrentThread() - oldBefore) / Keystrokes;
        document.SyncText();
        var newClock = System.Diagnostics.Stopwatch.StartNew();
        var newSpace = PerKeystroke(" ");
        newClock.Stop();

        // The same keystroke in a bare document of the same text: what AvaloniaEdit's document itself costs.
        var bare = new TextDocument(editor.Document.Text);
        var bareBefore = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Keystrokes; i++)
        {
            bare.Insert(bare.TextLength / 2, "x");
        }

        var bareCost = (GC.GetAllocatedBytesForCurrentThread() - bareBefore) / Keystrokes;

        // Each listener of the document, the caret and the text area taken out in turn.
        var rows = new List<(string Listener, long Without)>();
        var sources = DocumentEvents.Select(e => ((object)editor.Document, e))
            .Append((editor.TextArea, "TextEntering"))
            .Append((editor.TextArea, "TextEntered"))
            .Append((editor.TextArea.Caret, "PositionChanged"))
            .Append((editor, "TextChanged"));
        foreach (var (owner, name) in sources)
        {
            if (EventField(owner, name) is not { } field || field.GetValue(owner) is not Delegate all)
            {
                continue;
            }

            foreach (var handler in all.GetInvocationList())
            {
                field.SetValue(owner, Delegate.Remove(all, handler));
                try
                {
                    rows.Add(($"{owner.GetType().Name}.{name}: {handler.Method.DeclaringType?.Name}.{handler.Method.Name}", PerKeystroke()));
                }
                finally
                {
                    field.SetValue(owner, all);
                }
            }
        }

        var culture = CultureInfo.InvariantCulture;
        var report = new StringBuilder("# A keystroke in a long C# file\n\n");
        report.AppendLine(culture, $"- {editor.Document.LineCount} lines, {editor.Document.TextLength} characters ({textBytes / 1024.0:0} KB as a string); {document.Classifications.Count} colored ranges.");
        report.AppendLine(culture, $"- A letter typed in the editor allocates {whole / 1024.0:0.0} KB, a space {space / 1024.0:0.0} KB; in a bare AvaloniaEdit document of the same text, a keystroke allocates {bareCost / 1024.0:0.0} KB.");
        report.AppendLine(culture, $"- A space with the whole text sent to Roslyn as a new string, as before 0.21.11: {oldSpace / 1024.0:0.0} KB and {oldClock.Elapsed.TotalMicroseconds / Keystrokes:0} µs; as now: {newSpace / 1024.0:0.0} KB and {newClock.Elapsed.TotalMicroseconds / Keystrokes:0} µs.\n");
        report.AppendLine("| Without | KB per keystroke | Saved |");
        report.AppendLine("| --- | --- | --- |");
        foreach (var (listener, without) in rows.OrderBy(r => r.Without))
        {
            report.AppendLine(culture, $"| {listener} | {without / 1024.0:0.0} | {(whole - without) / 1024.0:0.0} |");
        }

        Smoothness.Write("keystroke", report.ToString());

        // What the language service has is the editor's text, however the edits reached it.
        document.SyncText();
        Assert.Equal(editor.Document.Text, language.Host.GetCurrentText(path));
        window.Close();
    }

    /// <summary>The field behind an event declared as a field, in the type or one of its bases.</summary>
    private static FieldInfo? EventField(object owner, string name)
    {
        for (var type = owner.GetType(); type is not null; type = type.BaseType)
        {
            if (type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly) is { } field && typeof(Delegate).IsAssignableFrom(field.FieldType))
            {
                return field;
            }
        }

        return null;
    }
}
