using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TabStudio.App.Editing;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>
/// There is one light bulb, at the caret line of the editor in use, and one Quick Actions menu. In 0.19 every open
/// document's hidden editor kept its bulb and menu in the window's overlay, so dozens piled up over the editor and the
/// Error List in an Avalonia.Samples file.
/// </summary>
public sealed class QuickActionsPopupTests
{
    [AvaloniaFact]
    public async Task Moving_the_caret_across_many_lines_with_fixes_shows_at_most_one_light_bulb_and_one_menu()
    {
        using var fixture = new WorkspaceFixture();
        var solution = await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var program = ActiveEditor(window);
        var editor = program.Editor;
        var lines = Enumerable.Range(1, editor.Document.LineCount)
            .Select(editor.Document.GetLineByNumber)
            .Where(line => editor.Document.GetText(line).Contains('(', StringComparison.Ordinal) && !editor.Document.GetText(line).TrimStart().StartsWith("//", StringComparison.Ordinal))
            .ToList();
        Assert.True(lines.Count >= 8, "the sample has lines with code to act on");

        // Other tests share the process, and with it the count of open menus.
        var baseline = CodeEditorView.OpenQuickActionsMenus;
        var withBulb = new List<int>();
        var menus = 0;
        foreach (var (line, index) in lines.Select((line, index) => (line, index)))
        {
            var offset = BeforeParenthesis(editor, line);
            editor.CaretOffset = offset;
            AssertAtMostOne(window, baseline);
            if (await WaitForAsync(() => program.IsLightBulbVisible, TimeSpan.FromSeconds(8)))
            {
                withBulb.Add(offset);
            }

            AssertAtMostOne(window, baseline);
            if (index % 2 == 0 && program.IsLightBulbVisible)
            {
                // Ctrl+. twice: the second replaces the first menu rather than opening another.
                fixture.Workspace.ActiveDocument!.RequestAction(EditorAction.QuickActions);
                await WaitForAsync(() => program.IsQuickActionsMenuOpen, TimeSpan.FromSeconds(8));
                fixture.Workspace.ActiveDocument!.RequestAction(EditorAction.QuickActions);
                await WaitForAsync(() => program.IsQuickActionsMenuOpen, TimeSpan.FromSeconds(8));
                if (program.IsQuickActionsMenuOpen)
                {
                    menus++;
                }

                AssertAtMostOne(window, baseline);
            }
        }

        Assert.True(withBulb.Count >= 3, $"a light bulb showed on {withBulb.Count} of {lines.Count} lines");
        Assert.True(menus >= 1, "the Quick Actions menu opened");

        // Moving the caret closes the menu and the bulb; Escape closes them too.
        editor.CaretOffset = withBulb[0];
        Dispatcher.UIThread.RunJobs();
        Assert.False(program.IsQuickActionsMenuOpen);
        Assert.Equal(baseline, CodeEditorView.OpenQuickActionsMenus);
        await UiTestHelpers.WaitUntilAsync(() => program.IsLightBulbVisible, TimeSpan.FromSeconds(15), "the light bulb again");
        editor.TextArea.Focus();
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.False(program.IsLightBulbVisible);

        // Typing closes them.
        editor.CaretOffset = withBulb[1];
        await UiTestHelpers.WaitUntilAsync(() => program.IsLightBulbVisible, TimeSpan.FromSeconds(15), "the light bulb on another line");
        fixture.Workspace.ActiveDocument!.RequestAction(EditorAction.QuickActions);
        await UiTestHelpers.WaitUntilAsync(() => program.IsQuickActionsMenuOpen, TimeSpan.FromSeconds(15), "the menu");
        editor.TextArea.Focus();
        Type(editor, " ");
        Dispatcher.UIThread.RunJobs();
        Assert.False(program.IsQuickActionsMenuOpen);
        Assert.Equal(baseline, CodeEditorView.OpenQuickActionsMenus);
        AssertAtMostOne(window, baseline);

        // Another document: the hidden editor's bulb and menu close, and only the visible editor can show one.
        editor.CaretOffset = withBulb[2];
        await UiTestHelpers.WaitUntilAsync(() => program.IsLightBulbVisible, TimeSpan.FromSeconds(15), "the light bulb before switching documents");
        fixture.Workspace.ActiveDocument!.RequestAction(EditorAction.QuickActions);
        await WaitForAsync(() => program.IsQuickActionsMenuOpen, TimeSpan.FromSeconds(8));
        await fixture.Workspace.OpenDocumentAsync(Path.Combine(Path.GetDirectoryName(solution)!, "src", "Greetings", "Greeter.cs"));
        var greeter = ActiveEditor(window);
        Assert.NotSame(program, greeter);
        Assert.False(program.IsLightBulbVisible);
        Assert.False(program.IsQuickActionsMenuOpen);
        AssertAtMostOne(window, baseline);

        greeter.Editor.CaretOffset = OffsetOf(greeter.Editor, "public Greeter(") + "public Gree".Length;
        await UiTestHelpers.WaitUntilAsync(() => greeter.IsLightBulbVisible, TimeSpan.FromSeconds(15), "the light bulb in the second document");
        Assert.False(program.IsLightBulbVisible);
        AssertAtMostOne(window, baseline);
        window.Close();
    }

    /// <summary>The caret position just before a line's first parenthesis, where Roslyn offers the most actions.</summary>
    private static int BeforeParenthesis(AvaloniaEdit.TextEditor editor, AvaloniaEdit.Document.DocumentLine line) =>
        line.Offset + Math.Max(0, editor.Document.GetText(line).IndexOf('(', StringComparison.Ordinal) - 1);

    /// <summary>At most one light bulb and one Quick Actions menu are open, across every document's editor.</summary>
    private static void AssertAtMostOne(Window window, int menusBefore)
    {
        Dispatcher.UIThread.RunJobs();
        var openBulbs = window.GetLogicalDescendants().OfType<Popup>().Count(p => p.IsOpen && p.Child is LightBulbView);
        Assert.InRange(openBulbs, 0, 1);
        Assert.InRange(CodeEditorView.OpenQuickActionsMenus - menusBefore, 0, 1);
        Assert.InRange(window.GetLogicalDescendants().OfType<CodeEditorView>().Count(v => v.IsQuickActionsMenuOpen), 0, 1);
    }

    /// <summary>Processes UI work until a condition holds or the time is up; whether it held.</summary>
    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(25);
        }

        Dispatcher.UIThread.RunJobs();
        return condition();
    }

    private static Window Show(WorkspaceFixture fixture)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        return window;
    }
}
