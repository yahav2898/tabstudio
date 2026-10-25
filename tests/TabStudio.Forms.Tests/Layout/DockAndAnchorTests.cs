using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;

namespace TabStudio.Forms.Tests.Layout;

/// <summary>Docking, anchoring and automatic sizing, as Windows Forms lays controls out.</summary>
public sealed class DockAndAnchorTests
{
    [AvaloniaFact]
    public void Docked_children_take_the_edges_in_reverse_z_order_and_fill_takes_the_rest()
    {
        using var panel = new Panel { Size = new Size(300, 200) };
        var fill = new Panel { Dock = DockStyle.Fill };
        var top = new Panel { Dock = DockStyle.Top, Height = 30 };
        var bottom = new Panel { Dock = DockStyle.Bottom, Height = 20 };
        var left = new Panel { Dock = DockStyle.Left, Width = 50 };

        // The designer adds the Fill control first, so it is docked last.
        panel.Controls.Add(fill);
        panel.Controls.Add(left);
        panel.Controls.Add(bottom);
        panel.Controls.Add(top);

        Assert.Equal(new Rectangle(0, 0, 300, 30), top.Bounds);
        Assert.Equal(new Rectangle(0, 180, 300, 20), bottom.Bounds);
        Assert.Equal(new Rectangle(0, 30, 50, 150), left.Bounds);
        Assert.Equal(new Rectangle(50, 30, 250, 150), fill.Bounds);
    }

    [AvaloniaFact]
    public void Docking_respects_the_parents_padding()
    {
        using var panel = new Panel { Size = new Size(200, 100), Padding = new Padding(10) };
        var fill = new Panel { Dock = DockStyle.Fill };
        panel.Controls.Add(fill);

        Assert.Equal(new Rectangle(10, 10, 180, 80), fill.Bounds);
    }

    [AvaloniaFact]
    public void A_control_anchored_bottom_right_keeps_its_distance_from_those_edges()
    {
        using var form = new Form { ClientSize = new Size(800, 450) };
        var button = new Button { Location = new Point(700, 400), Size = new Size(75, 23), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
        form.Controls.Add(button);

        form.ClientSize = new Size(1000, 600);

        Assert.Equal(new Rectangle(900, 550, 75, 23), button.Bounds);
    }

    [AvaloniaFact]
    public void A_control_anchored_left_and_right_stretches()
    {
        using var form = new Form { ClientSize = new Size(400, 300) };
        var box = new TextBox { Location = new Point(12, 12), Size = new Size(376, 23), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        form.Controls.Add(box);

        form.ClientSize = new Size(600, 300);

        Assert.Equal(12, box.Left);
        Assert.Equal(576, box.Width);
    }

    [AvaloniaFact]
    public void A_control_anchored_to_no_edge_keeps_its_relative_position()
    {
        using var form = new Form { ClientSize = new Size(400, 300) };
        var label = new Label { AutoSize = false, Location = new Point(150, 100), Size = new Size(100, 100), Anchor = AnchorStyles.None };
        form.Controls.Add(label);

        form.ClientSize = new Size(600, 500);

        Assert.Equal(new Rectangle(250, 200, 100, 100), label.Bounds);
    }

    [AvaloniaFact]
    public void Designer_code_measures_anchors_against_the_designed_client_size()
    {
        using var form = new Form();
        var ok = new Button();
        form.SuspendLayout();
        ok.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        ok.Location = new Point(713, 415);
        ok.Name = "ok";
        ok.Size = new Size(75, 23);
        form.AutoScaleDimensions = new SizeF(7F, 15F);
        form.AutoScaleMode = AutoScaleMode.Font;
        form.ClientSize = new Size(800, 450);
        form.Controls.Add(ok);
        form.ResumeLayout(false);

        form.ClientSize = new Size(900, 500);

        Assert.Equal(new Point(813, 465), ok.Location);
    }

    [AvaloniaFact]
    public void An_automatically_sized_label_grows_with_its_text()
    {
        using var form = new Form { ClientSize = new Size(400, 300) };
        var label = new Label { AutoSize = true, Location = new Point(10, 10), Text = "Hi" };
        form.Controls.Add(label);
        var before = label.Width;

        label.Text = "A much longer piece of text";

        Assert.True(label.Width > before + 50, $"The label is {label.Width} wide, it was {before}.");
        Assert.True(label.Height is > 10 and < 30, $"The label is {label.Height} high.");
    }

    [AvaloniaFact]
    public void A_one_line_text_box_keeps_the_height_of_its_font()
    {
        using var box = new TextBox { Size = new Size(100, 60) };

        Assert.Equal(23, box.Height);
    }

    [AvaloniaFact]
    public void A_menu_bar_and_a_status_bar_dock_to_the_top_and_the_bottom()
    {
        using var form = new Form { ClientSize = new Size(640, 480) };
        var menu = new MenuStrip();
        menu.Items.Add(new ToolStripMenuItem("&File"));
        var status = new StatusStrip();
        status.Items.Add(new ToolStripStatusLabel("Ready"));
        var fill = new Panel { Dock = DockStyle.Fill };
        form.Controls.Add(fill);
        form.Controls.Add(status);
        form.Controls.Add(menu);

        Assert.Equal(0, menu.Top);
        Assert.Equal(24, menu.Height);
        Assert.Equal(480, status.Bottom);
        Assert.Equal(22, status.Height);
        Assert.Equal(new Rectangle(0, 24, 640, 480 - 24 - 22), fill.Bounds);
    }
}
