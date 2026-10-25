using TabStudio.App.Previews;

namespace TabStudio.App.Xaml;

/// <summary>A control put into XAML text: the change, and where the new element's <c>&lt;</c> is once it is made.</summary>
/// <param name="Change">The change to the text.</param>
/// <param name="ElementStart">The offset of the new element's <c>&lt;</c> in the changed text.</param>
internal sealed record XamlInsertion(XamlTextChange Change, int ElementStart);

/// <summary>
/// Puts a control from the Toolbox into XAML at the caret, as Visual Studio's Toolbox does in the XAML editor: where
/// the caret is between elements, there; with the caret in a panel's start tag, as the panel's last child; in a
/// self-closing panel such as <c>&lt;StackPanel /&gt;</c>, by opening it; in any other element's tags, after the
/// element. The new element gets a line of its own at the indentation of its siblings.
/// </summary>
internal static class XamlToolboxInsertion
{
    /// <summary>Works out where a control goes and how it is written there.</summary>
    /// <param name="text">The XAML.</param>
    /// <param name="caret">The caret's offset.</param>
    /// <param name="snippet">The control's XAML, with two spaces per level of nesting.</param>
    /// <param name="isContainer">Whether elements of a type, by its name without prefix, hold other elements.</param>
    /// <returns>The insertion, or <see langword="null"/> when the text has no root element to put it in.</returns>
    public static XamlInsertion? Insert(string text, int caret, string snippet, Func<string, bool> isContainer)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(snippet);
        ArgumentNullException.ThrowIfNull(isContainer);

        var markup = XamlMarkup.Parse(text);
        if (markup.Root is not { } root)
        {
            return null;
        }

        var layout = new Layout(text, XamlTextLayout.NewLine(text), XamlTextLayout.IndentUnit(markup));
        caret = Math.Clamp(caret, 0, text.Length);
        if (markup.OtherMarkupAt(caret) is { } comment)
        {
            caret = comment.End;
        }

        var target = markup.InnermostAround(caret);
        int position;
        XamlElement? parent;
        if (target is not null && caret < target.StartTagEnd)
        {
            // In a start tag.
            if (target.IsSelfClosing && isContainer(target.LocalName))
            {
                return Open(layout, target, snippet);
            }

            (position, parent) = target.CanHaveContent ? (target.ContentEnd, target) : (target.End, target.Parent);
        }
        else if (target is { EndTagStart: { } endTag } && caret > endTag)
        {
            // In an end tag.
            (position, parent) = (target.End, target.Parent);
        }
        else
        {
            (position, parent) = (caret, target);
        }

        if (parent is null)
        {
            // Before or after the root: into the root.
            if (root.IsSelfClosing)
            {
                return Open(layout, root, snippet);
            }

            if (!root.CanHaveContent)
            {
                return null;
            }

            (position, parent) = (caret <= root.Start ? root.ContentStart : root.ContentEnd, root);
        }

        return Place(layout, position, parent, snippet);
    }

    /// <summary>Writes the control at a position in its parent's content, on a line of its own.</summary>
    private static XamlInsertion Place(Layout layout, int position, XamlElement parent, string snippet)
    {
        var text = layout.Text;
        var parentIndentation = XamlTextLayout.Indentation(text, parent.Start);
        var indentation = parentIndentation + layout.Unit;
        var body = XamlTextLayout.Indent(snippet, indentation, layout.Unit, layout.NewLine);
        var lineStart = XamlTextLayout.LineStart(text, position);
        var lineEnd = XamlTextLayout.LineEnd(text, position);
        var before = text.AsSpan(lineStart, position - lineStart);
        var after = text.AsSpan(position, lineEnd - position);
        var afterSpaces = after.Length - after.TrimStart().Length;

        // What follows on the line moves to a line of its own: the parent's end tag at the parent's indentation, a
        // sibling at the new element's.
        var next = after.TrimStart().StartsWith("</", StringComparison.Ordinal) ? parentIndentation : indentation;

        if (before.IsWhiteSpace())
        {
            var replaced = after.IsWhiteSpace() ? lineEnd - lineStart : position + afterSpaces - lineStart;
            var written = after.IsWhiteSpace() ? indentation + body : indentation + body + layout.NewLine + next;
            return new XamlInsertion(new XamlTextChange(lineStart, replaced, written), lineStart + indentation.Length);
        }

        if (after.IsWhiteSpace())
        {
            return new XamlInsertion(
                new XamlTextChange(lineEnd, 0, layout.NewLine + indentation + body),
                lineEnd + layout.NewLine.Length + indentation.Length);
        }

        return new XamlInsertion(
            new XamlTextChange(position, afterSpaces, layout.NewLine + indentation + body + layout.NewLine + next),
            position + layout.NewLine.Length + indentation.Length);
    }

    /// <summary>Opens a self-closing element, such as <c>&lt;StackPanel /&gt;</c>, with the control inside it.</summary>
    private static XamlInsertion Open(Layout layout, XamlElement element, string snippet)
    {
        var text = layout.Text;
        var indentation = XamlTextLayout.Indentation(text, element.Start);
        var inner = indentation + layout.Unit;
        var start = element.StartTagEnd - 2;
        while (start > element.NameEnd && char.IsWhiteSpace(text[start - 1]))
        {
            start--;
        }

        var written = ">" + layout.NewLine + inner + XamlTextLayout.Indent(snippet, inner, layout.Unit, layout.NewLine)
            + layout.NewLine + indentation + "</" + element.Name + ">";
        return new XamlInsertion(
            new XamlTextChange(start, element.StartTagEnd - start, written),
            start + 1 + layout.NewLine.Length + inner.Length);
    }

    /// <summary>The text being edited, with its line break and indentation unit.</summary>
    private sealed record Layout(string Text, string NewLine, string Unit);
}
