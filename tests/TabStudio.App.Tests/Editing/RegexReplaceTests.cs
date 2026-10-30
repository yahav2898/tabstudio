using System.Text;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using AvaloniaEdit;
using AvaloniaEdit.Search;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;

namespace TabStudio.App.Tests.Editing;

/// <summary>
/// Replace with a regular expression fills in its groups: AvaloniaEdit's Replace All turned "a.Value + b.Value",
/// searched with <c>(\w+)\.Value</c> and replaced with <c>$1</c>, into "$1 + $1".
/// </summary>
public sealed class RegexReplaceTests
{
    [AvaloniaFact]
    public void Replace_all_and_replace_next_expand_the_groups_of_the_match()
    {
        var profile = DeviceProfile.For(new DeviceInfo(8L * 1024 * 1024 * 1024, 4, false));
        using var document = new DocumentViewModel(Path.Combine(Path.GetTempPath(), "sum.cs"), new TextFileContent("var sum = a.Value + b.Value;\nvar max = c.Value;\n", new UTF8Encoding(false)), null, profile);
        using var view = new CodeEditorView(document);
        var window = new Window { Content = view, Width = 800, Height = 400 };
        window.Show();
        try
        {
            view.Search.Open();
            view.Search.IsReplaceMode = true;
            view.Search.UseRegex = true;
            view.Search.SearchPattern = @"(\w+)\.Value";
            view.Search.ReplacePattern = "$1";

            SearchCommands.ReplaceAll.Execute(null, view.Editor.TextArea);
            Assert.Equal("var sum = a + b;\nvar max = c;\n", document.Document.Text);

            document.Document.UndoStack.Undo();
            Assert.Equal("var sum = a.Value + b.Value;\nvar max = c.Value;\n", document.Document.Text);

            view.Search.ReplacePattern = "${1}.Amount";
            view.Editor.Select(0, 0);
            view.ReplaceNext();
            Assert.Equal("var sum = a.Amount + b.Value;\nvar max = c.Value;\n", document.Document.Text);
            Assert.Equal("b.Value", view.Editor.SelectedText);
        }
        finally
        {
            window.Close();
        }
    }

    [Fact]
    public void Whole_words_empty_matches_and_wrapping_follow_the_find_panel()
    {
        var regex = RegexReplace.Create("cat", matchCase: false)!;

        RegexReplacement[] whole = [new(8, 3, "dog")];
        Assert.Equal(whole, RegexReplace.All(regex, "concat. Cat", "dog", wholeWords: true));
        Assert.Equal(new RegexReplacement(0, 3, "x"), RegexReplace.Next(regex, "cat dog", 5, "x", wholeWords: false));
        Assert.Empty(RegexReplace.All(RegexReplace.Create("^", matchCase: true)!, "a\nb", "-", wholeWords: false));
        Assert.Null(RegexReplace.Create("(", matchCase: true));
    }
}
