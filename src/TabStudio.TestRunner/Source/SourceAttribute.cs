using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.TestRunner.Naming;

namespace TabStudio.TestRunner.Source;

/// <summary>
/// An attribute in source, read with the semantic model when its type resolves, and by its written name when
/// it does not, for example before the project's packages are restored.
/// </summary>
internal sealed class SourceAttribute
{
    private const string AttributeSuffix = "Attribute";

    private readonly AttributeData? _data;
    private readonly AttributeSyntax? _syntax;
    private readonly HashSet<string> _typeNames;

    private SourceAttribute(AttributeData? data, AttributeSyntax? syntax)
    {
        _data = data;
        _syntax = syntax;
        _typeNames = data?.AttributeClass is { TypeKind: not TypeKind.Error } attributeClass
            ? TypeAndBaseNames(attributeClass)
            : [];

        var name = _typeNames.Count > 0 ? data!.AttributeClass!.Name : syntax is null ? string.Empty : WrittenName(syntax.Name);
        ShortName = name.EndsWith(AttributeSuffix, StringComparison.Ordinal) && name.Length > AttributeSuffix.Length
            ? name[..^AttributeSuffix.Length]
            : name;
    }

    /// <summary>The name without the <c>Attribute</c> suffix, such as <c>Fact</c>.</summary>
    public string ShortName { get; }

    /// <summary>Where the attribute is written, or <see langword="null"/> when it is not in source.</summary>
    public Location? Location => _syntax?.GetLocation();

    /// <summary>Reads the attributes of a symbol with the semantic model.</summary>
    /// <param name="attributes">The symbol's attributes.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    public static List<SourceAttribute> FromSymbol(IEnumerable<AttributeData> attributes, CancellationToken cancellationToken) =>
        attributes
            .Select(a => new SourceAttribute(a, a.ApplicationSyntaxReference?.GetSyntax(cancellationToken) as AttributeSyntax))
            .ToList();

    /// <summary>Reads attributes by their written names only.</summary>
    /// <param name="lists">The attribute lists of a declaration.</param>
    public static List<SourceAttribute> FromSyntax(SyntaxList<AttributeListSyntax> lists) =>
        lists
            .Where(l => l.Target is null || l.Target.Identifier.IsKind(SyntaxKind.TypeKeyword) || l.Target.Identifier.IsKind(SyntaxKind.MethodKeyword))
            .SelectMany(l => l.Attributes)
            .Select(a => new SourceAttribute(null, a))
            .ToList();

    /// <summary>
    /// Whether this is the given attribute: by full name, including base classes, when its type resolved; by
    /// written name otherwise.
    /// </summary>
    /// <param name="fullName">The full name, such as <c>Xunit.FactAttribute</c>.</param>
    /// <param name="shortName">The written name, such as <c>Fact</c>.</param>
    /// <param name="otherShortName">Another written name that means the same, such as <c>Theory</c> for <c>Fact</c>.</param>
    public bool Is(string fullName, string shortName, string? otherShortName = null) =>
        _typeNames.Count > 0
            ? _typeNames.Contains(fullName)
            : ShortName == shortName || (otherShortName is not null && ShortName == otherShortName);

    /// <summary>The positional arguments as values, with a <c>params</c> array spread into its elements.</summary>
    public List<object?> PositionalValues()
    {
        var values = new List<object?>();
        if (_data is not null && _typeNames.Count > 0)
        {
            var parameters = _data.AttributeConstructor?.Parameters ?? [];
            for (var i = 0; i < _data.ConstructorArguments.Length; i++)
            {
                var argument = _data.ConstructorArguments[i];
                var isParams = i == parameters.Length - 1 && parameters[i].IsParams;
                if (isParams && argument.Kind == TypedConstantKind.Array)
                {
                    if (argument.IsNull)
                    {
                        values.Add(null);
                    }
                    else
                    {
                        values.AddRange(argument.Values.Select(Value));
                    }
                }
                else
                {
                    values.Add(Value(argument));
                }
            }

            return values;
        }

        foreach (var argument in Arguments())
        {
            if (argument.NameEquals is null)
            {
                values.Add(Evaluate(argument.Expression));
            }
        }

        return values;
    }

    /// <summary>The first positional argument when it is a string.</summary>
    public string? FirstString() => PositionalValues() is [string text, ..] ? text : null;

    /// <summary>A named argument, such as <c>Skip = "reason"</c>, or <see langword="null"/>.</summary>
    /// <param name="name">The property or field name.</param>
    public object? Named(string name)
    {
        if (_data is not null && _typeNames.Count > 0)
        {
            foreach (var (key, value) in _data.NamedArguments)
            {
                if (key == name)
                {
                    return Value(value);
                }
            }

            return null;
        }

