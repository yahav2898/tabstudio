using System.Buffers;
using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// Element paths: how an input, a layout issue or a recorded UI test names an element so that the same element is found
/// on every screen, whatever its size, such as <c>MainView/StackPanel[0]/Button#CountButton</c>. Capture, replay,
/// mirrored input, layout findings and recorded tests all use this one form.
/// </summary>
/// <remarks>
/// <para>
/// A path walks the app's logical tree from its root view, one segment per level. A segment is the element's type,
/// then <c>#Name</c> when it has a name, otherwise <c>[n]</c>, its index among the siblings of the same type. The
/// logical tree is what the XAML wrote, so it is the same on a phone and a tablet; controls' templates are left out, so
/// a tap on the text inside a button names the button.
/// </para>
/// <para>
/// The items of an items control are numbered by their index in the items, not among the containers created, so an
/// item that a small screen has not created yet is still found: it is scrolled into view first.
/// </para>
/// <para>
/// A bare name, such as <c>CountButton</c>, is the short form recorded tests use for named elements (see
/// <see cref="ShortName"/>). The recorded tests' driver, <c>RecordedApp</c>, finds elements by the same rules
/// (<see cref="Recording.UiTestDriverSource"/>).
/// </para>
/// </remarks>
public static class ElementPaths
{
    /// <summary>The character between segments.</summary>
    public const char Separator = '/';

    /// <summary>Names an element by its path from the root.</summary>
    /// <param name="root">The app's root view on its screen.</param>
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
    /// <param name="root">
    /// The app's root view on its screen, or an element around it, such as the theme TabStudio shows an app in: the
    /// root view is then looked for under it.
    /// </param>
    /// <param name="path">The path, as <see cref="Describe"/> wrote it on this screen or another, or a bare element name.</param>
    /// <returns>The element, or <see langword="null"/> when this screen does not have it.</returns>
    public static Control? Resolve(Visual root, string path) => Resolve(root, path, bringIntoView: true);

    /// <summary>The element a path names, on this screen, scrolling an item that is not created yet into view or not.</summary>
    /// <param name="root">The app's root view on its screen, or an element around it.</param>
    /// <param name="path">The path, or a bare element name.</param>
    /// <param name="bringIntoView">
    /// Whether an item a list has not created yet is scrolled into view to be found; without, it is not found.
    /// </param>
    internal static Control? Resolve(Visual root, string path, bool bringIntoView)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(path);
        if (root is not Control rootControl)
        {
            return null;
        }

