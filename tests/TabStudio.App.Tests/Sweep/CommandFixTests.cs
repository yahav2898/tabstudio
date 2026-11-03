using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using TabStudio.App.Controls;
using TabStudio.App.Tests.Theming;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Sweep;

/// <summary>
/// The problems the sweep of every command found (<see cref="EveryCommandTests"/>), one test each, written to fail
/// before its fix: a command that did nothing or the wrong thing, a control a finger could not reach, and the caret
/// under the on-screen keyboard.
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class CommandFixTests
{
    [AvaloniaFact]
    public async Task View_Solution_Explorer_and_Ctrl_Alt_L_show_it_instead_of_folding_the_side_bar()
    {
        using var sweep = new SweepHarness("fix-solution-explorer");
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        var menu = workspace.Commands.Commands.Single(c => c.Title == "Solution Explorer" && c.Path == "View");

        // From Git Changes: it hid the side bar, with Git Changes still in it.
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.GitChanges);
        menu.Command.Execute(menu.Parameter);
        Assert.Equal((SideBarPanel.SolutionExplorer, true), (workspace.SelectedSideBarPanel, workspace.IsSideBarVisible));

        // Again: it stays, as Visual Studio's does.
        menu.Command.Execute(menu.Parameter);
        Assert.Equal((SideBarPanel.SolutionExplorer, true), (workspace.SelectedSideBarPanel, workspace.IsSideBarVisible));

        // The key, from the editor, with the side bar folded away on Git Changes.
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.GitChanges);
        workspace.IsSideBarVisible = false;
        Editor(sweep).Editor.TextArea.Focus();
        sweep.PressKey(Key.L, KeyModifiers.Control | KeyModifiers.Alt);
        Assert.Equal((SideBarPanel.SolutionExplorer, true), (workspace.SelectedSideBarPanel, workspace.IsSideBarVisible));
        Assert.Equal("Ctrl+Alt+L", menu.Gesture);

        // Folding the side bar away is View > Side Bar.
        var toggle = workspace.Commands.Commands.Single(c => c.Title == "Side Bar" && c.Path == "View");
        toggle.Command.Execute(toggle.Parameter);
        Assert.False(workspace.IsSideBarVisible);
        await sweep.SettleAsync();
    }

    [AvaloniaFact]
    public async Task Sync_with_Active_Document_shows_the_file_in_Solution_Explorer_from_another_tool_window()
    {
        using var sweep = new SweepHarness("fix-sync");
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.TestExplorer);

        workspace.SyncWithActiveDocumentCommand.Execute(null);
        await sweep.SettleAsync();

        Assert.Equal(SideBarPanel.SolutionExplorer, workspace.SelectedSideBarPanel);
        Assert.Equal(sweep.ProgramPath, workspace.Explorer.SelectedItem?.Node.FullPath);
    }

    [AvaloniaFact]
    public async Task Navigate_Backward_and_Forward_are_available_only_when_they_go_somewhere()
    {
        using var sweep = new SweepHarness("fix-navigation");
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        var greeter = Directory.EnumerateFiles(Path.GetDirectoryName(sweep.SolutionPath)!, "Greeter.cs", SearchOption.AllDirectories).First();
        var back = sweep.Window.GetVisualDescendants().OfType<AdaptiveBarItem>().Single(i => i.Label == "Navigate Backward");

        // One document shown: the toolbar's Back looked tappable and did nothing.
        Assert.False(workspace.NavigateBackCommand.CanExecute(null));
        Assert.False(workspace.NavigateForwardCommand.CanExecute(null));
        await sweep.SettleAsync();
        Assert.False(back.IsEffectivelyEnabled);

        await workspace.OpenDocumentAsync(greeter);
        Assert.True(workspace.NavigateBackCommand.CanExecute(null));
        await sweep.SettleAsync();
        Assert.True(back.IsEffectivelyEnabled);

        workspace.NavigateBackCommand.Execute(null);
        Assert.Equal(sweep.ProgramPath, workspace.ActiveDocument?.FilePath);
        Assert.True(workspace.NavigateForwardCommand.CanExecute(null));
        Assert.False(workspace.NavigateBackCommand.CanExecute(null));

        workspace.NavigateForwardCommand.Execute(null);
        Assert.Equal(greeter, workspace.ActiveDocument?.FilePath);

        // A document closed is no longer somewhere to go back to.
        workspace.ActiveDocument = workspace.Documents.Single(d => d.FilePath == sweep.ProgramPath);
        workspace.CloseDocumentCommand.Execute(null);
        await sweep.SettleAsync();
        Assert.False(workspace.NavigateBackCommand.CanExecute(null));
        Assert.False(workspace.NavigateForwardCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Select_All_Cut_Copy_and_Paste_are_in_the_Edit_menu_and_Select_All_in_the_context_menu()
    {
        using var sweep = new SweepHarness("fix-clipboard");
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        var editor = Editor(sweep).Editor;
        var text = editor.Document.Text;
        string[] titles = ["Cut", "Copy", "Paste", "Select All"];
        foreach (var title in titles)
        {
            Assert.Contains(workspace.Commands.Commands, c => c.Title == title && c.Path == "Edit");
        }

        // Without a keyboard, a long press was the only way to the clipboard, and nothing selected the whole file.
        Run(workspace, "Select All");
        Assert.Equal((0, text.Length), (editor.SelectionStart, editor.SelectionLength));
        Run(workspace, "Copy");
        await sweep.SettleAsync();
        editor.Select(0, 0);
        editor.CaretOffset = text.Length;
        Run(workspace, "Paste");
        await sweep.SettleAsync();
        Assert.Equal(text + text, editor.Document.Text);

        editor.Select(text.Length, text.Length);
        Run(workspace, "Cut");
        Assert.Equal(text, editor.Document.Text);

        // The context menu a long press opens has Select All too.
        editor.Select(0, 0);
        editor.RaiseEvent(new ContextRequestedEventArgs());
        sweep.Pump();
        var menu = Assert.IsType<ContextMenu>(editor.ContextMenu);
        var item = Assert.Single(menu.Items.OfType<MenuItem>(), i => i.Header as string == "Select All");
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        menu.Close();
        Assert.Equal((0, text.Length), (editor.SelectionStart, editor.SelectionLength));
    }

    [AvaloniaFact]
    public async Task Go_To_Line_moves_the_caret_from_Ctrl_G_and_from_the_caret_in_the_status_bar()
    {
        using var sweep = new SweepHarness("fix-go-to-line");
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        var editor = Editor(sweep).Editor;
        editor.TextArea.Focus();

        sweep.PressKey(Key.G, KeyModifiers.Control);
        await sweep.SettleAsync();
        Assert.True(workspace.Prompt.IsOpen);
        Assert.Equal("Go To Line", workspace.Prompt.Title);
        workspace.Prompt.Text = "30";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await sweep.SettleAsync(200);
        Assert.Equal(30, editor.TextArea.Caret.Line);
        Assert.Equal(30, workspace.ActiveDocument!.CaretLine);

        // The caret's place in the status bar was a button that did nothing; a tap now asks for the line.
        var caret = sweep.Window.GetVisualDescendants().OfType<AdaptiveBarItem>().Single(i => i.Label == workspace.CaretText);
        Assert.False(caret.IsLabel);
        caret.Command!.Execute(caret.CommandParameter);
        await sweep.SettleAsync();
        Assert.True(workspace.Prompt.IsOpen);
        workspace.Prompt.Text = "not a line";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await sweep.SettleAsync();
        Assert.Equal(30, editor.TextArea.Caret.Line);
        Assert.Contains("is not a line number", workspace.StatusText, StringComparison.Ordinal);

        // Past the end goes to the last line.
        workspace.GoToLineCommand.Execute(null);
        await sweep.SettleAsync();
        workspace.Prompt.Text = "100000";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await sweep.SettleAsync(200);
        Assert.Equal(editor.Document.LineCount, editor.TextArea.Caret.Line);
    }

    [AvaloniaFact]
    public async Task The_status_bar_labels_are_not_buttons_that_do_nothing()
    {
        using var sweep = new SweepHarness("fix-status-labels");
        sweep.Show(1280, 800);
        await sweep.OpenSampleAsync();
        await sweep.SettleAsync();
        var bar = sweep.Window.GetVisualDescendants().OfType<AdaptiveBar>().Single(b => b.Name == "StatusBar");
        var items = bar.Children.OfType<AdaptiveBarItem>().Where(i => i.IsVisible).ToList();
        var spaces = Assert.Single(items, i => i.Label == "Spaces: 4");

        // A tap or the keyboard's focus reached it, and nothing happened.
        Assert.True(spaces.IsLabel);
        Assert.False(spaces.IsHitTestVisible);
        Assert.False(spaces.Focusable);
        foreach (var item in items)
        {
            Assert.True(item.IsLabel || item.Command is not null || item.Flyout is not null, $"\"{item.Label}\" is a button with nothing to run.");
            Assert.Equal(!item.IsLabel, item.IsHitTestVisible);
        }
    }

    [AvaloniaFact]
    public async Task The_caret_stays_above_the_on_screen_keyboard()
    {
        using var sweep = new SweepHarness("fix-caret");
        sweep.Show(1280, 800);
        await sweep.OpenSampleAsync();
        var view = Editor(sweep);
        var editor = view.Editor;
        editor.Document.Insert(editor.Document.TextLength, string.Concat(Enumerable.Repeat("// another line\n", 150)));
        await sweep.SettleAsync();
        var text = editor.TextArea.TextView;
        text.EnsureVisualLines();

        // The caret on a line near the bottom of what shows, as a tap there puts it, with the keyboard's focus.
        var line = text.VisualLines[^3].FirstDocumentLine.LineNumber;
        editor.TextArea.Caret.Line = line;
        editor.TextArea.Focus();
        await sweep.SettleAsync();
        Assert.True(InView(view), "The caret should start in view.");

        // The on-screen keyboard opens: Android makes the window shorter.
        sweep.Resize(1280, 440);
        await sweep.SettleAsync(200);
        Assert.Equal(line, editor.TextArea.Caret.Line);
        Assert.True(InView(view), $"Line {line}, the caret's, is under the on-screen keyboard.");

        // A caret the person scrolled away from stays where they left it.
        sweep.Resize(1280, 800);
        await sweep.SettleAsync();
        editor.ScrollToVerticalOffset(0);
        editor.TextArea.Caret.Line = editor.Document.LineCount;
        editor.ScrollToVerticalOffset(0);
        await sweep.SettleAsync();
        sweep.Resize(1280, 440);
        await sweep.SettleAsync(200);
        Assert.Equal(0, text.ScrollOffset.Y, 1);
    }

    [AvaloniaFact]
    public async Task A_confirmation_answers_Enter_and_Escape_and_keeps_keys_out_of_the_editor_behind_it()
    {
        using var sweep = new SweepHarness("fix-prompt");
        sweep.Show(1280, 800);
        var workspace = await sweep.OpenSampleAsync();
        var editor = Editor(sweep).Editor;
        var text = editor.Document.Text;
        editor.TextArea.Focus();

        // Escape: the dialog had no focus, so Escape and Enter went to the editor behind it.
        var answer = workspace.Prompt.ConfirmAsync("Delete Greeter.cs?", "It is deleted from the disk.", "Delete", destructive: true);
        await sweep.SettleAsync();
        sweep.PressKey(Key.Escape);
        await sweep.SettleAsync();
        Assert.True(answer.IsCompleted);
        Assert.False(await answer);
        Assert.Equal(text, editor.Document.Text);

        // Back in the editor, where the keyboard was.
        Assert.True(editor.TextArea.IsKeyboardFocusWithin);

        // Enter confirms; a key typed before it does not reach the editor.
        answer = workspace.Prompt.ConfirmAsync("Delete Greeter.cs?", "It is deleted from the disk.", "Delete", destructive: true);
        await sweep.SettleAsync();
        sweep.PressKey(Key.X);
        sweep.PressKey(Key.Enter);
        await sweep.SettleAsync();
        Assert.True(answer.IsCompleted);
        Assert.True(await answer);
        Assert.Equal(text, editor.Document.Text);

        // A prompt with a box still takes what is typed into it, and Escape cancels it.
        var name = workspace.Prompt.AskAsync("Rename", "The new name:", "Rename", "Greeter");
        await sweep.SettleAsync();
        sweep.PressKey(Key.Escape);
        await sweep.SettleAsync();
        Assert.Null(await name);
    }

    private static bool InView(CodeEditorView view)
    {
        var text = view.Editor.TextArea.TextView;
        var top = text.GetVisualTopByDocumentLine(view.Editor.TextArea.Caret.Line) - text.ScrollOffset.Y;
        return top >= -1 && top + text.DefaultLineHeight <= text.Bounds.Height + 1;
    }

    private static void Run(WorkspaceViewModel workspace, string title)
    {
        var entry = workspace.Commands.Commands.Single(c => c.Title == title && c.Path == "Edit");
        Assert.True(entry.Command.CanExecute(entry.Parameter), $"Edit > {title} is unavailable.");
        entry.Command.Execute(entry.Parameter);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static CodeEditorView Editor(SweepHarness sweep) =>
        sweep.Window.GetVisualDescendants().OfType<CodeEditorView>().First(e => e.IsEffectivelyVisible);
}
