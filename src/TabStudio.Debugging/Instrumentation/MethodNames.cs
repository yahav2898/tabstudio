using Microsoft.CodeAnalysis;

namespace TabStudio.Debugging.Instrumentation;

/// <summary>
/// Names functions the way the Call Stack panel shows them, for example <c>Calculator.Add(int, int)</c>, or in Visual
/// Basic <c>Calculator.Add(Integer, Integer)</c>, as the symbol's own language displays types.
/// </summary>
internal static class MethodNames
{
    /// <summary>What the Call Stack shows for a program's top-level statements.</summary>
    public const string TopLevelStatements = "<top-level statements>";

    private const string EntryPointName = "<Main>$";

    private static readonly SymbolDisplayFormat TypeFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    /// <summary>The display name of a function, with its parameter types.</summary>
    public static string Display(IMethodSymbol method)
    {
        if (method.Name == EntryPointName)
        {
            return TopLevelStatements;
        }

        var parameters = string.Join(", ", method.Parameters.Select(p => p.Type.ToDisplayString(TypeFormat)));
        return PathOf(method) + "(" + parameters + ")";
    }

    private static string PathOf(ISymbol? symbol) => symbol switch
    {
        null => string.Empty,
        IMethodSymbol { Name: EntryPointName } => TopLevelStatements,
        IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } lambda => Nested(lambda.ContainingSymbol, "<lambda>"),
        IMethodSymbol { MethodKind: MethodKind.LocalFunction } local => Nested(local.ContainingSymbol, local.Name),
        IMethodSymbol { AssociatedSymbol: { } owner } accessor when AccessorName(accessor) is { } kind =>
            TypeOf(accessor) + "." + MemberName(owner) + "." + kind,
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor when IsVisualBasic(constructor) => TypeOf(constructor) + ".New",
        IMethodSymbol { MethodKind: MethodKind.StaticConstructor } constructor when IsVisualBasic(constructor) => TypeOf(constructor) + ".Shared New",
        IMethodSymbol { MethodKind: MethodKind.Constructor } constructor => TypeOf(constructor) + "." + constructor.ContainingType.Name,
        IMethodSymbol { MethodKind: MethodKind.StaticConstructor } constructor => TypeOf(constructor) + ".static " + constructor.ContainingType.Name,
        ITypeSymbol type => type.ToDisplayString(TypeFormat),
        _ => TypeOf(symbol) + "." + symbol.Name,
    };

    /// <summary>A local function or lambda inside the top-level statements is shown by its own name only.</summary>
    private static string Nested(ISymbol container, string name) =>
        container is IMethodSymbol { Name: EntryPointName } ? name : PathOf(container) + "." + name;

    private static string TypeOf(ISymbol symbol) => symbol.ContainingType?.ToDisplayString(TypeFormat) ?? string.Empty;

    private static string MemberName(ISymbol owner) =>
        owner is IPropertySymbol { IsIndexer: true } && !IsVisualBasic(owner) ? "this[]" : owner.Name;

    private static bool IsVisualBasic(ISymbol symbol) => symbol.Language == LanguageNames.VisualBasic;

    private static string? AccessorName(IMethodSymbol accessor) =>
        IsVisualBasic(accessor) ? VisualBasicAccessorName(accessor) : CSharpAccessorName(accessor);

    private static string? CSharpAccessorName(IMethodSymbol accessor) => accessor.MethodKind switch
    {
        MethodKind.PropertyGet => "get",
        MethodKind.PropertySet => accessor.IsInitOnly ? "init" : "set",
        MethodKind.EventAdd => "add",
        MethodKind.EventRemove => "remove",
        _ => null,
    };

    private static string? VisualBasicAccessorName(IMethodSymbol accessor) => accessor.MethodKind switch
    {
        MethodKind.PropertyGet => "Get",
        MethodKind.PropertySet => "Set",
        MethodKind.EventAdd => "AddHandler",
        MethodKind.EventRemove => "RemoveHandler",
        MethodKind.EventRaise => "RaiseEvent",
        _ => null,
    };
}
