using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;

namespace TabStudio.Forms.Tests.Layout;

/// <summary>The layouts of table layout panels and flow layout panels.</summary>
public sealed class PanelLayoutTests
{
    [AvaloniaFact]
    public void A_table_gives_absolute_rows_their_size_and_shares_the_rest_by_percentage()
    {
        using var table = new TableLayoutPanel { Size = new Size(400, 300), ColumnCount = 2, RowCount = 2 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 75F));
        table.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        var name = new Label { Text = "Name", Dock = DockStyle.Fill };
        var box = new TextBox { Dock = DockStyle.Fill };
        var notes = new TextBox { Multiline = true, Dock = DockStyle.Fill };
        table.Controls.Add(name, 0, 0);
        table.Controls.Add(box, 1, 0);
        table.Controls.Add(notes, 0, 1);
        table.SetColumnSpan(notes, 2);

        Assert.Equal([100, 300], table.GetColumnWidths());
        Assert.Equal([40, 260], table.GetRowHeights());
        // A label's margin has no top or bottom, as in Windows Forms.
        Assert.Equal(new Rectangle(3, 0, 94, 40), name.Bounds);
        Assert.Equal(103, box.Left);
        Assert.Equal(new Rectangle(3, 43, 394, 254), notes.Bounds);
        Assert.Equal(new TableLayoutPanelCellPosition(1, 0), table.GetPositionFromControl(box));
    }

    [AvaloniaFact]
    public void Controls_without_a_cell_flow_into_the_free_cells_row_by_row()
    {
        using var table = new TableLayoutPanel { Size = new Size(200, 200), ColumnCount = 2 };
        var buttons = Enumerable.Range(0, 5).Select(i => new Button { Text = i.ToString(System.Globalization.CultureInfo.InvariantCulture) }).ToArray();
        table.Controls.AddRange(buttons);

        Assert.Equal(new TableLayoutPanelCellPosition(0, 0), table.GetPositionFromControl(buttons[0]));
        Assert.Equal(new TableLayoutPanelCellPosition(1, 0), table.GetPositionFromControl(buttons[1]));
        Assert.Equal(new TableLayoutPanelCellPosition(0, 1), table.GetPositionFromControl(buttons[2]));
        Assert.Equal(new TableLayoutPanelCellPosition(0, 2), table.GetPositionFromControl(buttons[4]));
    }

    [AvaloniaFact]
    public void Automatic_columns_take_the_width_of_their_widest_control()
    {
        using var table = new TableLayoutPanel { Size = new Size(400, 100), ColumnCount = 2, RowCount = 1 };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        var button = new Button { Size = new Size(80, 23) };
        table.Controls.Add(button, 0, 0);

        Assert.Equal(86, table.GetColumnWidths()[0]);
        Assert.Equal(314, table.GetColumnWidths()[1]);
    }

    [AvaloniaFact]
    public void A_flow_panel_wraps_its_controls_when_they_do_not_fit()
    {
        using var flow = new FlowLayoutPanel { Size = new Size(200, 200) };
        var buttons = Enumerable.Range(0, 3).Select(_ => new Button { Size = new Size(80, 23) }).ToArray();
        flow.Controls.AddRange(buttons);

        Assert.Equal(new Point(3, 3), buttons[0].Location);
        Assert.Equal(new Point(89, 3), buttons[1].Location);
        Assert.Equal(new Point(3, 32), buttons[2].Location);
    }

    [AvaloniaFact]
    public void A_flow_break_starts_a_new_row()
    {
        using var flow = new FlowLayoutPanel { Size = new Size(400, 200) };
        var first = new Button { Size = new Size(80, 23) };
        var second = new Button { Size = new Size(80, 23) };
        flow.Controls.Add(first);
        flow.Controls.Add(second);
        flow.SetFlowBreak(first, true);

        Assert.Equal(new Point(3, 32), second.Location);
    }

    [AvaloniaFact]
    public void A_top_down_flow_stacks_its_controls()
    {
        using var flow = new FlowLayoutPanel { Size = new Size(200, 400), FlowDirection = FlowDirection.TopDown };
        var first = new CheckBox { AutoSize = false, Size = new Size(100, 20) };
        var second = new CheckBox { AutoSize = false, Size = new Size(100, 20) };
        flow.Controls.Add(first);
        flow.Controls.Add(second);

        Assert.Equal(new Point(3, 29), second.Location);
    }
}
