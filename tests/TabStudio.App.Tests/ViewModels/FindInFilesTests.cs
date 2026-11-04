using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Tests.Views;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Find in Files (Ctrl+Shift+F) and Replace in Files (Ctrl+Shift+H): text in every file of the workspace, with Match
/// case, Match whole word and regular expressions, in a Find Results panel of its own. Its matches used to be listed in
/// the References panel, so Find All References replaced them, and a Find in Files replaced the references.
/// </summary>
public sealed class FindInFilesTests
{
    [AvaloniaFact]
    public async Task Find_in_files_opens_Find_Results_with_the_word_at_the_caret_and_lists_every_line_with_it()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        program.CaretOffset = program.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal) + "greeter.Gr".Length;

        workspace.FindInFilesCommand.Execute(null);

        Assert.Equal(BottomPanel.FindResults, workspace.SelectedPanel);
        Assert.True(workspace.IsFindResultsSelected);
        var find = workspace.FindResults;
        Assert.False(find.IsReplaceMode);
        Assert.Equal("Greet", find.FindText);

        find.FindText = "greeter";
        await find.FindAllCommand.ExecuteAsync(null);

        var results = find.Results;
        Assert.Equal("'greeter' in files", results.Title);
        Assert.False(results.IsSearching);

        // Letter case is ignored, as Visual Studio's default is: "Greeter" in Greeter.cs and "greeter" in Program.cs.
        Assert.Contains(results.Items, i => Path.GetFileName(i.FilePath) == "Greeter.cs" && i.LineText.Contains("public sealed class Greeter", StringComparison.Ordinal));
        Assert.Contains(results.Items, i => Path.GetFileName(i.FilePath) == "Program.cs" && i.LineText.Contains("greeter.Greet()", StringComparison.Ordinal));
        Assert.DoesNotContain(results.Items, i => i.FilePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        Assert.EndsWith("matches in " + results.Items.Select(i => i.FilePath).Distinct().Count() + " files", results.Summary, StringComparison.Ordinal);
        Assert.Contains(results.Rows, r => r is ReferenceFileRow { FileName: "Greeter.cs" } row && row.CountText.EndsWith("matches", StringComparison.Ordinal));

        // The References panel was left as it was.
        Assert.Empty(workspace.References.Items);
        Assert.Equal("Find All References", workspace.References.Title);

        var match = results.Rows.OfType<ReferenceLineRow>().First(r => Path.GetFileName(r.Item.FilePath) == "Greeter.cs");
        await results.NavigateCommand.ExecuteAsync(match);
        Assert.Equal("Greeter.cs", workspace.ActiveDocument!.FileName);
        Assert.Equal(match.Item.Line, workspace.ActiveDocument.PendingNavigation!.Value.Line);
    }

    [AvaloniaFact]
    public async Task Find_in_files_searches_unsaved_edits_and_says_when_nothing_is_found()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.ActiveDocument!.Document.Insert(0, "// Typed but not saved: zebra\n");

        await workspace.SearchInFilesAsync(workspace.Workspace!.RootDirectory, "ZEBRA");
        var results = workspace.FindResults.Results;
        var match = Assert.Single(results.Items);
        Assert.Equal(1, match.Line);
        Assert.Equal("// Typed but not saved: zebra".IndexOf("zebra", StringComparison.Ordinal) + 1, match.Column);
        Assert.Equal("1 match", results.Summary);

        await workspace.SearchInFilesAsync(workspace.Workspace!.RootDirectory, "no such text anywhere");
        Assert.Empty(results.Items);
        Assert.Contains("was not found", results.Summary, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Match_case_whole_word_and_regular_expressions_change_what_is_found()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var root = workspace.Workspace!.RootDirectory;
        await File.WriteAllTextAsync(Path.Combine(root, "zoo.txt"), "Zebra zebra zebras\nzebra_crossing zebra42 zebra\n");
        workspace.FindInFilesCommand.Execute(null);
        var find = workspace.FindResults;

        async Task<string[]> FindAsync(string text, bool matchCase = false, bool wholeWord = false, bool regex = false)
        {
            find.FindText = text;
            find.MatchCase = matchCase;
            find.MatchWholeWord = wholeWord;
            find.UseRegularExpressions = regex;
            await find.FindAllCommand.ExecuteAsync(null);
            Assert.False(find.Results.IsSearching);
            return [.. find.Results.Items.Where(i => Path.GetFileName(i.FilePath) == "zoo.txt").Select(i => $"{i.Line}:{i.Column}")];
        }

        Assert.Equal(new[] { "1:1", "1:7", "1:13", "2:1", "2:16", "2:24" }, await FindAsync("zebra"));
        Assert.Equal(new[] { "1:7", "1:13", "2:1", "2:16", "2:24" }, await FindAsync("zebra", matchCase: true));
        Assert.Equal(new[] { "1:1", "1:7", "2:24" }, await FindAsync("zebra", wholeWord: true));
        Assert.Equal(new[] { "1:7", "2:24" }, await FindAsync("zebra", matchCase: true, wholeWord: true));
        Assert.Equal(new[] { "2:16" }, await FindAsync(@"zebra\d+", regex: true));
        Assert.Equal(new[] { "1:1", "2:1" }, await FindAsync("^zebra", regex: true));
        Assert.Equal(new[] { "1:1" }, await FindAsync("^Z", matchCase: true, regex: true));

        // Without regular expressions, the same text is found as it is written.
        Assert.Empty(await FindAsync(@"zebra\d+"));
    }

    [AvaloniaFact]
    public async Task An_invalid_regular_expression_says_so_instead_of_throwing()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        workspace.FindInFilesCommand.Execute(null);
        var find = workspace.FindResults;
        find.FindText = "(greeter";
        find.UseRegularExpressions = true;

        await find.FindAllCommand.ExecuteAsync(null);

        Assert.Equal(BottomPanel.FindResults, workspace.SelectedPanel);
        Assert.Empty(find.Results.Items);
        Assert.False(find.Results.IsSearching);
        Assert.StartsWith("Not a valid regular expression:", find.Results.Summary, StringComparison.Ordinal);
        Assert.StartsWith("Not a valid regular expression:", workspace.StatusText, StringComparison.Ordinal);

        // Replace All with it changes nothing and says the same.
        var program = workspace.ActiveDocument!;
        var before = program.Document.Text;
        find.IsReplaceMode = true;
        find.ReplaceText = "x";
        await find.ReplaceAllCommand.ExecuteAsync(null);
        Assert.Equal(before, program.Document.Text);
        Assert.StartsWith("Not a valid regular expression:", find.Results.Summary, StringComparison.Ordinal);

        // Unescaped, the same text is plain text again, and is found.
        find.UseRegularExpressions = false;
        find.FindText = "greeter";
        await find.FindAllCommand.ExecuteAsync(null);
        Assert.NotEmpty(find.Results.Items);
    }

    [AvaloniaFact]
    public async Task Replace_all_edits_an_open_buffer_undoably_and_a_closed_file_on_disk_in_its_own_encoding()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var root = workspace.Workspace!.RootDirectory;
        var program = workspace.ActiveDocument!;
        var programOnDisk = await File.ReadAllTextAsync(program.FilePath);

        // An unsaved edit in the open Program.cs, and a closed file with a byte order mark and Windows line endings.
        program.Document.Insert(0, "// unsaved: gamma.Qty\n");
        var edited = program.Document.Text;
        var closed = Path.Combine(root, "closed.txt");
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        await File.WriteAllBytesAsync(closed, utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes("alpha.Qty + beta.Qty\r\nno match here\r\n")).ToArray());

        workspace.ReplaceInFilesCommand.Execute(null);
        var find = workspace.FindResults;
        Assert.True(find.IsReplaceMode);
        Assert.Equal(BottomPanel.FindResults, workspace.SelectedPanel);
        find.FindText = @"(\w+)\.Qty";
        find.ReplaceText = "$1";
        find.UseRegularExpressions = true;

        await find.ReplaceAllCommand.ExecuteAsync(null);

        // The open document changed in its editor, with its unsaved edit kept, and is not saved.
        Assert.Equal(edited.Replace("gamma.Qty", "gamma", StringComparison.Ordinal), program.Document.Text);
        Assert.True(program.IsDirty);
        Assert.Equal(programOnDisk, await File.ReadAllTextAsync(program.FilePath));

        // The closed file changed on disk, keeping its byte order mark and line endings.
        var expected = utf8Bom.GetPreamble().Concat(utf8Bom.GetBytes("alpha + beta\r\nno match here\r\n")).ToArray();
        Assert.Equal(expected, await File.ReadAllBytesAsync(closed));

        // How many replacements in how many files, in the panel and the status bar.
        Assert.Equal("3 replacements in 2 files", find.Results.Summary);
        Assert.Equal("3 replacements in 2 files", workspace.StatusText);
        Assert.Contains(find.Results.Items, i => Path.GetFileName(i.FilePath) == "closed.txt" && i.Line == 1 && i.Column == 13);
        Assert.Contains(find.Results.Items, i => i.FilePath == program.FilePath && i.Line == 1);

        // One Undo in the editor takes the replacement back, and only it.
        program.Document.UndoStack.Undo();
        Assert.Equal(edited, program.Document.Text);
    }

    [AvaloniaFact]
    public async Task Find_All_References_after_Find_in_Files_leaves_the_find_results_and_the_other_way_round()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var root = workspace.Workspace!.RootDirectory;
        await workspace.SearchInFilesAsync(root, "greeter");
        var found = workspace.FindResults.Results.Items.ToList();
        Assert.NotEmpty(found);

        var program = workspace.ActiveDocument!;
        program.CaretOffset = program.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal) + "greeter.Gr".Length;
        await workspace.FindAllReferencesCommand.ExecuteAsync(null);

        Assert.Equal(BottomPanel.References, workspace.SelectedPanel);
        Assert.Equal("'Greet' references", workspace.References.Title);
        Assert.Equal(2, workspace.References.Items.Count);
        Assert.Equal("'greeter' in files", workspace.FindResults.Results.Title);
        Assert.Equal(found, workspace.FindResults.Results.Items);

        await workspace.SearchInFilesAsync(root, "Console");

        Assert.Equal(BottomPanel.FindResults, workspace.SelectedPanel);
        Assert.Equal("'Console' in files", workspace.FindResults.Results.Title);
        Assert.Equal("'Greet' references", workspace.References.Title);
        Assert.Equal(2, workspace.References.Items.Count);
        Assert.Equal("2 references in 2 files", workspace.References.Summary);
    }

    [AvaloniaFact]
    public async Task Ctrl_Shift_F_and_Ctrl_Shift_H_open_find_and_replace_in_files_and_Enter_finds_all()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            var editor = EditorHarness.ActiveEditor(window).Editor;
            editor.CaretOffset = editor.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal) + 2;
            workspace.ActiveDocument!.CaretOffset = editor.CaretOffset;

            window.KeyPressQwerty(PhysicalKey.F, RawInputModifiers.Control | RawInputModifiers.Shift);

            var find = workspace.FindResults;
            await UiTestHelpers.WaitUntilAsync(() => workspace.SelectedPanel == BottomPanel.FindResults, because: "Ctrl+Shift+F opens Find Results");
            Assert.False(find.IsReplaceMode);
            Assert.Equal("greeter", find.FindText);
            var view = window.GetVisualDescendants().OfType<FindResultsView>().Single();
            var findBox = view.FindControl<TextBox>("FindBox")!;
            var replaceBox = view.FindControl<TextBox>("ReplaceBox")!;
            await UiTestHelpers.WaitUntilAsync(() => findBox.IsFocused, because: "the find box takes the keyboard");
            Assert.True(view.IsEffectivelyVisible);
            Assert.False(replaceBox.IsEffectivelyVisible);

            window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
            await UiTestHelpers.WaitUntilAsync(() => find.Results.Title == "'greeter' in files" && !find.Results.IsSearching, because: "Enter runs Find All");
            Assert.NotEmpty(find.Results.Items);

            window.KeyPressQwerty(PhysicalKey.H, RawInputModifiers.Control | RawInputModifiers.Shift);
            await UiTestHelpers.WaitUntilAsync(() => find.IsReplaceMode, because: "Ctrl+Shift+H shows Replace in Files");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(replaceBox.IsEffectivelyVisible);
            Assert.Equal(BottomPanel.FindResults, workspace.SelectedPanel);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Matches_have_their_lines_and_columns_and_binary_and_ignored_files_are_skipped()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "a.txt"), "one needle\r\ntwo\r\nneedle and needle\n");
        File.WriteAllBytes(Path.Combine(temp.Path, "image.bin"), [0x89, 0x50, 0, 0, (byte)'n', (byte)'e', (byte)'e', (byte)'d', (byte)'l', (byte)'e']);
        Directory.CreateDirectory(Path.Combine(temp.Path, "node_modules"));
        File.WriteAllText(Path.Combine(temp.Path, "node_modules", "x.js"), "needle");
        Directory.CreateDirectory(Path.Combine(temp.Path, "src"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "b.cs"), "// NEEDLE");

        var matches = FileTextSearch.Search(temp.Path, "needle", matchCase: false, openDocuments: null, CancellationToken.None);

        Assert.Equal(
            new[] { "a.txt 1:5 one needle", "a.txt 3:1 needle and needle", "a.txt 3:12 needle and needle", "b.cs 1:4 // NEEDLE" },
            matches.Select(m => $"{Path.GetFileName(m.FilePath)} {m.Line}:{m.Column} {m.LineText}"));
        Assert.Single(FileTextSearch.Search(temp.Path, "NEEDLE", matchCase: true, openDocuments: null, CancellationToken.None));
    }
}
