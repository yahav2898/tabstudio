using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using Microsoft.CodeAnalysis;
using TabStudio.Compilation;
using TabStudio.Forms.Design.Code;
using TabStudio.Forms.Design.Surface;

namespace TabStudio.Forms.Design.Tests;

/// <summary>
/// The designer's edits, written back into the designer file as Visual Studio writes it, and compiled against the
/// Forms layer to prove the file still builds.
/// </summary>
public sealed class DesignSurfaceTests
{
    [AvaloniaFact]
    public void A_button_added_to_the_template_is_written_as_Visual_Studio_writes_it()
    {
        var surface = new DesignSurface(Samples.Template, "Form", simplified: true);

        var name = surface.Add(typeof(Button), new Point(27, 30));

        Assert.Equal("button1", name);
        var text = surface.Text;
        Assert.Contains(Lines("""
                        components = new System.ComponentModel.Container();
                        button1 = new Button();
                        SuspendLayout();
                        //
                        // button1
                        //
                        button1.Location = new Point(24, 32);
                        button1.Name = "button1";
            """), Lines(text), StringComparison.Ordinal);
        Assert.Contains("button1.TabIndex = 0;", text, StringComparison.Ordinal);
        Assert.Contains("button1.Text = \"button1\";", text, StringComparison.Ordinal);
        Assert.Contains("button1.UseVisualStyleBackColor = true;", text, StringComparison.Ordinal);
        Assert.Contains(Lines("""
                        //
                        // Form1
                        //
                        AutoScaleDimensions = new SizeF(7F, 15F);
                        AutoScaleMode = AutoScaleMode.Font;
                        ClientSize = new Size(800, 450);
                        Controls.Add(button1);
                        Name = "Form1";
                        Text = "Form1";
                        ResumeLayout(false);
                    }

                    #endregion

                    private Button button1;
                }
            """), Lines(text), StringComparison.Ordinal);
        Assert.DoesNotContain("this.", text, StringComparison.Ordinal);
        Compiles(text, Samples.TemplateCode);
    }

