using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// Reads element paths, such as <c>MainView/StackPanel[0]/Button#CountButton</c>, as the screens write them for every
/// input (<see cref="AppInput.ElementPath"/>): finds the element a path names, tolerant of templates and screen sizes,
/// and the text an element shows, for the checks of recorded UI tests.
/// </summary>
/// <remarks>
/// <para>
/// A segment is <c>Type#Name</c> for a named element and <c>Type[index]</c> otherwise, the index counting the logical
/// children of that type. A path is followed from the root through logical children; when that fails, an element
/// named like the last segment is looked for anywhere, so a path survives a layout panel added around it. A bare name,
/// such as <c>CountButton</c>, is the short form recorded tests use for named elements.
/// </para>
/// <para>The generated UI tests' driver follows the same rules (see <c>UiTestDriverSource</c>).</para>
/// </remarks>
public static class ElementPaths
{
    /// <summary>The shortest form of a path that finds the same element: its name when it has one, otherwise the path.</summary>
    /// <param name="path">The path.</param>
    public static string ShortName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var last = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? path;
        var hash = last.IndexOf('#', StringComparison.Ordinal);
        return hash >= 0 && hash < last.Length - 1 ? last[(hash + 1)..] : path;
    }

    /// <summary>Finds the element a path names under an app's root.</summary>
    /// <param name="root">The app's root element.</param>
    /// <param name="path">The path, or a bare element name.</param>
    /// <returns>The element, or <see langword="null"/> when there is none.</returns>
    public static Control? Find(Control root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return null;
        }

        if (segments.Length == 1 && !segments[0].Contains('#', StringComparison.Ordinal) && !segments[0].Contains('[', StringComparison.Ordinal))
        {
            return FindByName(root, segments[0]) ?? (Matches(root, segments[0], null) ? root : null);
        }

        if (Walk(root, segments) is { } walked)
        {
            return walked;
        }

        var (type, name, _) = Parse(segments[^1]);
        return name is null ? null : FindByName(root, name) is { } named && Matches(named, type, name) ? named : null;
    }

    /// <summary>The text an element shows: its own text, or the texts inside it joined by spaces.</summary>
    /// <param name="element">The element.</param>
    public static string TextOf(Control element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element switch
        {
            TextBlock text => text.Text ?? string.Empty,
            TextBox box => box.Text ?? string.Empty,
            ContentControl { Content: string content } => content,
            _ => string.Join(' ', element.GetLogicalDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t))),
        };
    }

    private static Control? Walk(Control root, string[] segments)
    {
        Control current = root;
        var start = 0;
        if (Matches(root, Parse(segments[0]).Type, Parse(segments[0]).Name))
        {
            start = 1;
        }

        for (var i = start; i < segments.Length; i++)
        {
            var (type, name, index) = Parse(segments[i]);
            var children = current.GetLogicalChildren().OfType<Control>().ToList();
            Control? next;
            if (name is not null)
            {
                next = children.FirstOrDefault(c => c.Name == name && Matches(c, type, name));
            }
            else
            {
                var ofType = children.Where(c => Matches(c, type, null)).ToList();
                next = index < ofType.Count ? ofType[index] : index < children.Count ? children[index] : null;
            }

            if (next is null)
            {
                return null;
            }

            current = next;
        }

        return current;
    }

    private static Control? FindByName(Control root, string name) =>
        root.GetLogicalDescendants().OfType<Control>().FirstOrDefault(c => c.Name == name);

    private static bool Matches(Control control, string? type, string? name) =>
        (type is null || control.GetType().Name == type || IsTypeOf(control.GetType(), type)) && (name is null || control.Name == name);

    private static bool IsTypeOf(Type type, string name)
    {
        for (var current = type.BaseType; current is not null && current != typeof(StyledElement); current = current.BaseType)
        {
            if (current.Name == name)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A segment's type, name and index: <c>Button#Save</c>, <c>StackPanel[1]</c>, <c>#Save</c> or <c>Grid</c>.</summary>
    private static (string? Type, string? Name, int Index) Parse(string segment)
    {
        var hash = segment.IndexOf('#', StringComparison.Ordinal);
        if (hash >= 0)
        {
            return (hash == 0 ? null : segment[..hash], segment[(hash + 1)..], 0);
        }

        var open = segment.IndexOf('[', StringComparison.Ordinal);
        if (open > 0 && segment.EndsWith(']') && int.TryParse(segment[(open + 1)..^1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index))
        {
            return (segment[..open], null, index);
        }

        return (segment, null, 0);
    }
}
