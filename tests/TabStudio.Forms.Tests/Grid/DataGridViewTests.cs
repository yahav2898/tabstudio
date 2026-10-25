using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Tests.Infrastructure;

namespace TabStudio.Forms.Tests.Grid;

/// <summary>The grid: binding to lists, editing cells by touch and keys, sorting and selection.</summary>
public sealed class DataGridViewTests
{
    [AvaloniaFact]
    public void Binding_to_a_list_generates_a_column_for_each_property_and_a_row_for_each_item()
    {
        using var grid = new DataGridView { Size = new Size(400, 300) };
        grid.DataSource = People();

        Assert.Equal(["Name", "Age", "Active"], grid.Columns.Cast<DataGridViewColumn>().Select(column => column.HeaderText));
        Assert.IsType<DataGridViewCheckBoxColumn>(grid.Columns["Active"]);
        Assert.Equal(4, grid.Rows.Count);
        Assert.True(grid.Rows[3].IsNewRow);
        Assert.Equal("Bob", grid.Rows[1].Cells["Name"].Value);
        Assert.Equal(41, grid.Rows[1].Cells[1].Value);
    }

    [AvaloniaFact]
    public void A_cell_formats_its_value_with_the_column_style()
    {
        using var grid = new DataGridView();
        grid.DataSource = People();
        grid.Columns["Age"]!.DefaultCellStyle.Format = "N2";

        Assert.Equal((34.0).ToString("N2", System.Globalization.CultureInfo.CurrentCulture), grid.Rows[0].Cells["Age"].FormattedValue);
    }

