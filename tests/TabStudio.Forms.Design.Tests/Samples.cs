namespace TabStudio.Forms.Design.Tests;

/// <summary>Designer files as Visual Studio writes them.</summary>
internal static class Samples
{
    /// <summary>The Windows Forms App template's Form1.Designer.cs.</summary>
    public const string Template = """
        namespace HelloForms
        {
            partial class Form1
            {
                /// <summary>
                ///  Required designer variable.
                /// </summary>
                private System.ComponentModel.IContainer components = null;

                /// <summary>
                ///  Clean up any resources being used.
                /// </summary>
                /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
                protected override void Dispose(bool disposing)
                {
                    if (disposing && (components != null))
                    {
                        components.Dispose();
                    }
                    base.Dispose(disposing);
                }

                #region Windows Form Designer generated code

                /// <summary>
                ///  Required method for Designer support - do not modify
                ///  the contents of this method with the code editor.
                /// </summary>
                private void InitializeComponent()
                {
                    this.components = new System.ComponentModel.Container();
                    this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
                    this.ClientSize = new System.Drawing.Size(800, 450);
                    this.Text = "Form1";
                }

                #endregion
            }
        }
        """;

    /// <summary>The template's Form1.cs.</summary>
    public const string TemplateCode = """
        namespace HelloForms
        {
            public partial class Form1 : Form
            {
                public Form1()
                {
                    InitializeComponent();
                }
            }
        }
        """;

    /// <summary>A form with a group box of fields, a menu, a tooltip and statements the designer keeps as they are.</summary>
    public const string Orders = """
        namespace Orders
        {
            partial class Form1
            {
                private System.ComponentModel.IContainer components = null;

                protected override void Dispose(bool disposing)
                {
                    if (disposing && (components != null))
                    {
                        components.Dispose();
                    }
                    base.Dispose(disposing);
                }

                #region Windows Form Designer generated code

                private void InitializeComponent()
                {
                    components = new System.ComponentModel.Container();
                    menuStrip1 = new MenuStrip();
                    fileMenuItem = new ToolStripMenuItem();
                    exitMenuItem = new ToolStripMenuItem();
                    detailsGroup = new GroupBox();
                    nameLabel = new Label();
                    nameTextBox = new TextBox();
                    saveButton = new Button();
                    toolTip1 = new ToolTip(components);
                    menuStrip1.SuspendLayout();
                    detailsGroup.SuspendLayout();
                    SuspendLayout();
                    //
                    // menuStrip1
                    //
                    menuStrip1.Items.AddRange(new ToolStripItem[] { fileMenuItem });
                    menuStrip1.Location = new Point(0, 0);
                    menuStrip1.Name = "menuStrip1";
                    menuStrip1.Size = new Size(400, 24);
                    menuStrip1.TabIndex = 0;
                    //
                    // fileMenuItem
                    //
                    fileMenuItem.DropDownItems.AddRange(new ToolStripItem[] { exitMenuItem });
                    fileMenuItem.Name = "fileMenuItem";
                    fileMenuItem.Size = new Size(37, 20);
                    fileMenuItem.Text = "&File";
                    //
                    // exitMenuItem
                    //
                    exitMenuItem.Name = "exitMenuItem";
                    exitMenuItem.Size = new Size(93, 22);
                    exitMenuItem.Text = "E&xit";
                    exitMenuItem.Click += exitMenuItem_Click;
                    //
                    // detailsGroup
                    //
                    detailsGroup.Controls.Add(nameTextBox);
                    detailsGroup.Controls.Add(nameLabel);
                    detailsGroup.Location = new Point(12, 36);
                    detailsGroup.Name = "detailsGroup";
                    detailsGroup.Size = new Size(240, 100);
                    detailsGroup.TabIndex = 1;
                    detailsGroup.TabStop = false;
                    detailsGroup.Text = "Customer";
                    //
                    // nameLabel
                    //
                    nameLabel.AutoSize = true;
                    nameLabel.Location = new Point(12, 28);
                    nameLabel.Name = "nameLabel";
                    nameLabel.Size = new Size(42, 15);
                    nameLabel.TabIndex = 0;
                    nameLabel.Text = "Name:";
                    //
                    // nameTextBox
                    //
                    nameTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                    nameTextBox.Location = new Point(80, 24);
                    nameTextBox.Name = "nameTextBox";
                    nameTextBox.Size = new Size(144, 23);
                    nameTextBox.TabIndex = 1;
                    toolTip1.SetToolTip(nameTextBox, "The customer's name, as on the invoice.");
                    //
                    // saveButton
                    //
                    saveButton.BackColor = Color.FromArgb(0, 120, 215);
                    saveButton.Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point, 0);
                    saveButton.ForeColor = Color.White;
                    saveButton.Location = new Point(264, 152);
                    saveButton.Name = "saveButton";
                    saveButton.Size = new Size(120, 32);
                    saveButton.TabIndex = 2;
                    saveButton.Text = "Save";
                    saveButton.UseVisualStyleBackColor = false;
                    saveButton.Click += saveButton_Click;
                    //
                    // Form1
                    //
                    AcceptButton = saveButton;
                    AutoScaleDimensions = new SizeF(7F, 15F);
                    AutoScaleMode = AutoScaleMode.Font;
                    ClientSize = new Size(400, 200);
                    Controls.Add(saveButton);
                    Controls.Add(detailsGroup);
                    Controls.Add(menuStrip1);
                    MainMenuStrip = menuStrip1;
                    Name = "Form1";
                    Text = "Orders";
                    Load += Form1_Load;
                    menuStrip1.ResumeLayout(false);
                    menuStrip1.PerformLayout();
                    detailsGroup.ResumeLayout(false);
                    detailsGroup.PerformLayout();
                    ResumeLayout(false);
                    PerformLayout();
                }

                #endregion

                private MenuStrip menuStrip1;
                private ToolStripMenuItem fileMenuItem;
                private ToolStripMenuItem exitMenuItem;
                private GroupBox detailsGroup;
                private Label nameLabel;
                private TextBox nameTextBox;
                private Button saveButton;
                private ToolTip toolTip1;
            }
        }
        """;

    /// <summary>The Orders form's code file.</summary>
    public const string OrdersCode = """
        namespace Orders
        {
            public partial class Form1 : Form
            {
                public Form1()
                {
                    InitializeComponent();
                }

                private void Form1_Load(object sender, EventArgs e)
                {
                    nameTextBox.Text = "Contoso";
                }

                private void exitMenuItem_Click(object sender, EventArgs e) => Close();

                private void saveButton_Click(object sender, EventArgs e)
                {
                }
            }
        }
        """;
}
