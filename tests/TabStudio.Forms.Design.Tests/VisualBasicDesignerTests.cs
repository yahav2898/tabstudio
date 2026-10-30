using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Model;
using TabStudio.Forms.Design.Surface;
using GlobalImport = Microsoft.CodeAnalysis.VisualBasic.GlobalImport;
using VisualBasicBuild = TabStudio.Compilation.VisualBasic.VisualBasicCompiler;

namespace TabStudio.Forms.Design.Tests;

/// <summary>
/// Visual Basic forms: Form1.Designer.vb read and written back as Visual Studio writes it, byte for byte when nothing
/// changed; handlers wired by Handles clauses in Form1.vb; and the result compiled against the Forms layer.
/// </summary>
public sealed class VisualBasicDesignerTests
{
    [Fact]
    public void The_template_is_read_with_its_style_and_the_forms_properties()
    {
        var file = DesignerFile.Parse(VisualBasicSamples.Template, "Form", DesignerLanguage.VisualBasic);

        Assert.NotNull(file);
        Assert.Equal("Form1", file.ClassName);
        Assert.Equal(DesignerLanguage.VisualBasic, file.Language);
        var document = file.Document;
        Assert.Empty(document.Components);
        Assert.True(document.HasContainer);
        Assert.True(document.Style.QualifyWithThis);
        Assert.True(document.Style.FullTypeNames);
        Assert.Equal(typeof(Form), document.Root.Type);
        Assert.Equal("New System.Drawing.Size(800, 450)", document.Root.GetCode("ClientSize"));
        Assert.Equal("\"Form1\"", document.Root.GetCode("Text"));
        Assert.Equal("        ", document.Style.Indent);
    }

    [Fact]
    public void A_Me_qualified_file_is_read_with_its_components_values_events_and_other_statements()
    {
        var document = DesignerFile.Parse(VisualBasicSamples.Classic, "Form", DesignerLanguage.VisualBasic)!.Document;

        Assert.Equal(["Label1", "TextBox1", "Button1", "ToolTip1"], document.Components.Select(c => c.Name));
        Assert.True(document.Style.QualifyWithThis);
        Assert.Equal(typeof(ToolTip), document.Find("ToolTip1")!.Type);
        Assert.Equal("Me.components", document.Find("ToolTip1")!.ConstructorArguments);
        Assert.Equal("Friend WithEvents", document.Find("Button1")!.FieldModifiers);
        Assert.Equal(["Button1", "TextBox1", "Label1"], document.Root.Adds.Single(a => a.Collection == "Controls").Items);
        Assert.Equal("Button1_MouseEnter", document.Find("Button1")!.GetHandler("MouseEnter"));
        Assert.Contains("Me.ToolTip1.SetToolTip(Me.TextBox1, \"Your name\")", document.Find("TextBox1")!.OtherStatements);
        Assert.StartsWith("CType(((System.Windows.Forms.AnchorStyles.Top Or", document.Find("TextBox1")!.GetCode("Anchor"), StringComparison.Ordinal);

        // Visual Basic ignores case.
        Assert.Same(document.Find("Button1"), document.Find("BUTTON1"));
    }

    [Fact]
    public void A_file_without_InitializeComponent_is_not_a_designer_file() =>
        Assert.Null(DesignerFile.Parse("Public Class Form1\n    Sub Foo()\n    End Sub\nEnd Class\n", "Form", DesignerLanguage.VisualBasic));

    [Fact]
    public void Designer_files_Visual_Studio_wrote_are_written_back_byte_for_byte()
    {
        foreach (var sample in new[] { VisualBasicSamples.Button, VisualBasicSamples.Classic })
        {
            foreach (var text in new[] { sample, sample.ReplaceLineEndings("\r\n") })
            {
                var file = DesignerFile.Parse(text, "Form", DesignerLanguage.VisualBasic)!;

                Assert.Equal(text, DesignerWriter.Write(file, file.Document));
            }
        }
    }

