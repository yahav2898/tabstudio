using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using TabStudio.TestRunner.Naming;

namespace TabStudio.TestRunner.Source;

/// <summary>
/// Source discovery's view of Visual Basic syntax: tests in <c>Class</c> and <c>Module</c> blocks, attributes such
/// as <c>&lt;Fact&gt;</c> and <c>&lt;InlineData(1, "a")&gt;</c>, names compared without regard to case.
/// </summary>
/// <remarks>
/// The compiler puts every declaration in the project's root namespace, unless its namespace starts with
/// <c>Global</c>; from syntax alone, names are only right when the root namespace is given.
/// </remarks>
internal sealed class VisualBasicSourceLanguage : SourceLanguage
{
    private VisualBasicSourceLanguage()
    {
    }

    /// <summary>The one instance.</summary>
    public static VisualBasicSourceLanguage Instance { get; } = new();

    /// <inheritdoc />
    public override StringComparison NameComparison => StringComparison.OrdinalIgnoreCase;

    /// <inheritdoc />
    public override IEnumerable<SourceTypeDeclaration> Types(SyntaxNode root, string rootNamespace) =>
        root.DescendantNodes(node => node is CompilationUnitSyntax or NamespaceBlockSyntax or TypeBlockSyntax)
            .OfType<TypeBlockSyntax>()
            .Where(block => block is ClassBlockSyntax or ModuleBlockSyntax)
            .Select(block => Describe(block, rootNamespace));

    /// <inheritdoc />
    public override SourceMethodDeclaration? MethodOf(SyntaxNode declaration) => declaration switch
    {
        MethodBlockSyntax block => Describe(block.SubOrFunctionStatement),
        MethodStatementSyntax statement => Describe(statement),
        _ => null,
    };

    /// <inheritdoc />
    public override string AttributeName(SyntaxNode attribute) => RightmostName(((AttributeSyntax)attribute).Name);

    /// <inheritdoc />
    public override IReadOnlyList<(string? Name, SyntaxNode Expression)> AttributeArguments(SyntaxNode attribute) =>
        ((AttributeSyntax)attribute).ArgumentList is { } arguments
            ? [.. arguments.Arguments
                .OfType<SimpleArgumentSyntax>()
                .Select(a => (Name: a.NameColonEquals?.Name.Identifier.ValueText, Expression: (SyntaxNode)a.Expression))]
            : [];

    /// <inheritdoc />
    public override object? Evaluate(SyntaxNode expression) => EvaluateExpression((ExpressionSyntax)expression);

    private static SourceTypeDeclaration Describe(TypeBlockSyntax block, string rootNamespace)
    {
        var statement = block.BlockStatement;
        var isModule = block is ModuleBlockSyntax;
        return new SourceTypeDeclaration(
            statement,
            statement.Identifier,
            IsStatic: isModule,
            IsAbstract: statement.Modifiers.Any(SyntaxKind.MustInheritKeyword),
            IsGeneric: statement.TypeParameterList is not null,
            FullName: MetadataFullName(block, rootNamespace),
            Attributes: Attributes(statement.AttributeLists),
            Methods: [.. block.Members
                .Select(member => member switch
                {
                    MethodBlockSyntax method => method.SubOrFunctionStatement,
                    MethodStatementSyntax method => method,
                    _ => null,
                })
                .OfType<MethodStatementSyntax>()
                .Select(Describe)]);
    }

    /// <summary>Describes a <c>Sub</c> or <c>Function</c>; every method of a <c>Module</c> is <c>Shared</c>.</summary>
    private static SourceMethodDeclaration Describe(MethodStatementSyntax method) => new(
        method,
        method.Identifier,
        IsStatic: method.Modifiers.Any(SyntaxKind.SharedKeyword) || method.Parent is ModuleBlockSyntax || method.Parent?.Parent is ModuleBlockSyntax,
        ParameterNames: method.ParameterList is { } parameters
            ? [.. parameters.Parameters.Select(p => p.Identifier.Identifier.ValueText)]
            : [],
        Attributes: Attributes(method.AttributeLists));

