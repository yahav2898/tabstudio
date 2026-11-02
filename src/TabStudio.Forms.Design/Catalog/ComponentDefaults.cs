using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Windows.Forms;

namespace TabStudio.Forms.Design.Catalog;

/// <summary>
/// What Visual Studio's designer gives a new component and leaves out of the code: which controls show their name as
/// their text, which size themselves, which join the <c>components</c> container, the event a double-tap handles,
/// and the default value of each property, which the designer does not write.
/// </summary>
internal static class ComponentDefaults
{
    private static readonly ConcurrentDictionary<Type, object?> Prototypes = new();

    /// <summary>The types whose new instances show their name as their text, as <c>button1</c>.</summary>
    private static readonly Type[] NamedText =
    [
        typeof(ButtonBase), typeof(Label), typeof(GroupBox), typeof(TabPage), typeof(ToolStripMenuItem), typeof(ToolStripLabel),
        typeof(ToolStripButton), typeof(ToolStripDropDownButton), typeof(ToolStripSplitButton),
    ];

    /// <summary>The properties the designer always writes, whatever their value, as Visual Studio does.</summary>
    private static readonly HashSet<string> AlwaysWritten = new(StringComparer.Ordinal) { "Name", "Location", "Size", "TabIndex", "Text", "ClientSize" };

    /// <summary>The events a double-tap handles, for types without a <see cref="DefaultEventAttribute"/>.</summary>
    private static readonly (Type Type, string Event)[] DefaultEvents =
    [
        (typeof(Form), "Load"), (typeof(UserControl), "Load"), (typeof(TextBoxBase), "TextChanged"), (typeof(ComboBox), "SelectedIndexChanged"),
        (typeof(ListBox), "SelectedIndexChanged"), (typeof(CheckBox), "CheckedChanged"), (typeof(RadioButton), "CheckedChanged"),
        (typeof(NumericUpDown), "ValueChanged"), (typeof(DateTimePicker), "ValueChanged"), (typeof(TrackBar), "Scroll"),
        (typeof(DataGridView), "CellContentClick"), (typeof(System.Windows.Forms.Timer), "Tick"), (typeof(TabControl), "SelectedIndexChanged"),
        (typeof(LinkLabel), "LinkClicked"), (typeof(PictureBox), "Click"), (typeof(Panel), "Paint"), (typeof(GroupBox), "Enter"),
        (typeof(SplitContainer), "SplitterMoved"), (typeof(ProgressBar), "Click"), (typeof(ToolStrip), "ItemClicked"),
        (typeof(ToolStripItem), "Click"), (typeof(BindingSource), "CurrentChanged"), (typeof(Control), "Click"),
    ];

    /// <summary>Whether a new instance of a type shows its name as its text.</summary>
    /// <param name="type">The type.</param>
    public static bool ShowsNameAsText(Type type) => NamedText.Any(t => t.IsAssignableFrom(type));

    /// <summary>Whether the designer turns AutoSize on for a new instance, as it does for labels, check boxes and radio buttons.</summary>
    /// <param name="type">The type.</param>
    public static bool SizesItself(Type type) =>
        typeof(Label).IsAssignableFrom(type) || typeof(CheckBox).IsAssignableFrom(type) || typeof(RadioButton).IsAssignableFrom(type);

    /// <summary>Whether a new instance draws with the visual style's background, which the designer writes for buttons.</summary>
    /// <param name="type">The type.</param>
    public static bool UsesVisualStyleBackColor(Type type) => typeof(ButtonBase).IsAssignableFrom(type);

    /// <summary>Whether a component is created with the <c>components</c> container, as timers and tooltips are.</summary>
    /// <param name="type">The type.</param>
    public static bool JoinsContainer(Type type) =>
        !typeof(Control).IsAssignableFrom(type) && !typeof(ToolStripItem).IsAssignableFrom(type)
        && type.GetConstructor([typeof(IContainer)]) is not null;

    /// <summary>Whether a component is drawn on the form rather than listed below it, as a timer is.</summary>
    /// <param name="type">The type.</param>
    public static bool IsVisual(Type type) => typeof(Control).IsAssignableFrom(type) || typeof(ToolStripItem).IsAssignableFrom(type);

    /// <summary>
    /// The event a double-tap on a component handles, as in Visual Studio; null for a component without one, such as an
    /// ImageList or a ToolTip.
    /// </summary>
    /// <param name="type">The component's type.</param>
    public static string? DefaultEvent(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        if (type.GetCustomAttribute<DefaultEventAttribute>() is { Name: { Length: > 0 } name } && type.GetEvent(name) is not null)
        {
            return name;
        }

        return DefaultEvents.FirstOrDefault(d => d.Type.IsAssignableFrom(type)).Event;
    }

    /// <summary>
    /// Whether the designer writes a property's value: always for the few Visual Studio always writes, otherwise only
    /// when it differs from a new instance's.
    /// </summary>
    /// <param name="type">The component's type.</param>
    /// <param name="path">The property's path.</param>
    /// <param name="value">The value.</param>
    public static bool IsWritten(Type type, string path, object? value)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(path);
        if (AlwaysWritten.Contains(path))
        {
            return true;
        }

        return !Equals(DefaultValue(type, path), value);
    }

    /// <summary>A new instance's value of a property, or <see langword="null"/> when it cannot be read.</summary>
    /// <param name="type">The type.</param>
    /// <param name="path">The property's path.</param>
    public static object? DefaultValue(Type type, string path)
    {
        var prototype = Prototypes.GetOrAdd(type, static t =>
        {
            try
            {
                return Activator.CreateInstance(t);
            }
            catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or MemberAccessException or NotSupportedException or ArgumentException)
            {
                return null;
            }
        });
        object? current = prototype;
        foreach (var part in path.Split('.'))
        {
            if (current?.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance) is not { CanRead: true } property)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }
}
