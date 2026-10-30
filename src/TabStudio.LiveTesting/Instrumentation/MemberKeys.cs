using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>A member with code coverage tracks, as found in one version of a file.</summary>
/// <param name="TypeName">The full name of the containing type, as reflection writes it: <c>Ns.Outer+Inner`1</c>.</param>
/// <param name="Name">The member's name, such as <c>Add</c>, <c>.ctor</c> or <c>this[]</c>.</param>
/// <param name="Key">Identifies the member in its file across edits: the type, name and parameter types.</param>
/// <param name="Text">The member's tokens without trivia, so edits to whitespace and comments do not change it.</param>
internal sealed record MemberText(string TypeName, string Name, string Key, string Text);

/// <summary>
/// Names the members that coverage tracks, from syntax alone, so the same member gets the same key in every build
/// and in every version of its file while it is edited.
/// </summary>
/// <remarks>
/// This is C#; <see cref="VisualBasicMemberKeys"/> keys Visual Basic members the same way.
/// A tracked member is a function body that runs: a method, constructor, operator or conversion with a body, a
/// property, indexer or event with an accessor body or an expression body, or a file's top-level statements. Local
/// functions and lambdas belong to the member they are written in. Everything else in a file (fields,
/// auto-properties, attributes of types, using directives and so on) is its skeleton: a change there can affect
/// any code, so Live Unit Testing then runs every test that depends on the project.
/// </remarks>
internal static class MemberKeys
{
    /// <summary>The name of a file's top-level statements.</summary>
    public const string TopLevelName = "<top-level statements>";

    /// <summary>The type the compiler puts top-level statements in.</summary>
    public const string TopLevelTypeName = "Program";

    /// <summary>Whether coverage tracks a member: it has a body that runs. Destructors run on the finalizer thread and are left out.</summary>
    /// <param name="member">The member declaration.</param>
    public static bool IsTracked(MemberDeclarationSyntax member) => member switch
    {
        DestructorDeclarationSyntax => false,
        BaseMethodDeclarationSyntax method => method.Body is not null || method.ExpressionBody is not null,
        PropertyDeclarationSyntax property => property.ExpressionBody is not null || HasAccessorBody(property.AccessorList),
        IndexerDeclarationSyntax indexer => indexer.ExpressionBody is not null || HasAccessorBody(indexer.AccessorList),
        EventDeclarationSyntax eventDeclaration => HasAccessorBody(eventDeclaration.AccessorList),
        _ => false,
    };

    /// <summary>The containing type's name and the member's own name and key, before duplicates are numbered.</summary>
    /// <param name="member">A tracked member.</param>
    public static (string TypeName, string Name, string Key) Describe(MemberDeclarationSyntax member)
    {
        ArgumentNullException.ThrowIfNull(member);
        var typeName = TypeNameOf(member);
        var name = member switch
        {
            MethodDeclarationSyntax method => Explicit(method.ExplicitInterfaceSpecifier) + method.Identifier.ValueText + Arity(method.TypeParameterList),
            ConstructorDeclarationSyntax constructor => constructor.Modifiers.Any(SyntaxKind.StaticKeyword) ? ".cctor" : ".ctor",
            OperatorDeclarationSyntax op => Explicit(op.ExplicitInterfaceSpecifier) + "operator " + (op.CheckedKeyword.IsKind(SyntaxKind.None) ? string.Empty : "checked ") + op.OperatorToken.Text,
            ConversionOperatorDeclarationSyntax conversion => Explicit(conversion.ExplicitInterfaceSpecifier) + conversion.ImplicitOrExplicitKeyword.Text + " operator " + Compact(conversion.Type),
            PropertyDeclarationSyntax property => Explicit(property.ExplicitInterfaceSpecifier) + property.Identifier.ValueText,
            IndexerDeclarationSyntax indexer => Explicit(indexer.ExplicitInterfaceSpecifier) + "this[]",
            EventDeclarationSyntax eventDeclaration => Explicit(eventDeclaration.ExplicitInterfaceSpecifier) + eventDeclaration.Identifier.ValueText,
            _ => member.Kind().ToString(),
        };

        var parameters = member switch
        {
            BaseMethodDeclarationSyntax method => method.ParameterList.Parameters,
            IndexerDeclarationSyntax indexer => indexer.ParameterList.Parameters,
            _ => (SeparatedSyntaxList<ParameterSyntax>?)null,
        };

        var key = typeName + "." + name;
        if (parameters is { } list)
        {
            key += "(" + string.Join(",", list.Select(Parameter)) + ")";
        }

        return (typeName, name, key);
    }

    /// <summary>Finds the tracked members of a file in source order, with their texts and unique keys.</summary>
    /// <param name="root">The file's syntax root.</param>
    public static IReadOnlyList<MemberText> Collect(SyntaxNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var members = new List<MemberText>();
        var keys = new KeyCounter();
        if (root is CompilationUnitSyntax unit && TopLevelStatements(unit) is { Count: > 0 } globals)
        {
            members.Add(new MemberText(TopLevelTypeName, TopLevelName, keys.Unique(TopLevelTypeName + "." + TopLevelName), TokensOf(globals)));
        }

        foreach (var member in TrackedMembers(root))
        {
            var (typeName, name, key) = Describe(member);
            members.Add(new MemberText(typeName, name, keys.Unique(key), TokensOf([member])));
        }

        return members;
    }

