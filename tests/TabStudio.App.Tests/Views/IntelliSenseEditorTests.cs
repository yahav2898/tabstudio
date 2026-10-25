using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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
        var window = Show(fixture);
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "new Greeter(") + "new Gree".Length;

        fixture.Workspace.QuickInfoCommand.Execute(null);

        await UiTestHelpers.WaitUntilAsync(() => view.QuickInfo is not null, because: "Quick Info");
        var info = view.QuickInfo!;
        Assert.Contains("Greetings.Greeter", string.Concat(info.Signature.Select(r => r.Text)), StringComparison.Ordinal);
        Assert.Contains(info.Sections, s => string.Concat(s.Runs.Select(r => r.Text)).Contains("friendly greeting", StringComparison.Ordinal));
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
    public async Task The_context_menu_puts_IntelliSense_first_then_editing()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var window = Show(fixture);
        var view = ActiveEditor(window);

        var headers = view.Editor.ContextMenu!.Items.OfType<MenuItem>().Select(i => i.Header as string).ToList();

        Assert.Equal(
            new string?[]
            {
                "Quick Actions and Refactorings...", "Rename...", "Go To Definition", "Go To Implementation", "Find All References",
                "Cut", "Copy", "Paste", "Format Document", "Toggle Line Comment",
            },
            headers);
        window.Close();
    }

    private static Window Show(WorkspaceFixture fixture)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = fixture.Workspace } };
        window.Show();
        return window;
    }
}
