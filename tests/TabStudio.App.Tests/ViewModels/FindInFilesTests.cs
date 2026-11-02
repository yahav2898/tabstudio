using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>Find in Files (Ctrl+Shift+F): text in every file of the workspace, in the References panel.</summary>
public sealed class FindInFilesTests
{
    [AvaloniaFact]
    public async Task Find_in_files_lists_every_line_with_the_word_at_the_caret_and_opens_a_match()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var program = workspace.ActiveDocument!;
        program.CaretOffset = program.Document.Text.IndexOf("greeter.Greet()", StringComparison.Ordinal) + "greeter.Gr".Length;

        var find = workspace.FindInFilesCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the Find in Files prompt");
        Assert.Equal("Greet", workspace.Prompt.Text);
        workspace.Prompt.Text = "greeter";
        workspace.Prompt.ConfirmCommand.Execute(null);
        await find;

        Assert.Equal(BottomPanel.References, workspace.SelectedPanel);
        var references = workspace.References;
        Assert.Equal("'greeter' in files", references.Title);
        Assert.False(references.IsSearching);

        // Letter case is ignored, as Visual Studio's default is: "Greeter" in Greeter.cs and "greeter" in Program.cs.
        Assert.Contains(references.Items, i => Path.GetFileName(i.FilePath) == "Greeter.cs" && i.LineText.Contains("public sealed class Greeter", StringComparison.Ordinal));
        Assert.Contains(references.Items, i => Path.GetFileName(i.FilePath) == "Program.cs" && i.LineText.Contains("greeter.Greet()", StringComparison.Ordinal));
        Assert.DoesNotContain(references.Items, i => i.FilePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
        Assert.EndsWith("matches in " + references.Items.Select(i => i.FilePath).Distinct().Count() + " files", references.Summary, StringComparison.Ordinal);
        Assert.Contains(references.Rows, r => r is ReferenceFileRow { FileName: "Greeter.cs" } row && row.CountText.EndsWith("matches", StringComparison.Ordinal));

        var match = references.Rows.OfType<ReferenceLineRow>().First(r => Path.GetFileName(r.Item.FilePath) == "Greeter.cs");
        await references.NavigateCommand.ExecuteAsync(match);
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
        var match = Assert.Single(workspace.References.Items);
        Assert.Equal(1, match.Line);
        Assert.Equal("// Typed but not saved: zebra".IndexOf("zebra", StringComparison.Ordinal) + 1, match.Column);
        Assert.Equal("1 match", workspace.References.Summary);

        await workspace.SearchInFilesAsync(workspace.Workspace!.RootDirectory, "no such text anywhere");
        Assert.Empty(workspace.References.Items);
        Assert.Contains("was not found", workspace.References.Summary, StringComparison.Ordinal);
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
