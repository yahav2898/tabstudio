using Avalonia.Headless.XUnit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using TabStudio.App.Views.DesktopMode;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// How a change of the companion's typing field, as an on-screen keyboard makes it, becomes keys on the big screen:
/// the shared beginning and end of the field's text before and after.
/// </summary>
public sealed class DesktopTypingRelayTests
{
    [Theory]
    [InlineData("", "var", 0, 0)]
    [InlineData("var", "var x", 3, 0)]
    [InlineData("var x", "var", 3, 0)]
    [InlineData("teh", "the", 1, 0)]
    [InlineData("// typed on a tablet", "// Typed on a tablet", 3, 16)]
    [InlineData("aaa", "aaaa", 3, 0)]
    [InlineData("abc", "", 0, 0)]
    public void The_change_is_found_between_what_the_texts_share(string before, string after, int prefix, int suffix)
    {
        Assert.Equal((prefix, suffix), DesktopTypingRelay.Common(before, after));
    }

    [AvaloniaFact]
    public void Tab_indents_to_the_next_stop_at_the_caret_and_Shift_Tab_takes_one_indentation_off_its_line()
    {
        var area = Editor("ab\n");
        area.Caret.Offset = 2;

        DesktopTypingRelay.Indent(area);
        Assert.Equal("ab  \n", area.Document.Text);

        area.Document.Text = "        x\n";
        area.Caret.Offset = 9;
        DesktopTypingRelay.Outdent(area);
        Assert.Equal("    x\n", area.Document.Text);
    }

    [AvaloniaFact]
    public void Tab_and_Shift_Tab_move_every_selected_line_but_one_the_selection_only_reaches()
    {
        var area = Editor("a\nb\nc\n");
        area.Selection = Selection.Create(area, 0, 4);

        DesktopTypingRelay.Indent(area);
        Assert.Equal("    a\n    b\nc\n", area.Document.Text);

        area.Selection = Selection.Create(area, 0, 12);
        DesktopTypingRelay.Outdent(area);
        Assert.Equal("a\nb\nc\n", area.Document.Text);
    }

    private static TextArea Editor(string text)
    {
        var area = new TextArea { Document = new TextDocument(text) };
        area.Options.ConvertTabsToSpaces = true;
        area.Options.IndentationSize = 4;
        return area;
    }
}
