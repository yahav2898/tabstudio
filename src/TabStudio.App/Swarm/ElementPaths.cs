using System.Buffers;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// Element paths: how an input or a layout issue names an element so that the same element is found on every screen,
/// whatever its size, such as <c>MainView/StackPanel[0]/Button#CountButton</c>.
/// </summary>
/// <remarks>
/// <para>
/// A path walks the app's logical tree from its root, one segment per level. A segment is the element's type, then
/// <c>#Name</c> when it has a name, otherwise <c>[n]</c>, its index among the siblings of the same type. The logical
/// tree is what the XAML wrote, so it is the same on a phone and a tablet; controls' templates are left out, so a tap
/// on the text inside a button names the button.
/// </para>
/// <para>
/// The items of an items control are numbered by their index in the items, not among the containers created, so an
/// item that a small screen has not created yet is still found: it is scrolled into view first.
/// </para>
/// </remarks>
public static class ElementPaths
{
    /// <summary>The character between segments.</summary>
    public const char Separator = '/';

    /// <summary>Names an element by its path from the root.</summary>
    /// <param name="root">The app's root element on its screen.</param>
    /// <param name="element">An element under the root, such as the one a tap hit; a part of a control's template names the control.</param>
    /// <returns>The path, or <see langword="null"/> when the element is not under the root.</returns>
    public static string? Describe(Visual root, Visual element)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(element);
        if (Chain(root, element) is not { } chain)
        {
            return null;
        }

        var path = new StringBuilder(Segment(root, parent: null));
        for (var i = 1; i < chain.Count; i++)
        {
            path.Append(Separator).Append(Segment(chain[i], chain[i - 1]));
        }

