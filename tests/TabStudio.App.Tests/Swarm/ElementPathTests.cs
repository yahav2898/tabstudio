using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using TabStudio.App.Swarm;

namespace TabStudio.App.Tests.Swarm;

/// <summary>
/// Element paths name an element so the same element is found on every screen: names where elements have them,
/// otherwise the type and index among logical siblings, items by their index, and template parts as their control.
/// </summary>
public sealed class ElementPathTests
{
    [AvaloniaFact]
    public void An_element_is_named_by_its_name_or_its_type_and_index_from_the_root()
    {
        var view = new SampleView();
        var window = SwarmTestHelpers.Show(view, 360, 640);

        Assert.Equal("SampleView", ElementPaths.Describe(view, view));
        Assert.Equal("SampleView/DockPanel[0]/StackPanel[0]/Button#Save", ElementPaths.Describe(view, view.Save));
        Assert.Equal("SampleView/DockPanel[0]/StackPanel[0]/TextBlock[1]", ElementPaths.Describe(view, view.Subtitle));
        Assert.Equal("SampleView/DockPanel[0]/StackPanel[0]/Grid[0]/TextBox[1]", ElementPaths.Describe(view, view.Second));
        Assert.Equal("SampleView/DockPanel[0]/ListBox#Items/ListBoxItem[2]", ElementPaths.Describe(view, view.Items.ContainerFromIndex(2)!));
        window.Close();
    }

    [AvaloniaFact]
    public void A_part_of_a_control_s_template_is_named_as_the_control_and_content_as_the_control_s_child()
    {
        var view = new SampleView();
        var window = SwarmTestHelpers.Show(view, 360, 640);

        // The button's border and presenter come from its template; its text is its content.
        var presenter = view.Save.GetVisualDescendants().OfType<ContentPresenter>().First();
        var text = view.Save.GetLogicalDescendants().OfType<TextBlock>().Single();
        Assert.Equal("SampleView/DockPanel[0]/StackPanel[0]/Button#Save", ElementPaths.Describe(view, presenter));
        // The text is a TextBlock, or an AccessText where the button's theme recognizes access keys.
        Assert.Equal($"SampleView/DockPanel[0]/StackPanel[0]/Button#Save/{text.GetType().Name}[0]", ElementPaths.Describe(view, text));
        Assert.Same(view.Save, ElementPaths.Addressable(view, presenter));
        Assert.Null(ElementPaths.Describe(view, new Button()));
        window.Close();
    }

    [AvaloniaFact]
    public void Paths_written_on_a_phone_find_the_same_elements_on_a_tablet()
    {
        var phone = new SampleView();
        var tablet = new SampleView();
        var phoneWindow = SwarmTestHelpers.Show(phone, 360, 640);
        var tabletWindow = SwarmTestHelpers.Show(tablet, 800, 1280);

        foreach (var (onPhone, onTablet) in new (Control, Control)[] { (phone.Save, tablet.Save), (phone.Subtitle, tablet.Subtitle), (phone.Second, tablet.Second) })
        {
            var path = ElementPaths.Describe(phone, onPhone)!;
            Assert.Same(onTablet, ElementPaths.Resolve(tablet, path));
        }

        // The tablet shows more items; an item the phone has not created yet is scrolled into view to be found.
        var item = ElementPaths.Describe(tablet, tablet.Items.ContainerFromIndex(6)!)!;
        Assert.Null(phone.Items.ContainerFromIndex(60));
        var far = ElementPaths.Resolve(phone, item.Replace("[6]", "[60]", StringComparison.Ordinal));
        Assert.Equal("Item 60", Assert.IsType<ListBoxItem>(far).Content);
        Assert.Equal("Item 6", Assert.IsType<ListBoxItem>(ElementPaths.Resolve(phone, item)).Content);

        // A path from a screen with a wrapper the other does not have still finds a named element.
        Assert.Same(tablet.Save, ElementPaths.Resolve(tablet, "SampleView/Border[0]/DockPanel[0]/StackPanel[0]/Button#Save"));
        Assert.Null(ElementPaths.Resolve(tablet, "SampleView/DockPanel[0]/StackPanel[0]/Button[7]"));
        phoneWindow.Close();
        tabletWindow.Close();
    }

    /// <summary>A small app view: named and unnamed elements, a grid, and a long list below them.</summary>
    private sealed class SampleView : UserControl
    {
        public SampleView()
        {
            Save = new Button { Name = "Save", Content = "Save" };
            Subtitle = new TextBlock { Text = "Second line" };
            Second = new TextBox { Text = "Second box" };
            Items = new ListBox { Name = "Items", ItemsSource = Enumerable.Range(0, 100).Select(i => $"Item {i}").ToList() };
            var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
            grid.Children.Add(new TextBox { Text = "First box" });
            grid.Children.Add(Second);
            Grid.SetColumn(Second, 1);
            // The list fills what the screen has left, so a tall screen creates more of its items.
            var top = new StackPanel { Children = { new TextBlock { Text = "Title" }, Subtitle, Save, grid } };
            DockPanel.SetDock(top, Dock.Top);
            Content = new DockPanel { Children = { top, Items } };
        }

        public Button Save { get; }

        public TextBlock Subtitle { get; }

        public TextBox Second { get; }

        public ListBox Items { get; }
    }
}