    /// <summary>
    /// The file without its tracked members, as tokens without trivia: two versions with the same skeleton differ
    /// only inside function bodies (or in whitespace and comments).
    /// </summary>
    /// <param name="root">The file's syntax root.</param>
    public static string Skeleton(SyntaxNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var builder = new StringBuilder();
        foreach (var token in root.DescendantTokens(node => !IsTrackedNode(node)))
        {
            builder.Append(token.Text).Append(' ');
        }

        return builder.ToString();
    }

    /// <summary>The tracked members of a file, in source order. Members inside function bodies are never tracked.</summary>
    /// <param name="root">The file's syntax root.</param>
    public static IEnumerable<MemberDeclarationSyntax> TrackedMembers(SyntaxNode root) =>
        root.DescendantNodes(node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax or TypeDeclarationSyntax)
            .OfType<MemberDeclarationSyntax>()
            .Where(IsTracked);

    /// <summary>The leading run of global statements of a file: its top-level statements.</summary>
    /// <param name="unit">The file's syntax root.</param>
    public static IReadOnlyList<GlobalStatementSyntax> TopLevelStatements(CompilationUnitSyntax unit) =>
        unit.Members.TakeWhile(m => m is GlobalStatementSyntax).Cast<GlobalStatementSyntax>().ToList();

    /// <summary>
    /// Whether a function with this signature returns a value, so its expression body becomes a <c>return</c>:
    /// not for <c>void</c>, and not for an <c>async</c> method whose task type has no result, such as <c>Task</c>.
    /// </summary>
    /// <param name="returnType">The declared return type.</param>
    /// <param name="modifiers">The function's modifiers.</param>
    public static bool ReturnsValue(TypeSyntax returnType, SyntaxTokenList modifiers)
    {
        ArgumentNullException.ThrowIfNull(returnType);
        if (returnType is PredefinedTypeSyntax predefined && predefined.Keyword.IsKind(SyntaxKind.VoidKeyword))
        {
            return false;
        }

        if (!modifiers.Any(SyntaxKind.AsyncKeyword))
        {
            return true;
        }

        var rightmost = returnType switch
        {
            QualifiedNameSyntax qualified => qualified.Right,
            AliasQualifiedNameSyntax alias => alias.Name,
            _ => returnType,
        };
        return rightmost is GenericNameSyntax;
    }

    /// <summary>The full name of the type that declares a node, as reflection writes it, such as <c>Ns.Outer+Inner`1</c>.</summary>
    /// <param name="node">A node inside a type, or a top-level statement.</param>
    public static string TypeNameOf(SyntaxNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var name = string.Empty;
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case BaseTypeDeclarationSyntax type:
                    var typeName = type.Identifier.ValueText + Arity(type is TypeDeclarationSyntax declaration ? declaration.TypeParameterList : null);
                    name = name.Length == 0 ? typeName : typeName + "+" + name;
                    break;
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    var namespaceName = Compact(namespaceDeclaration.Name);
                    name = name.Length == 0 ? namespaceName : namespaceName + "." + name;
                    break;
            }
        }

        return name.Length == 0 ? TopLevelTypeName : name;
    }

    private static bool IsTrackedNode(SyntaxNode node) =>
        node is GlobalStatementSyntax || (node is MemberDeclarationSyntax member && IsTracked(member));

    private static bool HasAccessorBody(AccessorListSyntax? accessors) =>
        accessors is not null && accessors.Accessors.Any(a => a.Body is not null || a.ExpressionBody is not null);

    private static string Explicit(ExplicitInterfaceSpecifierSyntax? specifier) =>
        specifier is null ? string.Empty : Compact(specifier.Name) + ".";

    private static string Arity(TypeParameterListSyntax? parameters) =>
        parameters is { Parameters.Count: > 0 and var count } ? "`" + count.ToString(CultureInfo.InvariantCulture) : string.Empty;

    private static string Parameter(ParameterSyntax parameter)
    {
        var modifiers = parameter.Modifiers.Where(m => !m.IsKind(SyntaxKind.ThisKeyword)).Select(m => m.Text + " ");
        return string.Concat(modifiers) + (parameter.Type is { } type ? Compact(type) : string.Empty);
    }

    /// <summary>A node's text without any whitespace, so formatting does not change a key.</summary>
    /// <param name="node">The node.</param>
    internal static string Compact(SyntaxNode node)
    {
        var text = node.ToString();
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>The tokens of nodes without trivia, so whitespace and comments do not change the text.</summary>
    /// <param name="nodes">The nodes.</param>
    internal static string TokensOf(IEnumerable<SyntaxNode> nodes)
    {
        var builder = new StringBuilder();
        foreach (var node in nodes)
        {
            foreach (var token in node.DescendantTokens())
            {
                builder.Append(token.Text).Append(' ');
            }
        }

        return builder.ToString();
    }

    /// <summary>Numbers members that would otherwise share a key, such as the parts of code inside <c>#if</c> branches.</summary>
    internal sealed class KeyCounter
    {
        private readonly Dictionary<string, int> _seen = new(StringComparer.Ordinal);

        /// <summary>Returns the key itself the first time, then the key with <c>#2</c>, <c>#3</c>….</summary>
        /// <param name="key">The member's key.</param>
        public string Unique(string key)
        {
            var count = _seen.TryGetValue(key, out var seen) ? seen + 1 : 1;
            _seen[key] = count;
            return count == 1 ? key : key + "#" + count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