    [AvaloniaFact]
    public void Moving_and_resizing_write_the_new_bounds_and_undo_and_redo_restore_them()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);
        var original = surface.Text;
        var button = surface.Document!.Find("saveButton")!;

        surface.SetBounds([(button, new Rectangle(200, 160, 136, 40))]);

        Assert.Contains("saveButton.Location = new Point(200, 160);", surface.Text, StringComparison.Ordinal);
        Assert.Contains("saveButton.Size = new Size(136, 40);", surface.Text, StringComparison.Ordinal);
        Assert.Equal(new Rectangle(200, 160, 136, 40), surface.Root!.Controls["saveButton"]!.Bounds);
        var moved = surface.Text;

        surface.Undo();
        Assert.Equal(original, surface.Text);
        Assert.Equal(new Rectangle(264, 152, 120, 32), surface.Root!.Controls["saveButton"]!.Bounds);
        Assert.True(surface.CanRedo);

        surface.Redo();
        Assert.Equal(moved, surface.Text);
    }

    [AvaloniaFact]
    public void Deleting_a_container_deletes_its_controls_and_the_code_that_names_them()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);

        surface.Delete([surface.Document!.Find("detailsGroup")!]);

        var text = surface.Text;
        foreach (var gone in new[] { "detailsGroup", "nameLabel", "nameTextBox" })
        {
            Assert.DoesNotContain(gone, text, StringComparison.Ordinal);
        }

        // The tooltip stays; only its call for the deleted text box went with it.
        Assert.Contains("toolTip1 = new ToolTip(components);", text, StringComparison.Ordinal);
        Assert.Null(surface.Root!.Controls["detailsGroup"]);
        Compiles(text, Samples.OrdersCode.Replace("nameTextBox.Text = \"Contoso\";", string.Empty, StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void Properties_are_written_in_Visual_Studios_form_and_defaults_are_left_out()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);
        var box = surface.Document!.Find("nameTextBox")!;

        surface.SetProperty(box, "Anchor", AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right);
        surface.SetProperty(surface.Document!.Find("nameTextBox")!, "BackColor", Color.LightYellow);
        surface.SetProperty(surface.Document!.Find("nameTextBox")!, "Font", new Font("Segoe UI", 12F, FontStyle.Italic, GraphicsUnit.Point, 0));
        surface.SetProperty(surface.Document!.Find("nameTextBox")!, "Enabled", false);
        surface.SetProperty(surface.Document!.Find("saveButton")!, "ForeColor", SystemColors.ControlText);

        var text = surface.Text;
        Assert.Contains("nameTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;", text, StringComparison.Ordinal);
        Assert.Contains("nameTextBox.BackColor = Color.LightYellow;", text, StringComparison.Ordinal);
        Assert.Contains("nameTextBox.Font = new Font(\"Segoe UI\", 12F, FontStyle.Italic, GraphicsUnit.Point, 0);", text, StringComparison.Ordinal);
        Assert.Contains("nameTextBox.Enabled = false;", text, StringComparison.Ordinal);
        Assert.DoesNotContain("saveButton.ForeColor", text, StringComparison.Ordinal);

        // Back to the default, the property leaves the file.
        surface.SetProperty(surface.Document!.Find("nameTextBox")!, "Enabled", true);
        Assert.DoesNotContain("nameTextBox.Enabled", surface.Text, StringComparison.Ordinal);
        Assert.False(((TextBox)surface.Root!.Controls.Find("nameTextBox", true)[0]).Font.Bold);
        Compiles(surface.Text, Samples.OrdersCode);
    }

    [AvaloniaFact]
    public void Renaming_a_control_renames_its_field_and_the_code_that_names_it_but_not_its_text()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);

        surface.Rename(surface.Document!.Find("saveButton")!, "okButton");

        var text = surface.Text;
        Assert.DoesNotContain("saveButton ", text, StringComparison.Ordinal);
        Assert.Contains("private Button okButton;", text, StringComparison.Ordinal);
        Assert.Contains("okButton.Name = \"okButton\";", text, StringComparison.Ordinal);
        Assert.Contains("AcceptButton = okButton;", text, StringComparison.Ordinal);
        Assert.Contains("Controls.Add(okButton);", text, StringComparison.Ordinal);
        Assert.Contains("okButton.Click += saveButton_Click;", text, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => surface.Rename(surface.Document!.Find("okButton")!, "nameLabel"));
        Assert.Throws<ArgumentException>(() => surface.Rename(surface.Document!.Find("okButton")!, "class"));
        Compiles(text, Samples.OrdersCode);
    }

    [AvaloniaFact]
    public void A_double_tap_handler_is_added_to_the_code_file_and_wired_in_the_designer_file()
    {
        var surface = new DesignSurface(Samples.Template, "Form", simplified: true);
        var name = surface.Add(typeof(TextBox), new Point(16, 16));
        var box = surface.Document!.Find(name)!;
        var handler = CodeBehind.HandlerName(name, "TextChanged");

        surface.SetHandler(box, "TextChanged", handler);
        var (code, caret) = CodeBehind.AddHandler(Samples.TemplateCode, "Form1", handler, CodeBehind.DelegateTypeOf(typeof(TextBox), "TextChanged")!, fullTypeNames: false);

        Assert.Contains("textBox1.TextChanged += textBox1_TextChanged;", surface.Text, StringComparison.Ordinal);
        Assert.Contains(Lines("""
                    private void textBox1_TextChanged(object sender, EventArgs e)
                    {

                    }
            """), Lines(code), StringComparison.Ordinal);

        // The caret goes on the empty line inside the handler, after its indentation.
        var lineStart = code.LastIndexOf('\n', caret - 1) + 1;
        Assert.True(string.IsNullOrWhiteSpace(code[lineStart..caret]));
        Assert.Equal("{", code[..lineStart].TrimEnd().Split('\n')[^1].Trim());

        // A second double-tap goes to the handler that is there.
        var (again, caretAgain) = CodeBehind.AddHandler(code, "Form1", handler, typeof(EventHandler), fullTypeNames: false);
        Assert.Equal(code, again);
        Assert.True(caretAgain > code.IndexOf("textBox1_TextChanged(object", StringComparison.Ordinal));
        Compiles(surface.Text, code);
    }

    [AvaloniaFact]
    public void Pasted_copies_get_new_names_and_move_off_the_originals()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);
        var clip = surface.Copy([surface.Document!.Find("detailsGroup")!])!;

        var pasted = surface.Paste(clip);

        Assert.Equal(["groupBox1"], pasted);
        var text = surface.Text;
        Assert.Contains("groupBox1.Location = new Point(28, 52);", text, StringComparison.Ordinal);
        Assert.Contains("groupBox1.Controls.Add(textBox1);", text, StringComparison.Ordinal);
        Assert.Contains("groupBox1.Controls.Add(label1);", text, StringComparison.Ordinal);
        Assert.Contains("label1.Text = \"Name:\";", text, StringComparison.Ordinal);
        Assert.DoesNotContain("textBox1.TextChanged", text, StringComparison.Ordinal);
        Assert.Equal(2, surface.Root!.Controls.Find("label1", true)[0].Parent!.Controls.Count);
        Compiles(text, Samples.OrdersCode);
    }

    [AvaloniaFact]
    public void A_control_moved_into_a_group_box_joins_its_controls()
    {
        var surface = new DesignSurface(Samples.Orders, "Form", simplified: true);
        var save = surface.Document!.Find("saveButton")!;
        var (owner, collection, _) = surface.ContainerAt(new Point(100, 100));

        surface.Reparent(save, owner, collection, new Point(120, 56));

        Assert.Equal("detailsGroup", owner.Name);
        Assert.Contains("detailsGroup.Controls.Add(saveButton);", surface.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("            Controls.Add(saveButton);", surface.Text, StringComparison.Ordinal);
        Assert.Equal("detailsGroup", surface.Root!.Controls.Find("saveButton", true)[0].Parent!.Name);
        Compiles(surface.Text, Samples.OrdersCode);
    }

    [AvaloniaFact]
    public void Statements_the_designer_does_not_read_are_kept()
    {
        var text = Samples.Orders.Replace(
            "nameTextBox.TabIndex = 1;",
            "nameTextBox.TabIndex = 1;\n                    nameTextBox.TextChanged += (s, e) => System.Diagnostics.Debug.WriteLine(\"changed\");",
            StringComparison.Ordinal);
        var surface = new DesignSurface(text, "Form", simplified: true);

        surface.SetProperty(surface.Document!.Find("saveButton")!, "Text", "Save customer");

        Assert.Contains("toolTip1.SetToolTip(nameTextBox, \"The customer's name, as on the invoice.\");", surface.Text, StringComparison.Ordinal);
        Assert.Contains("nameTextBox.TextChanged += (s, e) => System.Diagnostics.Debug.WriteLine(\"changed\");", surface.Text, StringComparison.Ordinal);
        Assert.Contains("saveButton.Text = \"Save customer\";", surface.Text, StringComparison.Ordinal);
    }

    /// <summary>The text with its line endings as <c>\n</c> and no spaces at their ends, for comparing blocks of lines.</summary>
    private static string Lines(string text) =>
        string.Join('\n', text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(line => line.TrimEnd()));

    /// <summary>Compiles a form's designer file and code file against the Forms layer, failing the test on an error.</summary>
    private static void Compiles(string designer, string code)
    {
        var compiler = new CSharpCompiler([.. ReferenceAssemblies.Net100, MetadataReference.CreateFromFile(typeof(Form).Assembly.Location)]);
        var result = compiler.Compile(new CompilationRequest
        {
            AssemblyName = "Designed",
            Kind = AssemblyKind.Library,
            Documents =
            [
                new SourceDocument("Usings.cs", "global using System.Drawing;\nglobal using System.Windows.Forms;\n"),
                new SourceDocument("Form1.Designer.cs", designer),
                new SourceDocument("Form1.cs", code),
            ],
        });

        Assert.True(result.Success, string.Join('\n', result.Diagnostics) + "\n" + designer);
    }
}
