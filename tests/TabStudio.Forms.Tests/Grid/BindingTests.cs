using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Tests.Infrastructure;

namespace TabStudio.Forms.Tests.Grid;

/// <summary>Simple binding through a binding source: text boxes follow the grid's current row and write back.</summary>
public sealed class BindingTests
{
    [AvaloniaFact]
    public void Text_boxes_bound_to_a_binding_source_show_the_grids_current_row()
    {
        DataGridView grid = null!;
        TextBox name = null!;
        var people = new BindingList<DataGridViewTests.Person>
        {
            new() { Name = "Alice", Age = 34 },
            new() { Name = "Bob", Age = 41 },
            new() { Name = "Carol", Age = 29 },
        };
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form { ClientSize = new Size(500, 300) };
            var source = new BindingSource { DataSource = people };
            grid = new DataGridView { Location = new Point(0, 0), Size = new Size(500, 200), DataSource = source };
            name = new TextBox { Location = new Point(10, 220) };
            name.DataBindings.Add("Text", source, "Name");
            form.Controls.Add(grid);
            form.Controls.Add(name);
            return form;
        });

        Assert.Equal("Alice", run.Invoke(() => name.Text));

        var cell = run.Invoke(() => grid.GetCellDisplayRectangle(0, 2, cutOverflow: false));
        run.Tap(grid, new Point(cell.X + 5, cell.Y + 5));
        Assert.Equal("Carol", run.Invoke(() => name.Text));

        run.Tap(name);
        run.Invoke(() => name.SelectAll());
        run.Type("Caroline");
        run.Tap(grid, new Point(cell.X + 5, cell.Y + 5));

        Assert.Equal("Caroline", run.Invoke(() => people[2].Name));
    }

    [AvaloniaFact]
    public void A_binding_source_of_a_type_starts_as_an_empty_list_of_that_type()
    {
        using var source = new BindingSource { DataSource = typeof(DataGridViewTests.Person) };

        Assert.Empty(source);
        Assert.True(source.AllowNew);
        Assert.IsType<DataGridViewTests.Person>(source.AddNew());
        Assert.Single(source);
        Assert.Equal(0, source.Position);
        Assert.Contains("Age", source.GetItemProperties(null).Cast<PropertyDescriptor>().Select(p => p.Name));
    }
}
