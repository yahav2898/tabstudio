using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;
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
    public void Statements_about_fields_the_designer_did_not_create_are_kept()
    {
        var source = Samples.Template
            .Replace("private System.ComponentModel.IContainer components = null;", "private System.ComponentModel.IContainer components = null;\n        private int counter;\n        private System.Windows.Forms.Button helper;", StringComparison.Ordinal)
            .Replace("this.Text = \"Form1\";", "this.Text = \"Form1\";\n            this.counter = 5;\n            this.helper = CreateButton();\n            this.helper.Text = \"Help\";\n            this.helper.Click += this.helper_Click;", StringComparison.Ordinal);
        var file = DesignerFile.Parse(source)!;

        var written = DesignerWriter.Write(file, file.Document);

        Assert.Contains("this.counter = 5;", written, StringComparison.Ordinal);
        Assert.Contains("this.helper = CreateButton();", written, StringComparison.Ordinal);
        Assert.Contains("this.helper.Text = \"Help\";", written, StringComparison.Ordinal);
        Assert.Contains("this.helper.Click += this.helper_Click;", written, StringComparison.Ordinal);
        Assert.True(written.IndexOf("CreateButton()", StringComparison.Ordinal) < written.IndexOf("\"Help\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Components_without_a_default_event_list_their_events_without_failing()
    {
        Assert.Null(Catalog.ComponentDefaults.DefaultEvent(typeof(ImageList)));
        Assert.Null(Catalog.ComponentDefaults.DefaultEvent(typeof(ToolTip)));
        _ = Catalog.PropertyCatalog.EventsOf(typeof(ToolTip));
        Assert.Equal("Click", Catalog.ComponentDefaults.DefaultEvent(typeof(Button)));
    }

    [Fact]
    public void Line_separators_and_floats_are_written_as_valid_literals()
    {
        Assert.Equal("\"a\\u2028b\"", ValueWriter.Literal("a\u2028b"));
        Assert.DoesNotContain('\u2028', VisualBasicValueWriter.Literal("a\u2028b"));
        Assert.Equal("9.9F", ValueWriter.Write(9.9F, CodeStyle.Simplified));
    }

    [Fact]
    public void Components_declared_together_are_declared_once()
    {
        var source = Samples.Template
            .Replace("#region Windows Form Designer generated code", "private System.Windows.Forms.Button okButton, cancelButton;\n\n        #region Windows Form Designer generated code", StringComparison.Ordinal)
            .Replace("this.Text = \"Form1\";", "this.Text = \"Form1\";\n            this.okButton = new System.Windows.Forms.Button();\n            this.cancelButton = new System.Windows.Forms.Button();", StringComparison.Ordinal);
        var file = DesignerFile.Parse(source)!;

        var written = DesignerWriter.Write(file, file.Document);

        Assert.DoesNotContain("okButton, cancelButton;", written, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(written, @"Button okButton;"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(written, @"Button cancelButton;"));
    }

    [Fact]
    public void Property_and_event_names_written_in_another_case_are_the_same_member()
    {
        var button = new DesignComponent("Button1", "System.Windows.Forms.Button");
        button.SetCode("text", "\"Old\"");
        button.SetHandler("click", "Button1_Click");

        button.SetCode("Text", "\"New\"");
        button.SetHandler("Click", "Button1_Click");

        Assert.Equal("\"New\"", button.GetCode("Text"));
        Assert.Single(button.Properties);
        Assert.Single(button.Events);
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