    [AvaloniaFact]
    public void The_form_is_built_from_a_Visual_Basic_designer_file()
    {
        var surface = new DesignSurface(VisualBasicSamples.Classic, "Form", simplified: false, DesignerLanguage.VisualBasic, VisualBasicSamples.ClassicCode);

        Assert.Null(surface.LoadError);
        Assert.Empty(surface.Problems);
        Assert.Equal(VisualBasicSamples.Classic, surface.Text);
        var form = Assert.IsType<Form>(surface.Root);
        Assert.Equal("Greeter", form.Text);
        Assert.Equal(new Size(284, 85), form.ClientSize);
        var button = (Button)form.Controls["Button1"]!;
        Assert.Equal("Say \"hi\"", button.Text);
        Assert.Equal(new Rectangle(197, 50, 75, 23), button.Bounds);
        Assert.Equal(FontStyle.Bold, button.Font.Style);
        Assert.Equal(Color.FromArgb(0, 120, 215).ToArgb(), button.BackColor.ToArgb());
        Assert.Equal(AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right, ((TextBox)form.Controls["TextBox1"]!).Anchor);
        Assert.True(((Label)form.Controls["Label1"]!).AutoSize);

        // The Handles clause in Form1.vb wires Click; the designer file's AddHandler wires MouseEnter.
        var component = surface.Document!.Find("Button1")!;
        Assert.Equal("Button1_Click", component.GetHandler("Click"));
        Assert.True(component.Events.Single(e => e.Event == "Click").IsHandlesClause);
        Assert.Equal("Button1_MouseEnter", component.GetHandler("MouseEnter"));
    }

    [AvaloniaFact]
    public void Moving_and_resizing_change_only_the_bounds_lines_and_undo_restores_the_file()
    {
        var surface = new DesignSurface(VisualBasicSamples.Button, "Form", simplified: true, DesignerLanguage.VisualBasic);
        var original = surface.Text;

        surface.SetBounds([(surface.Document!.Find("Button1")!, new Rectangle(200, 160, 136, 40))]);

        var expected = original
            .Replace("Button1.Location = New Point(12, 12)", "Button1.Location = New Point(200, 160)", StringComparison.Ordinal)
            .Replace("Button1.Size = New Size(94, 29)", "Button1.Size = New Size(136, 40)", StringComparison.Ordinal);
        Assert.Equal(expected, surface.Text);
        Assert.Equal(new Rectangle(200, 160, 136, 40), surface.Root!.Controls["Button1"]!.Bounds);

        surface.Undo();
        Assert.Equal(original, surface.Text);
    }

