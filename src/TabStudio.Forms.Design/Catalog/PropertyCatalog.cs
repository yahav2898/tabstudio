using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace TabStudio.Forms.Design.Catalog;

/// <summary>How the Properties panel edits a property.</summary>
internal enum PropertyEditor
{
    /// <summary>A line of text.</summary>
    Text,

    /// <summary>Several lines of text.</summary>
    MultilineText,

    /// <summary>A whole number.</summary>
    Integer,

    /// <summary>A decimal number.</summary>
    Decimal,

    /// <summary>On or off.</summary>
    Boolean,

    /// <summary>One value of an enumeration.</summary>
    Choice,

    /// <summary>A location: X and Y.</summary>
    Point,

    /// <summary>A size: width and height.</summary>
    Size,

    /// <summary>The edges a control is anchored to.</summary>
    Anchor,

    /// <summary>A color.</summary>
    Color,

    /// <summary>A font's size in points.</summary>
    FontSize,

    /// <summary>A font's family.</summary>
    FontFamily,
}

/// <summary>A property the Properties panel shows.</summary>
/// <param name="Path">The property's path, such as <c>Text</c>, or <c>Font</c> for the font's size and family.</param>
/// <param name="Label">What the panel calls it.</param>
/// <param name="Category">Its group, as in Visual Studio: Appearance, Behavior, Layout, Design.</param>
/// <param name="Editor">How it is edited.</param>
/// <param name="ValueType">The property's type.</param>
internal sealed record DesignProperty(string Path, string Label, string Category, PropertyEditor Editor, Type ValueType);

/// <summary>
/// The properties and events the Properties panel shows for a component: those people set most, in Visual Studio's
/// categories, as far as the component's type has them.
/// </summary>
internal static class PropertyCatalog
{
    private static readonly (string Path, string Category, PropertyEditor? Editor)[] ControlProperties =
    [
        ("Name", "Design", PropertyEditor.Text),
        ("Text", "Appearance", null),
        ("Font", "Appearance", PropertyEditor.FontFamily),
        ("Font.Size", "Appearance", PropertyEditor.FontSize),
        ("ForeColor", "Appearance", PropertyEditor.Color),
        ("BackColor", "Appearance", PropertyEditor.Color),
        ("TextAlign", "Appearance", null),
        ("BorderStyle", "Appearance", null),
        ("FlatStyle", "Appearance", null),
        ("FormBorderStyle", "Appearance", null),
        ("Checked", "Appearance", null),
        ("Enabled", "Behavior", PropertyEditor.Boolean),
        ("Visible", "Behavior", PropertyEditor.Boolean),
        ("ReadOnly", "Behavior", null),
        ("Multiline", "Behavior", null),
        ("UseSystemPasswordChar", "Behavior", null),
        ("DropDownStyle", "Behavior", null),
        ("Minimum", "Behavior", null),
        ("Maximum", "Behavior", null),
        ("Value", "Behavior", null),
        ("Format", "Behavior", null),
        ("SizeMode", "Behavior", null),
        ("Interval", "Behavior", null),
        ("TabIndex", "Behavior", PropertyEditor.Integer),
        ("ControlBox", "Window Style", null),
        ("MaximizeBox", "Window Style", null),
        ("MinimizeBox", "Window Style", null),
        ("StartPosition", "Layout", null),
        ("Location", "Layout", PropertyEditor.Point),
        ("Size", "Layout", PropertyEditor.Size),
        ("ClientSize", "Layout", PropertyEditor.Size),
        ("Anchor", "Layout", PropertyEditor.Anchor),
        ("Dock", "Layout", PropertyEditor.Choice),
        ("AutoSize", "Layout", PropertyEditor.Boolean),
        ("Padding", "Layout", null),
    ];

    private static readonly string[] CommonEvents =
    [
        "Click", "DoubleClick", "TextChanged", "CheckedChanged", "SelectedIndexChanged", "ValueChanged", "Scroll", "Tick",
        "CellClick", "CellContentClick", "CellValueChanged", "SelectionChanged", "ItemClicked", "LinkClicked",
        "Load", "Shown", "FormClosing", "FormClosed", "Activated", "KeyDown", "KeyPress", "KeyUp",
        "MouseDown", "MouseUp", "MouseMove", "Enter", "Leave", "Validating", "Paint", "Resize",
    ];

    /// <summary>The properties the panel shows for a type, in order.</summary>
    /// <param name="type">The component's type.</param>
    /// <param name="isRoot">Whether it is the form being designed, which shows its client size rather than its location.</param>
    public static IReadOnlyList<DesignProperty> PropertiesOf(Type type, bool isRoot)
    {
        ArgumentNullException.ThrowIfNull(type);
        var properties = new List<DesignProperty>();
        foreach (var (path, category, editor) in ControlProperties)
        {
            if (isRoot && path is "Location" or "Size" or "Anchor" or "Dock" or "TabIndex" or "Visible")
            {
                continue;
            }

            if (!isRoot && path == "ClientSize")
            {
                continue;
            }

            if (Resolve(type, path) is not { } property || !property.CanWrite && path != "Font.Size")
            {
                continue;
            }

            if ((editor ?? EditorFor(property.PropertyType, path)) is { } kind)
            {
                properties.Add(new DesignProperty(path, path == "Font.Size" ? "Font Size" : path == "Font" ? "Font" : property.Name, category, kind, property.PropertyType));
            }
        }

        return properties;
    }

    /// <summary>The events the panel shows for a type: the common ones it has, its default event first.</summary>
    /// <param name="type">The component's type.</param>
    public static IReadOnlyList<string> EventsOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        var defaultEvent = ComponentDefaults.DefaultEvent(type);
        return [.. CommonEvents.Where(e => type.GetEvent(e) is not null).OrderBy(e => e == defaultEvent ? 0 : 1)];
    }

    private static PropertyInfo? Resolve(Type type, string path)
    {
        PropertyInfo? property = null;
        foreach (var part in path.Split('.'))
        {
            property = type.GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
            if (property is null)
            {
                return null;
            }

            type = property.PropertyType;
        }

        return property;
    }

    private static PropertyEditor? EditorFor(Type type, string path) => type switch
    {
        _ when type == typeof(string) => path == "Text" ? PropertyEditor.MultilineText : PropertyEditor.Text,
        _ when type == typeof(bool) => PropertyEditor.Boolean,
        _ when type == typeof(int) => PropertyEditor.Integer,
        _ when type == typeof(decimal) => PropertyEditor.Decimal,
        _ when type == typeof(Color) => PropertyEditor.Color,
        _ when type.IsEnum => PropertyEditor.Choice,
        _ => null,
    };
}