    /// <summary>
    /// The attributes of a declaration. Visual Basic writes the attributes of a function's return value in its
    /// <c>As</c> clause, so every attribute here applies to the declaration itself.
    /// </summary>
    private static List<SyntaxNode> Attributes(SyntaxList<AttributeListSyntax> lists) =>
        [.. lists.SelectMany(l => l.Attributes)];

    /// <summary>
    /// The name <see cref="Type.FullName"/> gives a class: its enclosing types and namespaces, with the root namespace
    /// in front unless the outermost namespace starts with <c>Global</c>.
    /// </summary>
    private static string MetadataFullName(TypeBlockSyntax block, string rootNamespace)
    {
        var name = TypeName(block.BlockStatement);
        var namespaces = new List<string>();
        var isGlobal = false;
        for (var parent = block.Parent; parent is not null; parent = parent.Parent)
        {
            switch (parent)
            {
                case TypeBlockSyntax outer:
                    name = TypeName(outer.BlockStatement) + "+" + name;
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

        return namespaces.Count == 0 ? name : string.Join('.', namespaces) + "." + name;
    }

    private static string TypeName(TypeStatementSyntax statement) =>
        statement.Identifier.ValueText + (statement.Arity > 0 ? "`" + statement.Arity.ToString(CultureInfo.InvariantCulture) : string.Empty);

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
                parts.Add(name.ToString());
                return false;
        }
    }

    private static string RightmostName(TypeSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        PredefinedTypeSyntax predefined => PredefinedName(predefined.Keyword.Kind()),
        ArrayTypeSyntax array => RightmostName(array.ElementType) + "[]",
        NullableTypeSyntax => "Nullable`1",
        _ => name.ToString(),
    };

    /// <summary>Evaluates an argument as written, when there is no semantic model to ask.</summary>
    private static object? EvaluateExpression(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => literal.Kind() switch
        {
            SyntaxKind.TrueLiteralExpression => true,
            SyntaxKind.FalseLiteralExpression => false,
            SyntaxKind.NothingLiteralExpression => null,
            _ => literal.Token.Value,
        },
        UnaryExpressionSyntax { Operand: LiteralExpressionSyntax literal } unary when unary.IsKind(SyntaxKind.UnaryMinusExpression) => Negate(EvaluateExpression(literal)),
        GetTypeExpressionSyntax getType => new FormattedValue("typeof(" + RightmostName(getType.Type) + ")"),
        MemberAccessExpressionSyntax member => new FormattedValue(member.Name.Identifier.ValueText),
        CollectionInitializerSyntax collection => collection.Initializers.Select(EvaluateExpression).ToArray(),
        ArrayCreationExpressionSyntax array => array.Initializer.Initializers.Select(EvaluateExpression).ToArray(),
        PredefinedCastExpressionSyntax cast => EvaluateExpression(cast.Expression),
        CastExpressionSyntax cast => EvaluateExpression(cast.Expression),
        ParenthesizedExpressionSyntax parenthesized => EvaluateExpression(parenthesized.Expression),
        _ => new FormattedValue(expression.ToString()),
    };

    /// <summary>The runtime name of a Visual Basic keyword type, as <c>Type.Name</c> prints it.</summary>
    private static string PredefinedName(SyntaxKind keyword) => keyword switch
    {
        SyntaxKind.IntegerKeyword => "Int32",
        SyntaxKind.LongKeyword => "Int64",
        SyntaxKind.ShortKeyword => "Int16",
        SyntaxKind.UIntegerKeyword => "UInt32",
        SyntaxKind.ULongKeyword => "UInt64",
        SyntaxKind.UShortKeyword => "UInt16",
        SyntaxKind.ByteKeyword => "Byte",
        SyntaxKind.SByteKeyword => "SByte",
        SyntaxKind.StringKeyword => "String",
        SyntaxKind.BooleanKeyword => "Boolean",
        SyntaxKind.DoubleKeyword => "Double",
        SyntaxKind.SingleKeyword => "Single",
        SyntaxKind.DecimalKeyword => "Decimal",
        SyntaxKind.CharKeyword => "Char",
        SyntaxKind.ObjectKeyword => "Object",
        SyntaxKind.DateKeyword => "DateTime",
        _ => keyword.ToString(),
    };
}
