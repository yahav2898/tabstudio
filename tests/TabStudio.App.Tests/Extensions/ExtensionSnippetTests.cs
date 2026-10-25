using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using TabStudio.Compilation.Features;
using TabStudio.Testing.Extensions;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Extensions;

/// <summary>Snippets an installed extension contributes, in the real editor: from completion and from Insert Snippet.</summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class ExtensionSnippetTests
{
    // Lumen's snippets, with one whose tab stop appears twice.
    private const string Snippets = """
        {
          "Function": { "prefix": "fn", "body": ["fn ${1:name}(${2:args}) {", "\t$0", "}"], "description": "A function" },
          "Print the file's name": { "prefix": ["pfile", "printfile"], "body": "print(\"${TM_FILENAME_BASE/(.*)/${1:/upcase}/}\")$0" },
          "If": { "prefix": "ifs", "body": "if ${1|ready,done,failed|} {\n\t$2\n}" },
          "Twice": { "prefix": "twice", "body": "let ${1:x} = ${1} + ${1}" }
        }
        """;

    [AvaloniaFact]
    public async Task A_snippet_expands_from_completion_and_Tab_moves_through_its_tab_stops()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        var editor = view.Editor;
        var completion = view.Completion!;
        editor.CaretOffset = editor.Document.TextLength;

        Type(editor, "fn");
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen && completion.SelectedItem?.Entry.DisplayText == "fn", because: "the snippet in the completion list");
        Assert.Equal(SymbolGlyph.Snippet, completion.SelectedItem!.Entry.Kind);
        await UiTestHelpers.WaitUntilAsync(() => completion.HasDescription, because: "the snippet's description");
        await UiTestHelpers.WaitUntilAsync(
            () =>
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                return view.IsCompletionShown;
            },
            because: "the list under the word");
        UiTestHelpers.SaveScreenshot(window, "ext-06-snippet-completion");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.False(completion.IsOpen);
        Assert.EndsWith("fn name(args) {\n    \n}", editor.Text, StringComparison.Ordinal);
        Assert.Equal("name", editor.SelectedText);
        Assert.NotNull(view.Snippet);

        Type(editor, "square");
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal("args", editor.SelectedText);
        Type(editor, "n");
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.EndsWith("fn square(n) {\n    \n}", editor.Text, StringComparison.Ordinal);
        Assert.Equal("    ", editor.Document.GetText(editor.Document.GetLineByOffset(editor.CaretOffset)));
        Assert.Null(view.Snippet);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Linked_tab_stops_follow_typing_and_variables_and_choices_are_filled_in()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync(LumenExtension.Builder().Add("snippets/lumen.code-snippets", Snippets).Build());
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        var editor = view.Editor;
        var completion = view.Completion!;
        editor.CaretOffset = editor.Document.TextLength;

        // A tab stop that appears three times: typing in the first changes the others, as one undo step per edit.
        Type(editor, "twice");
        await UiTestHelpers.WaitUntilAsync(() => completion.SelectedItem?.Entry.DisplayText == "twice", because: "the snippet");
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Type(editor, "total");
        Assert.Equal("let total = total + total", CaretLine(editor));
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Null(view.Snippet);
        editor.CaretOffset = editor.Document.TextLength;

        // TM_FILENAME_BASE, upper-cased by the snippet's transform.
        Type(editor, "\npfile");
        await UiTestHelpers.WaitUntilAsync(() => completion.SelectedItem?.Entry.DisplayText == "pfile", because: "the snippet");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal("print(\"HELLO\")", CaretLine(editor));
        Assert.Equal(editor.Document.GetLineByOffset(editor.CaretOffset).EndOffset, editor.CaretOffset);

        // A choice tab stop lists its options; picking one replaces the first.
        Type(editor, "\nifs");
        await UiTestHelpers.WaitUntilAsync(() => completion.SelectedItem?.Entry.DisplayText == "ifs", because: "the snippet");
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        await UiTestHelpers.WaitUntilAsync(() => completion.IsOpen && completion.Items.Count == 3, because: "the choices");
        Assert.Equal(new[] { "ready", "done", "failed" }, completion.Items.Select(i => i.Entry.DisplayText).ToArray());
        UiTestHelpers.SaveScreenshot(window, "ext-07-snippet-choice");
        completion.MoveSelection(1);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal("if done {", CaretLine(editor));
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal(string.Empty, CaretLine(editor));
        Type(editor, "go()");
        Assert.Equal("go()", CaretLine(editor));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Insert_Snippet_lists_the_snippets_of_the_file_language_and_inserts_the_chosen_one()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        var editor = view.Editor;
        editor.CaretOffset = editor.Document.TextLength;

        var command = fixture.Workspace.InsertSnippetCommand.ExecuteAsync(null);
        var picker = fixture.Workspace.QuickPick;
        await UiTestHelpers.WaitUntilAsync(() => picker.IsOpen, because: "the snippet picker");
        Assert.Equal(new[] { "Function", "If", "Print the file's name" }, picker.Items.Select(i => i.Label).ToArray());
        picker.FilterText = "func";
        Assert.Equal("Function", Assert.Single(picker.Items).Label);
        UiTestHelpers.SaveScreenshot(window, "ext-08-insert-snippet");

        picker.Accept();
        await command;

        Assert.False(picker.IsOpen);
        Assert.EndsWith("fn name(args) {\n    \n}", editor.Text, StringComparison.Ordinal);
        Assert.Equal("name", editor.SelectedText);

        // A file without snippets says so instead of showing an empty list.
        await fixture.OpenAsync("notes.txt");
        await fixture.Workspace.InsertSnippetCommand.ExecuteAsync(null);
        Assert.False(picker.IsOpen);
        Assert.Contains("no snippets", fixture.Workspace.StatusText, StringComparison.Ordinal);
        window.Close();
    }
}
