using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>
/// Names the members that coverage tracks in Visual Basic, from syntax alone, as <see cref="MemberKeys"/> does in
/// C#: the same member gets the same key in every build and in every version of its file while it is edited.
/// </summary>
/// <remarks>
/// <para>
/// A tracked member is a block with a body that runs: a <c>Sub</c> or <c>Function</c>, a <c>Sub New</c>, an
/// <c>Operator</c>, or a property or custom event with accessor blocks. Lambdas belong to the member they are written
/// in. Partial method declarations, whose bodies are empty by rule, and <c>Finalize</c> overrides, which run on the
/// finalizer thread, are left out; so is everything outside member bodies, the file's skeleton.
/// </para>
/// <para>
/// Type names are written as reflection writes them, with the project's root namespace in front unless the namespace
/// starts with <c>Global</c>, so they compare with the type names of tests and of the coverage of a build.
/// </para>
/// </remarks>
internal static class VisualBasicMemberKeys
{
    /// <summary>Whether coverage tracks a member: it is a block whose body runs.</summary>
    /// <param name="member">A member of a type.</param>
    public static bool IsTracked(StatementSyntax member) => member switch
    {
        MethodBlockSyntax method => !IsPartialDeclaration(method) && !IsFinalizer(method),
        ConstructorBlockSyntax or OperatorBlockSyntax or PropertyBlockSyntax or EventBlockSyntax => true,
        _ => false,
    };

    /// <summary>The containing type's name and the member's own name and key, before duplicates are numbered.</summary>
    /// <param name="member">A tracked member.</param>
    /// <param name="rootNamespace">The project's root namespace, or an empty string.</param>
    public static (string TypeName, string Name, string Key) Describe(StatementSyntax member, string rootNamespace)
    {
        ArgumentNullException.ThrowIfNull(member);
        ArgumentNullException.ThrowIfNull(rootNamespace);
        var typeName = TypeNameOf(member, rootNamespace);
        string name;
        ParameterListSyntax? parameters = null;
        var hasParameterList = true;
        switch (member)
        {
            case MethodBlockSyntax method:
                var statement = method.SubOrFunctionStatement;
                name = statement.Identifier.ValueText + Arity(statement.TypeParameterList);
                parameters = statement.ParameterList;
                break;
            case ConstructorBlockSyntax constructor:
                var isShared = constructor.SubNewStatement.Modifiers.Any(SyntaxKind.SharedKeyword) || constructor.Parent is ModuleBlockSyntax;
                name = isShared ? ".cctor" : ".ctor";
                parameters = constructor.SubNewStatement.ParameterList;
                break;
            case OperatorBlockSyntax op:
                name = OperatorName(op.OperatorStatement);
                parameters = op.OperatorStatement.ParameterList;
                break;
            case PropertyBlockSyntax property:
                // A property is keyed by its parameters only when it has some, such as a default Item(index).
                name = property.PropertyStatement.Identifier.ValueText;
                parameters = property.PropertyStatement.ParameterList;
                hasParameterList = parameters is { Parameters.Count: > 0 };
                break;
            case EventBlockSyntax eventBlock:
                name = eventBlock.EventStatement.Identifier.ValueText;
                hasParameterList = false;
                break;
            default:
                name = member.Kind().ToString();
                hasParameterList = false;
                break;
        }

        var key = typeName + "." + name;
        if (hasParameterList)
        {
            key += "(" + string.Join(",", (parameters?.Parameters ?? default).Select(Parameter)) + ")";
        }

        return (typeName, name, key);
    }

    /// <summary>Finds the tracked members of a file in source order, with their texts and unique keys.</summary>
    /// <param name="root">The file's syntax root.</param>
    /// <param name="rootNamespace">The project's root namespace, or an empty string.</param>
    public static IReadOnlyList<MemberText> Collect(SyntaxNode root, string rootNamespace)
    {
        ArgumentNullException.ThrowIfNull(root);
        var members = new List<MemberText>();
        var keys = new MemberKeys.KeyCounter();
        foreach (var member in TrackedMembers(root))
        {
            var (typeName, name, key) = Describe(member, rootNamespace);
            members.Add(new MemberText(typeName, name, keys.Unique(key), MemberKeys.TokensOf([member])));
        }

        return members;
    }

    /// <summary>
    /// The file without its tracked members, as tokens without trivia: two versions with the same skeleton differ
    /// only inside member bodies (or in whitespace, line breaks and comments).
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

    /// <summary>The tracked members of a file, in source order. Members inside member bodies are never tracked.</summary>
    /// <param name="root">The file's syntax root.</param>
    public static IEnumerable<StatementSyntax> TrackedMembers(SyntaxNode root) =>
        root.DescendantNodes(node => node is CompilationUnitSyntax or NamespaceBlockSyntax or TypeBlockSyntax)
            .OfType<StatementSyntax>()
            .Where(IsTracked);