    [AvaloniaFact]
    public void A_button_added_to_the_template_is_written_as_Visual_Studio_writes_Visual_Basic()
    {
        var surface = new DesignSurface(VisualBasicSamples.Template, "Form", simplified: true, DesignerLanguage.VisualBasic, VisualBasicSamples.TemplateCode);

        var name = surface.Add(typeof(Button), new Point(27, 30));

        Assert.Equal("Button1", name);
        var text = Lines(surface.Text);
        Assert.Contains(Lines("""
                    components = New System.ComponentModel.Container()
                    Button1 = New Button()
                    SuspendLayout()
                    '
                    ' Button1
                    '
                    Button1.Location = New Point(24, 32)
                    Button1.Name = "Button1"
            """), text, StringComparison.Ordinal);
        Assert.Contains("        Button1.TabIndex = 0\n", text, StringComparison.Ordinal);
        Assert.Contains("        Button1.Text = \"Button1\"\n", text, StringComparison.Ordinal);
        Assert.Contains("        Button1.UseVisualStyleBackColor = True\n", text, StringComparison.Ordinal);
        Assert.Contains(Lines("""
                    '
                    ' Form1
                    '
                    AutoScaleDimensions = New SizeF(7F, 15F)
                    AutoScaleMode = AutoScaleMode.Font
                    ClientSize = New Size(800, 450)
                    Controls.Add(Button1)
                    Name = "Form1"
                    Text = "Form1"
                    ResumeLayout(False)
                End Sub

                Friend WithEvents Button1 As Button

            End Class
            """), text, StringComparison.Ordinal);
        Assert.Contains("        ' Button1\n", surface.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Me.", surface.Text.Replace("MyBase.", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Compiles(surface.Text, VisualBasicSamples.TemplateCode);
    }

    [AvaloniaFact]
    public void A_text_box_added_from_the_Toolbox_gets_its_field_after_the_others_and_its_statements_in_order()
    {
        var surface = new DesignSurface(VisualBasicSamples.Button, "Form", simplified: true, DesignerLanguage.VisualBasic, VisualBasicSamples.ButtonCode);

        var name = surface.Add(typeof(TextBox), new Point(27, 70));

        Assert.Equal("TextBox1", name);
        var text = Lines(surface.Text);
        Assert.Contains(Lines("""
                    Button1 = New Button()
                    TextBox1 = New TextBox()
                    SuspendLayout()
            """), text, StringComparison.Ordinal);
        Assert.Contains(Lines("""
                    '
                    ' TextBox1
                    '
                    TextBox1.Location = New Point(24, 72)
                    TextBox1.Name = "TextBox1"
            """), text, StringComparison.Ordinal);
        Assert.Contains("        TextBox1.TabIndex = 1\n", text, StringComparison.Ordinal);
        Assert.Contains(Lines("""
                    Controls.Add(TextBox1)
                    Controls.Add(Button1)
            """), text, StringComparison.Ordinal);
        Assert.Contains(Lines("""
                End Sub

                Friend WithEvents Button1 As Button
                Friend WithEvents TextBox1 As TextBox

            End Class
            """), text, StringComparison.Ordinal);
        Compiles(surface.Text, VisualBasicSamples.ButtonCode);
    }

    [AvaloniaFact]
    public void A_double_tap_handler_is_added_to_the_code_file_with_a_Handles_clause()
    {
        var surface = new DesignSurface(VisualBasicSamples.Template, "Form", simplified: true, DesignerLanguage.VisualBasic, VisualBasicSamples.TemplateCode);
        var name = surface.Add(typeof(TextBox), new Point(16, 16));
        var box = surface.Document!.Find(name)!;
        var handler = CodeBehind.HandlerName(name, "TextChanged");
        var designer = surface.Text;

        var (code, caret) = surface.AttachHandler(VisualBasicSamples.TemplateCode, box, "TextChanged", handler, CodeBehind.DelegateTypeOf(typeof(TextBox), "TextChanged")!);

        Assert.Equal(Lines("""
            Public Class Form1

                Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs) Handles TextBox1.TextChanged

                End Sub
            End Class
            """), Lines(code));

        // The Handles clause wires it: the designer file does not change, and the event shows the handler.
        Assert.Equal(designer, surface.Text);
        Assert.Equal("TextBox1_TextChanged", surface.Document!.Find(name)!.GetHandler("TextChanged"));

        // The caret goes on the empty line inside the handler, after its indentation.
        var lineStart = code.LastIndexOf('\n', caret - 1) + 1;
        Assert.True(string.IsNullOrWhiteSpace(code[lineStart..caret]));
        Assert.EndsWith("Handles TextBox1.TextChanged", code[..lineStart].TrimEnd(), StringComparison.Ordinal);

        // A second double-tap goes to the handler that is there.
        var (again, caretAgain) = surface.AttachHandler(code, surface.Document!.Find(name)!, "TextChanged", handler, typeof(EventHandler));
        Assert.Equal(code, again);
        Assert.Equal(caret, caretAgain);

        // The form's own event is handled from MyBase, after a blank line.
        var (withLoad, _) = surface.AttachHandler(code, surface.Document!.Root, "Load", CodeBehind.HandlerName("Form1", "Load"), typeof(EventHandler));
        Assert.Contains(Lines("""
                End Sub

                Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load

                End Sub
            End Class
            """), Lines(withLoad), StringComparison.Ordinal);
        Assert.Equal("Form1_Load", surface.Document!.Root.GetHandler("Load"));
        Compiles(surface.Text, withLoad);

        // Clearing the event takes it off the Handles clause and leaves the handler.
        var cleared = surface.DetachHandler(withLoad, surface.Document!.Find(name)!, "TextChanged");
        Assert.Contains("    Private Sub TextBox1_TextChanged(sender As Object, e As EventArgs)\n", Lines(cleared), StringComparison.Ordinal);
        Assert.DoesNotContain("Handles TextBox1", cleared, StringComparison.Ordinal);
        Assert.Null(surface.Document!.Find(name)!.GetHandler("TextChanged"));
        Compiles(surface.Text, cleared);
    }

    [AvaloniaFact]
    public void Renaming_a_control_renames_its_field_its_Handles_clauses_and_the_handlers_named_after_it()
    {
        var surface = new DesignSurface(VisualBasicSamples.Button, "Form", simplified: true, DesignerLanguage.VisualBasic, VisualBasicSamples.ButtonCode);
        Assert.Equal("Button1_Click", surface.Document!.Find("Button1")!.GetHandler("Click"));

        surface.Rename(surface.Document!.Find("Button1")!, "OkButton");
        var code = surface.RenameInCode(VisualBasicSamples.ButtonCode, "Button1", "OkButton");

        var text = surface.Text;
        Assert.Contains("    Friend WithEvents OkButton As Button\n", text, StringComparison.Ordinal);
        Assert.Contains("        OkButton = New Button()\n", text, StringComparison.Ordinal);
        Assert.Contains("        OkButton.Name = \"OkButton\"\n", text, StringComparison.Ordinal);
        Assert.Contains("        OkButton.Text = \"Button1\"\n", text, StringComparison.Ordinal);
        Assert.Contains("        Controls.Add(OkButton)\n", text, StringComparison.Ordinal);
        Assert.Contains("    Private Sub OkButton_Click(sender As Object, e As EventArgs) Handles OkButton.Click\n", code, StringComparison.Ordinal);
        Assert.Contains("        OkButton.Text = \"Clicked\"\n", code, StringComparison.Ordinal);
        Assert.Equal("OkButton_Click", surface.Document!.Find("OkButton")!.GetHandler("Click"));
        Assert.Throws<ArgumentException>(() => surface.Rename(surface.Document!.Find("OkButton")!, "Class"));
        Compiles(text, code);
    }

    [AvaloniaFact]
    public void Deleting_a_control_takes_it_off_the_Handles_clauses_and_leaves_its_handler()
    {
        var surface = new DesignSurface(VisualBasicSamples.Button, "Form", simplified: true, DesignerLanguage.VisualBasic, VisualBasicSamples.ButtonCode);
        IReadOnlyList<string>? deleted = null;
        surface.Deleted += (_, names) => deleted = names;

        surface.Delete([surface.Document!.Find("Button1")!]);

        Assert.NotNull(deleted);
        Assert.Equal(["Button1"], deleted);
        var code = surface.ForgetHandlers(VisualBasicSamples.ButtonCode, deleted);
        Assert.DoesNotContain("Button1", surface.Text, StringComparison.Ordinal);
        Assert.Contains("    Private Sub Button1_Click(sender As Object, e As EventArgs)\n", code, StringComparison.Ordinal);
        Assert.DoesNotContain("Handles", code, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void Properties_set_in_a_Me_qualified_file_are_written_in_its_style()
    {
        var surface = new DesignSurface(VisualBasicSamples.Classic, "Form", simplified: false, DesignerLanguage.VisualBasic, VisualBasicSamples.ClassicCode);

        surface.SetProperty(surface.Document!.Find("TextBox1")!, "Anchor", AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
        surface.SetProperty(surface.Document!.Find("TextBox1")!, "BackColor", Color.LightYellow);
        surface.SetProperty(surface.Document!.Find("TextBox1")!, "Font", new Font("Segoe UI", 12F, FontStyle.Italic, GraphicsUnit.Point, 0));
        surface.SetProperty(surface.Document!.Find("Button1")!, "BackColor", Color.FromArgb(255, 128, 0));
        surface.SetProperty(surface.Document!.Find("Label1")!, "Text", "Line 1\r\nLine \"2\"");

        var text = surface.Text;
        Assert.Contains("        Me.TextBox1.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)\n", text, StringComparison.Ordinal);
        Assert.Contains("        Me.TextBox1.BackColor = System.Drawing.Color.LightYellow\n", text, StringComparison.Ordinal);
        Assert.Contains("        Me.TextBox1.Font = New System.Drawing.Font(\"Segoe UI\", 12.0!, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point, CType(0, Byte))\n", text, StringComparison.Ordinal);
        Assert.Contains("        Me.Button1.BackColor = System.Drawing.Color.FromArgb(CType(CType(255, Byte), Integer), CType(CType(128, Byte), Integer), CType(CType(0, Byte), Integer))\n", text, StringComparison.Ordinal);
        Assert.Contains("        Me.Label1.Text = \"Line 1\" & Global.Microsoft.VisualBasic.ChrW(13) & Global.Microsoft.VisualBasic.ChrW(10) & \"Line \"\"2\"\"\"\n", text, StringComparison.Ordinal);
        Assert.Contains("        Me.ToolTip1.SetToolTip(Me.TextBox1, \"Your name\")\n", text, StringComparison.Ordinal);
        Assert.Contains("        AddHandler Me.Button1.MouseEnter, AddressOf Me.Button1_MouseEnter\n", text, StringComparison.Ordinal);
        Assert.Contains("    End Sub\n    Friend WithEvents Label1 As System.Windows.Forms.Label\n", text, StringComparison.Ordinal);

        // What the designer wrote reads back to the same values.
        Assert.Empty(surface.Problems);
        Assert.Equal("Line 1\r\nLine \"2\"", surface.Root!.Controls["Label1"]!.Text);
        Assert.Equal(AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right, surface.Root!.Controls["TextBox1"]!.Anchor);
        Compiles(text, VisualBasicSamples.ClassicCode);
    }

    [AvaloniaFact]
    public void A_Me_qualified_file_is_written_simplified_in_a_project_that_imports_Windows_Forms()
    {
        var surface = new DesignSurface(VisualBasicSamples.Classic, "Form", simplified: true, DesignerLanguage.VisualBasic, VisualBasicSamples.ClassicCode);

        surface.SetBounds([(surface.Document!.Find("Button1")!, new Rectangle(200, 48, 75, 23))]);

        var text = surface.Text;
        Assert.Contains("        components = New System.ComponentModel.Container()\n", text, StringComparison.Ordinal);
        Assert.Contains("        ToolTip1 = New ToolTip(components)\n", text, StringComparison.Ordinal);
        Assert.Contains("        SuspendLayout()\n", text, StringComparison.Ordinal);
        Assert.Contains("        Button1.Location = New Point(200, 48)\n", text, StringComparison.Ordinal);
        Assert.Contains("        ToolTip1.SetToolTip(TextBox1, \"Your name\")\n", text, StringComparison.Ordinal);
        Assert.Contains("        AddHandler Button1.MouseEnter, AddressOf Button1_MouseEnter\n", text, StringComparison.Ordinal);
        Assert.Contains("        Controls.Add(Button1)\n", text, StringComparison.Ordinal);
        Assert.Contains("    Friend WithEvents Button1 As Button\n", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Me.", text.Replace("MyBase.", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Compiles(text, VisualBasicSamples.ClassicCode);
    }

    [AvaloniaFact]
    public void Menu_items_typed_in_a_menu_strip_are_named_as_Visual_Studio_names_them_in_Visual_Basic()
    {
        var surface = new DesignSurface(VisualBasicSamples.Template, "Form", simplified: true, DesignerLanguage.VisualBasic, VisualBasicSamples.TemplateCode);
        var strip = surface.Add(typeof(MenuStrip), new Point(40, 40));

        var file = surface.AddItem(surface.Document!.Find(strip)!, typeof(ToolStripMenuItem), "&File");
        var open = surface.AddItem(surface.Document!.Find(file)!, typeof(ToolStripMenuItem), "Open...");

        Assert.Equal("MenuStrip1", strip);
        Assert.Equal("FileToolStripMenuItem", file);
        Assert.Equal("OpenToolStripMenuItem", open);
        var text = surface.Text;
        Assert.Contains("        MenuStrip1.Items.AddRange(New ToolStripItem() {FileToolStripMenuItem})\n", text, StringComparison.Ordinal);
        Assert.Contains("        FileToolStripMenuItem.DropDownItems.AddRange(New ToolStripItem() {OpenToolStripMenuItem})\n", text, StringComparison.Ordinal);
        Assert.Contains("        MainMenuStrip = MenuStrip1\n", text, StringComparison.Ordinal);
        Assert.Contains("    Friend WithEvents FileToolStripMenuItem As ToolStripMenuItem\n", text, StringComparison.Ordinal);
        var menu = (MenuStrip)surface.Document!.Find(strip)!.Instance!;
        Assert.Equal("&File", Assert.Single(menu.Items.Cast<ToolStripItem>()).Text);
        Compiles(text, VisualBasicSamples.TemplateCode);
    }

    /// <summary>The text with its line endings as <c>\n</c> and no spaces at their ends, for comparing blocks of lines.</summary>
    private static string Lines(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd()));

    /// <summary>
    /// Compiles a form's designer file and code file as Visual Basic against the Forms layer, with the imports of a
    /// Windows Forms project, failing the test on an error.
    /// </summary>
    private static void Compiles(string designer, string code)
    {
        var compilation = VisualBasicBuild.CreateCompilation(
            new CompilationRequest
            {
                AssemblyName = "Designed",
                Kind = AssemblyKind.Library,
                Documents =
                [
                    new SourceDocument("Form1.Designer.vb", designer),
                    new SourceDocument("Form1.vb", code),
                ],
            },
            [.. ReferenceAssemblies.Net100, MetadataReference.CreateFromFile(typeof(Form).Assembly.Location)]);
        string[] imports = [.. VisualBasicBuild.DefaultImports, .. VisualBasicBuild.WindowsFormsImports];
        compilation = compilation.WithOptions(compilation.Options.WithGlobalImports(GlobalImport.Parse(imports)));

        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        var success = errors.Count == 0;
        Assert.True(success, string.Join('\n', errors) + "\n" + designer + "\n" + code);
    }
}
