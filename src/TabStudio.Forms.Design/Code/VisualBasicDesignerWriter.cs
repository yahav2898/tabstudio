using System.Text;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Writes a designed form back into its Visual Basic designer file as Visual Studio does: InitializeComponent's
/// statements with the creations, the layout calls and a commented section per component, in the same order as for C#;
/// and the components' <c>Friend WithEvents</c> fields after it. Every other character of the file is left as it is.
/// </summary>
/// <remarks>
/// Events are wired by <c>Handles</c> clauses in the code file, which this does not write; only those the file wired
/// with <c>AddHandler</c> are written again.
/// </remarks>
internal static class VisualBasicDesignerWriter
{
    /// <summary>The designer file's new text.</summary>
    /// <param name="file">The file as it was read.</param>
    /// <param name="document">The form as it is designed now.</param>
    public static string Write(DesignerFile file, DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(document);
        var text = file.Text;
        var style = document.Style;
        var edits = new List<(int Start, int End, string Text)> { (file.Body.Start, file.Body.End, WriteBody(file, document)) };

        // The fields: where the old ones were, or after InitializeComponent.
        var fields = WriteFields(document, file.MemberIndent);
        if (file.ComponentFields.Count > 0)
        {
            var lines = file.ComponentFields.Select(f => TextLines.LineSpan(text, f)).OrderBy(l => l.Start).ToList();
            for (var i = 0; i < lines.Count; i++)
            {
                var (start, end) = lines[i];

                // Without fields, one of the blank lines they stood between goes with them.
                if (fields.Length == 0 && i == lines.Count - 1 && TextLines.IsBlankLine(text, end) && IsAfterBlankLine(text, lines[0].Start))
                {
                    end = TextLines.LineEnd(text, end);
                }

                edits.Add((start, end, i == 0 ? fields : string.Empty));
            }
        }
        else if (document.Components.Count > 0)
        {
            var at = file.FieldsInsertionPoint;
            if (style.QualifyWithThis)
            {
                // Older Visual Studio writes them right after End Sub.
                edits.Add((at, at, fields));
            }
            else if (TextLines.IsBlankLine(text, at))
            {
                // Visual Studio now writes them between blank lines: End Sub, a blank line, the fields, a blank line.
                var next = TextLines.LineEnd(text, at);
                edits.Add((next, next, fields + style.NewLine));
            }
            else
            {
                edits.Add((at, at, style.NewLine + fields + style.NewLine));
            }
        }

        var builder = new StringBuilder(text);
        foreach (var (start, end, replacement) in edits.OrderByDescending(e => e.Start))
        {
            builder.Remove(start, end - start).Insert(start, replacement);
        }

        return builder.ToString();
    }

    /// <summary>InitializeComponent's statements, whole lines, between its <c>Sub</c> and its <c>End Sub</c>.</summary>
    /// <param name="file">The file as it was read.</param>
    /// <param name="document">The form as it is designed now.</param>
    internal static string WriteBody(DesignerFile file, DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(document);
        var style = document.Style;
        var lines = new List<string>();
        if (document.HasContainer)
        {
            lines.Add(document.ContainerStatement ?? $"{style.Member("components")} = New System.ComponentModel.Container()");
        }

        lines.AddRange(document.LeadingStatements);
        foreach (var component in document.Components)
        {
            lines.Add($"{style.Member(component.Name)} = New {component.TypeName}({component.ConstructorArguments})");
        }

        foreach (var component in document.Components)
        {
            if (DesignerWriter.SupportsInitialize(component))
            {
                lines.Add($"{Initializable(component, style)}.BeginInit()");
            }

            foreach (var container in DesignerWriter.SuspendedParts(component))
            {
                lines.Add($"{Target(component, container, style)}SuspendLayout()");
            }
        }

        lines.Add($"{Target(document.Root, string.Empty, style)}SuspendLayout()");
        foreach (var component in document.Components)
        {
            WriteSection(lines, component, style, file.SectionComment);
        }

        WriteSection(lines, document.Root, style, file.SectionComment);
        foreach (var component in document.Components)
        {
            foreach (var container in DesignerWriter.SuspendedParts(component))
            {
                lines.Add($"{Target(component, container, style)}ResumeLayout(False)");
                if (DesignerWriter.NeedsLayout(document, component, container))
                {
                    lines.Add($"{Target(component, container, style)}PerformLayout()");
                }
            }

            if (DesignerWriter.SupportsInitialize(component))
            {
                lines.Add($"{Initializable(component, style)}.EndInit()");
            }
        }

        lines.Add($"{Target(document.Root, string.Empty, style)}ResumeLayout(False)");
        if (DesignerWriter.NeedsLayout(document, document.Root, string.Empty))
        {
            lines.Add($"{Target(document.Root, string.Empty, style)}PerformLayout()");
        }

        var builder = new StringBuilder();
        foreach (var line in lines)
        {
            builder.Append(line.Length == 0 ? string.Empty : style.Indent + line).Append(style.NewLine);
        }

        if (file.BodyEndsWithBlankLine)
        {
            builder.Append(style.NewLine);
        }

        return builder.ToString();
    }