        return path.ToString();
    }

    /// <summary>The element a path names, on this screen.</summary>
    /// <param name="root">The app's root element on its screen.</param>
    /// <param name="path">The path, as <see cref="Describe"/> wrote it, on this screen or another.</param>
    /// <returns>The element, or <see langword="null"/> when this screen does not have it.</returns>
    public static Control? Resolve(Visual root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        if (root is not Control rootControl || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var segments = path.Split(Separator);
        Control current = rootControl;
        for (var i = 1; i < segments.Length; i++)
        {
            if (!PathSegment.TryParse(segments[i], out var segment) || Child(current, segment) is not { } child)
            {
                // A wrapper the other screen does not have: a named target is still found by its name.
                return PathSegment.TryParse(segments[^1], out var last) && last.Name is { } name ? FindByName(rootControl, last.Type, name) : null;
            }

            current = child;
        }

        return current;
    }

    /// <summary>
    /// The element a path would name for this one: itself, or the control whose template it is part of, as long as
    /// it is under the root.
    /// </summary>
    /// <param name="root">The app's root element on its screen.</param>
    /// <param name="element">An element under the root.</param>
    /// <returns>The element, or <see langword="null"/> when it is not under the root.</returns>
    public static Control? Addressable(Visual root, Visual element)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(element);
        return Chain(root, element) is { } chain ? chain[^1] as Control : null;
    }

    /// <summary>
    /// The logical path from the root down to the element, or to the control whose template holds it: each element in
    /// it is a logical child of the one before, the root first.
    /// </summary>
    private static List<ILogical>? Chain(Visual root, Visual element)
    {
        var chain = new List<ILogical>();
        ILogical? current = element;
        while (current is not null && !ReferenceEquals(current, root))
        {
            var parent = current.LogicalParent;
            if (parent is null)
            {
                return null;
            }

            if (IsLogicalChild(parent, current))
            {
                chain.Add(current);
            }
            else
            {
                // A part of a control's template: the path names the control.
                chain.Clear();
            }

            current = parent;
        }

        if (current is null)
        {
            return null;
        }

        chain.Add(current);
        chain.Reverse();
        return chain;
    }

    private static bool IsLogicalChild(ILogical parent, ILogical child)
    {
        var children = parent.LogicalChildren;
        for (var i = 0; i < children.Count; i++)
        {
            if (ReferenceEquals(children[i], child))
            {
                return true;
            }
        }

        return false;
    }

    private static string Segment(ILogical element, ILogical? parent)
    {
        var type = TypeName(element);
        if (element is StyledElement { Name: { Length: > 0 } name } && PathSegment.IsValidName(name))
        {
            return $"{type}#{name}";
        }

        if (parent is null)
        {
            return type;
        }

        var index = parent is ItemsControl items && element is Control container && items.IndexFromContainer(container) is >= 0 and var item
            ? item
            : SiblingIndex(parent, element, type);
        return string.Create(CultureInfo.InvariantCulture, $"{type}[{index}]");
    }

    private static int SiblingIndex(ILogical parent, ILogical element, string type)
    {
        var index = 0;
        var children = parent.LogicalChildren;
        for (var i = 0; i < children.Count; i++)
        {
            if (ReferenceEquals(children[i], element))
            {
                return index;
            }

            if (TypeName(children[i]) == type)
            {
                index++;
            }
        }

        return index;
    }

    private static Control? Child(Control parent, PathSegment segment)
    {
        var children = ((ILogical)parent).LogicalChildren;
        if (segment.Name is { } name)
        {
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is Control child && child.Name == name)
                {
                    return child;
                }
            }

            return null;
        }

        // An item of an items control, by its index in the items, created and scrolled into view when it is not yet.
        if (parent is ItemsControl items && segment.Index < items.ItemCount && ContainerFromIndex(items, segment.Index) is { } container && TypeName(container) == segment.Type)
        {
            return container;
        }

        var index = 0;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is Control child && TypeName(child) == segment.Type && index++ == segment.Index)
            {
                return child;
            }
        }

        return null;
    }

    private static Control? ContainerFromIndex(ItemsControl items, int index)
    {
        if (items.ContainerFromIndex(index) is { } realized)
        {
            return realized;
        }

        items.ScrollIntoView(index);
        items.UpdateLayout();
        return items.ContainerFromIndex(index);
    }

    private static Control? FindByName(Control root, string type, string name)
    {
        Control? found = null;
        foreach (var element in root.GetLogicalDescendants())
        {
            if (element is Control control && control.Name == name && TypeName(control) == type)
            {
                if (found is not null)
                {
                    // The name is not unique: guessing could tap the wrong element.
                    return null;
                }

                found = control;
            }
        }

        return found;
    }

    /// <summary>A type's name without its generic arity, as segments write it.</summary>
    private static string TypeName(object element)
    {
        var name = element.GetType().Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }

    /// <summary>One segment of a path: a type, then a name or an index.</summary>
    private readonly record struct PathSegment(string Type, string? Name, int Index)
    {
        /// <summary>The characters a name in a segment cannot have.</summary>
        private static readonly SearchValues<char> Reserved = SearchValues.Create("/#[]");

        /// <summary>Whether a name can be written in a segment, rather than an index.</summary>
        public static bool IsValidName(string name) => name.AsSpan().IndexOfAny(Reserved) < 0;

        public static bool TryParse(string text, out PathSegment segment)
        {
            segment = default;
            var hash = text.IndexOf('#', StringComparison.Ordinal);
            if (hash > 0)
            {
                segment = new PathSegment(text[..hash], text[(hash + 1)..], 0);
                return segment.Name!.Length > 0;
            }

            var bracket = text.IndexOf('[', StringComparison.Ordinal);
            if (bracket > 0 && text.EndsWith(']')
                && int.TryParse(text.AsSpan(bracket + 1, text.Length - bracket - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                segment = new PathSegment(text[..bracket], null, index);
                return true;
            }

            // A segment without an index, such as the root's, is the first of its type.
            segment = new PathSegment(text, null, 0);
            return text.Length > 0;
        }
    }
}
