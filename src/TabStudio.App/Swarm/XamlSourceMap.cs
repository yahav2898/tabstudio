using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using TabStudio.App.Previews;
using TabStudio.App.Xaml;

namespace TabStudio.App.Swarm;

/// <summary>Where an element of a running app is written in its XAML.</summary>
/// <param name="AssemblyName">The assembly the XAML is compiled into, which names its project.</param>
/// <param name="AppPath">The XAML file's path in its project, such as <c>/Views/MainView.axaml</c>.</param>
/// <param name="Start">The offset of the element's start tag.</param>
/// <param name="Length">The length of its start tag.</param>
/// <param name="Line">The 1-based line of the start tag.</param>
/// <param name="Column">The 1-based column of the start tag.</param>
/// <param name="Snippet">The element's XAML, shortened when long, for Claude.</param>
public sealed record XamlSourceLocation(string AssemblyName, string AppPath, int Start, int Length, int Line, int Column, string Snippet);

/// <summary>
/// Finds where an element of a running app is written in the app's XAML, through the same positions the XAML
/// designer's tools edit by: the XAML compiled into the app is read with <see cref="XamlMarkup"/>, and the element's
/// logical path from the view that owns the XAML is walked through it, by name where the element has one, otherwise
/// by type and index, and into an items control's item template for its items.
/// </summary>
/// <remarks>
/// An element the XAML did not write, such as one a view model's code created, maps to the nearest element above it
/// that the XAML did write.
/// </remarks>
internal static class XamlSourceMap
{
    private const int SnippetLines = 30;

    /// <summary>Property elements whose children are the element's own logical children.</summary>
    private static readonly string[] ContentProperties = [".Content", ".Child", ".Children", ".Items", ".Header", ".Pane"];

    /// <summary>Finds an element in its XAML.</summary>
    /// <param name="element">An element of the running app.</param>
    /// <returns>Where it is written, or <see langword="null"/> when no XAML of the app holds it.</returns>
    public static XamlSourceLocation? Find(Control element)
    {
        ArgumentNullException.ThrowIfNull(element);

        // A view's instance is found where it is used, and the app's root view, used nowhere, in its own XAML.
        return Find(element, inOwnXaml: false) ?? Find(element, inOwnXaml: true);
    }

    private static XamlSourceLocation? Find(Control element, bool inOwnXaml)
    {
        // The steps from the view that owns the XAML down to the element.
        var chain = new List<ILogical>();
        ILogical? current = element;
        (string Xaml, string Path)? document = null;
        while (current is not null)
        {
            if (inOwnXaml || !ReferenceEquals(current, element))
            {
                document = AvaloniaAppHost.XamlOf(current.GetType().Assembly, current.GetType());
                if (document is not null)
                {
                    break;
                }
            }

            var parent = current.LogicalParent;
            if (parent is not null && !parent.LogicalChildren.Contains(current))
            {
                // A part of a control's template is not in any XAML of the app: start again from the control.
                chain.Clear();
            }
            else
            {
                chain.Add(current);
            }

            current = parent;
        }

        if (current is null || document is null)
        {
            return null;
        }

        var (xaml, path) = document.Value;
        chain.Reverse();
        var markup = XamlMarkup.Parse(xaml);
        if (markup.Root is not { } written)
        {
            return null;
        }

        var parentControl = current;
        var inTemplate = false;
        foreach (var step in chain)
        {
            var next = inTemplate ? FirstObject(written) : Child(markup, written, parentControl, step);
            inTemplate = false;
            if (next is null && parentControl is ItemsControl items && step is Control container && items.IndexFromContainer(container) >= 0
                && ItemTemplate(written) is { } template)
            {
                // A container the items control made: its content is the item template's.
                next = template;
                inTemplate = true;
            }

            if (next is null)
            {
                break;
            }

            written = next;
            parentControl = step;
        }

        return Location(current.GetType().Assembly.GetName().Name ?? string.Empty, path, xaml, written);
    }

    /// <summary>The element of the XAML that wrote a logical child, by its name or by its type and index.</summary>
    private static XamlElement? Child(XamlMarkup markup, XamlElement parent, ILogical parentControl, ILogical child)
    {
        if (child is StyledElement { Name: { Length: > 0 } name })
        {
            foreach (var element in markup.Elements)
            {
                if (!element.IsPropertyElement && (element.FindAttribute("x:Name")?.Value == name || element.FindAttribute("Name")?.Value == name))
                {
                    return element;
                }
            }
        }

        var type = child.GetType().Name;
        var index = 0;
        foreach (var sibling in parentControl.LogicalChildren)
        {
            if (ReferenceEquals(sibling, child))
            {
                break;
            }

            if (sibling.GetType().Name == type)
            {
                index++;
            }
        }

        var written = Objects(parent).Where(e => e.LocalName == type).ToList();
        return index < written.Count ? written[index] : null;
    }

    /// <summary>The objects an element holds: those written inside it, and inside its content properties.</summary>
    private static IEnumerable<XamlElement> Objects(XamlElement element)
    {
        foreach (var child in element.Children)
        {
            if (!child.IsPropertyElement)
            {
                yield return child;
            }
            else if (ContentProperties.Any(p => child.LocalName.EndsWith(p, StringComparison.Ordinal)))
            {
                foreach (var inner in child.Children.Where(c => !c.IsPropertyElement))
                {
                    yield return inner;
                }
            }
        }
    }

    /// <summary>The data template of an items control's <c>ItemTemplate</c>, if its XAML writes one.</summary>
    private static XamlElement? ItemTemplate(XamlElement itemsControl) =>
        itemsControl.Children
            .Where(c => c.IsPropertyElement && c.LocalName.EndsWith(".ItemTemplate", StringComparison.Ordinal))
            .SelectMany(c => c.Children)
            .FirstOrDefault(c => !c.IsPropertyElement);

    private static XamlElement? FirstObject(XamlElement element) => Objects(element).FirstOrDefault();

    private static XamlSourceLocation Location(string assemblyName, string path, string xaml, XamlElement element)
    {
        var (line, column) = LineAndColumn(xaml, element.Start);
        var end = element.End > element.Start ? element.End : element.StartTagEnd;
        var snippetLines = xaml[element.Start..Math.Min(end, xaml.Length)].Split('\n');
        var snippet = snippetLines.Length <= SnippetLines
            ? string.Join('\n', snippetLines)
            : string.Join('\n', snippetLines.Take(SnippetLines)) + "\n  …";
        var length = Math.Max(1, (element.StartTagEnd > element.Start ? element.StartTagEnd : element.NameEnd) - element.Start);
        return new XamlSourceLocation(assemblyName, path, element.Start, length, line, column, Indent(snippet, column));
    }

    /// <summary>Gives the snippet's first line the indentation the others have in the file.</summary>
    private static string Indent(string snippet, int column) => new string(' ', Math.Max(0, column - 1)) + snippet;

    /// <summary>The 1-based line and column of an offset.</summary>
    internal static (int Line, int Column) LineAndColumn(string text, int offset)
    {
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, offset - lineStart + 1);
    }
}
