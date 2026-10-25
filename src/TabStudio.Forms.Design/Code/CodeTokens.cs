using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TabStudio.Forms.Design.Code;

/// <summary>Finds and renames a component's name in designer code by its tokens, so text in strings and comments is left alone.</summary>
internal static class CodeTokens
{
    /// <summary>Whether code names a component, as <c>button1</c> or <c>this.button1</c>, not as a member of something else.</summary>
    /// <param name="code">The code.</param>
    /// <param name="name">The component's name.</param>
    public static bool Mentions(string code, string name)
    {
        ArgumentNullException.ThrowIfNull(code);
        var tokens = SyntaxFactory.ParseTokens(code).ToList();
        return tokens.Where((token, i) => IsReference(tokens, i, name)).Any();
    }

    /// <summary>Renames a component everywhere code names it.</summary>
    /// <param name="code">The code.</param>
    /// <param name="oldName">The old name.</param>
    /// <param name="newName">The new name.</param>
    public static string Rename(string code, string oldName, string newName) =>
        Rename(code, new Dictionary<string, string>(StringComparer.Ordinal) { [oldName] = newName });

    /// <summary>Renames several components at once, as pasting copies does.</summary>
    /// <param name="code">The code.</param>
    /// <param name="names">The new name of each old name.</param>
    public static string Rename(string code, IReadOnlyDictionary<string, string> names)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(names);
        var tokens = SyntaxFactory.ParseTokens(code).ToList();
        var builder = new StringBuilder(code.Length);
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            builder.Append(token.IsKind(SyntaxKind.IdentifierToken) && names.TryGetValue(token.ValueText, out var renamed) && IsReference(tokens, i, token.ValueText)
                ? token.LeadingTrivia.ToFullString() + renamed + token.TrailingTrivia.ToFullString()
                : token.ToFullString());
        }

        return builder.ToString();
    }

    /// <summary>
    /// Code in the style Visual Studio writes for a project with implicit usings: <c>this.button1</c> becomes
    /// <c>button1</c>, and <c>System.Drawing.Point</c> or <c>System.Windows.Forms.AnchorStyles</c> lose their
    /// namespaces, which the project imports. Strings and comments are left as they are.
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
    /// How many tokens of a prefix the simplified style drops at a token: <c>this .</c> before a name, <c>global ::</c>,
    /// and the namespaces a Windows Forms project imports before a type in them.
    /// </summary>
    private static int SkippedPrefixLength(List<SyntaxToken> tokens, int index)
    {
        bool Is(int at, SyntaxKind kind, string? text = null) =>
            at < tokens.Count && tokens[at].IsKind(kind) && (text is null || tokens[at].ValueText == text);

        if (Is(index, SyntaxKind.ThisKeyword) && Is(index + 1, SyntaxKind.DotToken) && Is(index + 2, SyntaxKind.IdentifierToken))
        {
            return 2;
        }

        if (Is(index, SyntaxKind.IdentifierToken, "global") && Is(index + 1, SyntaxKind.ColonColonToken))
        {
            return 2;
        }

        if ((index > 0 && Is(index - 1, SyntaxKind.DotToken)) || !Is(index, SyntaxKind.IdentifierToken, "System") || !Is(index + 1, SyntaxKind.DotToken))
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

    private static bool IsReference(List<SyntaxToken> tokens, int index, string name)
    {
        var token = tokens[index];
        if (!token.IsKind(SyntaxKind.IdentifierToken) || token.ValueText != name)
        {
            return false;
        }

        // button1, or this.button1; not other.button1.
        return index == 0 || !tokens[index - 1].IsKind(SyntaxKind.DotToken) || (index > 1 && tokens[index - 2].IsKind(SyntaxKind.ThisKeyword));
    }
}
