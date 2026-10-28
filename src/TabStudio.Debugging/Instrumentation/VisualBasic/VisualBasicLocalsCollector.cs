using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using TabStudio.Debugging.Symbols;
using VisualBasicFacts = Microsoft.CodeAnalysis.VisualBasic.SyntaxFacts;

namespace TabStudio.Debugging.Instrumentation.VisualBasic;

/// <summary>
/// Decides which variables a Visual Basic hook reads: <c>Me</c>, the parameters and the locals that are in scope by
/// name and declared before the hook, whose values can be boxed.
/// </summary>
/// <remarks>
/// Visual Basic never refuses to read a local before it is assigned (it is <c>Nothing</c> or zero), so unlike C# there
/// is no definite assignment check: a <c>Dim x As Integer</c> shows <c>0</c>. A local is not read inside the statement
/// that declares it, such as a lambda in its own initializer, where an inferred type would become circular. <c>ByRef</c>
/// parameters are only read in the function that declares them, because lambdas cannot capture them, and the hidden
/// local that holds a <c>Function</c>'s return value is never shown.
/// </remarks>
internal static class VisualBasicLocalsCollector
{
    /// <summary>The name of the current instance, as the Locals panel shows it.</summary>
    public const string MeName = "Me";

    /// <summary>Types as Visual Basic writes them, such as <c>Integer</c> or <c>List(Of String)</c>.</summary>
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat
        .RemoveMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>The variables a hook at <paramref name="position"/> reports, in the order the hook passes them.</summary>
    /// <param name="model">The semantic model of the original tree.</param>
    /// <param name="position">Where the hook goes: the start of a statement or a condition.</param>
    /// <param name="context">The function the hook is in.</param>
    /// <param name="candidates">The parameters and locals of the function and of the functions around it.</param>
    /// <param name="cancellationToken">Cancels the analysis.</param>
    public static IReadOnlyList<CollectedLocal> Collect(
        SemanticModel model,
        int position,
        FunctionContext context,
        IReadOnlyList<ISymbol> candidates,
        CancellationToken cancellationToken)
    {
        var result = new List<CollectedLocal>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (context.ThisType is { } thisType)
        {
            result.Add(new CollectedLocal(MeName, new DebugLocalSlot(MeName, thisType)));
            names.Add(MeName);
        }

        foreach (var symbol in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetReadableType(symbol, position, context, cancellationToken, out var type) &&
                !names.Contains(symbol.Name) &&
                IsVisibleByName(model, symbol, position))
            {
                names.Add(symbol.Name);
                result.Add(new CollectedLocal(
                    VisualBasicHookText.Identifier(symbol.Name),
                    new DebugLocalSlot(symbol.Name, type.ToDisplayString(TypeFormat))));
            }
        }

        return result;
    }

    /// <summary>
    /// What a function's exception filter reports, <c>Me</c> and every parameter whose value can be boxed: the slots
    /// and the expressions that read them, in the same order.
    /// </summary>
    /// <param name="method">The function.</param>
    /// <param name="thisType">The type of <c>Me</c> when the function may read it, otherwise <see langword="null"/>.</param>
    public static (IReadOnlyList<DebugLocalSlot> Slots, IReadOnlyList<string> Values) Parameters(IMethodSymbol method, string? thisType)
    {
        var slots = new List<DebugLocalSlot>();
        var values = new List<string>();
        if (thisType is not null)
        {
            slots.Add(new DebugLocalSlot(MeName, thisType));
            values.Add(MeName);
        }

        foreach (var parameter in method.Parameters)
        {
            if (VisualBasicFacts.IsValidIdentifier(parameter.Name) && LocalsCollector.IsBoxable(parameter.Type))
            {
                slots.Add(new DebugLocalSlot(parameter.Name, parameter.Type.ToDisplayString(TypeFormat)));
                values.Add(VisualBasicHookText.Identifier(parameter.Name));
            }
        }

        return (slots, values);
    }

    private static bool TryGetReadableType(ISymbol symbol, int position, FunctionContext context, CancellationToken cancellationToken, out ITypeSymbol type)
    {
        RefKind refKind;
        switch (symbol)
        {
            case ILocalSymbol { IsFunctionValue: false } local:
                type = local.Type;
                refKind = local.RefKind;
                if (local.Locations.FirstOrDefault() is not { IsInSource: true } location ||
                    location.SourceSpan.Start >= position ||
                    IsInsideDeclaration(local, position, cancellationToken))
                {
                    return false;
                }

                break;
            case IParameterSymbol { IsThis: false } parameter:
                type = parameter.Type;
                refKind = parameter.RefKind;
                break;
            default:
                type = null!;
                return false;
        }

        return VisualBasicFacts.IsValidIdentifier(symbol.Name) &&
            LocalsCollector.IsBoxable(type) &&
            context.CanRead(symbol.ContainingSymbol, byReference: refKind != RefKind.None);
    }

    /// <summary>Whether a position is inside the statement that declares a local, such as its initializer.</summary>
    private static bool IsInsideDeclaration(ILocalSymbol local, int position, CancellationToken cancellationToken) =>
        local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken) is { } declaration &&
        declaration.AncestorsAndSelf().OfType<StatementSyntax>().FirstOrDefault() is { } statement &&
        statement.Span.Contains(position);

    private static bool IsVisibleByName(SemanticModel model, ISymbol symbol, int position)
    {
        var location = symbol.Locations.FirstOrDefault();
        foreach (var candidate in model.LookupSymbols(position, name: symbol.Name))
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, symbol) ||
                (candidate.Kind == symbol.Kind && location is not null && Equals(candidate.Locations.FirstOrDefault(), location)))
            {
                return true;
            }
        }

        return false;
    }
}
