namespace TabStudio.App.Toolbox;

/// <summary>A control in the Toolbox: what a tap inserts at the caret.</summary>
public sealed class ToolboxItem
{
    /// <summary>The control's name, such as <c>Button</c>.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>What it is for, in a few words.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The icon, the name of the geometry <c>Icon.Toolbox.&lt;Icon&gt;</c>.</summary>
    public string Icon { get; init; } = string.Empty;

    /// <summary>The XAML a tap inserts, with two spaces per level of nesting.</summary>
    public string Xaml { get; init; } = string.Empty;
}

/// <summary>A group of controls in the Toolbox, such as "Layout".</summary>
public sealed class ToolboxCategory
{
    /// <summary>The group's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Its controls, in the order the Toolbox lists them.</summary>
    public IReadOnlyList<ToolboxItem> Items { get; init; } = [];
}

/// <summary>A group of properties in the Properties panel, such as "Layout".</summary>
public sealed class PropertyCategory
{
    /// <summary>The group's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The properties in it, by their XAML names, such as <c>Margin</c> or <c>Grid.Row</c>.</summary>
    public IReadOnlyList<string> Properties { get; init; } = [];
}

/// <summary>
/// A UI framework's controls and properties for the Toolbox and the Properties panel of desktop mode, read from a
/// JSON file in the app (Toolbox/Avalonia.json), so another framework is a matter of adding its file.
/// </summary>
public sealed class ToolboxFramework
{
    /// <summary>The framework's name, such as "Avalonia".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The extensions of the XAML files it applies to, such as <c>.axaml</c>.</summary>
    public IReadOnlyList<string> FileExtensions { get; init; } = [];

    /// <summary>The XML namespace of its controls, such as <c>https://github.com/avaloniaui</c>.</summary>
    public string XmlNamespace { get; init; } = string.Empty;

    /// <summary>The Toolbox's groups of controls.</summary>
    public IReadOnlyList<ToolboxCategory> Categories { get; init; } = [];

    /// <summary>The elements that hold other elements, by name, such as <c>StackPanel</c> and <c>Border</c>.</summary>
    public IReadOnlyList<string> Containers { get; init; } = [];

    /// <summary>Attributes every element can have, such as <c>x:Name</c>.</summary>
    public IReadOnlyList<string> CommonAttributes { get; init; } = [];

    /// <summary>
    /// The attached properties a parent gives its children, by the parent's name, such as <c>Grid.Row</c> for
    /// <c>Grid</c>; those under <c>*</c> apply to every element.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> AttachedProperties { get; init; } = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>The Properties panel's groups, in order; properties in none of them are listed under "Other".</summary>
    public IReadOnlyList<PropertyCategory> PropertyCategories { get; init; } = [];

    /// <summary>Whether the framework's Toolbox and Properties apply to a file.</summary>
    /// <param name="path">The file's path or name.</param>
    public bool AppliesTo(string path)
    {
        var extension = Path.GetExtension(path);
        foreach (var candidate in FileExtensions)
        {
            if (string.Equals(candidate, extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether elements of a type hold other elements.</summary>
    /// <param name="localName">The element's name without prefix, such as <c>StackPanel</c>.</param>
    public bool IsContainer(string localName) => Containers.Contains(localName, StringComparer.Ordinal);

    /// <summary>The Properties panel's group of a property, or <see langword="null"/> when it is in none.</summary>
    /// <param name="property">The property's XAML name.</param>
    public string? CategoryOf(string property)
    {
        foreach (var category in PropertyCategories)
        {
            if (category.Properties.Contains(property, StringComparer.Ordinal))
            {
                return category.Name;
            }
        }

        return null;
    }

    /// <summary>The attached properties an element can set: those every element has and those its parent gives it.</summary>
    /// <param name="parentLocalName">The parent element's name without prefix, if it has a parent.</param>
    public IReadOnlyList<string> AttachedPropertiesFor(string? parentLocalName)
    {
        var properties = new List<string>();
        if (AttachedProperties.TryGetValue("*", out var everywhere))
        {
            properties.AddRange(everywhere);
        }

        if (parentLocalName is not null && AttachedProperties.TryGetValue(parentLocalName, out var fromParent))
        {
            properties.AddRange(fromParent);
        }

        return properties;
    }
}