    /// <summary>A component's section: comments with its name, then its properties and collections by name, then its events.</summary>
    private static void WriteSection(List<string> lines, DesignComponent component, CodeStyle style, string comment)
    {
        var events = component.Events.Where(e => !e.IsHandlesClause).ToList();

        // A component with nothing to set, such as a tooltip, has no section, as in Visual Studio.
        if (!component.IsRoot && component.Properties.Count == 0 && events.Count == 0 && component.OtherStatements.Count == 0
            && component.Adds.All(a => a.Items.Count == 0))
        {
            return;
        }

        lines.Add(comment);
        lines.Add(comment + component.Name);
        lines.Add(comment);
        var members = new List<(string Key, List<string> Lines)>();
        foreach (var property in component.Properties)
        {
            members.Add((property.Path, [$"{Target(component, property.Path, style)} = {property.Code}"]));
        }

        foreach (var add in component.Adds)
        {
            var items = add.Items.Select(item => Reference(item, component, style)).ToList();
            List<string> addLines = add.IsRange
                ? [$"{Target(component, add.Collection, style)}.AddRange(New {add.RangeType}() {{{string.Join(", ", items)}}})"]
                : [.. items.Select(item => $"{Target(component, add.Collection, style)}.Add({item})")];
            if (items.Count > 0)
            {
                members.Add((add.Collection, addLines));
            }
        }

        foreach (var (_, memberLines) in members.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            lines.AddRange(memberLines);
        }

        foreach (var handled in events)
        {
            lines.Add($"AddHandler {Target(component, handled.Event, style)}, AddressOf {style.Member(handled.Handler)}");
        }

        lines.AddRange(component.OtherStatements);
    }

    /// <summary>What a member of a component is written as: <c>Button1.Text</c>, or the form's <c>Text</c>, with <c>Me.</c> in that style.</summary>
    /// <param name="component">The component.</param>
    /// <param name="path">The member's path; empty for a prefix that ends with a dot, or nothing for the form.</param>
    /// <param name="style">How the file writes code.</param>
    private static string Target(DesignComponent component, string path, CodeStyle style)
    {
        if (component.IsRoot)
        {
            return path.Length == 0 ? (style.QualifyWithThis ? "Me." : string.Empty) : style.Member(path);
        }

        return path.Length == 0 ? style.Member(component.Name) + "." : $"{style.Member(component.Name)}.{path}";
    }

    /// <summary>An item of a collection add: the component it names written in the file's style, the rest as it was.</summary>
    private static string Reference(string item, DesignComponent owner, CodeStyle style)
    {
        if (!style.QualifyWithThis)
        {
            return item;
        }

        var parts = item.Split(',', 2);
        var first = parts[0].Trim();
        var isName = VisualBasicCodeTokens.IsValidName(first) && !string.Equals(first, owner.Name, StringComparison.OrdinalIgnoreCase);
        return isName ? style.Member(first) + (parts.Length > 1 ? "," + parts[1] : string.Empty) : item;
    }

    private static string Initializable(DesignComponent component, CodeStyle style) =>
        $"CType({style.Member(component.Name)}, System.ComponentModel.ISupportInitialize)";

    /// <summary>
    /// The components' fields, <c>Friend WithEvents Button1 As Button</c>, in the order the file declared them, new
    /// ones last in the order they were created.
    /// </summary>
    private static string WriteFields(DesignDocument document, string indent)
    {
        var builder = new StringBuilder();
        foreach (var component in document.Components.OrderBy(c => c.FieldIndex ?? int.MaxValue))
        {
            builder.Append(indent).Append(component.FieldModifiers).Append(' ').Append(component.Name).Append(" As ").Append(component.TypeName)
                .Append(document.Style.NewLine);
        }

        return builder.ToString();
    }

    /// <summary>Whether the line before the one that starts at a position is blank.</summary>
    private static bool IsAfterBlankLine(string text, int lineStart) =>
        lineStart > 0 && TextLines.IsBlankLine(text, TextLines.LineStart(text, lineStart - 1));
}
