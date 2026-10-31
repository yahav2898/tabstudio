using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.Views;
using TabStudio.Compilation.Features;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Views;

/// <summary>IntelliSense through the real editor: typing, keys and pointer, as the user does it.</summary>
public sealed class IntelliSenseEditorTests
{
    [AvaloniaFact]
    public async Task Typing_a_dot_opens_the_completion_list_that_filters_and_commits_with_Tab()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var editor = view.Editor;
        var completion = view.Completion!;

        StartLineAfter(editor, "Console.WriteLine();");
        Type(editor, "Console.");
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen, because: "the completion list");
        Assert.Contains(completion.Items, i => i.Entry.DisplayText == "WriteLine" && i.Kind == SymbolGlyph.Method);
        Assert.DoesNotContain(completion.Items, i => i.Entry.DisplayText == "while");

        Type(editor, "WriL");
        Assert.Equal("WriteLine", completion.SelectedItem?.Entry.DisplayText);
        Assert.False(completion.IsSoftSelection);
        Assert.Contains(completion.SelectedItem!.Runs, r => r.IsBold && r.Text == "Wri");
        Assert.True(editor.TextArea.IsKeyboardFocusWithin);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        await UiTestHelpers.WaitUntilAsync(() => CaretLine(editor) == "Console.WriteLine", because: "the committed item");
        Assert.False(completion.IsOpen);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_commit_character_commits_the_item_and_opens_parameter_info()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var editor = view.Editor;

        StartLineAfter(editor, "Console.WriteLine();");
        Type(editor, "Console.Write");
        await UiTestHelpers.WaitUntilAsync(() => view.Completion!.SelectedItem?.Entry.DisplayText == "Write", because: "the completion list");
        Type(editor, "(");

        await UiTestHelpers.WaitUntilAsync(() => CaretLine(editor) == "Console.Write(", because: "the commit");
        var help = view.SignatureHelp!;
        await UiTestHelpers.WaitUntilAsync(() => help.IsOpen, because: "parameter info");
        Assert.True(help.HasOverloads);
        Assert.Contains(help.Signature, r => r.IsBold);
        var first = help.SelectedIndex;

        help.NextOverloadCommand.Execute(null);

        Assert.NotEqual(first, help.SelectedIndex);
        Type(editor, ")");
        await UiTestHelpers.WaitUntilAsync(() => !help.IsOpen, because: "parameter info to close after the call");
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_line_break_from_an_on_screen_keyboard_commits_like_Enter()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var editor = view.Editor;

        StartLineAfter(editor, "Console.WriteLine();");
        Type(editor, "Console.");
        await UiTestHelpers.WaitUntilAsync(() => view.Completion!.IsOpen, because: "the completion list");
        Type(editor, "WriteL\n");

        await UiTestHelpers.WaitUntilAsync(() => CaretLine(editor) == "Console.WriteLine", because: "the committed item");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Escape_closes_the_list_without_changing_the_text()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var editor = view.Editor;

        StartLineAfter(editor, "Console.WriteLine();");
        Type(editor, "Console.Wr");
        await UiTestHelpers.WaitUntilAsync(() => view.Completion!.IsOpen, because: "the completion list");
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(view.Completion!.IsOpen);
        Assert.Equal("Console.Wr", CaretLine(editor));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Quick_info_shows_the_signature_and_documentation_at_the_caret()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var greetings = fixture.Workspace.Workspace!.Projects.Single(p => p.Name == "Greetings").DirectoryPath;
        await fixture.Workspace.OpenDocumentAsync(Path.Combine(greetings, "Greeter.cs"));
        var window = Show(fixture);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "class Greeter") + "class Gree".Length;

        fixture.Workspace.QuickInfoCommand.Execute(null);

        await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "Quick Info");
        var info = view.QuickInfo!;
        Assert.Equal(SymbolGlyph.Class, info.Kind);
        Assert.Contains("class Greetings.Greeter", string.Concat(info.Signature.Select(r => r.Text)), StringComparison.Ordinal);
        Assert.Contains(info.Signature, r => r.Text == "Greeter" && r.Classification == "class name");
        Assert.Contains(info.Sections, s => string.Concat(s.Runs.Select(r => r.Text)).Contains("friendly greeting", StringComparison.Ordinal));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Resting_the_pointer_on_a_symbol_shows_quick_info()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var point = PointOf(window, view.Editor, OffsetOf(view.Editor, "new Greeter(") + "new Gre".Length);

        window.MouseMove(point - new Vector(12, 0));
        window.MouseMove(point);

        await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "Quick Info under the pointer");
        Assert.Contains("Greeter(string? name)", string.Concat(view.QuickInfo!.Signature.Select(r => r.Text)), StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Quick_info_also_shows_the_warning_at_the_same_place()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var document = fixture.Workspace.ActiveDocument!;
        StartLineAfter(view.Editor, "Console.WriteLine();");
        Type(view.Editor, "int unused = 42;");
        await document.AnalyzeNowAsync();
        await UiTestHelpers.WaitUntilAsync(() => document.Diagnostics.Any(d => d.Id == "CS0219"), because: "the warning");
        view.Editor.CaretOffset = OffsetOf(view.Editor, "unused") + 2;

        fixture.Workspace.QuickInfoCommand.Execute(null);

        await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "Quick Info");
        var info = view.QuickInfo!;
        Assert.Contains("unused", string.Concat(info.Signature.Select(r => r.Text)), StringComparison.Ordinal);
        var warning = Assert.Single(info.Diagnostics);
        Assert.Equal("CS0219", warning.Code);
        Assert.True(warning.IsWarning);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_symbol_at_the_caret_is_highlighted_wherever_it_is_used()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);

        view.Editor.CaretOffset = OffsetOf(view.Editor, "var greeter") + "var gree".Length;

        await UiTestHelpers.WaitUntilAsync(() => view.ReferenceHighlights!.Highlights.Count >= 2, because: "the highlights");
        Assert.Contains(view.ReferenceHighlights!.Highlights, h => h.Value == HighlightKind.Definition);
        Assert.Contains(view.ReferenceHighlights.Highlights, h => h.Value == HighlightKind.Reference);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_light_bulb_offers_the_missing_using_and_applies_it()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var editor = view.Editor;
        StartLineAfter(editor, "Console.WriteLine();");
        Type(editor, "var builder = new StringBuilder");
        await UiTestHelpers.WaitUntilAsync(() => CaretLine(editor) == "var builder = new StringBuilder", because: "the typing");
        view.Completion!.Close();

        editor.CaretOffset = OffsetOf(editor, "new StringBuilder") + "new String".Length;

        await UiTestHelpers.WaitUntilAsync(() => view.IsLightBulbVisible, because: "the light bulb");
        var fix = Assert.Single(view.CodeActions, a => a.Title == "using System.Text;");
        await view.ApplyCodeActionAsync(fix);
        Assert.Contains("using System.Text;", editor.Text, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Ctrl_click_goes_to_the_definition()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var point = PointOf(window, view.Editor, OffsetOf(view.Editor, "greeter.Greet()") + "greeter.".Length + 1);

        window.MouseDown(point, MouseButton.Left, RawInputModifiers.Control);
        window.MouseUp(point, MouseButton.Left, RawInputModifiers.Control);

        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.ActiveDocument?.FileName == "Greeter.cs", because: "the definition");
        window.Close();
    }

    [AvaloniaFact]
    public async Task F12_in_the_editor_goes_to_the_definition()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "greeter.Greet()") + "greeter.Gr".Length;

        window.KeyPressQwerty(PhysicalKey.F12, RawInputModifiers.None);

        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.ActiveDocument?.FileName == "Greeter.cs", because: "the definition");
        window.Close();
    }

    [AvaloniaFact]
    public async Task Ctrl_R_Ctrl_R_asks_for_the_new_name()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "var greeter") + "var gree".Length;

        window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.Control);
        window.KeyPressQwerty(PhysicalKey.R, RawInputModifiers.Control);

        await UiTestHelpers.WaitUntilAsync(() => fixture.Workspace.Prompt.IsOpen, because: "the rename prompt");
        Assert.Equal("greeter", fixture.Workspace.Prompt.Text);
        fixture.Workspace.Prompt.CancelCommand.Execute(null);
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_context_menu_puts_IntelliSense_first_then_editing()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);

        var menu = view.Editor.ContextMenu!;
        menu.Open(view.Editor);
        Dispatcher.UIThread.RunJobs();
        var headers = menu.Items.OfType<MenuItem>().Where(i => i.IsVisible).Select(i => i.Header as string).ToList();
        menu.Close();

        Assert.Equal(
            new string?[]
            {
                "Quick Actions and Refactorings...", "Rename...", "Go To Definition", "Peek Definition", "Go To Implementation", "Find All References",
                "Cut", "Copy", "Paste", "Format Document", "Toggle Line Comment",
            },
            headers);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_right_click_moves_the_caret_and_opens_the_context_menu()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);
        var offset = OffsetOf(view.Editor, "greeter.Greet()") + "greeter.Gr".Length;
        var point = PointOf(window, view.Editor, offset);

        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        await UiTestHelpers.WaitUntilAsync(() => view.Editor.ContextMenu!.IsOpen, because: "the context menu");

        Assert.InRange(view.Editor.CaretOffset, offset - 1, offset + 1);
        view.Editor.ContextMenu!.Close();
        window.Close();
    }

    private static Window Show(WorkspaceFixture fixture)
    {
        // Tall enough that the line the pointer goes to is above the floating touch bar, whose 44-pixel targets and
        // the shell's 44-pixel bars leave the editor less height than 800 pixels did.
        var window = new Window { Width = 1280, Height = 960, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        return window;
    }
}
