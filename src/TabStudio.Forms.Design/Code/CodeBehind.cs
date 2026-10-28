using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// What the designer changes in a form's own code file, such as Form1.cs: the event handlers a double-tap creates,
/// and the names of renamed components. It also reads the form's base class from it. A Visual Basic form's Form1.vb
/// is <see cref="VisualBasicCodeBehind"/>'s.
/// </summary>
internal static class CodeBehind
{
    /// <summary>The base type of the designed class, as the code file names it, such as <c>Form</c>.</summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="language">The code file's language.</param>
    /// <returns>The base type's name, or <see langword="null"/> when the file does not give one.</returns>
    public static string? BaseTypeOf(string codeText, string className, DesignerLanguage language = DesignerLanguage.CSharp)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        if (language == DesignerLanguage.VisualBasic)
        {
            return VisualBasicCodeBehind.BaseTypeOf(codeText, className);
        }

        return FindClass(CSharpSyntaxTree.ParseText(codeText).GetCompilationUnitRoot(), className)?.BaseList?.Types.FirstOrDefault()?.Type.ToString();
    }

    /// <summary>Whether the code file has a method of a name in the class.</summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="methodName">The method's name.</param>
    public static bool HasMethod(string codeText, string className, string methodName)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        return FindClass(CSharpSyntaxTree.ParseText(codeText).GetCompilationUnitRoot(), className)?.Members
            .OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == methodName) == true;
    }

    /// <summary>
    /// Adds an event handler to the class, as a double-tap on a control does in Visual Studio, unless it is there
    /// already; either way, returns where the caret goes in its body.
    /// </summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="className">The class's name.</param>
    /// <param name="handlerName">The handler's name, such as <c>button1_Click</c>.</param>
    /// <param name="delegateType">The event's delegate type, such as <see cref="EventHandler"/>.</param>
    /// <param name="fullTypeNames">Whether types are written with their namespaces.</param>
    /// <returns>The new text, and the offset in it of the line inside the handler's body.</returns>
    /// <exception cref="InvalidOperationException">The file has no such class.</exception>
    public static (string Text, int Caret) AddHandler(string codeText, string className, string handlerName, Type delegateType, bool fullTypeNames)
    {
        ArgumentNullException.ThrowIfNull(codeText);
        ArgumentNullException.ThrowIfNull(delegateType);
        var root = CSharpSyntaxTree.ParseText(codeText).GetCompilationUnitRoot();
        var type = FindClass(root, className) ?? throw new InvalidOperationException($"The code file has no class named {className}.");
        if (type.Members.OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == handlerName) is { } existing)
        {
            var body = existing.Body;
            var caret = body is null ? existing.SpanStart
                : body.Statements.Count > 0 ? body.Statements[0].SpanStart
                : LineEnd(codeText, body.OpenBraceToken.Span.End);
            return (codeText, caret);
        }

        var newLine = codeText.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var classIndent = IndentOf(codeText, type.SpanStart);
        var indent = type.Members.Count > 0 ? IndentOf(codeText, type.Members[0].SpanStart) : classIndent + "    ";
        var parameters = delegateType.GetMethod("Invoke")?.GetParameters() ?? [];
        var signature = string.Join(", ", parameters.Select(p => $"{TypeResolver.NameOf(p.ParameterType, fullTypeNames)} {p.Name}"));

        var method = new StringBuilder();
        if (type.Members.Count > 0)
        {
            method.Append(newLine);
        }

        method.Append(indent).Append("private void ").Append(handlerName).Append('(').Append(signature).Append(')').Append(newLine);
        method.Append(indent).Append('{').Append(newLine);
        var caretLine = method.Length + indent.Length + 4;
        method.Append(indent).Append("    ").Append(newLine);
        method.Append(indent).Append('}').Append(newLine);

        // The handler goes last in the class, before its closing brace's line.
        var insertAt = LineStart(codeText, type.CloseBraceToken.SpanStart);
        return (codeText.Insert(insertAt, method.ToString()), insertAt + caretLine);
    }

    /// <summary>Renames a component's field wherever the code file names it.</summary>
    /// <param name="codeText">The code file's text.</param>
    /// <param name="oldName">The old name.</param>
    /// <param name="newName">The new name.</param>
    public static string RenameField(string codeText, string oldName, string newName) => CodeTokens.Rename(codeText, oldName, newName);

    /// <summary>The name Visual Studio gives a new handler: the component's name, an underscore, the event's.</summary>
    /// <param name="componentName">The component's name; for the form, its class's name.</param>
    /// <param name="eventName">The event.</param>
    public static string HandlerName(string componentName, string eventName) => componentName + "_" + eventName;

    /// <summary>The delegate type of a type's event, or <see langword="null"/> when the type has no such event.</summary>
    /// <param name="type">The type.</param>
    /// <param name="eventName">The event.</param>
    public static Type? DelegateTypeOf(Type type, string eventName) =>
        type.GetEvent(eventName, BindingFlags.Public | BindingFlags.Instance)?.EventHandlerType;

    private static ClassDeclarationSyntax? FindClass(CompilationUnitSyntax root, string className) =>
        root.DescendantNodes().OfType<ClassDeclarationSyntax>().FirstOrDefault(c => c.Identifier.ValueText == className);

    private static int LineStart(string text, int position) => text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;

    private static int LineEnd(string text, int position)
    {
        var end = text.IndexOf('\n', position);
        return end < 0 ? text.Length : end + 1;
    }

    private static string IndentOf(string text, int position)
    {
        var start = LineStart(text, position);
        return new string([.. text.AsSpan(start, position - start).ToArray().TakeWhile(char.IsWhiteSpace)]);
    }
}
