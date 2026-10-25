using TabStudio.App.Xaml;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>Reading XAML as text, finished or being typed, for the Toolbox and the Properties panel.</summary>
public sealed class XamlMarkupTests
{
    private const string Form = """
        <UserControl xmlns="https://github.com/avaloniaui"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <!-- The form: <Button /> in a comment is not an element. -->
          <Grid RowDefinitions="Auto,*">
            <Grid.Styles>
            </Grid.Styles>
            <TextBlock Text="Name" />
            <Button Grid.Row="1" Content="Save &amp; close" x:Name="Save" />
          </Grid>
        </UserControl>
        """;

    [Fact]
    public void Reads_elements_attributes_and_namespaces_and_skips_comments()
    {
        var markup = XamlMarkup.Parse(Form);

        Assert.Equal(["UserControl", "Grid", "Grid.Styles", "TextBlock", "Button"], markup.Elements.Select(e => e.Name));
        var root = Assert.IsType<XamlElement>(markup.Root);
        var grid = Assert.Single(root.Children);
        Assert.Equal(["Grid.Styles", "TextBlock", "Button"], grid.Children.Select(e => e.Name));
        Assert.True(grid.Children[0].IsPropertyElement);

        var button = grid.Children[2];
        Assert.True(button.IsSelfClosing);
        Assert.Equal(["Grid.Row", "Content", "x:Name"], button.Attributes.Select(a => a.Name));
        Assert.Equal("Save & close", button.FindAttribute("Content")!.Value);
        Assert.Equal("https://github.com/avaloniaui", XamlMarkup.NamespaceOf(button));
        Assert.True(root.Attributes[1].IsNamespaceDeclaration);
        Assert.Equal(Form.IndexOf("</Grid>", StringComparison.Ordinal), grid.EndTagStart);
    }

    [Fact]
    public void Finds_the_element_at_the_caret()
    {
        var markup = XamlMarkup.Parse(Form);

        Assert.Equal("Button", markup.ElementAt(Form.IndexOf("Save &amp;", StringComparison.Ordinal))!.Name);
        Assert.Equal("Grid", markup.ElementAt(Form.IndexOf("Auto,*", StringComparison.Ordinal))!.Name);

        // In the indentation of a line that starts a tag, the caret is on that element.
        var textBlockLine = Form.IndexOf("    <TextBlock", StringComparison.Ordinal);
        Assert.Equal("TextBlock", markup.ElementAt(textBlockLine + 1)!.Name);

        // In a property element, the properties are those of the element it belongs to.
        var styles = Form.IndexOf("</Grid.Styles>", StringComparison.Ordinal) - 2;
        Assert.Equal("Grid.Styles", markup.ElementAt(styles)!.Name);
        Assert.Equal("Grid", markup.ObjectAt(styles)!.Name);

        Assert.Null(markup.ElementAt(Form.Length));
        Assert.NotNull(markup.OtherMarkupAt(Form.IndexOf("<Button />", StringComparison.Ordinal)));
    }

    [Fact]
    public void Reads_XAML_that_is_being_typed()
    {
        const string typing = """
            <StackPanel>
              <Button Content="Save"
              <TextBlock Text="x" />
              <Border>
            </StackPanel>
            """;

        var markup = XamlMarkup.Parse(typing);

        Assert.Equal(["StackPanel", "Button", "TextBlock", "Border"], markup.Elements.Select(e => e.Name));
        var button = markup.Elements[1];
        Assert.False(button.IsStartTagComplete);
        Assert.Equal("Save", button.FindAttribute("Content")!.Value);
        Assert.All(markup.Elements.Skip(1), e => Assert.Same(markup.Root, e.Parent));

        // The unclosed Border ends where its parent's end tag starts.
        var border = markup.Elements[3];
        Assert.Null(border.EndTagStart);
        Assert.Equal(typing.IndexOf("</StackPanel>", StringComparison.Ordinal), border.End);
    }

    [Theory]
    [InlineData("Plain", "Plain")]
    [InlineData("A &lt; B &amp;&amp; C &gt; D", "A < B && C > D")]
    [InlineData("&quot;quoted&quot; &apos;too&apos;", "\"quoted\" 'too'")]
    [InlineData("&#65;&#x42;", "AB")]
    [InlineData("&unknown; stays", "&unknown; stays")]
    public void Decodes_character_references(string written, string value) =>
        Assert.Equal(value, XamlMarkup.Decode(written));

    [Fact]
    public void Learns_the_indentation_of_the_file()
    {
        Assert.Equal("  ", XamlTextLayout.IndentUnit(XamlMarkup.Parse(Form)));
        Assert.Equal("\t", XamlTextLayout.IndentUnit(XamlMarkup.Parse("<Grid>\n\t<Button />\n</Grid>")));
        Assert.Equal(XamlTextLayout.DefaultIndentUnit, XamlTextLayout.IndentUnit(XamlMarkup.Parse("<Grid><Button /></Grid>")));
    }
}
