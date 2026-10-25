using System.Xml.Linq;
using Avalonia.Headless.XUnit;
using TabStudio.App.Converters;
using TabStudio.App.Toolbox;
using TabStudio.App.Xaml;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// The Toolbox's data: Avalonia's controls, each a real control whose XAML parses and sets only properties it has, with
/// an icon in the theme; and the Properties panel's view of Avalonia's types.
/// </summary>
public sealed class ToolboxCatalogTests
{
    private const string Namespaces = "xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [AvaloniaFact]
    public void Lists_Avalonia_s_controls_with_XAML_that_parses_and_icons_that_exist()
    {
        var catalog = ToolboxCatalog.LoadEmbedded();

        var avalonia = Assert.Single(catalog.Frameworks);
        Assert.Equal("Avalonia", avalonia.Name);
        Assert.Same(avalonia, catalog.ForFile("/Projects/App/Views/MainView.axaml"));
        Assert.Null(catalog.ForFile("/Projects/App/Program.cs"));
        Assert.Null(catalog.ForFile(null));
        Assert.Equal(["Common Controls", "Layout", "Text and Input", "Lists and Navigation", "Shapes and Media"], avalonia.Categories.Select(c => c.Name));

        var items = avalonia.Categories.SelectMany(c => c.Items).ToList();
        Assert.True(items.Count >= 40, $"{items.Count} controls");
        Assert.Equal(items.Count, items.Select(i => i.Name).Distinct().Count());
        foreach (var item in items)
        {
            var element = XDocument.Parse($"<Root {Namespaces}>{item.Xaml}</Root>").Root!.Elements().Single();
            Assert.Equal(item.Name, element.Name.LocalName);
            var type = AvaloniaXamlTypes.Find(item.Name) ?? throw new Xunit.Sdk.XunitException($"Avalonia has no {item.Name}.");
            var properties = AvaloniaXamlTypes.PropertiesOf(type).Select(p => p.Name).ToHashSet();
            foreach (var attribute in element.Attributes())
            {
                Assert.True(properties.Contains(attribute.Name.LocalName), $"{item.Name} has no {attribute.Name.LocalName}.");
            }

            Assert.True(DesktopModeConverters.HasToolboxIcon(item.Icon), $"{item.Name}'s icon {item.Icon} is not in the theme.");
            Assert.False(string.IsNullOrWhiteSpace(item.Description));
        }

        Assert.All(avalonia.Containers, name => Assert.NotNull(AvaloniaXamlTypes.Find(name)));
        Assert.True(avalonia.IsContainer("StackPanel"));
        Assert.False(avalonia.IsContainer("Button"));
        Assert.Equal("Layout", avalonia.CategoryOf("Margin"));
        Assert.Null(avalonia.CategoryOf("Tag"));
        Assert.Equal(["ToolTip.Tip", "Grid.Row", "Grid.Column", "Grid.RowSpan", "Grid.ColumnSpan"], avalonia.AttachedPropertiesFor("Grid"));
        Assert.All(avalonia.AttachedProperties.Values.SelectMany(names => names), name => Assert.NotNull(AvaloniaXamlTypes.Attached(name)));
    }

    [AvaloniaFact]
    public void Knows_the_properties_of_Avalonia_s_types_with_choices_for_enums_and_booleans()
    {
        var button = AvaloniaXamlTypes.Find("Button")!;
        var properties = AvaloniaXamlTypes.PropertiesOf(button).ToDictionary(p => p.Name);

        Assert.Equal("Avalonia.Controls.Button", button.FullName);
        Assert.Equal("object", properties["Content"].TypeName);
        Assert.Null(properties["Content"].Choices);
        Assert.Equal("Thickness", properties["Margin"].TypeName);
        Assert.Equal(["Stretch", "Left", "Center", "Right"], properties["HorizontalAlignment"].Choices);
        Assert.Equal(["True", "False"], properties["IsEnabled"].Choices);
        Assert.DoesNotContain("IsPressed", properties.Keys);

        Assert.Contains(AvaloniaXamlTypes.PropertiesOf(AvaloniaXamlTypes.Find("Grid")!), p => p.Name == "ColumnDefinitions");
        Assert.Equal("int", AvaloniaXamlTypes.Attached("Grid.Row")!.TypeName);
        Assert.Equal(["Left", "Bottom", "Right", "Top"], AvaloniaXamlTypes.Attached("DockPanel.Dock")!.Choices);
        Assert.Null(AvaloniaXamlTypes.Attached("Grid.Nothing"));
        Assert.Null(AvaloniaXamlTypes.Find("NoSuchControl"));
    }

    [Fact]
    public void Says_what_is_wrong_with_a_framework_file()
    {
        using var broken = new MemoryStream("{ \"name\": "u8.ToArray());
        using var nameless = new MemoryStream("{ \"categories\": [] }"u8.ToArray());

        Assert.Contains("Broken.json", Assert.Throws<InvalidDataException>(() => ToolboxCatalog.Read(broken, "Broken.json")).Message, StringComparison.Ordinal);
        Assert.Contains("does not name", Assert.Throws<InvalidDataException>(() => ToolboxCatalog.Read(nameless, "Nameless.json")).Message, StringComparison.Ordinal);
    }
}