        foreach (var argument in Arguments())
        {
            if (argument.NameEquals?.Name.Identifier.ValueText == name)
            {
                return Evaluate(argument.Expression);
            }
        }

        return null;
    }

    /// <summary>Whether the attribute's type implements an interface, such as MSTest's <c>ITestDataSource</c>.</summary>
    /// <param name="interfaceFullName">The interface's full name.</param>
    public bool Implements(string interfaceFullName) =>
        _data?.AttributeClass is { } attributeClass &&
        attributeClass.AllInterfaces.Any(i => SymbolNames.MetadataFullName(i) == interfaceFullName);

    private SeparatedSyntaxList<AttributeArgumentSyntax> Arguments() =>
        _syntax?.ArgumentList?.Arguments ?? default;

    private static HashSet<string> TypeAndBaseNames(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            names.Add(SymbolNames.MetadataFullName(current));
        }

        return names;
    }

    private static string WrittenName(NameSyntax name) => name switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => name.ToString(),
    };

    /// <summary>Turns a constant from the semantic model into the value reflection would see.</summary>
    private static object? Value(TypedConstant constant)
    {
        if (constant.IsNull)
        {
            return null;
        }

        return constant.Kind switch
        {
            TypedConstantKind.Array => constant.Values.Select(Value).ToArray(),
            TypedConstantKind.Enum => new FormattedValue(EnumMemberName(constant) ?? Convert.ToString(constant.Value, CultureInfo.InvariantCulture) ?? "?"),
            TypedConstantKind.Type => new FormattedValue("typeof(" + ((constant.Value as ITypeSymbol)?.MetadataName ?? "?") + ")"),
            TypedConstantKind.Error => new FormattedValue("?"),
            _ => constant.Value,
        };
    }

    private static string? EnumMemberName(TypedConstant constant) =>
        (constant.Type as INamedTypeSymbol)?.GetMembers()
            .OfType<IFieldSymbol>()
            .FirstOrDefault(f => f.HasConstantValue && Equals(f.ConstantValue, constant.Value))?.Name;

    /// <summary>Evaluates an argument as written, when there is no semantic model to ask.</summary>
    private static object? Evaluate(ExpressionSyntax expression) => expression switch
    {
        LiteralExpressionSyntax literal => literal.Token.Value,
        PrefixUnaryExpressionSyntax { RawKind: (int)SyntaxKind.UnaryMinusExpression, Operand: LiteralExpressionSyntax literal } => Negate(literal.Token.Value),
        TypeOfExpressionSyntax typeOf => new FormattedValue("typeof(" + RightmostName(typeOf.Type) + ")"),
        MemberAccessExpressionSyntax member => new FormattedValue(member.Name.Identifier.ValueText),
        ImplicitArrayCreationExpressionSyntax array => array.Initializer.Expressions.Select(Evaluate).ToArray(),
        ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions.Select(Evaluate).ToArray(),
        CollectionExpressionSyntax collection => collection.Elements.OfType<ExpressionElementSyntax>().Select(e => Evaluate(e.Expression)).ToArray(),
        CastExpressionSyntax cast => Evaluate(cast.Expression),
        ParenthesizedExpressionSyntax parenthesized => Evaluate(parenthesized.Expression),
        _ => new FormattedValue(expression.ToString()),
    };

    private static object? Negate(object? value) => value switch
    {
        int number => -number,
        long number => -number,
        float number => -number,
        double number => -number,
        decimal number => -number,
        uint number => -(long)number,
        _ => value,
    };

    private static string RightmostName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        PredefinedTypeSyntax predefined => PredefinedName(predefined.Keyword.Kind()),
        _ => type.ToString(),
    };

    /// <summary>The runtime name of a C# keyword type, as <see cref="Type.Name"/> prints it.</summary>
    private static string PredefinedName(SyntaxKind keyword) => keyword switch
    {
        SyntaxKind.IntKeyword => "Int32",
        SyntaxKind.LongKeyword => "Int64",
        SyntaxKind.StringKeyword => "String",
        SyntaxKind.BoolKeyword => "Boolean",
        SyntaxKind.DoubleKeyword => "Double",
        SyntaxKind.FloatKeyword => "Single",
        SyntaxKind.DecimalKeyword => "Decimal",
        SyntaxKind.CharKeyword => "Char",
        SyntaxKind.ObjectKeyword => "Object",
        SyntaxKind.ByteKeyword => "Byte",
        SyntaxKind.ShortKeyword => "Int16",
        _ => keyword.ToString(),
    };
}
