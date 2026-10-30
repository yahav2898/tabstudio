using System.ComponentModel;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Writes a designed form back into its designer file as Visual Studio does: InitializeComponent with the components'
/// creations, the layout calls, a commented section of properties, collections and events per component in
/// alphabetical order, the form's section last; and the components' fields after <c>#endregion</c>. The rest of the
/// file is left as it is. A Visual Basic file is written by <see cref="VisualBasicDesignerWriter"/>.
/// </summary>
internal static class DesignerWriter
{
    /// <summary>The designer file's new text.</summary>
    /// <param name="file">The file as it was read.</param>
    /// <param name="document">The form as it is designed now.</param>
    public static string Write(DesignerFile file, DesignDocument document)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(document);
        if (file.Language == DesignerLanguage.VisualBasic)
        {
            return VisualBasicDesignerWriter.Write(file, document);
        }

        var text = file.Text;
        var style = document.Style;
        var edits = new List<(int Start, int End, string Text)>();

        // The method's body, from its brace to its brace.
        var methodIndent = style.Indent.Length >= 4 ? style.Indent[..^4] : string.Empty;
        edits.Add((file.Body.Start, file.Body.End, WriteBody(document, methodIndent)));

        // The fields: where the old ones were, or after #endregion, or at the end of the class.
        var fields = WriteFields(document, methodIndent);
        if (file.ComponentFields.Count > 0)
        {
            var lines = file.ComponentFields.Select(f => TextLines.LineSpan(text, f)).OrderBy(l => l.Start).ToList();
            for (var i = 0; i < lines.Count; i++)
            {
                edits.Add((lines[i].Start, lines[i].End, i == 0 ? fields : string.Empty));
            }
        }
        else if (document.Components.Count > 0)
        {
            edits.Add((file.FieldsInsertionPoint, file.FieldsInsertionPoint, style.NewLine + fields));
        }

        var builder = new StringBuilder(text);
        foreach (var (start, end, replacement) in edits.OrderByDescending(e => e.Start))
        {
            builder.Remove(start, end - start).Insert(start, replacement);
        }

