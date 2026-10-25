using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.Views;
using TabStudio.Testing.Extensions;
using static TabStudio.App.Tests.Views.EditorHarness;

namespace TabStudio.App.Tests.Extensions;

/// <summary>
/// An installed extension in the real editor: its grammar colors a new file type, its theme the editor, and its
/// language configuration drives comments, auto-closing, indentation and folding, as the extension declares.
/// </summary>
[Collection(ExtensionsTestGroup.Name)]
public sealed class ExtensionEditorTests
{
    [AvaloniaFact]
    public async Task An_installed_grammar_colors_a_new_file_type_and_its_theme_colors_the_editor()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        var document = await fixture.OpenAsync("hello.lumen");

        Assert.Equal("Lumen", document.FileType.DisplayName);
        Assert.Equal("LUM", document.FileType.Badge);
        Assert.Equal("lumen", document.FileType.LanguageId);
        var view = ActiveEditor(window);
        var keyword = OffsetOf(view.Editor, "fn greet");
        var plain = OffsetOf(view.Editor, "= 42");
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) is { } color && color != view.ForegroundAt(plain), because: "the keyword to be colored by the grammar");

        fixture.Service.SelectColorTheme(fixture.NightThemeKey);

        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) == Color.Parse(LumenExtension.NightKeyword), because: "the theme's keyword color");
        Assert.Equal(Color.Parse(LumenExtension.NightString), view.ForegroundAt(OffsetOf(view.Editor, "\"Hello, \"") + 1));
        Assert.Equal(Color.Parse(LumenExtension.NightBackground), Assert.IsAssignableFrom<ISolidColorBrush>(view.EditorBackground).Color);

        // The injection grammar marks TODO in comments; the embedded JSON is colored by the JSON grammar.
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(OffsetOf(view.Editor, "TODO")) == Color.Parse("#FF5F5F"), because: "the injected TODO color");
        Assert.Equal(Color.Parse(LumenExtension.NightString), view.ForegroundAt(OffsetOf(view.Editor, "\"ok\"") + 1));

        view.Editor.CaretOffset = OffsetOf(view.Editor, "let count");
        UiTestHelpers.SaveScreenshot(window, "ext-04-lumen-night");

        fixture.Service.SelectColorTheme(null);

        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) != Color.Parse(LumenExtension.NightKeyword), because: "TabStudio's own colors");
        Assert.NotEqual(Color.Parse(LumenExtension.NightBackground), (view.EditorBackground as ISolidColorBrush)?.Color);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_color_theme_colors_CSharp_by_the_classifications_of_the_language_service()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.Workspace.LoadAsync(await new SampleLibrary(fixture.Paths).EnsureExtractedAsync());
        var window = fixture.Show();
        var view = ActiveEditor(window);
        var keyword = OffsetOf(view.Editor, "foreach");
        var record = OffsetOf(view.Editor, "Language(\"C#\"");
        var method = OffsetOf(view.Editor, "WriteLine(greeter");
        var text = OffsetOf(view.Editor, "\"What's") + 1;

        fixture.Service.SelectColorTheme(fixture.NightThemeKey);

        // The theme's semantic token colors apply to Roslyn's classifications, and its TextMate rules to the rest.
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(record) == Color.Parse("#FFD866"), because: "the record in the theme's class color");
        Assert.Equal(Color.Parse("#7FD3FF"), view.ForegroundAt(method));
        Assert.Equal(Color.Parse(LumenExtension.NightKeyword), view.ForegroundAt(keyword));
        Assert.Equal(Color.Parse(LumenExtension.NightString), view.ForegroundAt(text));
        Assert.Equal(Color.Parse(LumenExtension.NightBackground), Assert.IsAssignableFrom<ISolidColorBrush>(view.EditorBackground).Color);
        view.Editor.CaretOffset = keyword;
        UiTestHelpers.SaveScreenshot(window, "ext-14-csharp-lumen-night");
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_color_theme_picker_previews_each_theme_and_Escape_goes_back()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        var keyword = OffsetOf(view.Editor, "fn greet");
        var picker = fixture.Workspace.QuickPick;

        var pick = fixture.Workspace.SelectColorThemeCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => picker.IsOpen, because: "the theme picker");
        Assert.Equal(new[] { "TabStudio Dark", LumenExtension.NightTheme, LumenExtension.DayTheme, "Browse Color Themes on Open VSX…" }, picker.Items.Select(i => i.Label).ToArray());
        Assert.Equal("current", picker.SelectedItem!.Description);

        picker.MoveSelection(1);

        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) == Color.Parse(LumenExtension.NightKeyword), because: "the previewed theme");
        UiTestHelpers.SaveScreenshot(window, "ext-10-theme-picker");
        picker.Cancel();
        await pick;

        Assert.Null(fixture.Service.Settings.ColorTheme);
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) != Color.Parse(LumenExtension.NightKeyword), because: "the colors before the preview");

        pick = fixture.Workspace.SelectColorThemeCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => picker.IsOpen, because: "the theme picker");
        picker.MoveSelection(2);
        picker.Accept();
        await pick;

        Assert.Equal(fixture.Service.Contributions.Themes.Single(t => t.Label == LumenExtension.DayTheme).Key, fixture.Service.Settings.ColorTheme);
        Assert.Equal(Color.Parse("#FBFAF5"), Assert.IsAssignableFrom<ISolidColorBrush>(view.EditorBackground).Color);
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_file_that_is_open_takes_its_language_when_the_extension_is_installed_and_loses_it_when_disabled()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        var document = await fixture.OpenAsync("hello.lumen");
        Assert.Same(FileTypes.PlainText, document.FileType);
        var view = ActiveEditor(window);

        var installed = await fixture.InstallLumenAsync();

        Assert.Equal("Lumen", document.FileType.DisplayName);
        var keyword = OffsetOf(view.Editor, "fn greet");
        var plain = OffsetOf(view.Editor, "= 42");
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) is { } color && color != view.ForegroundAt(plain), because: "the new grammar's colors");
        Assert.NotNull(view.LanguageRules);

        await fixture.Service.SetEnabledAsync(installed.Id, enabled: false);

        Assert.Same(FileTypes.PlainText, document.FileType);
        Assert.Null(view.LanguageRules);
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) == view.ForegroundAt(plain), because: "the coloring to go");
        window.Close();
    }

    [AvaloniaFact]
    public async Task A_script_without_an_extension_is_recognized_by_its_first_line()
    {
        using var fixture = new ExtensionsFixture();
        var package = LumenExtension.PackageJson().Replace(
            "\"filenames\": [\"Lumenfile\"],",
            "\"filenames\": [\"Lumenfile\"], \"firstLine\": \"^#!.*\\\\blumen\\\\b\",",
            StringComparison.Ordinal);
        await fixture.InstallLumenAsync(LumenExtension.Builder().Add("package.json", package).Build());
        File.WriteAllText(Path.Combine(fixture.Folder, "deploy"), "#!/usr/bin/env lumen\nfn deploy() {\n    return 1\n}\n");
        await fixture.OpenFolderAsync();
        var window = fixture.Show();

        var document = await fixture.OpenAsync("deploy");
        var view = ActiveEditor(window);

        Assert.Equal("Lumen", document.FileType.DisplayName);
        var keyword = OffsetOf(view.Editor, "fn deploy");
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(keyword) is { } color && color != view.TextForeground, because: "the grammar's colors");
        Assert.NotNull(view.LanguageRules);
        Assert.Equal("#", document.FileType.LineComment);
        window.Close();
    }

    [AvaloniaFact]
    public async Task Toggle_comment_uses_the_line_comment_of_the_language()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        view.Editor.CaretOffset = OffsetOf(view.Editor, "let count");

        fixture.Workspace.ToggleCommentCommand.Execute(null);

        Assert.Equal("# let count = 42", CaretLine(view.Editor));

        fixture.Workspace.ToggleCommentCommand.Execute(null);

        Assert.Equal("let count = 42", CaretLine(view.Editor));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Brackets_and_quotes_close_themselves_are_typed_over_surround_the_selection_and_delete_in_pairs()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        var editor = view.Editor;
        editor.CaretOffset = editor.Document.TextLength;

        Type(editor, "print(");
        Assert.Equal("print()", CaretLine(editor));
        Type(editor, "\"hi");
        Assert.Equal("print(\"hi\")", CaretLine(editor));
        Type(editor, "\")");
        Assert.Equal("print(\"hi\")", CaretLine(editor));
        Assert.Equal(editor.Document.GetLineByOffset(editor.CaretOffset).EndOffset, editor.CaretOffset);

        // A selection is surrounded by the typed bracket.
        var hi = OffsetOf(editor, "hi\"");
        editor.Select(hi, 2);
        Type(editor, "[");
        Assert.Equal("print(\"[hi]\")", CaretLine(editor));
        Assert.Equal("hi", editor.SelectedText);

        // Backspace between a pair that closed itself deletes both halves.
        editor.TextArea.ClearSelection();
        editor.CaretOffset = editor.Document.GetLineByOffset(editor.CaretOffset).EndOffset;
        Type(editor, " (");
        Assert.Equal("print(\"[hi]\") ()", CaretLine(editor));
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        Assert.Equal("print(\"[hi]\") ", LineText(editor));

        // In a comment a quote does not close itself, as notIn says.
        var comment = OffsetOf(editor, "Greets everyone");
        await UiTestHelpers.WaitUntilAsync(() => view.ForegroundAt(comment) != view.ForegroundAt(OffsetOf(editor, "= 42")), because: "the comment's tokens");
        editor.CaretOffset = comment + "Greets".Length;
        Type(editor, " \"");
        Assert.Equal("# Greets \" everyone. TODO: localize", CaretLine(editor));
        window.Close();
    }

    [AvaloniaFact]
    public async Task Enter_indents_by_the_language_rules_and_continues_comments_and_a_closing_brace_outdents()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        var editor = view.Editor;
        editor.CaretOffset = editor.Document.TextLength;

        // Between a pair of braces, the closing one moves to its own line and the caret is indented.
        Type(editor, "fn three() {");
        Assert.Equal("fn three() {}", CaretLine(editor));
        Type(editor, "\n");
        Assert.Equal("    ", LineText(editor));
        Assert.Equal("}", editor.Document.GetText(editor.Document.GetLineByOffset(editor.CaretOffset).NextLine!));

        // A closing brace typed at the start of an over-indented line outdents it to the block's level.
        Type(editor, "if ready {\n");
        Assert.Equal("        ", LineText(editor));
        Type(editor, "}");
        Assert.Equal("    }", LineText(editor));

        // Enter in the middle of a comment continues it.
        editor.CaretOffset = OffsetOf(editor, "Greets everyone") + "Greets".Length;
        Type(editor, "\n");
        Assert.Equal("# everyone. TODO: localize", CaretLine(editor));
        window.Close();
    }

    [AvaloniaFact]
    public async Task The_file_folds_by_indentation_and_region_markers_and_the_bracket_at_the_caret_is_matched()
    {
        using var fixture = new ExtensionsFixture();
        await fixture.InstallLumenAsync();
        await fixture.OpenFolderAsync();
        var window = fixture.Show();
        await fixture.OpenAsync("hello.lumen");
        var view = ActiveEditor(window);
        var editor = view.Editor;

        await UiTestHelpers.WaitUntilAsync(() => view.Folding?.AllFoldings.Any() == true, because: "the folds");
        var folds = view.Folding!.AllFoldings.Select(f => editor.Document.GetLineByOffset(f.StartOffset).LineNumber).ToList();
        Assert.Contains(editor.Document.GetLineByOffset(OffsetOf(editor, "fn greet")).LineNumber, folds);
        Assert.Contains(editor.Document.GetLineByOffset(OffsetOf(editor, "# region Helpers")).LineNumber, folds);

        editor.CaretOffset = OffsetOf(editor, "{\n    let count") + 1;
        await UiTestHelpers.WaitUntilAsync(() => view.MatchedBracket is not null, because: "the bracket match");
        Assert.Equal(OffsetOf(editor, "{\n    let count"), view.MatchedBracket!.OpenStart);
        Assert.Equal('}', editor.Document.GetCharAt(view.MatchedBracket.CloseStart));
        window.Close();
    }

    /// <summary>The caret line's whole text, with its indentation.</summary>
    private static string LineText(AvaloniaEdit.TextEditor editor) =>
        editor.Document.GetText(editor.Document.GetLineByOffset(editor.CaretOffset));
}
