using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// What the designer reads and changes in a Visual Basic form's own code file, such as Form1.vb: the handlers whose
/// <c>Handles</c> clauses wire the form's events, as Visual Studio wires them in Visual Basic
/// (<c>Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click</c>), the handlers a double-tap
/// creates, and the names of renamed components. Visual Basic's names ignore case, and so does this.
/// </summary>
internal static class VisualBasicCodeBehind
{
    /// <summary>What a Handles clause names the form by for its own events, as Visual Studio writes it: <c>Handles MyBase.Load</c>.</summary>
    public const string FormEventSource = "MyBase";

    /// <summary>The base type the code file gives the class, as in <c>Inherits UserControl</c>.</summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <returns>The base type's name, or <see langword="null"/> when the file does not give one, as Form1.vb does not.</returns>
    public static string? BaseTypeOf(string codeText, string className)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        return Classes(Parse(codeText), className).SelectMany(c => c.Inherits).SelectMany(i => i.Types).Select(t => t.ToString()).FirstOrDefault();
    }

    /// <summary>The events the class's handlers handle by their Handles clauses.</summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <returns>
    /// Each handled event: the component's field, or an empty string for the form's own (<c>MyBase</c>, <c>Me</c> or
    /// <c>MyClass</c>); the event; and the handler.
    /// </returns>
    public static IReadOnlyList<(string Source, string Event, string Handler)> HandlersOf(string codeText, string className)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        var handlers = new List<(string Source, string Event, string Handler)>();
        foreach (var method in Methods(Parse(codeText), className))
        {
            var statement = method.SubOrFunctionStatement;
            foreach (var item in statement.HandlesClause?.Events ?? default)
            {
                var source = item.EventContainer switch
                {
                    KeywordEventContainerSyntax => string.Empty,
                    WithEventsEventContainerSyntax withEvents => withEvents.Identifier.ValueText,
                    _ => null,
                };
                if (source is not null)
                {
                    handlers.Add((source, item.EventMember.Identifier.ValueText, statement.Identifier.ValueText));
                }
            }
        }

        return handlers;
    }

    /// <summary>
    /// Adds an event handler to the class, as a double-tap on a control does in Visual Studio: before <c>End Class</c>,
    /// with a Handles clause; a handler that is there already gets the Handles clause it lacks. Either way, returns
    /// where the caret goes in its body.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="handlerName">The handler's name, such as <c>Button1_Click</c>.</param>
    /// <param name="delegateType">The event's delegate type, such as <see cref="EventHandler"/>.</param>
    /// <param name="source">
    /// What the Handles clause names the event's owner by: the component's field, or <see cref="FormEventSource"/> for
    /// the form; <see langword="null"/> for a handler without a Handles clause, which the designer file wires.
    /// </param>
    /// <param name="eventName">The event, such as <c>Click</c>.</param>
    /// <param name="fullTypeNames">Whether types are written with their namespaces.</param>
    /// <returns>The new text, and the offset in it of the line inside the handler's body.</returns>
    /// <exception cref="InvalidOperationException">The file has no such class.</exception>
    public static (string Text, int Caret) AddHandler(string codeText, string className, string handlerName, Type delegateType, string? source, string eventName, bool fullTypeNames)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(handlerName);
        ArgumentNullException.ThrowIfNull(delegateType);
        ArgumentNullException.ThrowIfNull(eventName);
        var root = Parse(codeText);
        var type = Classes(root, className).FirstOrDefault() ?? throw new InvalidOperationException($"The code file has no class named {className}.");
        var handles = source is null ? null : source + "." + eventName;
        if (Methods(root, className).FirstOrDefault(m => Same(m.SubOrFunctionStatement.Identifier.ValueText, handlerName)) is { } existing)
        {
            var statement = existing.SubOrFunctionStatement;
            var caret = CaretIn(codeText, existing);
            if (source is null || Handles(statement, source, eventName))
            {
                return (codeText, caret);
            }

            // The caret is after the statement, so it moves with what is inserted at its end.
            var (at, insert) = statement.HandlesClause is { } clause ? (clause.Span.End, ", " + handles) : (statement.Span.End, " Handles " + handles);
            return (codeText.Insert(at, insert), caret + insert.Length);
        }

        var newLine = TextLines.NewLineOf(codeText);
        var classIndent = TextLines.IndentOf(codeText, type.SpanStart);
        var indent = type.Members.Count > 0 ? TextLines.IndentOf(codeText, type.Members[0].SpanStart) : classIndent + "    ";
        var parameters = delegateType.GetMethod("Invoke")?.GetParameters() ?? [];
        var signature = string.Join(", ", parameters.Select(p => $"{p.Name} As {TypeResolver.VisualBasicNameOf(p.ParameterType, fullTypeNames)}"));

        var method = new StringBuilder();
        if (type.Members.Count > 0)
        {
            method.Append(newLine);
        }

        method.Append(indent).Append("Private Sub ").Append(handlerName).Append('(').Append(signature).Append(')');
        if (handles is not null)
        {
            method.Append(" Handles ").Append(handles);
        }

        method.Append(newLine);
        var caretLine = method.Length + indent.Length + 4;
        method.Append(indent).Append("    ").Append(newLine);
        method.Append(indent).Append("End Sub").Append(newLine);

        // The handler goes last in the class, before its End Class line.
        var insertAt = TextLines.LineStart(codeText, type.EndClassStatement.SpanStart);
        return (codeText.Insert(insertAt, method.ToString()), insertAt + caretLine);
    }

    /// <summary>Takes an event off the Handles clauses that handle it, leaving the handlers, as clearing it in Visual Studio's Properties does.</summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="component">The component's field, or <see langword="null"/> for the form's own event.</param>
    /// <param name="eventName">The event.</param>
    /// <returns>The new text.</returns>
    public static string RemoveHandler(string codeText, string className, string? component, string eventName)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(eventName);
        return RemoveHandles(codeText, className, item => Matches(item, component, eventName));
    }

    /// <summary>
    /// Takes a deleted component off every Handles clause, leaving the handlers, as deleting a control in Visual Studio
    /// does: a Handles clause cannot name a field that is not there.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="component">The component's field.</param>
    /// <returns>The new text.</returns>
    public static string RemoveHandlesOf(string codeText, string className, string component)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(component);
        return RemoveHandles(codeText, className, item => item.EventContainer is WithEventsEventContainerSyntax withEvents && Same(withEvents.Identifier.ValueText, component));
    }

    /// <summary>
    /// Renames a component's field wherever the code file names it, its Handles clauses included, and its handlers
    /// that have Visual Studio's names, such as <c>Button1_Click</c> for one that handles <c>Button1.Click</c>, with
    /// the code that names them.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="oldName">The old name.</param>
    /// <param name="newName">The new name.</param>
    /// <returns>The new text.</returns>
    public static string RenameField(string codeText, string className, string oldName, string newName)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(oldName);
        ArgumentNullException.ThrowIfNull(newName);
        var root = Parse(codeText);
        var methods = Methods(root, className).ToList();
        var existing = methods.Select(m => m.SubOrFunctionStatement.Identifier.ValueText).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [oldName] = newName };
        foreach (var method in methods)
        {
            var statement = method.SubOrFunctionStatement;
            var name = statement.Identifier.ValueText;
            foreach (var item in statement.HandlesClause?.Events ?? default)
            {
                if (item.EventContainer is WithEventsEventContainerSyntax container
                    && (Same(container.Identifier.ValueText, oldName) || Same(container.Identifier.ValueText, newName))
                    && Same(name, CodeBehind.HandlerName(oldName, item.EventMember.Identifier.ValueText)))
                {
                    var renamed = CodeBehind.HandlerName(newName, item.EventMember.Identifier.ValueText);
                    if (!existing.Contains(renamed))
                    {
                        renames[name] = renamed;
                    }
                }
            }
        }

        var edits = root.DescendantTokens()
            .Where(t => t.IsKind(SyntaxKind.IdentifierToken) && renames.ContainsKey(t.ValueText) && VisualBasicCodeTokens.IsReference(t))
            .Select(t => (t.Span, Text: VisualBasicCodeTokens.Spelled(t, renames[t.ValueText])))
            .OrderByDescending(e => e.Span.Start)
            .ToList();
        var builder = new StringBuilder(codeText);
        foreach (var (span, text) in edits)
        {
            builder.Remove(span.Start, span.Length).Insert(span.Start, text);
        }

        return builder.ToString();
    }

    private static SyntaxNode Parse(string text) => VisualBasicSyntaxTree.ParseText(text).GetRoot();

    /// <summary>
    /// Where the caret goes in a handler: at its first statement, or on the blank line of an empty one after its
    /// indentation, where a new handler puts it.
    /// </summary>
    private static int CaretIn(string text, MethodBlockSyntax method)
    {
        if (method.Statements.Count > 0)
        {
            return method.Statements[0].SpanStart;
        }

        var lineStart = TextLines.LineEnd(text, method.SubOrFunctionStatement.Span.End);
        if (lineStart >= method.EndSubOrFunctionStatement.SpanStart || !TextLines.IsBlankLine(text, lineStart))
        {
            return lineStart;
        }

        var position = lineStart;
        while (position < text.Length && text[position] is ' ' or '\t')
        {
            position++;
        }

        return position;
    }

    /// <summary>Takes the items a predicate picks off the class's Handles clauses, one at a time, each from the text the last one left.</summary>
    private static string RemoveHandles(string codeText, string className, Func<HandlesClauseItemSyntax, bool> predicate)
    {
        var text = codeText;
        for (var removed = 0; removed < 256; removed++)
        {
            var found = Methods(Parse(text), className)
                .Select(m => m.SubOrFunctionStatement.HandlesClause)
                .OfType<HandlesClauseSyntax>()
                .SelectMany(c => c.Events.Select((item, index) => (Clause: c, Item: item, Index: index)))
                .Where(e => predicate(e.Item))
                .Select(e => (Found: true, Span: RemovalSpan(e.Clause, e.Index)))
                .FirstOrDefault();
            if (!found.Found)
            {
                break;
            }

            text = text.Remove(found.Span.Start, found.Span.Length);
        }

        return text;
    }

    private static bool Same(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ClassBlockSyntax> Classes(SyntaxNode root, string className) =>
        root.DescendantNodes().OfType<ClassBlockSyntax>().Where(c => Same(c.ClassStatement.Identifier.ValueText, className));

    private static IEnumerable<MethodBlockSyntax> Methods(SyntaxNode root, string className) =>
        Classes(root, className).SelectMany(c => c.Members.OfType<MethodBlockSyntax>());

    /// <summary>Whether a method's Handles clause handles an event of a source.</summary>
    private static bool Handles(MethodStatementSyntax statement, string source, string eventName) =>
        statement.HandlesClause?.Events.Any(item => Same(item.EventMember.Identifier.ValueText, eventName) && item.EventContainer switch
        {
            KeywordEventContainerSyntax => IsFormSource(source),
            WithEventsEventContainerSyntax withEvents => Same(withEvents.Identifier.ValueText, source),
            _ => false,
        }) == true;

    private static bool IsFormSource(string source) => Same(source, "MyBase") || Same(source, "Me") || Same(source, "MyClass");

    /// <summary>Whether a Handles clause's item is a component's event: <c>Button1.Click</c>, or <c>MyBase.Load</c> for the form.</summary>
    private static bool Matches(HandlesClauseItemSyntax item, string? component, string eventName) =>
        Same(item.EventMember.Identifier.ValueText, eventName) && item.EventContainer switch
        {
            KeywordEventContainerSyntax => component is null,
            WithEventsEventContainerSyntax withEvents => component is not null && Same(withEvents.Identifier.ValueText, component),
            _ => false,
        };

    /// <summary>
    /// What goes when an item leaves a Handles clause: the whole clause, with the space before it, for its only item;
    /// otherwise the item with a comma next to it.
    /// </summary>
    private static TextSpan RemovalSpan(HandlesClauseSyntax clause, int index)
    {
        var events = clause.Events;
        if (events.Count == 1)
        {
            var start = clause.GetFirstToken().GetPreviousToken().Span.End;
            return TextSpan.FromBounds(start, clause.Span.End);
        }

        return index == 0
            ? TextSpan.FromBounds(events[0].SpanStart, events[1].SpanStart)
            : TextSpan.FromBounds(events[index - 1].Span.End, events[index].Span.End);
    }
}