        var segments = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || !PathSegment.TryParse(segments[0], out var first))
        {
            return null;
        }

        // A bare name: the element of that name, anywhere under the root, or the root view itself by its type.
        if (segments.Length == 1 && first.IsBare)
        {
            return FirstByName(rootControl, segments[0]) ?? RootView(rootControl, first);
        }

        // The path starts at the app's root view: the root given, or the view under the theme around it.
        var current = RootView(rootControl, first) ?? rootControl;
        for (var i = 1; i < segments.Length; i++)
        {
            if (!PathSegment.TryParse(segments[i], out var segment) || Child(current, segment, bringIntoView) is not { } child)
            {
                // A wrapper the other screen does not have: a named target is still found by its name, if it is the only one.
                return PathSegment.TryParse(segments[^1], out var last) && last.Name is { } name ? OnlyByName(rootControl, last.Type, name) : null;
            }

            current = child;
        }

        return current;
    }

    /// <summary>
    /// The element a path would name for this one: itself, or the control whose template it is part of, as long as
    /// it is under the root.
    /// </summary>
    /// <param name="root">The app's root view on its screen.</param>
    /// <param name="element">An element under the root.</param>
    /// <returns>The element, or <see langword="null"/> when it is not under the root.</returns>
    public static Control? Addressable(Visual root, Visual element)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(element);
        return Chain(root, element) is { } chain ? chain[^1] as Control : null;
    }

    /// <summary>
    /// The shortest form of a path, which recorded tests write: the element's name when it has one, such as
    /// <c>CountButton</c>, otherwise the whole path.
    /// </summary>
    /// <param name="path">The path.</param>
    public static string ShortName(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var segments = path.Split(Separator, StringSplitOptions.RemoveEmptyEntries);
        var last = segments.Length > 0 ? segments[^1] : path;
        var hash = last.IndexOf('#', StringComparison.Ordinal);
        return hash >= 0 && hash < last.Length - 1 ? last[(hash + 1)..] : path;
    }

    /// <summary>
    /// The text an element shows, for the checks of recorded UI tests: a text block's or text box's text, a content
    /// control's text, or the texts inside it joined by spaces.
    /// </summary>
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
        var type = TypeName(element.GetType());
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

            if (TypeName(children[i].GetType()) == type)
            {
                index++;
            }
        }

        return index;
    }

    /// <summary>
    /// The app's root view that a path's first segment names: the root given, or, when a host wraps the view, as
    /// TabStudio wraps an app in its theme, the first element under it that the segment names.
    /// </summary>
    private static Control? RootView(Control root, PathSegment segment)
    {
        if (segment.Matches(root))
        {
            return root;
        }

        if (segment.IsIndexed)
        {
            return null;
        }

        foreach (var element in root.GetLogicalDescendants())
        {
            if (element is Control control && segment.Matches(control))
            {
                return control;
            }
        }

        return null;
    }

    private static Control? Child(Control parent, PathSegment segment, bool bringIntoView)
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
        if (parent is ItemsControl items && segment.Index < items.ItemCount
            && (bringIntoView ? ContainerFromIndex(items, segment.Index) : items.ContainerFromIndex(segment.Index)) is { } container
            && TypeName(container.GetType()) == segment.Type)
        {
            return container;
        }

        var index = 0;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is Control child && TypeName(child.GetType()) == segment.Type && index++ == segment.Index)
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

    /// <summary>The first element of a name under the root, in the order of the logical tree, as a bare name finds it.</summary>
    private static Control? FirstByName(Control root, string name)
    {
        foreach (var element in root.GetLogicalDescendants())
        {
            if (element is Control control && control.Name == name)
            {
                return control;
            }
        }

        return null;
    }

    /// <summary>The element of a name and type under the root, if it is the only one.</summary>
    private static Control? OnlyByName(Control root, string type, string name)
    {
        Control? found = null;
        foreach (var element in root.GetLogicalDescendants())
        {
            if (element is Control control && control.Name == name && IsOfType(control, type))
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

    /// <summary>Whether an element is of a type, by the name segments write, or of a type derived from it.</summary>
    private static bool IsOfType(Control control, string type)
    {
        for (var current = control.GetType(); current is not null && current != typeof(StyledElement); current = current.BaseType)
        {
            if (TypeName(current) == type)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>A type's name without its generic arity, as segments write it.</summary>
    private static string TypeName(Type type)
    {
        var name = type.Name;
        var tick = name.IndexOf('`', StringComparison.Ordinal);
        return tick < 0 ? name : name[..tick];
    }

    /// <summary>One segment of a path: a type, then a name or an index.</summary>
    /// <param name="Type">The element's type.</param>
    /// <param name="Name">Its name, for <c>Type#Name</c>.</param>
    /// <param name="Index">Its index among the siblings of its type, for <c>Type[n]</c>.</param>
    /// <param name="IsIndexed">Whether the segment gave an index.</param>
    private readonly record struct PathSegment(string Type, string? Name, int Index, bool IsIndexed)
    {
        /// <summary>The characters a name in a segment cannot have.</summary>
        private static readonly SearchValues<char> Reserved = SearchValues.Create("/#[]");

        /// <summary>Whether the segment is a word alone: a bare element name, or the root view's type.</summary>
        public bool IsBare => Name is null && !IsIndexed;

        /// <summary>Whether a name can be written in a segment, rather than an index.</summary>
        public static bool IsValidName(string name) => name.AsSpan().IndexOfAny(Reserved) < 0;

        /// <summary>Whether the segment names an element: of its type or one derived from it, and with its name if it gives one.</summary>
        public bool Matches(Control control) => IsOfType(control, Type) && (Name is null || control.Name == Name);

        public static bool TryParse(string text, out PathSegment segment)
        {
            segment = default;
            var hash = text.IndexOf('#', StringComparison.Ordinal);
            if (hash > 0)
            {
                segment = new PathSegment(text[..hash], text[(hash + 1)..], 0, IsIndexed: false);
                return segment.Name!.Length > 0;
            }

            var bracket = text.IndexOf('[', StringComparison.Ordinal);
            if (bracket > 0 && text.EndsWith(']')
                && int.TryParse(text.AsSpan(bracket + 1, text.Length - bracket - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var index))
            {
                segment = new PathSegment(text[..bracket], null, index, IsIndexed: true);
                return true;
            }

            // A segment without an index, such as the root's, is the first of its type.
            segment = new PathSegment(text, null, 0, IsIndexed: false);
            return text.Length > 0 && hash < 0 && bracket < 0;
        }
    }
}
