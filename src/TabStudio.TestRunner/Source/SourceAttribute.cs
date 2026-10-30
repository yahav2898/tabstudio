using System.Globalization;
using Microsoft.CodeAnalysis;
using TabStudio.TestRunner.Naming;

namespace TabStudio.TestRunner.Source;

/// <summary>
/// An attribute in source, read with the semantic model when its type resolves, and by its written name when
/// it does not, for example before the project's packages are restored.
/// </summary>
/// <remarks>
/// Written names are compared the way the attribute's language binds them: exactly in C#, ignoring case in Visual
/// Basic, where <c>&lt;fact&gt;</c> is a <c>&lt;Fact&gt;</c>.
/// </remarks>
internal sealed class SourceAttribute
{
    private const string AttributeSuffix = "Attribute";

    private readonly AttributeData? _data;
    private readonly SyntaxNode? _syntax;
    private readonly SourceLanguage _language;
    private readonly HashSet<string> _typeNames;

    private SourceAttribute(AttributeData? data, SyntaxNode? syntax, SourceLanguage language)
    {
        _data = data;
        _syntax = syntax;
        _language = language;
        _typeNames = data?.AttributeClass is { TypeKind: not TypeKind.Error } attributeClass
            ? TypeAndBaseNames(attributeClass)
            : [];

        var name = _typeNames.Count > 0 ? data!.AttributeClass!.Name : syntax is null ? string.Empty : language.AttributeName(syntax);
        ShortName = name.EndsWith(AttributeSuffix, language.NameComparison)
            && name.Length > AttributeSuffix.Length
            ? name[..^AttributeSuffix.Length]
            : name;
    }

    /// <summary>The name without the <c>Attribute</c> suffix, such as <c>Fact</c>.</summary>
    public string ShortName { get; }

    /// <summary>Where the attribute is written, or <see langword="null"/> when it is not in source.</summary>
    public Location? Location => _syntax?.GetLocation();

    /// <summary>Reads the attributes of a symbol with the semantic model.</summary>
    /// <param name="attributes">The symbol's attributes.</param>
    /// <param name="language">The language of the source being searched.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    public static List<SourceAttribute> FromSymbol(IEnumerable<AttributeData> attributes, SourceLanguage language, CancellationToken cancellationToken) =>
        attributes
            .Select(a => new SourceAttribute(a, a.ApplicationSyntaxReference?.GetSyntax(cancellationToken), language))
            .ToList();

    /// <summary>Reads attributes by their written names only.</summary>
    /// <param name="attributes">The attribute nodes of a declaration, as its <see cref="SourceLanguage"/> lists them.</param>
    /// <param name="language">Their language.</param>
    public static List<SourceAttribute> FromSyntax(IEnumerable<SyntaxNode> attributes, SourceLanguage language) =>
        attributes.Select(a => new SourceAttribute(null, a, language)).ToList();

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
            : HasShortName(shortName) || (otherShortName is not null && HasShortName(otherShortName));

    /// <summary>Whether the attribute's name, without the <c>Attribute</c> suffix, is the given one.</summary>
    /// <param name="shortName">A name such as <c>DynamicData</c>.</param>
    public bool HasShortName(string shortName) => _language.NamesEqual(ShortName, shortName);

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

        foreach (var (name, expression) in Arguments())
        {
            if (name is null)
            {
                values.Add(_language.Evaluate(expression));
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
                if (_language.NamesEqual(key, name))
                {
                    return Value(value);
                }
            }

            return null;
        }

        foreach (var (argumentName, expression) in Arguments())
        {
            if (argumentName is not null && _language.NamesEqual(argumentName, name))
            {
                return _language.Evaluate(expression);
            }
        }

        return null;
    }

    /// <summary>Whether the attribute's type implements an interface, such as MSTest's <c>ITestDataSource</c>.</summary>
    /// <param name="interfaceFullName">The interface's full name.</param>
    public bool Implements(string interfaceFullName) =>
        _data?.AttributeClass is { } attributeClass &&
        attributeClass.AllInterfaces.Any(i => SymbolNames.MetadataFullName(i) == interfaceFullName);

    private IReadOnlyList<(string? Name, SyntaxNode Expression)> Arguments() =>
        _syntax is null ? [] : _language.AttributeArguments(_syntax);

    private static HashSet<string> TypeAndBaseNames(INamedTypeSymbol type)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            names.Add(SymbolNames.MetadataFullName(current));
        }

        return names;
    }

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
}
