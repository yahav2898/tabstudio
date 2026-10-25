using TabStudio.App.Previews;
using TabStudio.App.Xaml;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// The Properties panel's attribute edits and the Toolbox's insertions: they change only what they must, and look as
/// if typed, with the file's own indentation.
/// </summary>
public sealed class XamlEditsTests
{
    private const string Panel = """
        <UserControl xmlns="https://github.com/avaloniaui">
          <StackPanel Spacing="8">
            <TextBlock Text="Hello" />
          </StackPanel>
        </UserControl>
        """;

    [Fact]
    public void Sets_an_attribute_that_is_written_escaping_the_value()
    {
        const string xaml = "<Button Content=\"Old\" Width=\"80\" />";

        Assert.Equal("<Button Content=\"Save &amp; &lt;close> &quot;now&quot;\" Width=\"80\" />", Set(xaml, "Button", "Content", "Save & <close> \"now\""));
        Assert.Equal("<Button Content='It&apos;s' />", Set("<Button Content='Old' />", "Button", "Content", "It's"));
    }

    [Fact]
    public void Adds_an_attribute_after_the_last_one_or_on_a_line_of_its_own()
    {
        Assert.Equal("<Button Width=\"120\" />", Set("<Button />", "Button", "Width", "120"));
        Assert.Equal("<Button Content=\"Go\" Width=\"120\" />", Set("<Button Content=\"Go\" />", "Button", "Width", "120"));
        Assert.Equal(
            "<Button Content=\"Go\"\n        Width=\"120\"\n        Height=\"40\" />",
            Set("<Button Content=\"Go\"\n        Width=\"120\" />", "Button", "Height", "40"));
    }

    [Fact]
    public void Removes_an_attribute_with_the_space_before_it()
    {
        Assert.Equal("<Button Content=\"Go\" Height=\"40\" />", Remove("<Button Content=\"Go\" Width=\"120\" Height=\"40\" />", "Button", "Width"));
        Assert.Equal("<Button Content=\"Go\" />", Remove("<Button Content=\"Go\"\n        Width=\"120\" />", "Button", "Width"));
        Assert.Equal("<Button Width=\"120\" />", Remove("<Button Content=\"Go\" Width=\"120\" />", "Button", "Content"));
        Assert.Null(XamlAttributeEdits.Remove("<Button />", XamlMarkup.Parse("<Button />").Root!, "Width"));
    }

    [Theory]
    [InlineData("Width", true)]
    [InlineData("x:Name", true)]
    [InlineData("Grid.Row", true)]
    [InlineData("", false)]
    [InlineData("2Columns", false)]
    [InlineData("Grid.", false)]
    [InlineData("Has space", false)]
    public void Knows_which_names_can_be_attributes(string name, bool valid) =>
        Assert.Equal(valid, XamlAttributeEdits.IsValidName(name));

    [Fact]
    public void Puts_a_control_tapped_with_the_caret_in_a_panel_s_start_tag_last_in_the_panel()
    {
        var caret = Panel.IndexOf("<StackPanel", StringComparison.Ordinal) + 3;

        var (text, elementStart) = Insert(Panel, caret, "<Button Content=\"Button\" />");

        Assert.Equal("""
            <UserControl xmlns="https://github.com/avaloniaui">
              <StackPanel Spacing="8">
                <TextBlock Text="Hello" />
                <Button Content="Button" />
              </StackPanel>
            </UserControl>
            """, text);
        Assert.Equal(text.IndexOf("<Button", StringComparison.Ordinal), elementStart);
    }

    [Fact]
    public void Puts_a_control_after_the_element_whose_tag_has_the_caret()
    {
        var expected = """
            <UserControl xmlns="https://github.com/avaloniaui">
              <StackPanel Spacing="8">
                <TextBlock Text="Hello" />
                <Button />
              </StackPanel>
            </UserControl>
            """;

        Assert.Equal(expected, Insert(Panel, Panel.IndexOf("Hello", StringComparison.Ordinal), "<Button />").Text);
        Assert.Equal(expected, Insert(Panel, Panel.IndexOf("/>", StringComparison.Ordinal) + 2, "<Button />").Text);
    }

