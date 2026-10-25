using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Surface;

namespace TabStudio.Forms.Design.Tests;

/// <summary>Reading designer files with Roslyn, and building their forms without running the app's code.</summary>
public sealed class DesignerFileTests
{
    [Fact]
    public void The_template_is_read_with_its_style_and_the_forms_properties()
    {
        var file = DesignerFile.Parse(Samples.Template);

        Assert.NotNull(file);
        Assert.Equal("Form1", file.ClassName);
        Assert.Equal("HelloForms", file.Namespace);
        var document = file.Document;
        Assert.Empty(document.Components);
        Assert.True(document.HasContainer);
        Assert.True(document.Style.QualifyWithThis);
        Assert.Equal("new System.Drawing.Size(800, 450)", document.Root.GetCode("ClientSize"));
        Assert.Equal("\"Form1\"", document.Root.GetCode("Text"));
        Assert.Equal("            ", document.Style.Indent[..12]);
    }

    [Fact]
    public void Components_properties_events_collections_and_other_statements_are_read()
    {
        var document = DesignerFile.Parse(Samples.Orders)!.Document;

        Assert.Equal(["menuStrip1", "fileMenuItem", "exitMenuItem", "detailsGroup", "nameLabel", "nameTextBox", "saveButton", "toolTip1"], document.Components.Select(c => c.Name));
        var toolTip = document.Find("toolTip1")!;
        Assert.Equal(typeof(ToolTip), toolTip.Type);
        Assert.Equal("components", toolTip.ConstructorArguments);

        var group = document.Find("detailsGroup")!;
        Assert.Equal(["nameTextBox", "nameLabel"], group.Adds.Single(a => a.Collection == "Controls").Items);
        var menu = document.Find("menuStrip1")!.Adds.Single();
        Assert.True(menu.IsRange);
        Assert.Equal("ToolStripItem", menu.RangeType);

        Assert.Equal("exitMenuItem_Click", document.Find("exitMenuItem")!.GetHandler("Click"));
        Assert.Equal("Form1_Load", document.Root.GetHandler("Load"));
        Assert.Equal("AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right", document.Find("nameTextBox")!.GetCode("Anchor"));
        Assert.Contains("toolTip1.SetToolTip(nameTextBox, \"The customer's name, as on the invoice.\");", document.Find("nameTextBox")!.OtherStatements);
        Assert.Equal("saveButton", document.Root.GetCode("AcceptButton"));
    }

    [Fact]
    public void A_file_without_InitializeComponent_is_not_a_designer_file() =>
        Assert.Null(DesignerFile.Parse("namespace A { class B { void C() { } } }"));

    [Fact]
    public void Writing_an_unchanged_file_gives_the_same_designer_code()
    {
        var file = DesignerFile.Parse(Samples.Orders)!;

        var written = DesignerWriter.Write(file, file.Document);

        Assert.Equal(Normalize(Samples.Orders), Normalize(written));
    }

    [AvaloniaFact]
    public void The_form_is_built_from_the_designer_file_with_the_Forms_layers_controls()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);

        Assert.Null(surface.LoadError);
        var form = Assert.IsType<Form>(surface.Root);
        Assert.Equal("Orders", form.Text);
        Assert.Equal(new Size(400, 200), form.ClientSize);
        var group = (GroupBox)form.Controls["detailsGroup"]!;
        Assert.Equal(new Rectangle(12, 36, 240, 100), group.Bounds);
        var box = (TextBox)group.Controls["nameTextBox"]!;
        Assert.Equal(AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, box.Anchor);
        var save = (Button)form.Controls["saveButton"]!;
        Assert.Equal(FontStyle.Bold, save.Font.Style);
        Assert.Equal(Color.FromArgb(0, 120, 215).ToArgb(), save.BackColor.ToArgb());
        var menu = (MenuStrip)form.Controls["menuStrip1"]!;
        Assert.Equal("&File", menu.Items[0].Text);
        Assert.Equal("E&xit", ((ToolStripMenuItem)menu.Items[0]).DropDownItems[0].Text);
        Assert.Empty(surface.Problems);
    }

    [AvaloniaFact]
    public void A_control_of_a_type_the_designer_does_not_know_is_a_placeholder_of_its_size()
    {
        var text = Samples.Orders
            .Replace("saveButton = new Button();", "saveButton = new Contoso.Controls.FancyButton();", StringComparison.Ordinal)
            .Replace("private Button saveButton;", "private Contoso.Controls.FancyButton saveButton;", StringComparison.Ordinal);

        var surface = new DesignSurface(text, "Form", simplified: true);

        var placeholder = Assert.IsType<PlaceholderControl>(surface.Root!.Controls["saveButton"]);
        Assert.Equal(new Rectangle(264, 152, 120, 32), placeholder.Bounds);
        Assert.Equal("Contoso.Controls.FancyButton", placeholder.TypeName);
    }

    [AvaloniaFact]
    public void Hit_testing_finds_the_innermost_control_and_strip_items()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);

        Assert.Equal("nameTextBox", surface.HitTest(new Point(12 + 80 + 10, 36 + 24 + 5))!.Name);
        Assert.Equal("detailsGroup", surface.HitTest(new Point(12 + 4, 36 + 90))!.Name);
        Assert.Equal("Form1", surface.HitTest(new Point(390, 190))!.Name);
        Assert.Equal("fileMenuItem", surface.HitTest(new Point(20, 10))!.Name);
    }

    private static string Normalize(string text) => string.Join('\n', text.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n').Select(l => l.TrimEnd()));
}
