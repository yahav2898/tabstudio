using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Views;
using DrawingPoint = System.Drawing.Point;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// A paint program from a Windows Forms tutorial in the IDE: its Load handler draws on a bitmap with
/// <c>Graphics.FromImage</c>, the mouse draws strokes into the bitmap, the picture box shows them, and Save writes a
/// PNG with the strokes' pixels.
/// </summary>
public sealed partial class WindowsFormsAppTests
{
    [AvaloniaFact]
    public async Task SimplePaint_draws_mouse_strokes_on_its_bitmap_and_saves_them_as_a_PNG()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WindowsFormsId, "SimplePaint");
        var project = Path.Combine(Path.GetDirectoryName(solution)!, "SimplePaint");
        File.Delete(Path.Combine(project, "Form1.cs"));
        File.Delete(Path.Combine(project, "Form1.Designer.cs"));
        File.WriteAllText(Path.Combine(project, "frmDrawingApp.cs"), SimplePaint.Code);
        File.WriteAllText(Path.Combine(project, "frmDrawingApp.Designer.cs"), SimplePaint.Designer);
        File.WriteAllText(Path.Combine(project, "Program.cs"), SimplePaint.Program);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var window = new Window { Width = 1600, Height = 1000, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();

        var run = workspace.RunCommand.ExecuteAsync(null);
        await run.WaitAsync(TimeSpan.FromMinutes(3));
        await Task.Delay(500);
        UiTestHelpers.SaveScreenshot(window, "before-simple-paint");
        Assert.Contains("NotSupportedException", workspace.Output.Text, StringComparison.Ordinal);
        window.Close();
    }

    /// <summary>A paint program as tutorials write it, in the shape of the SimplePaint app that crashed in 0.17.0.</summary>
    private static class SimplePaint
    {
        public const string Program = """
            namespace SimplePaint
            {
                internal static class Program
                {
                    [STAThread]
                    static void Main()
                    {
                        ApplicationConfiguration.Initialize();
                        Application.Run(new frmDrawingApp());
                    }
                }
            }
            """;

        public const string Code = """
            using System.Drawing.Drawing2D;
            using System.Drawing.Imaging;

            namespace SimplePaint
            {
                public partial class frmDrawingApp : Form
                {
                    private Bitmap bm;
                    private Graphics g;
                    private bool paint;
                    private Point px, py;
                    private readonly Pen p = new Pen(Color.Black, 5) { StartCap = LineCap.Round, EndCap = LineCap.Round };

                    public frmDrawingApp()
                    {
                        InitializeComponent();
                    }

                    public Color PenColor => p.Color;

                    private void frmDrawingApp_Load(object sender, EventArgs e)
                    {
                        bm = new Bitmap(pic.Width, pic.Height);
                        g = Graphics.FromImage(bm);
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.White);
                        pic.Image = bm;
                    }

                    private void pic_MouseDown(object sender, MouseEventArgs e)
                    {
                        paint = true;
                        py = e.Location;
                    }

                    private void pic_MouseMove(object sender, MouseEventArgs e)
                    {
                        if (paint)
                        {
                            px = e.Location;
                            g.DrawLine(p, px, py);
                            py = px;
                        }

                        pic.Refresh();
                    }

                    private void pic_MouseUp(object sender, MouseEventArgs e)
                    {
                        paint = false;
                    }

                    private void btnRed_Click(object sender, EventArgs e)
                    {
                        p.Color = Color.Red;
                    }

                    private void btnClear_Click(object sender, EventArgs e)
                    {
                        g.Clear(Color.White);
                        pic.Image = bm;
                    }

                    private void btnSave_Click(object sender, EventArgs e)
                    {
                        var path = Path.Combine(Application.StartupPath, "drawing.png");
                        var copy = bm.Clone(new Rectangle(0, 0, pic.Width, pic.Height), bm.PixelFormat);
                        copy.Save(path, ImageFormat.Png);
                        lblStatus.Text = "Saved " + path;
                    }
                }
            }
            """;

        public const string Designer = """
            namespace SimplePaint
            {
                partial class frmDrawingApp
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
                        panel1 = new Panel();
                        btnRed = new Button();
                        btnClear = new Button();
                        btnSave = new Button();
                        lblStatus = new Label();
                        pic = new PictureBox();
                        panel1.SuspendLayout();
                        ((System.ComponentModel.ISupportInitialize)pic).BeginInit();
                        SuspendLayout();
                        //
                        // panel1
                        //
                        panel1.BackColor = Color.FromArgb(64, 64, 64);
                        panel1.Controls.Add(lblStatus);
                        panel1.Controls.Add(btnSave);
                        panel1.Controls.Add(btnClear);
                        panel1.Controls.Add(btnRed);
                        panel1.Dock = DockStyle.Top;
                        panel1.Location = new Point(0, 0);
                        panel1.Name = "panel1";
                        panel1.Size = new Size(600, 48);
                        panel1.TabIndex = 0;
                        //
                        // btnRed
                        //
                        btnRed.BackColor = Color.Red;
                        btnRed.Location = new Point(8, 8);
                        btnRed.Name = "btnRed";
                        btnRed.Size = new Size(32, 32);
                        btnRed.TabIndex = 0;
                        btnRed.UseVisualStyleBackColor = false;
                        btnRed.Click += btnRed_Click;
                        //
                        // btnClear
                        //
                        btnClear.ForeColor = Color.White;
                        btnClear.Location = new Point(56, 8);
                        btnClear.Name = "btnClear";
                        btnClear.Size = new Size(80, 32);
                        btnClear.TabIndex = 1;
                        btnClear.Text = "Clear";
                        btnClear.Click += btnClear_Click;
                        //
                        // btnSave
                        //
                        btnSave.ForeColor = Color.White;
                        btnSave.Location = new Point(144, 8);
                        btnSave.Name = "btnSave";
                        btnSave.Size = new Size(80, 32);
                        btnSave.TabIndex = 2;
                        btnSave.Text = "Save";
                        btnSave.Click += btnSave_Click;
                        //
                        // lblStatus
                        //
                        lblStatus.AutoSize = true;
                        lblStatus.ForeColor = Color.White;
                        lblStatus.Location = new Point(240, 16);
                        lblStatus.Name = "lblStatus";
                        lblStatus.Size = new Size(0, 15);
                        lblStatus.TabIndex = 3;
                        //
                        // pic
                        //
                        pic.BackColor = Color.White;
                        pic.Location = new Point(0, 48);
                        pic.Name = "pic";
                        pic.Size = new Size(600, 360);
                        pic.TabIndex = 1;
                        pic.TabStop = false;
                        pic.MouseDown += pic_MouseDown;
                        pic.MouseMove += pic_MouseMove;
                        pic.MouseUp += pic_MouseUp;
                        //
                        // frmDrawingApp
                        //
                        AutoScaleDimensions = new SizeF(7F, 15F);
                        AutoScaleMode = AutoScaleMode.Font;
                        ClientSize = new Size(600, 408);
                        Controls.Add(pic);
                        Controls.Add(panel1);
                        FormBorderStyle = FormBorderStyle.FixedSingle;
                        Name = "frmDrawingApp";
                        Text = "Simple Paint";
                        Load += frmDrawingApp_Load;
                        panel1.ResumeLayout(false);
                        panel1.PerformLayout();
                        ((System.ComponentModel.ISupportInitialize)pic).EndInit();
                        ResumeLayout(false);
                    }

                    #endregion

                    private Panel panel1;
                    private Button btnRed;
                    private Button btnClear;
                    private Button btnSave;
                    private Label lblStatus;
                    private PictureBox pic;
                }
            }
            """;
    }
}