        return builder.ToString();
    }

    /// <summary>InitializeComponent's body, braces included.</summary>
    internal static string WriteBody(DesignDocument document, string methodIndent)
    {
        var style = document.Style;
        var lines = new List<string>();
        if (document.HasContainer)
        {
            lines.Add($"{style.Member("components")} = new System.ComponentModel.Container();");
        }

        lines.AddRange(document.LeadingStatements);
        foreach (var component in document.Components)
        {
            lines.Add($"{style.Member(component.Name)} = new {component.TypeName}({component.ConstructorArguments});");
        }

        foreach (var component in document.Components)
        {
            if (SupportsInitialize(component))
            {
                lines.Add($"{Initializable(component, style)}.BeginInit();");
            }

            foreach (var container in SuspendedParts(component))
            {
                lines.Add($"{Target(component, container, style)}SuspendLayout();");
            }
        }

        lines.Add($"{Target(document.Root, string.Empty, style)}SuspendLayout();");
        foreach (var component in document.Components)
        {
            WriteSection(lines, component, style);
        }

        WriteSection(lines, document.Root, style);
        foreach (var component in document.Components)
        {
            foreach (var container in SuspendedParts(component))
            {
                lines.Add($"{Target(component, container, style)}ResumeLayout(false);");
                if (NeedsLayout(document, component, container))
                {
                    lines.Add($"{Target(component, container, style)}PerformLayout();");
                }
            }

            if (SupportsInitialize(component))
            {
                lines.Add($"{Initializable(component, style)}.EndInit();");
            }
        }

        lines.Add($"{Target(document.Root, string.Empty, style)}ResumeLayout(false);");
        if (NeedsLayout(document, document.Root, string.Empty))
        {
            lines.Add($"{Target(document.Root, string.Empty, style)}PerformLayout();");
        }

        var builder = new StringBuilder("{").Append(style.NewLine);
        foreach (var line in lines)
        {
            builder.Append(line.Length == 0 ? string.Empty : style.Indent + line).Append(style.NewLine);
        }

        return builder.Append(methodIndent).Append('}').ToString();
    }

    /// <summary>A component's section: a comment with its name, then its properties and collections by name, then its events.</summary>
    private static void WriteSection(List<string> lines, DesignComponent component, CodeStyle style)
    {
        // A component with nothing to set, such as a tooltip, has no section, as in Visual Studio.
        if (!component.IsRoot && component.Properties.Count == 0 && component.Events.Count == 0 && component.OtherStatements.Count == 0
            && component.Adds.All(a => a.Items.Count == 0))
        {
            return;
        }

        lines.Add("// ");
        lines.Add("// " + component.Name);
        lines.Add("// ");
        var members = new List<(string Key, List<string> Lines)>();
        foreach (var property in component.Properties)
        {
            members.Add((property.Path, [$"{Target(component, property.Path, style)} = {property.Code};"]));
        }

        foreach (var add in component.Adds)
        {
            var items = add.Items.Select(item => Reference(item, component, style)).ToList();
            List<string> addLines = add.IsRange
                ? [$"{Target(component, add.Collection, style)}.AddRange(new {add.RangeType}[] {{ {string.Join(", ", items)} }});"]
                : [.. items.Select(item => $"{Target(component, add.Collection, style)}.Add({item});")];
            if (items.Count > 0)
            {
                members.Add((add.Collection, addLines));
            }
        }

        foreach (var (_, memberLines) in members.OrderBy(m => m.Key, StringComparer.Ordinal))
        {
            lines.AddRange(memberLines);
        }

        foreach (var handled in component.Events)
        {
            var handler = style.QualifyWithThis && EventType(component, handled.Event) is { } delegateType
                ? $"new {delegateType}({style.Member(handled.Handler)})"
                : style.Member(handled.Handler);
            lines.Add($"{Target(component, handled.Event, style)} += {handler};");
        }

        lines.AddRange(component.OtherStatements);
    }

    /// <summary>What a member of a component is written as: <c>button1.Text</c>, or the form's <c>Text</c>.</summary>
    /// <param name="component">The component.</param>
    /// <param name="path">The member's path; empty for a prefix that ends with a dot, or nothing for the form.</param>
    /// <param name="style">How the file writes code.</param>
    private static string Target(DesignComponent component, string path, CodeStyle style)
    {
        if (component.IsRoot)
        {
            return path.Length == 0 ? (style.QualifyWithThis ? "this." : string.Empty) : style.Member(path);
        }

        return path.Length == 0 ? style.Member(component.Name) + "." : $"{style.Member(component.Name)}.{path}";
    }

    /// <summary>An item of a collection add: the components it names written in the file's style, the rest as it was.</summary>
    private static string Reference(string item, DesignComponent owner, CodeStyle style)
    {
        if (!style.QualifyWithThis)
        {
            return item;
        }

        var parts = item.Split(',', 2);
        var first = parts[0].Trim();
        var isName = SyntaxFacts.IsValidIdentifier(first) && first != "this" && !string.Equals(first, owner.Name, StringComparison.Ordinal);
        return isName ? style.Member(first) + (parts.Length > 1 ? "," + parts[1] : string.Empty) : item;
    }

    private static string Initializable(DesignComponent component, CodeStyle style) =>
        style.QualifyWithThis
            ? $"((System.ComponentModel.ISupportInitialize)({style.Member(component.Name)}))"
            : $"((System.ComponentModel.ISupportInitialize){component.Name})";

    /// <summary>Whether InitializeComponent calls a component's BeginInit and EndInit, as it does a picture box's.</summary>
    /// <param name="component">The component.</param>
    internal static bool SupportsInitialize(DesignComponent component) =>
        component.Type is { } type && typeof(ISupportInitialize).IsAssignableFrom(type);

    /// <summary>
    /// The parts of a component whose layout InitializeComponent suspends while it adds their children: the component
    /// itself when it has controls or strip items, and a split container's panels that have controls.
    /// </summary>
    /// <param name="component">The component.</param>
    internal static IEnumerable<string> SuspendedParts(DesignComponent component)
    {
        foreach (var add in component.Adds.Where(a => a.Items.Count > 0 && a.Collection.EndsWith(".Controls", StringComparison.Ordinal)).OrderBy(a => a.Collection, StringComparer.Ordinal))
        {
            yield return add.Collection[..^".Controls".Length] + ".";
        }

        if (component.Adds.Any(a => a.Items.Count > 0 && a.Collection is "Controls" or "Items" or "TabPages")
            || (component.Type is { } type && typeof(System.Windows.Forms.SplitContainer).IsAssignableFrom(type)))
        {
            yield return string.Empty;
        }
    }

    /// <summary>
    /// Whether a container is laid out again after its layout resumes, as Visual Studio has it: a strip always, and
    /// any container with a strip or a control that sizes itself.
    /// </summary>
    /// <param name="document">The form.</param>
    /// <param name="component">The container.</param>
    /// <param name="part">The part of it, such as <c>Panel1.</c>, or empty for the container itself.</param>
    internal static bool NeedsLayout(DesignDocument document, DesignComponent component, string part)
    {
        if (component.Type is { } type && typeof(System.Windows.Forms.ToolStrip).IsAssignableFrom(type) && part.Length == 0)
        {
            return true;
        }

        var collection = part.Length == 0 ? "Controls" : part + "Controls";
        var children = component.Adds.Where(a => a.Collection == collection).SelectMany(a => a.Items).Select(i => document.Find(i.Split(',')[0].Trim()));
        return children.Any(child => child is not null
            && ((child.Type is { } childType && typeof(System.Windows.Forms.ToolStrip).IsAssignableFrom(childType)) || string.Equals(child.GetCode("AutoSize"), "true", StringComparison.OrdinalIgnoreCase)));
    }

    private static string? EventType(DesignComponent component, string eventName) =>
        (component.Type ?? (component.IsRoot ? typeof(System.Windows.Forms.Form) : null))?.GetEvent(eventName)?.EventHandlerType?.FullName?.Replace('+', '.');

    private static string WriteFields(DesignDocument document, string indent)
    {
        var builder = new StringBuilder();
        foreach (var component in document.Components)
        {
            builder.Append(indent).Append(component.FieldModifiers).Append(' ').Append(component.TypeName).Append(' ').Append(component.Name).Append(';').Append(document.Style.NewLine);
        }

        return builder.ToString();
    }
}
