using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Tests.Infrastructure;

namespace TabStudio.Forms.Tests.Controls;

/// <summary>
/// The .NET Framework's StatusBar, which VBDev2Dev's Dots builds in its designer file. It was missing from the Forms
/// layer, with Form.AutoScaleBaseSize, so Dots had 13 errors.
/// </summary>
public sealed class StatusBarTests
{
    /// <summary>What Dots' <c>frmBoard.InitializeComponent</c> does, line for line.</summary>
    private static (Form Form, StatusBar Bar, StatusBarPanel Name, StatusBarPanel Move, StatusBarPanel Boxes) Dots()
    {
        var form = new Form { ClientSize = new Size(292, 266) };
        var sbar = new StatusBar();
        var sbpName = new StatusBarPanel();
        var sbpMove = new StatusBarPanel();
        var sbpBoxes = new StatusBarPanel();
        ((ISupportInitialize)sbpName).BeginInit();
        ((ISupportInitialize)sbpMove).BeginInit();
        ((ISupportInitialize)sbpBoxes).BeginInit();
        form.SuspendLayout();
        sbar.Location = new Point(0, 244);
        sbar.Name = "sbar";
        sbar.Panels.AddRange([sbpName, sbpMove, sbpBoxes]);
        sbar.ShowPanels = true;
        sbar.Size = new Size(292, 22);
        sbar.TabIndex = 0;
        sbar.Text = "StatusBar1";
        sbpName.AutoSize = StatusBarPanelAutoSize.Contents;
        sbpName.Width = 10;
        sbpMove.AutoSize = StatusBarPanelAutoSize.Contents;
        sbpMove.Width = 10;
        sbpBoxes.AutoSize = StatusBarPanelAutoSize.Spring;
        sbpBoxes.Width = 256;
        form.AutoScaleBaseSize = new Size(5, 13);
        form.Controls.Add(sbar);
        ((ISupportInitialize)sbpName).EndInit();
        ((ISupportInitialize)sbpMove).EndInit();
        ((ISupportInitialize)sbpBoxes).EndInit();
        form.ResumeLayout(false);
        return (form, sbar, sbpName, sbpMove, sbpBoxes);
    }

    [Fact]
    public void Dots_designer_code_builds_a_bar_docked_to_the_bottom_whose_panels_fit_their_text_and_spring()
    {
        var (form, bar, name, move, boxes) = Dots();
        using var _ = form;
        form.ClientSize = new Size(400, 300);
        name.Text = "Player 1";
        move.Text = "Invalid Move";

        Assert.Equal(new Size(5, 13), form.AutoScaleBaseSize);
        Assert.Equal(DockStyle.Bottom, bar.Dock);
        Assert.Equal(300, bar.Bottom);
        Assert.Equal(400, bar.Width);
        Assert.Same(bar, name.Parent);
        Assert.Equal(3, bar.Panels.Count);
        Assert.Equal(TextRenderer.MeasureText("Invalid Move", bar.Font).Width + 8, move.Width);
        Assert.True(move.Width > name.Width, $"{move.Width} > {name.Width}");
        Assert.Equal(400 - 16 - 4 - name.Width - move.Width, boxes.Width);
    }

    [AvaloniaFact]
    public void A_tap_on_a_panel_raises_PanelClick_with_that_panel_and_the_text_is_drawn()
    {
        StatusBar bar = null!;
        StatusBarPanel boxes = null!;
        var clicked = new List<StatusBarPanel>();
        using var run = FormsTestRun.Start(() =>
        {
            var dots = Dots();
            bar = dots.Bar;
            boxes = dots.Boxes;
            dots.Name.Text = "Player 1";
            boxes.Text = "3";
            bar.PanelClick += (_, e) => clicked.Add(e.StatusBarPanel);
            return dots.Form;
        });

        var bounds = run.Invoke(() => bar.PanelBounds[2]);
        run.Tap(bar, new Point(bounds.Left + (bounds.Width / 2), bar.Height / 2));

        Assert.Equal([boxes], run.Invoke(() => clicked.ToList()));
        Assert.Contains("Panels.Count: 3", run.Invoke(() => bar.ToString()), StringComparison.Ordinal);
    }
}
