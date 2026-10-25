using System.Drawing;
using System.Windows.Forms;
using Avalonia.Headless.XUnit;
using TabStudio.Forms.Tests.Infrastructure;

namespace TabStudio.Forms.Tests.Runtime;

/// <summary>Menus, tool bars, combo boxes and list boxes, driven by taps and keys.</summary>
public sealed class StripAndListTests
{
    [AvaloniaFact]
    public void A_tap_on_a_menu_opens_it_and_a_tap_on_an_item_clicks_it_and_closes_the_menu()
    {
        Form form = null!;
        MenuStrip menu = null!;
        ToolStripMenuItem file = null!;
        ToolStripMenuItem open = null!;
        var clicked = new List<string>();
        using var run = FormsTestRun.Start(() =>
        {
            form = new Form { ClientSize = new Size(400, 300) };
            menu = new MenuStrip();
            file = new ToolStripMenuItem("&File");
            open = new ToolStripMenuItem("&Open...", null, (_, _) => clicked.Add("open"), Keys.Control | Keys.O);
            file.DropDownItems.AddRange([open, new ToolStripSeparator(), new ToolStripMenuItem("E&xit")]);
            menu.Items.Add(file);
            menu.ItemClicked += (_, e) => clicked.Add("bar:" + e.ClickedItem.Text);
            form.Controls.Add(menu);
            form.MainMenuStrip = menu;
            return form;
        });

        run.Tap(menu, Center(run.Invoke(() => file.Bounds)));
        Assert.Equal(1, run.Invoke(() => form.WindowInternal!.Popups.Count));

        var popup = run.Invoke(() => form.WindowInternal!.Popups[0]);
        var item = run.Invoke(() => open.Bounds);
        run.TapWindow(form, new Point(run.Invoke(() => popup.Bounds.X) + item.X + 20, run.Invoke(() => popup.Bounds.Y) + item.Y + (item.Height / 2)));

        Assert.Equal(["open"], run.Invoke(() => clicked.ToList()));
        Assert.Empty(run.Invoke(() => form.WindowInternal!.Popups));
    }

    [AvaloniaFact]
    public void A_shortcut_key_clicks_its_menu_item()
    {
        var clicked = 0;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            var menu = new MenuStrip();
            var file = new ToolStripMenuItem("&File");
            file.DropDownItems.Add(new ToolStripMenuItem("&Save", null, (_, _) => clicked++, Keys.Control | Keys.S));
            menu.Items.Add(file);
            form.Controls.Add(menu);
            form.MainMenuStrip = menu;
            return form;
        });

        run.Key(Keys.Control | Keys.S);

        Assert.Equal(1, run.Invoke(() => clicked));
    }

    [AvaloniaFact]
    public void A_tool_bar_button_raises_Click_and_ItemClicked()
    {
        ToolStrip strip = null!;
        ToolStripButton bold = null!;
        var log = new List<string>();
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            strip = new ToolStrip();
            bold = new ToolStripButton("Bold") { CheckOnClick = true };
            bold.Click += (_, _) => log.Add("click");
            strip.ItemClicked += (_, e) => log.Add("item:" + e.ClickedItem.Text);
            strip.Items.Add(bold);
            form.Controls.Add(strip);
            return form;
        });

        run.Tap(strip, Center(run.Invoke(() => bold.Bounds)));

        Assert.Equal(["item:Bold", "click"], run.Invoke(() => log.ToList()));
        Assert.True(run.Invoke(() => bold.Checked));
    }

    [AvaloniaFact]
    public void A_combo_box_opens_its_list_and_a_tap_picks_an_item()
    {
        Form form = null!;
        ComboBox combo = null!;
        var changes = new List<int>();
        using var run = FormsTestRun.Start(() =>
        {
            form = new Form();
            combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(10, 10) };
            combo.Items.AddRange(["Red", "Green", "Blue"]);
            combo.SelectedIndexChanged += (_, _) => changes.Add(combo.SelectedIndex);
            form.Controls.Add(combo);
            return form;
        });

        run.Tap(combo);
        Assert.True(run.Invoke(() => combo.DroppedDown));

        var list = run.Invoke(() => form.WindowInternal!.Popups[0].Bounds);
        var row = run.Invoke(() => combo.ItemHeight);
        run.TapWindow(form, new Point(list.X + 20, list.Y + 1 + row + (row / 2)));

        Assert.False(run.Invoke(() => combo.DroppedDown));
        Assert.Equal("Green", run.Invoke(() => combo.Text));
        Assert.Equal([1], run.Invoke(() => changes.ToList()));
    }

    [AvaloniaFact]
    public void A_list_box_bound_to_a_list_shows_the_display_member_and_selects_by_value()
    {
        ListBox list = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            list = new ListBox { Size = new Size(150, 100) };
            list.DisplayMember = nameof(City.Name);
            list.ValueMember = nameof(City.Code);
            list.DataSource = new List<City> { new("TLV", "Tel Aviv"), new("NYC", "New York"), new("PAR", "Paris") };
            form.Controls.Add(list);
            return form;
        });

        Assert.Equal("Tel Aviv", run.Invoke(() => list.Text));

        run.Invoke(() => list.SelectedValue = "PAR");

        Assert.Equal(2, run.Invoke(() => list.SelectedIndex));
        Assert.Equal("Paris", run.Invoke(() => list.GetItemText(list.SelectedItem)));
    }

    [AvaloniaFact]
    public void A_tap_on_a_list_box_row_selects_it_and_the_arrow_keys_move_the_selection()
    {
        ListBox list = null!;
        using var run = FormsTestRun.Start(() =>
        {
            var form = new Form();
            list = new ListBox { Size = new Size(150, 100) };
            list.Items.AddRange(["One", "Two", "Three", "Four"]);
            form.Controls.Add(list);
            return form;
        });

        var row = run.Invoke(() => list.ItemHeight);
        run.Tap(list, new Point(20, row + (row / 2)));
        Assert.Equal(1, run.Invoke(() => list.SelectedIndex));

        run.Key(Keys.Down);
        Assert.Equal("Three", run.Invoke(() => list.SelectedItem));
    }

    private static Point Center(Rectangle bounds) => new(bounds.X + (bounds.Width / 2), bounds.Y + (bounds.Height / 2));

    private sealed record City(string Code, string Name);
}
