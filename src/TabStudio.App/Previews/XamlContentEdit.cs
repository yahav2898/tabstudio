namespace TabStudio.App.Previews;

/// <summary>A change to XAML text: <see cref="Length"/> characters at <see cref="Offset"/> become <see cref="Text"/>.</summary>
/// <param name="Offset">Where the change starts.</param>
/// <param name="Length">How many characters it replaces.</param>
/// <param name="Text">The new text.</param>
internal sealed record XamlTextChange(int Offset, int Length, string Text);

/// <summary>
/// Puts new XAML in as a view's content, such as a screen drawn with Sketch to XAML: in place of the root element's
/// content element, keeping its property elements (<c>&lt;Design.DataContext&gt;</c>, <c>&lt;UserControl.Styles&gt;</c>)
/// and every other character of the file as they are.
/// </summary>
internal static class XamlContentEdit
{
    /// <summary>The change that makes <paramref name="content"/> the root element's content.</summary>
    /// <param name="xaml">The file's XAML.</param>
    /// <param name="content">The new content: one element, such as a <c>Grid</c>.</param>
    /// <returns>The change, or <see langword="null"/> when the file has no root element to put it in.</returns>
    public static XamlTextChange? ReplaceContent(string xaml, string content)
    {
        ArgumentNullException.ThrowIfNull(xaml);
        ArgumentNullException.ThrowIfNull(content);
        if (NextElement(xaml, 0) is not { } root || TagEnd(xaml, root) is not { } rootTagEnd)
        {
            return null;
        }

        var rootColumn = ColumnOf(xaml, root);
        if (xaml[rootTagEnd - 2] == '/')
        {
            // <UserControl ... /> becomes <UserControl ...> content </UserControl>.
            var name = NameAt(xaml, root);
            var body = $">{Environment.NewLine}{Indent(content, rootColumn + 2)}{Environment.NewLine}{new string(' ', rootColumn)}</{name}>";
            var slash = xaml.LastIndexOf('/', rootTagEnd - 1);
            return new XamlTextChange(slash, rootTagEnd - slash, body);
        }

        // The first child element without a dot in its name is the content; property elements stay.
        var position = rootTagEnd;
        while (NextElement(xaml, position) is { } child && !IsEndTag(xaml, child))
        {
            if (ElementEnd(xaml, child) is not { } childEnd)
            {
                return null;
            }

            if (!NameAt(xaml, child).Contains('.', StringComparison.Ordinal))
            {
                return new XamlTextChange(child, childEnd - child, Indent(content, ColumnOf(xaml, child)).TrimStart());
            }

            position = childEnd;
        }

        // No content yet: the new one goes just before the root's end tag.
        if (NextElement(xaml, position) is not { } endTag)
        {
            return null;
        }

        var lineStart = xaml.LastIndexOf('\n', endTag - 1) + 1;
        var indentation = new string(' ', rootColumn + 2);
        return string.IsNullOrWhiteSpace(xaml[lineStart..endTag])
            ? new XamlTextChange(lineStart, 0, Indent(content, rootColumn + 2) + Environment.NewLine)
            : new XamlTextChange(endTag, 0, Environment.NewLine + indentation + Indent(content, rootColumn + 2).TrimStart() + Environment.NewLine + new string(' ', rootColumn));
    }

    /// <summary>The offset of the next element's or end tag's <c>&lt;</c>, skipping comments, declarations and text.</summary>
    private static int? NextElement(string xaml, int from)
    {
        for (var i = xaml.IndexOf('<', from); i >= 0 && i < xaml.Length - 1; i = xaml.IndexOf('<', i + 1))
        {
            if (xaml.AsSpan(i).StartsWith("<!--"))
            {
                var end = xaml.IndexOf("-->", i, StringComparison.Ordinal);
                if (end < 0)
                {
                    return null;
                }

                i = end;
            }
            else if (xaml[i + 1] is '?' or '!')
            {
                var end = xaml.IndexOf('>', i);
                if (end < 0)
                {
                    return null;
                }

                i = end;
            }
            else
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>The offset just past the <c>&gt;</c> of the tag at <paramref name="start"/>, honoring quoted attribute values.</summary>
    private static int? TagEnd(string xaml, int start)
    {
        char? quote = null;
        for (var i = start + 1; i < xaml.Length; i++)
        {
            var c = xaml[i];
            if (quote is { } open)
            {
                if (c == open)
                {
                    quote = null;
                }
            }
            else if (c is '"' or '\'')
            {
                quote = c;
            }
            else if (c == '>')
            {
                return i + 1;
            }
        }

        return null;
    }

    /// <summary>The offset just past the element at <paramref name="start"/>: its end tag, or its own tag when self-closing.</summary>
    private static int? ElementEnd(string xaml, int start)
    {
        var depth = 0;
        int? position = start;
        while (position is { } at && NextElement(xaml, at) is { } tag)
        {
            if (TagEnd(xaml, tag) is not { } end)
            {
                return null;
            }

            if (IsEndTag(xaml, tag))
            {
                depth--;
            }
            else if (xaml[end - 2] != '/')
            {
                depth++;
            }

            if (depth == 0)
            {
                return end;
            }

            position = end;
        }

        return null;
    }

    private static bool IsEndTag(string xaml, int tag) => xaml[tag + 1] == '/';

    private static string NameAt(string xaml, int tag)
    {
        var start = tag + 1;
        var end = start;
        while (end < xaml.Length && !char.IsWhiteSpace(xaml[end]) && xaml[end] is not ('>' or '/'))
        {
            end++;
        }

        return xaml[start..end];
    }

    private static int ColumnOf(string xaml, int offset) => offset - (xaml.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1);

    /// <summary>The content's lines, each indented by <paramref name="column"/> spaces more than its own first line.</summary>
    private static string Indent(string content, int column)
    {
        var lines = content.Trim().ReplaceLineEndings("\n").Split('\n');
        var common = lines.Skip(1).Where(l => l.Trim().Length > 0).Select(l => l.Length - l.TrimStart().Length).DefaultIfEmpty(0).Min();
        var prefix = new string(' ', column);
        return string.Join(
            Environment.NewLine,
            lines.Select((line, i) => line.Trim().Length == 0 ? string.Empty : prefix + (i == 0 ? line.TrimStart() : line[Math.Min(common, line.Length - line.TrimStart().Length)..])));
    }
}
