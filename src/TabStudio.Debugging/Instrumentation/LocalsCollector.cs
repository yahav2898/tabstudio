using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Instrumentation;

/// <summary>A variable a hook passes to the debugger: the C# expression that reads it and how it is shown.</summary>
/// <param name="Expression">The expression in the hook, such as <c>@count</c> or <c>this</c>.</param>
/// <param name="Slot">The name and declared type shown in the Locals panel.</param>
internal sealed record CollectedLocal(string Expression, DebugLocalSlot Slot);

/// <summary>
/// Decides which variables a hook can read at a given point without changing whether the program compiles or what
/// it does.
/// </summary>
/// <remarks>
/// A variable is reported when it is in scope by name, definitely assigned on entry to the statement (from
/// <see cref="SemanticModel"/> data flow analysis), declared before it, and its value can be boxed: ref structs such
/// as <see cref="Span{T}"/>, pointers and type parameters that allow ref structs are left out. Reference variables
/// (ref locals and ref, in or out parameters) are only read in the function that declares them, because lambdas
/// and local functions cannot capture them.
/// </remarks>
internal static class LocalsCollector
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.MinimallyQualifiedFormat;

    /// <summary>The variables a hook before <paramref name="node"/> (a statement or an expression body) reports.</summary>
    public static IReadOnlyList<CollectedLocal> Collect(SemanticModel model, SyntaxNode node, FunctionContext context, CancellationToken cancellationToken)
    {
        var result = new List<CollectedLocal>();
        if (context.ThisType is { } thisType)
        {
            result.Add(new CollectedLocal("this", new DebugLocalSlot("this", thisType)));
        }

        var flow = AnalyzeDataFlow(model, node);
        if (flow is null)
        {
            return result;
        }

        var position = node.SpanStart;
        var candidates = flow.DefinitelyAssignedOnEntry
            .Where(symbol => symbol is ILocalSymbol or IParameterSymbol)
            .OrderBy(symbol => symbol is ILocalSymbol ? 1 : 0)
            .ThenBy(symbol => symbol.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0);

        foreach (var symbol in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (TryGetReadableType(symbol, position, context, out var type) && IsVisibleByName(model, symbol, position))
            {
                result.Add(new CollectedLocal("@" + symbol.Name, new DebugLocalSlot(symbol.Name, type.ToDisplayString(TypeFormat))));
            }
        }

        return result;
    }

    /// <summary>The expressions a function's fault filter passes: <c>this</c> and every readable parameter.</summary>
    public static IReadOnlyList<DebugLocalSlot> ParameterSlots(IMethodSymbol method, string? thisType)
    {
        var slots = new List<DebugLocalSlot>();
        if (thisType is not null)
        {
            slots.Add(new DebugLocalSlot("this", thisType));
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.Out && !parameter.IsDiscard && SyntaxFacts.IsValidIdentifier(parameter.Name) && IsBoxable(parameter.Type))
            {
                slots.Add(new DebugLocalSlot(parameter.Name, parameter.Type.ToDisplayString(TypeFormat)));
            }
        }

        return slots;
    }

    /// <summary>Whether a value of this type can be stored in an <see cref="object"/>.</summary>
    public static bool IsBoxable(ITypeSymbol? type) =>
        type is not null &&
        type.TypeKind is not (TypeKind.Error or TypeKind.Pointer or TypeKind.FunctionPointer) &&
        !type.IsRefLikeType &&
        type.SpecialType != SpecialType.System_Void &&
        type is not ITypeParameterSymbol { AllowsRefLikeType: true };

    private static DataFlowAnalysis? AnalyzeDataFlow(SemanticModel model, SyntaxNode node)
    {
        try
        {
            var flow = node switch
            {
                StatementSyntax statement => model.AnalyzeDataFlow(statement),
                ExpressionSyntax expression => model.AnalyzeDataFlow(expression),
                _ => null,
            };
            return flow is { Succeeded: true } ? flow : null;
        }
        catch (ArgumentException)
        {
            // Roslyn refuses regions it cannot analyze; the hook then reports no locals.
            return null;
        }
    }

    private static bool TryGetReadableType(ISymbol symbol, int position, FunctionContext context, out ITypeSymbol type)
    {
        RefKind refKind;
        switch (symbol)
        {
            case ILocalSymbol local:
                type = local.Type;
                refKind = local.RefKind;
                // In unreachable code every variable counts as assigned, including ones declared further down.
                if (local.Locations.FirstOrDefault() is { IsInSource: true } location && location.SourceSpan.Start >= position)
                {
                    return false;
                }

                break;
            case IParameterSymbol { IsDiscard: false, IsThis: false } parameter:
                type = parameter.Type;
                refKind = parameter.RefKind;
                break;
            default:
                type = null!;
                return false;
        }

        return SyntaxFacts.IsValidIdentifier(symbol.Name) &&
            IsBoxable(type) &&
            context.CanRead(symbol.ContainingSymbol, byReference: refKind != RefKind.None);
    }

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
