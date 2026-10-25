using AvaloniaEdit.Document;
using TabStudio.App.Editing;

namespace TabStudio.App.Tests.Editing;

public sealed class CommentTogglerTests
{
    private static readonly FileType CSharp = FileTypes.For("Program.cs");
    private static readonly FileType Xml = FileTypes.For("App.csproj");

    [Fact]
    public void Comments_lines_at_their_common_indentation()
    {
        var document = new TextDocument("    var a = 1;\n        var b = 2;\n");

        Assert.True(CommentToggler.Toggle(document, 1, 2, CSharp));

        Assert.Equal("    // var a = 1;\n    //     var b = 2;\n", document.Text);
    }

    [Fact]
    public void Uncomments_when_every_line_is_commented()
    {
        var document = new TextDocument("    // var a = 1;\n    //     var b = 2;\n");

        CommentToggler.Toggle(document, 1, 2, CSharp);

        Assert.Equal("    var a = 1;\n        var b = 2;\n", document.Text);
    }

    [Fact]
    public void Blank_lines_are_left_alone()
    {
        var document = new TextDocument("a();\n\nb();");

        CommentToggler.Toggle(document, 1, 3, CSharp);

        Assert.Equal("// a();\n\n// b();", document.Text);
    }

    [Fact]
    public void Xml_uses_block_comments_per_line()
    {
        var document = new TextDocument("  <A />\n  <B />");

        CommentToggler.Toggle(document, 1, 2, Xml);
        Assert.Equal("  <!-- <A /> -->\n  <!-- <B /> -->", document.Text);

        CommentToggler.Toggle(document, 1, 2, Xml);
        Assert.Equal("  <A />\n  <B />", document.Text);
    }

    [Fact]
    public void Toggling_is_a_single_undo_step()
    {
        var document = new TextDocument("a();\nb();");

        CommentToggler.Toggle(document, 1, 2, CSharp);
        document.UndoStack.Undo();

        Assert.Equal("a();\nb();", document.Text);
    }
}