    [AvaloniaFact]
    public void A_tap_on_the_current_cell_edits_it_and_Enter_commits_the_typed_value_to_the_item()
    {
        DataGridView grid = null!;
        var people = People();
        var changes = new List<(int Column, int Row)>();
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(500, 300) };
            grid = new DataGridView { Dock = DockStyle.Fill, DataSource = people };
            grid.CellValueChanged += (_, e) => changes.Add((e.ColumnIndex, e.RowIndex));
            form.Controls.Add(grid);
            return form;
        });

        var cell = run.Invoke(() => grid.GetCellDisplayRectangle(1, 0, cutOverflow: false));
        var center = new Point(cell.X + (cell.Width / 2), cell.Y + (cell.Height / 2));
        run.Tap(grid, center);
        Assert.Equal(new Point(1, 0), run.Invoke(() => grid.CurrentCellAddress));
        Assert.False(run.Invoke(() => grid.IsCurrentCellInEditMode));

        run.Tap(grid, center);
        Assert.True(run.Invoke(() => grid.IsCurrentCellInEditMode));

        run.Invoke(() => ((TextBox)grid.EditingControl!).SelectAll());
        run.Type("42");
        run.Key(Keys.Enter);

        Assert.False(run.Invoke(() => grid.IsCurrentCellInEditMode));
        Assert.Equal(42, run.Invoke(() => people[0].Age));
        Assert.Equal([(1, 0)], run.Invoke(() => changes.ToList()));
        Assert.Equal(new Point(1, 1), run.Invoke(() => grid.CurrentCellAddress));
    }

    [AvaloniaFact]
    public void A_value_that_does_not_parse_raises_DataError_and_keeps_editing()
    {
        DataGridView grid = null!;
        Exception? error = null;
        var people = People();
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(500, 300) };
            grid = new DataGridView { Dock = DockStyle.Fill, DataSource = people };
            grid.DataError += (_, e) =>
            {
                error = e.Exception;
                e.Cancel = true;
            };
            form.Controls.Add(grid);
            return form;
        });

        run.Invoke(() =>
        {
            grid.CurrentCell = grid.Rows[0].Cells[1];
            grid.BeginEdit(selectAll: true);
        });
        run.Type("forty");
        run.Key(Keys.Enter);

        Assert.NotNull(run.Invoke(() => error));
        Assert.True(run.Invoke(() => grid.IsCurrentCellInEditMode));
        Assert.Equal(34, run.Invoke(() => people[0].Age));
    }

    [AvaloniaFact]
    public void A_tap_on_a_check_box_cell_toggles_the_bound_value()
    {
        DataGridView grid = null!;
        var people = People();
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(500, 300) };
            grid = new DataGridView { Dock = DockStyle.Fill, DataSource = people };
            form.Controls.Add(grid);
            return form;
        });

        var cell = run.Invoke(() => grid.GetCellDisplayRectangle(2, 1, cutOverflow: false));
        run.Tap(grid, new Point(cell.X + (cell.Width / 2), cell.Y + (cell.Height / 2)));

        Assert.True(run.Invoke(() => people[1].Active));
    }

    [AvaloniaFact]
    public void Typing_in_the_row_for_new_records_adds_an_item()
    {
        DataGridView grid = null!;
        var people = People();
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(500, 300) };
            grid = new DataGridView { Dock = DockStyle.Fill, DataSource = people };
            form.Controls.Add(grid);
            return form;
        });

        run.Invoke(() =>
        {
            grid.CurrentCell = grid.Rows[grid.NewRowIndex].Cells[0];
            grid.BeginEdit(selectAll: true);
        });
        run.Type("Dana");
        run.Key(Keys.Enter);

        Assert.Equal(4, run.Invoke(() => people.Count));
        Assert.Equal("Dana", run.Invoke(() => people[3].Name));
        Assert.True(run.Invoke(() => grid.Rows[grid.Rows.Count - 1].IsNewRow));
        Assert.Equal(5, run.Invoke(() => grid.Rows.Count));
    }

    [AvaloniaFact]
    public void A_tap_on_a_column_header_sorts_an_unbound_grid()
    {
        DataGridView grid = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(500, 300) };
            grid = new DataGridView { Dock = DockStyle.Fill, AllowUserToAddRows = false };
            grid.Columns.Add("city", "City");
            grid.Columns.Add("people", "People");
            grid.Columns["people"]!.ValueType = typeof(int);
            grid.Rows.Add("Paris", 2100000);
            grid.Rows.Add("Haifa", 290000);
            grid.Rows.Add("Tokyo", 14000000);
            form.Controls.Add(grid);
            return form;
        });

        var header = run.Invoke(() => grid.GetCellDisplayRectangle(1, -1, cutOverflow: false));
        run.Tap(grid, new Point(header.X + 10, header.Y + (header.Height / 2)));

        Assert.Equal(["Haifa", "Paris", "Tokyo"], run.Invoke(() => grid.Rows.Cast<DataGridViewRow>().Select(row => (string)row.Cells[0].Value!).ToList()));
        Assert.Equal(SortOrder.Ascending, run.Invoke(() => grid.SortOrder));

        run.Tap(grid, new Point(header.X + 10, header.Y + (header.Height / 2)));
        Assert.Equal("Tokyo", run.Invoke(() => grid.Rows[0].Cells[0].Value));
    }

    [AvaloniaFact]
    public void Full_row_selection_selects_the_row_of_the_tapped_cell()
    {
        DataGridView grid = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(500, 300) };
            grid = new DataGridView { Dock = DockStyle.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, DataSource = People() };
            form.Controls.Add(grid);
            return form;
        });

        var cell = run.Invoke(() => grid.GetCellDisplayRectangle(0, 2, cutOverflow: false));
        run.Tap(grid, new Point(cell.X + 5, cell.Y + 5));

        Assert.Equal([2], run.Invoke(() => grid.SelectedRows.Cast<DataGridViewRow>().Select(row => row.Index).ToList()));
        Assert.Equal("Carol", run.Invoke(() => ((Person)grid.CurrentRow!.DataBoundItem!).Name));
    }

    [AvaloniaFact]
    public void Columns_that_fill_share_the_grid_width_by_weight()
    {
        using var grid = new DataGridView { Size = new Size(402, 200), RowHeadersVisible = false, BorderStyle = BorderStyle.FixedSingle };
        grid.Columns.Add("a", "A");
        grid.Columns.Add("b", "B");
        grid.Columns["a"]!.FillWeight = 100;
        grid.Columns["b"]!.FillWeight = 300;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        Assert.Equal(100, grid.Columns["a"]!.Width);
        Assert.Equal(300, grid.Columns["b"]!.Width);
    }

    private static BindingList<Person> People() =>
    [
        new() { Name = "Alice", Age = 34, Active = true },
        new() { Name = "Bob", Age = 41, Active = false },
        new() { Name = "Carol", Age = 29, Active = true },
    ];

    /// <summary>A record of the grid tests' data.</summary>
    public sealed class Person
    {
        /// <summary>The name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>The age.</summary>
        public int Age { get; set; }

        /// <summary>Whether the person is active.</summary>
        public bool Active { get; set; }
    }
}