    /// <summary>
    /// The full name of the type that declares a node, as reflection writes it, such as <c>Root.Ns.Outer+Inner`1</c>.
    /// </summary>
    /// <param name="node">A node inside a type.</param>
    /// <param name="rootNamespace">The project's root namespace, or an empty string.</param>
    public static string TypeNameOf(SyntaxNode node, string rootNamespace)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(rootNamespace);
        var typeName = string.Empty;
        var namespaces = new List<string>();
        var isGlobal = false;
        for (var parent = node.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case TypeBlockSyntax type:
                    var statement = type.BlockStatement;
                    var name = statement.Identifier.ValueText + Arity(statement.TypeParameterList);
                    typeName = typeName.Length == 0 ? name : name + "+" + typeName;
                    break;
                case NamespaceBlockSyntax namespaceBlock:
                    var parts = new List<string>();
                    isGlobal = NameParts(namespaceBlock.NamespaceStatement.Name, parts);
                    namespaces.InsertRange(0, parts);
                    break;
            }
        }

        if (!isGlobal && rootNamespace.Length > 0)
        {
            namespaces.Insert(0, rootNamespace);
        }

        return namespaces.Count == 0 ? typeName : string.Join('.', namespaces) + "." + typeName;
    }

    private static bool IsTrackedNode(SyntaxNode node) => node is StatementSyntax statement && IsTracked(statement);

    private static bool IsPartialDeclaration(MethodBlockSyntax method) =>
        method.SubOrFunctionStatement.Modifiers.Any(SyntaxKind.PartialKeyword);

    private static bool IsFinalizer(MethodBlockSyntax method) =>
        string.Equals(method.SubOrFunctionStatement.Identifier.ValueText, "Finalize", StringComparison.OrdinalIgnoreCase)
        && method.SubOrFunctionStatement.Modifiers.Any(SyntaxKind.OverridesKeyword)
        && (method.SubOrFunctionStatement.ParameterList?.Parameters.Count ?? 0) == 0;

    /// <summary>
    /// An operator's name: <c>operator +</c>, or for a conversion its direction and target type, such as
    /// <c>Widening operator CType Money</c>.
    /// </summary>
    private static string OperatorName(OperatorStatementSyntax statement)
    {
        if (!statement.OperatorToken.IsKind(SyntaxKind.CTypeKeyword))
        {
            return "operator " + statement.OperatorToken.ValueText;
        }

        var direction = statement.Modifiers.Any(SyntaxKind.WideningKeyword) ? "Widening "
            : statement.Modifiers.Any(SyntaxKind.NarrowingKeyword) ? "Narrowing "
            : string.Empty;
        return direction + "operator CType " + (statement.AsClause is { } asClause ? MemberKeys.Compact(asClause.Type) : string.Empty);
    }

    /// <summary>
    /// A parameter's part of a key: <c>ByRef</c> and <c>ParamArray</c>, which change the signature, then its type with
    /// any <c>?</c> or array bounds written on its name. <c>ByVal</c>, <c>Optional</c> and default values are left out.
    /// </summary>
    private static string Parameter(ParameterSyntax parameter)
    {
        var modifiers = parameter.Modifiers
            .Where(m => m.IsKind(SyntaxKind.ByRefKeyword) || m.IsKind(SyntaxKind.ParamArrayKeyword))
            .Select(m => m.IsKind(SyntaxKind.ByRefKeyword) ? "ByRef " : "ParamArray ");
        var identifier = parameter.Identifier;
        var type = parameter.AsClause is { } asClause ? MemberKeys.Compact(asClause.Type) : "Object";
        return string.Concat(modifiers) + type + identifier.Nullable.Text + string.Concat(identifier.ArrayRankSpecifiers.Select(MemberKeys.Compact));
    }

    private static string Arity(TypeParameterListSyntax? parameters) =>
        parameters is { Parameters.Count: > 0 and var count } ? "`" + count.ToString(CultureInfo.InvariantCulture) : string.Empty;

    /// <summary>Adds the identifiers of a namespace name; returns whether it starts with <c>Global</c>.</summary>
    private static bool NameParts(NameSyntax name, List<string> parts)
    {
        switch (name)
        {
            case GlobalNameSyntax:
                return true;
            case QualifiedNameSyntax qualified:
                var isGlobal = NameParts(qualified.Left, parts);
                parts.Add(qualified.Right.Identifier.ValueText);
                return isGlobal;
            case SimpleNameSyntax simple:
                parts.Add(simple.Identifier.ValueText);
                return false;
            default:
                parts.Add(MemberKeys.Compact(name));
                return false;
        }
    }
}
