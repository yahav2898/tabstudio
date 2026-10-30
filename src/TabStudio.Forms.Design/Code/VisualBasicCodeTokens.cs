using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Finds and renames a component's name in Visual Basic designer code by its tokens, so text in strings and comments
/// is left alone. Visual Basic's names ignore case, so <c>button1</c> names <c>Button1</c>.
/// </summary>
internal static class VisualBasicCodeTokens
{
    /// <summary>
    /// Whether a name can be a component's or a method's: a Visual Basic identifier of letters, digits and underscores
    /// that is not a reserved keyword, which Visual Basic reads as one identifier token.
    /// </summary>
    /// <param name="name">The name.</param>
    public static bool IsValidName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (name.Length == 0 || char.IsDigit(name[0]) || !name.All(c => char.IsLetterOrDigit(c) || c == '_'))
        {
            return false;
        }

        var token = SyntaxFactory.ParseToken(name);
        return token.IsKind(SyntaxKind.IdentifierToken) && !token.ContainsDiagnostics && token.ToString().Length == name.Length;
    }

    /// <summary>Whether code names a component, as <c>Button1</c> or <c>Me.Button1</c>, not as a member of something else.</summary>
    /// <param name="code">The code.</param>
    /// <param name="name">The component's name.</param>
    public static bool Mentions(string code, string name)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);
        var tokens = SyntaxFactory.ParseTokens(code).ToList();
        return tokens.Where((token, i) => token.IsKind(SyntaxKind.IdentifierToken) && Same(token.ValueText, name) && IsReference(tokens, i)).Any();
    }

    /// <summary>Renames components everywhere code names them.</summary>
    /// <param name="code">The code.</param>
    /// <param name="names">The new name of each old name.</param>
    public static string Rename(string code, IReadOnlyDictionary<string, string> names)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(names);
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (oldName, newName) in names)
        {
            renames[oldName] = newName;
        }

        var tokens = SyntaxFactory.ParseTokens(code).ToList();
        var builder = new StringBuilder(code.Length);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            builder.Append(token.IsKind(SyntaxKind.IdentifierToken) && renames.TryGetValue(token.ValueText, out var renamed) && IsReference(tokens, i)
                ? token.LeadingTrivia.ToFullString() + Spelled(token, renamed) + token.TrailingTrivia.ToFullString()
                : token.ToFullString());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Code in the style Visual Studio writes for a project that imports System.Drawing and System.Windows.Forms, as
    /// the Windows Desktop SDK imports them: <c>Me.Button1</c> becomes <c>Button1</c>, and <c>System.Drawing.Point</c>
    /// or <c>System.Windows.Forms.AnchorStyles</c> lose their namespaces. Strings and comments are left as they are.
    /// </summary>
    /// <param name="code">The code.</param>
    public static string Simplify(string code)
    {
        ArgumentNullException.ThrowIfNull(code);
        var tokens = SyntaxFactory.ParseTokens(code).ToList();
        var builder = new StringBuilder(code.Length);
        var pendingTrivia = string.Empty;
        for (var i = 0; i < tokens.Count; i++)
        {
            var skip = SkippedPrefixLength(tokens, i);
            if (skip > 0)
            {
                // The prefix goes; its leading trivia stays with what follows it.
                pendingTrivia += tokens[i].LeadingTrivia.ToFullString();
                i += skip - 1;
                continue;
            }

            var token = tokens[i];
            builder.Append(pendingTrivia.Length > 0 ? pendingTrivia + token.ToString() + token.TrailingTrivia.ToFullString() : token.ToFullString());
            pendingTrivia = string.Empty;
        }

        return builder.Append(pendingTrivia).ToString();
    }

    /// <summary>
    /// Whether an identifier token names something itself, as <c>Button1</c> or <c>Me.Button1</c> do, rather than a
    /// member of something else, as in <c>other.Button1</c>.
    /// </summary>
    /// <param name="token">The token.</param>
    internal static bool IsReference(SyntaxToken token)
    {
        var previous = token.GetPreviousToken();
        if (!previous.IsKind(SyntaxKind.DotToken))
        {
            return true;
        }

        var before = previous.GetPreviousToken();
        return before.IsKind(SyntaxKind.MeKeyword) || before.IsKind(SyntaxKind.MyClassKeyword);
    }

    /// <summary>A new name for a token, in brackets when the token had them, as <c>[Text]</c>.</summary>
    /// <param name="token">The token.</param>
    /// <param name="name">The new name.</param>
    internal static string Spelled(SyntaxToken token, string name) => token.Text.StartsWith('[') ? "[" + name + "]" : name;

    private static bool Same(string first, string second) => string.Equals(first, second, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// How many tokens of a prefix the simplified style drops at a token: <c>Me .</c> before a name, <c>Global .</c>,
    /// and the namespaces a Windows Forms project imports before a type in them.
    /// </summary>
    private static int SkippedPrefixLength(List<SyntaxToken> tokens, int index)
    {
        bool Is(int at, SyntaxKind kind, string? text = null) =>
            at < tokens.Count && tokens[at].IsKind(kind) && (text is null || Same(tokens[at].ValueText, text));

        if (Is(index, SyntaxKind.MeKeyword) && Is(index + 1, SyntaxKind.DotToken) && Is(index + 2, SyntaxKind.IdentifierToken))
        {
            return 2;
        }

        if (Is(index, SyntaxKind.GlobalKeyword) && Is(index + 1, SyntaxKind.DotToken))
        {
            return 2;
        }

        // Not a namespace's System, as in Other.System, but Global.System is.
        var afterDot = index > 0 && Is(index - 1, SyntaxKind.DotToken) && !(index > 1 && Is(index - 2, SyntaxKind.GlobalKeyword));
        if (afterDot || !Is(index, SyntaxKind.IdentifierToken, "System") || !Is(index + 1, SyntaxKind.DotToken))
        {
            return 0;
        }

        // System.Windows.Forms.X and System.Drawing.X; System.X for a type in System, such as System.EventHandler.
        if (Is(index + 2, SyntaxKind.IdentifierToken, "Windows") && Is(index + 3, SyntaxKind.DotToken) && Is(index + 4, SyntaxKind.IdentifierToken, "Forms") && Is(index + 5, SyntaxKind.DotToken)
            && Is(index + 6, SyntaxKind.IdentifierToken))
        {
            return 6;
        }

        if (Is(index + 2, SyntaxKind.IdentifierToken, "Drawing") && Is(index + 3, SyntaxKind.DotToken) && Is(index + 4, SyntaxKind.IdentifierToken)
            && TypeResolver.Resolve("System.Drawing." + tokens[index + 4].ValueText) is not null)
        {
            return 4;
        }

        return Is(index + 2, SyntaxKind.IdentifierToken) && TypeResolver.Resolve("System." + tokens[index + 2].ValueText) is { Namespace: "System" } ? 2 : 0;
    }

    private static bool IsReference(List<SyntaxToken> tokens, int index) =>
        index == 0 || !tokens[index - 1].IsKind(SyntaxKind.DotToken)
        || (index > 1 && (tokens[index - 2].IsKind(SyntaxKind.MeKeyword) || tokens[index - 2].IsKind(SyntaxKind.MyClassKeyword)));
}
