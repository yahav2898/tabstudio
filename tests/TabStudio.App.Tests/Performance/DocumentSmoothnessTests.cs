using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Services;
using TabStudio.App.Tests.Extensions;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// A long session with many tabs: twenty long C# files opened, switched between, typed in with every tab open and
/// closed again; then the whole workspace opened and closed three times with the app's own services, as a day of work
/// does. Each switch must be quick, and what is closed must give its memory back. Writes
/// <c>smoothness-documents.md</c> and <c>smoothness-memory.md</c>.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class DocumentSmoothnessTests
{
    private const int Tabs = 20;

    [AvaloniaFact]
    public async Task Twenty_long_tabs_switch_quickly_and_closing_them_frees_them()
    {
        if (Smoothness.IsLightPass)
        {
            return;
        }

        using var fixture = new WorkspaceFixture();
        var solution = await new SampleLibrary(fixture.Paths).EnsureExtractedAsync();
        var files = await WriteLongFilesAsync(solution, Tabs);
        await fixture.Workspace.LoadAsync(solution);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        Smoothness.Settle(window);

        using var stalls = new UiStallMonitor(Dispatcher.UIThread, TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(4)) { Phase = "opening the tabs" };
        var opening = new List<UiCost>();
        var clock = Stopwatch.StartNew();
        foreach (var file in files)
        {
            var started = clock.Elapsed;
            var document = await fixture.Workspace.OpenDocumentAsync(file);
            Assert.NotNull(document);
            opening.Add(Smoothness.Measure(window, () => { }) with { Time = clock.Elapsed - started });
        }

        // The tab in view colored by Roslyn, as after a moment of work.
        stalls.Phase = "the tab's first analysis";
        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.ActiveDocument?.Classifications.Count > 1000, TimeSpan.FromMinutes(3), "the active tab's colors");
        var analysed = clock.Elapsed;

        stalls.Phase = "switching tabs";
        var switching = new List<UiCost>();
        for (var i = 0; i < Tabs * 2; i++)
        {
            var next = fixture.Workspace.Documents[i * 7 % Tabs];
            switching.Add(Smoothness.Measure(window, () => fixture.Workspace.ActiveDocument = next));
        }

        stalls.Phase = "typing with every tab open";
        var view = window.GetVisualDescendants().OfType<CodeEditorView>().Single(v => v.IsVisible);
        view.Editor.CaretOffset = view.Editor.Document.TextLength / 2;
        var typing = new List<UiCost>();
        for (var i = 0; i < 40; i++)
        {
            typing.Add(Smoothness.Measure(window, () => view.Editor.TextArea.PerformTextInput("x")));
        }

        stalls.Phase = "closing the tabs";
        var (documents, editors) = Track(window, fixture.Workspace);
        var closing = new List<UiCost>();
        foreach (var document in fixture.Workspace.Documents.Where(d => files.Contains(d.FilePath)).ToList())
        {
            document.Document.UndoStack.MarkAsOriginalFile();
            closing.Add(Smoothness.Measure(window, () => fixture.Workspace.CloseDocumentCommand.Execute(document)));
        }

        await Task.Delay(500);
        Smoothness.Settle(window);
        stalls.Dispose();
        var (aliveDocuments, aliveEditors) = Alive(documents, editors);

        var report = new StringBuilder("# Twenty long tabs\n\n");
        var culture = CultureInfo.InvariantCulture;
        report.AppendLine(culture, $"- The sample with {Tabs} more C# files of about 1,500 lines each, in the IDE's window at 1280 × 800.");
        report.AppendLine(culture, $"- The tab in view colored by Roslyn {analysed.TotalSeconds:0.0} s after the first was opened.\n");
        report.AppendLine("| Action | Cost |");
        report.AppendLine("| --- | --- |");
        report.AppendLine(culture, $"| Opening a tab, to its first frame | {Smoothness.Spread(opening)} |");
        report.AppendLine(culture, $"| Switching to another tab, to its first frame | {Smoothness.Spread(switching)} |");
        report.AppendLine(culture, $"| A keystroke with {Tabs + 1} tabs open | {Smoothness.Spread(typing)} |");
        report.AppendLine(culture, $"| Closing a tab | {Smoothness.Spread(closing)} |");
        report.AppendLine(culture, $"\nAfter closing them: {aliveDocuments} of {documents.Count} documents and {aliveEditors} of {editors.Count} editors still in memory.\n");
        report.AppendLine(culture, $"The UI thread: {stalls.Summary()}.\n");
        report.Append(stalls.Table(12));
        Smoothness.Write("documents", report.ToString());
        window.Close();
    }

    [AvaloniaFact]
    public async Task Opening_and_closing_a_workspace_all_day_gives_its_memory_back()
    {
        if (Smoothness.IsLightPass)
        {
            return;
        }

        using var temp = new TempDirectory();
        await using var services = AppServices.Create(new AppPaths(Path.Combine(temp.Path, "app")));
        var main = services.GetRequiredService<MainViewModel>();
        var solution = await services.GetRequiredService<SampleLibrary>().EnsureExtractedAsync();
        var files = await WriteLongFilesAsync(solution, 6);
        var window = new Window { Width = 1280, Height = 800, Content = new MainView { DataContext = main } };
        window.Show();
        Smoothness.Settle(window);

        var memory = new List<long>();
        var workspaces = new List<WeakReference>();
        var documents = new List<WeakReference>();
        var editors = new List<WeakReference>();
        memory.Add(Collect());
        for (var cycle = 0; cycle < 3; cycle++)
        {
            await OpenWorkAndCloseAsync(main, window, solution, files, workspaces, documents, editors);
            memory.Add(Collect());
        }

        var report = new StringBuilder("# Opening and closing a workspace three times\n\n");
        var culture = CultureInfo.InvariantCulture;
        report.AppendLine(culture, $"- The app's services, its main view at 1280 × 800, the sample with 6 long files: opened, its files opened and typed in, built, closed.\n");
        report.AppendLine(culture, $"- Managed memory after a full collection, before and after each cycle: {string.Join(" → ", memory.Select(m => (m / 1048576.0).ToString("0.0", culture) + " MB"))}.");
        report.AppendLine(culture, $"- Still in memory after the cycles: {workspaces.Count(w => w.IsAlive)} of {workspaces.Count} workspaces, {documents.Count(w => w.IsAlive)} of {documents.Count} documents, {editors.Count(w => w.IsAlive)} of {editors.Count} editors.");
        Smoothness.Write("memory", report.ToString());
        window.Close();
    }

    /// <summary>Opens the workspace, works in it and closes it; returns nothing that keeps it alive.</summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task OpenWorkAndCloseAsync(MainViewModel main, Window window, string solution, List<string> files, List<WeakReference> workspaces, List<WeakReference> documents, List<WeakReference> editors)
    {
        await main.OpenWorkspaceAsync(solution);
        var workspace = main.Workspace ?? throw new InvalidOperationException("The sample did not open.");
        foreach (var file in files)
        {
            await workspace.OpenDocumentAsync(file);
        }

        Smoothness.Settle(window);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Documents.All(d => d.Classifications.Count > 100), TimeSpan.FromMinutes(3), "the tabs' colors");
        var view = window.GetVisualDescendants().OfType<CodeEditorView>().Single(v => v.IsVisible);
        view.Editor.TextArea.PerformTextInput("// typed\n");
        await workspace.BuildCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.State != IdeState.Busy, TimeSpan.FromMinutes(3), "the build");
        var (tracked, shown) = Track(window, workspace);
        workspaces.Add(new WeakReference(workspace));
        documents.AddRange(tracked);
        editors.AddRange(shown);
        foreach (var document in workspace.Documents)
        {
            document.Document.UndoStack.MarkAsOriginalFile();
        }

        await workspace.CloseWorkspaceCommand.ExecuteAsync(null);
        Smoothness.Settle(window);
    }

    private static (List<WeakReference> Documents, List<WeakReference> Editors) Track(Window window, WorkspaceViewModel workspace) =>
        ([.. workspace.Documents.Select(d => new WeakReference(d))],
         [.. window.GetVisualDescendants().OfType<CodeEditorView>().Select(v => new WeakReference(v))]);

    private static (int Documents, int Editors) Alive(List<WeakReference> documents, List<WeakReference> editors)
    {
        Collect();
        return (documents.Count(d => d.IsAlive), editors.Count(e => e.IsAlive));
    }

    private static long Collect()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static async Task<List<string>> WriteLongFilesAsync(string solution, int count)
    {
        var folder = Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings");
        var files = new List<string>();
        for (var i = 0; i < count; i++)
        {
            var path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"Long{i}.cs"));
            var code = LongSampleCode.CSharp(1500).Replace("namespace Greetings.Operations;", string.Create(CultureInfo.InvariantCulture, $"namespace Greetings.Operations{i};"), StringComparison.Ordinal);
            await File.WriteAllTextAsync(path, code);
            files.Add(path);
        }

        return files;
    }
}
