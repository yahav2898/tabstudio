using Microsoft.CodeAnalysis.CSharp;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// The code the designer writes into a document and reads from it, in the designer file's language: C# for
/// Form1.Designer.cs, Visual Basic for Form1.Designer.vb.
/// </summary>
internal static class DesignCode
{
    /// <summary>A string literal: <c>"OK"</c>.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="text">The text.</param>
    public static string Literal(CodeStyle style, string text) =>
        IsVisualBasic(style) ? VisualBasicValueWriter.Literal(text) : ValueWriter.Literal(text);

    /// <summary>A Boolean literal: <c>true</c>, or Visual Basic's <c>True</c>.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="value">The value.</param>
    public static string BooleanLiteral(CodeStyle style, bool value) =>
        IsVisualBasic(style) ? (value ? "True" : "False") : (value ? "true" : "false");

    /// <summary>A property value as code, or <see langword="null"/> for a value the designer cannot write.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="value">The value.</param>
    public static string? Write(CodeStyle style, object? value) =>
        IsVisualBasic(style) ? VisualBasicValueWriter.Write(value, style) : ValueWriter.Write(value, style);

    /// <summary>The name a type is written with: <c>Button</c> or <c>System.Windows.Forms.Button</c>.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="type">The type.</param>
    public static string TypeName(CodeStyle style, Type type) =>
        IsVisualBasic(style) ? TypeResolver.VisualBasicNameOf(type, style.FullTypeNames) : TypeResolver.NameOf(type, style.FullTypeNames);

    /// <summary>The modifiers of a new component's field: <c>private</c>, or Visual Basic's <c>Friend WithEvents</c>.</summary>
    /// <param name="style">How the file writes code.</param>
    public static string FieldModifiers(CodeStyle style) => IsVisualBasic(style) ? VisualBasicDesignerFile.FieldModifiers : "private";

    /// <summary>What works out the values the file's code assigns.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="components">Finds a component's live object by its field's name; <c>this</c> is the root.</param>
    public static IValueEvaluator Evaluator(CodeStyle style, Func<string, object?> components) =>
        IsVisualBasic(style) ? new VisualBasicExpressionEvaluator(components) : new ExpressionEvaluator(components);

    /// <summary>Renames components everywhere code names them.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="code">The code.</param>
    /// <param name="names">The new name of each old name.</param>
    public static string Rename(CodeStyle style, string code, IReadOnlyDictionary<string, string> names) =>
        IsVisualBasic(style) ? VisualBasicCodeTokens.Rename(code, names) : CodeTokens.Rename(code, names);

    /// <summary>Renames a component everywhere code names it.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="code">The code.</param>
    /// <param name="oldName">The old name.</param>
    /// <param name="newName">The new name.</param>
    public static string Rename(CodeStyle style, string code, string oldName, string newName) =>
        Rename(style, code, new Dictionary<string, string>(StringComparer.Ordinal) { [oldName] = newName });

    /// <summary>Whether code names a component.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="code">The code.</param>
    /// <param name="name">The component's name.</param>
    public static bool Mentions(CodeStyle style, string code, string name) =>
        IsVisualBasic(style) ? VisualBasicCodeTokens.Mentions(code, name) : CodeTokens.Mentions(code, name);

    /// <summary>Code in the simplified style, without <c>this.</c> or <c>Me.</c> and the namespaces the project imports.</summary>
    /// <param name="style">How the file writes code.</param>
    /// <param name="code">The code.</param>
    public static string Simplify(CodeStyle style, string code) =>
        IsVisualBasic(style) ? VisualBasicCodeTokens.Simplify(code) : CodeTokens.Simplify(code);

    /// <summary>Whether a name can be a component's or a handler's: an identifier that is not a keyword.</summary>
    /// <param name="language">The language.</param>
    /// <param name="name">The name.</param>
    public static bool IsValidName(DesignerLanguage language, string name) =>
        language == DesignerLanguage.VisualBasic
            ? VisualBasicCodeTokens.IsValidName(name)
            : SyntaxFacts.IsValidIdentifier(name) && SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None;

    private static bool IsVisualBasic(CodeStyle style) => style.Language == DesignerLanguage.VisualBasic;
}