    [Fact]
    public void Puts_a_control_on_the_blank_line_with_the_caret()
    {
        var xaml = Panel.Replace("    <TextBlock Text=\"Hello\" />", "    <TextBlock Text=\"Hello\" />\n      \n    <TextBlock Text=\"Bye\" />", StringComparison.Ordinal);
        var caret = xaml.IndexOf("      \n", StringComparison.Ordinal) + 6;

        var text = Insert(xaml, caret, "<Button />").Text;

        Assert.Contains("    <TextBlock Text=\"Hello\" />\n    <Button />\n    <TextBlock Text=\"Bye\" />", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Opens_a_self_closing_panel_to_put_a_control_in_it()
    {
        const string xaml = "<UserControl xmlns=\"https://github.com/avaloniaui\">\n  <StackPanel Spacing=\"8\" />\n</UserControl>";

        var text = Insert(xaml, xaml.IndexOf("Spacing", StringComparison.Ordinal), "<Button />").Text;

        Assert.Equal("<UserControl xmlns=\"https://github.com/avaloniaui\">\n  <StackPanel Spacing=\"8\">\n    <Button />\n  </StackPanel>\n</UserControl>", text);
    }

    [Fact]
    public void Lays_out_a_nested_snippet_with_the_file_s_indentation()
    {
        const string xaml = "<UserControl xmlns=\"https://github.com/avaloniaui\">\n    <Grid>\n    </Grid>\n</UserControl>";

        var text = Insert(xaml, xaml.IndexOf("<Grid", StringComparison.Ordinal) + 2, "<ComboBox>\n  <ComboBoxItem Content=\"One\" />\n</ComboBox>").Text;

        Assert.Equal(
            "<UserControl xmlns=\"https://github.com/avaloniaui\">\n    <Grid>\n        <ComboBox>\n            <ComboBoxItem Content=\"One\" />\n        </ComboBox>\n    </Grid>\n</UserControl>",
            text);
    }

    [Fact]
    public void Puts_a_control_tapped_outside_the_root_or_in_a_comment_where_it_can_go()
    {
        var first = Insert(Panel, 0, "<Button />").Text;
        Assert.StartsWith("<UserControl xmlns=\"https://github.com/avaloniaui\">\n  <Button />\n  <StackPanel", first, StringComparison.Ordinal);

        var withComment = Panel.Replace("    <TextBlock", "    <!-- A note -->\n    <TextBlock", StringComparison.Ordinal);
        var afterComment = Insert(withComment, withComment.IndexOf("note", StringComparison.Ordinal), "<Button />").Text;
        Assert.Contains("<!-- A note -->\n    <Button />\n    <TextBlock", afterComment, StringComparison.Ordinal);

        Assert.Null(XamlToolboxInsertion.Insert("<!-- nothing yet -->", 0, "<Button />", _ => false));
    }

    private static string Apply(string text, XamlTextChange change) =>
        text[..change.Offset] + change.Text + text[(change.Offset + change.Length)..];

    private static string Set(string xaml, string element, string name, string value)
    {
        var target = XamlMarkup.Parse(xaml).Elements.Single(e => e.Name == element);
        return Apply(xaml, XamlAttributeEdits.Set(xaml, target, name, value));
    }

    private static string Remove(string xaml, string element, string name)
    {
        var target = XamlMarkup.Parse(xaml).Elements.Single(e => e.Name == element);
        return Apply(xaml, XamlAttributeEdits.Remove(xaml, target, name)!);
    }

    private static (string Text, int ElementStart) Insert(string xaml, int caret, string snippet)
    {
        var insertion = XamlToolboxInsertion.Insert(xaml, caret, snippet, name => name is "StackPanel" or "Grid");
        Assert.NotNull(insertion);
        return (Apply(xaml, insertion.Change), insertion.ElementStart);
    }
}
