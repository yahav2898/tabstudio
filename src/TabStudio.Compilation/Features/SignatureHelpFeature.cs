using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TabStudio.Compilation.Features;

/// <summary>
/// Signature help (Parameter Info): the parameter list of the method, constructor, indexer or attribute the caret is
/// in, with the argument being typed highlighted.
/// </summary>
/// <remarks>
/// <para>
/// Roslyn's own signature help services are internal, so this works from the semantic model: it finds the
/// innermost argument list around the caret, the overloads that can be called there, and the overload that best
/// matches the arguments typed so far.
/// </para>
/// <para>
/// C# and Visual Basic files are both served, each shown in its own syntax: <c>string Greeter.Greet(string name)</c>
/// and <c>Greeter.Greet(name As String) As String</c>.
/// </para>
/// </remarks>
public sealed partial class SignatureHelpFeature
{
    private const SymbolDisplayMiscellaneousOptions MiscellaneousOptions =
        SymbolDisplayMiscellaneousOptions.UseSpecialTypes
        | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
        | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier;

    /// <summary>A member without its parameters: <c>string Greeter.Greet</c>. Also used for types.</summary>
    private static readonly SymbolDisplayFormat MemberFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeRef | SymbolDisplayMemberOptions.IncludeContainingType,
        miscellaneousOptions: MiscellaneousOptions);

    /// <summary>One parameter: <c>params object[] args</c> or <c>int count = 1</c>.</summary>
    private static readonly SymbolDisplayFormat ParameterFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeDefaultValue
            | SymbolDisplayParameterOptions.IncludeExtensionThis,
        miscellaneousOptions: MiscellaneousOptions);

    private readonly RoslynWorkspaceHost _host;

    internal SignatureHelpFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Returns signature help at a position.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret.</param>
    /// <param name="cancellationToken">Cancels the request, typically because the user typed again.</param>
    /// <returns>The overloads, or <see langword="null"/> when the caret is not in an argument list.</returns>
    public async Task<SignatureHelpResult?> GetSignatureHelpAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document
            || await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root
            || await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false) is not { } model)
        {
            return null;
        }

        var caret = Math.Clamp(position, 0, root.FullSpan.End);
        var site = root.Language == LanguageNames.VisualBasic ? FindVisualBasicCallSite(root, caret) : FindCallSite(root, caret);
        if (site is null)
        {
            return null;
        }

        var (candidates, resolved) = GetCandidates(model, site.Owner, caret, cancellationToken);
        if (candidates.Count == 0)
        {
            return null;
        }

        var argumentIndex = site.Separators.Count(s => s.SpanStart < caret);
        var argumentName = argumentIndex < site.Arguments.Count ? site.Arguments[argumentIndex].Name : null;

        var ordered = candidates
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(c => ParametersOf(c).Length)
            .ThenBy(c => c.ToDisplayString(MemberFormat), StringComparer.Ordinal)
            .ToList();

        var active = ChooseActive(ordered, resolved, site, model, cancellationToken);
        var items = ordered
            .Select(c => model.Language == LanguageNames.VisualBasic
                ? CreateVisualBasicItem(c, model, caret, argumentIndex, argumentName, site.NameComparison, cancellationToken)
                : CreateItem(c, model, caret, argumentIndex, argumentName, cancellationToken))
            .ToList();

        return new SignatureHelpResult(items, active, argumentIndex, argumentName, site.SpanStart, site.SpanLength);
    }

    /// <summary>Finds the innermost argument list that contains the caret.</summary>
    private static CallSite? FindCallSite(SyntaxNode root, int position)
    {
        // The token just left of the caret: for "Greet(|" at the end of a file it is the parenthesis, not the end of file.
        var token = root.FindToken(position > 0 ? position - 1 : 0);
        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case ArgumentListSyntax { Parent: { } owner } list
                    when owner is InvocationExpressionSyntax or BaseObjectCreationExpressionSyntax or ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax
                    && IsInside(list.OpenParenToken, list.CloseParenToken, position):
                    return CallSite.Create(owner, list.OpenParenToken, list.CloseParenToken, list.Arguments.GetSeparators(), list.Arguments.Select(ToArgument));

                case BracketedArgumentListSyntax { Parent: ElementAccessExpressionSyntax owner } list
                    when IsInside(list.OpenBracketToken, list.CloseBracketToken, position):
                    return CallSite.Create(owner, list.OpenBracketToken, list.CloseBracketToken, list.Arguments.GetSeparators(), list.Arguments.Select(ToArgument));

                case AttributeArgumentListSyntax { Parent: AttributeSyntax owner } list
                    when IsInside(list.OpenParenToken, list.CloseParenToken, position):
                    return CallSite.Create(owner, list.OpenParenToken, list.CloseParenToken, list.Arguments.GetSeparators(), list.Arguments.Select(ToArgument));

                case StatementSyntax or MemberDeclarationSyntax:
                    // Signature help belongs to the call being typed, not to a call around a lambda body.
                    return null;
            }
        }

        return null;
    }

    private static bool IsInside(SyntaxToken open, SyntaxToken close, int position) =>
        position > open.SpanStart && (close.IsMissing || position <= close.SpanStart);

    private static CallArgument ToArgument(ArgumentSyntax argument) =>
        new(argument.NameColon?.Name.Identifier.ValueText, argument.Expression);

    private static CallArgument ToArgument(AttributeArgumentSyntax argument) =>
        new(argument.NameColon?.Name.Identifier.ValueText ?? argument.NameEquals?.Name.Identifier.ValueText, argument.Expression);

    /// <summary>The overloads that can be called at a call site, and the one the compiler picked, if any.</summary>
    private static (List<ISymbol> Candidates, ISymbol? Resolved) GetCandidates(SemanticModel model, SyntaxNode owner, int position, CancellationToken cancellationToken)
    {
        switch (owner)
        {
            case InvocationExpressionSyntax invocation:
            {
                var info = model.GetSymbolInfo(invocation, cancellationToken);
                var group = model.GetMemberGroup(invocation.Expression, cancellationToken).OfType<IMethodSymbol>().ToList<ISymbol>();
                if (group.Count == 0)
                {
                    if (model.GetTypeInfo(invocation.Expression, cancellationToken).Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke })
                    {
                        group.Add(invoke);
                    }
                    else if (info.Symbol is { } symbol)
                    {
                        group.Add(symbol);
                    }
                    else
                    {
                        group.AddRange(info.CandidateSymbols);
                    }
                }

                return (group, info.Symbol);
            }

            case BaseObjectCreationExpressionSyntax creation:
            {
                var info = model.GetSymbolInfo(creation, cancellationToken);
                var type = model.GetTypeInfo(creation, cancellationToken).Type as INamedTypeSymbol ?? ContainingTypeOf(info);
                return (Constructors(type, model, position, exclude: null), info.Symbol);
            }

            case ConstructorInitializerSyntax initializer:
            {
                var info = model.GetSymbolInfo(initializer, cancellationToken);
                var enclosing = model.GetEnclosingSymbol(position, cancellationToken) as IMethodSymbol;
                var type = ContainingTypeOf(info)
                    ?? (initializer.IsKind(SyntaxKind.BaseConstructorInitializer) ? enclosing?.ContainingType?.BaseType : enclosing?.ContainingType);
                return (Constructors(type, model, position, exclude: enclosing), info.Symbol);
            }

            case PrimaryConstructorBaseTypeSyntax baseType:
            {
                var info = model.GetSymbolInfo(baseType, cancellationToken);
                var type = model.GetTypeInfo(baseType.Type, cancellationToken).Type as INamedTypeSymbol ?? ContainingTypeOf(info);
                return (Constructors(type, model, position, exclude: null), info.Symbol);
            }

            case ElementAccessExpressionSyntax access:
            {
                var info = model.GetSymbolInfo(access, cancellationToken);
                var group = model.GetIndexerGroup(access.Expression, cancellationToken).ToList<ISymbol>();
                return (group, info.Symbol);
            }

            case AttributeSyntax attribute:
            {
                var info = model.GetSymbolInfo(attribute, cancellationToken);
                var type = ContainingTypeOf(info) ?? model.GetTypeInfo(attribute, cancellationToken).Type as INamedTypeSymbol;
                return (Constructors(type, model, position, exclude: null), info.Symbol);
            }

            default:
                return owner.Language == LanguageNames.VisualBasic
                    ? GetVisualBasicCandidates(model, owner, position, cancellationToken)
                    : ([], null);
        }
    }

    private static INamedTypeSymbol? ContainingTypeOf(SymbolInfo info) =>
        (info.Symbol ?? info.CandidateSymbols.FirstOrDefault())?.ContainingType;

    private static List<ISymbol> Constructors(INamedTypeSymbol? type, SemanticModel model, int position, IMethodSymbol? exclude) =>
        type is null
            ? []
            : type.InstanceConstructors
                .Where(c => model.IsAccessible(position, c) && !SymbolEqualityComparer.Default.Equals(c, exclude))
                .ToList<ISymbol>();

    private static ImmutableArray<IParameterSymbol> ParametersOf(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.Parameters,
        IPropertySymbol property => property.Parameters,
        _ => [],
    };

    /// <summary>The symbol two overload lists agree on, whether or not they are constructed or reduced.</summary>
    private static ISymbol DefinitionOf(ISymbol symbol) =>
        symbol is IMethodSymbol { ReducedFrom: { } reducedFrom } ? reducedFrom.OriginalDefinition : symbol.OriginalDefinition;

    /// <summary>Picks the overload to show first: the one the compiler resolved, else the best match for the arguments.</summary>
    private static int ChooseActive(List<ISymbol> overloads, ISymbol? resolved, CallSite site, SemanticModel model, CancellationToken cancellationToken)
    {
        if (resolved is not null)
        {
            var definition = DefinitionOf(resolved);
            var index = overloads.FindIndex(o => SymbolEqualityComparer.Default.Equals(DefinitionOf(o), definition));
            if (index >= 0)
            {
                return index;
            }
        }

        var best = 0;
        var bestScore = int.MinValue;
        for (var i = 0; i < overloads.Count; i++)
        {
            var score = Score(ParametersOf(overloads[i]), site, model, cancellationToken);
            if (score > bestScore)
            {
                best = i;
                bestScore = score;
            }
        }

        return best;
    }

    /// <summary>How well the arguments typed so far fit an overload. Higher is better.</summary>
    private static int Score(ImmutableArray<IParameterSymbol> parameters, CallSite site, SemanticModel model, CancellationToken cancellationToken)
    {
        const int NotApplicable = -1000;
        var isVariadic = parameters.Length > 0 && parameters[^1].IsParams;
        if (site.Arguments.Count > parameters.Length && !isVariadic)
        {
            return NotApplicable;
        }

        var score = 0;
        for (var i = 0; i < site.Arguments.Count; i++)
        {
            var argument = site.Arguments[i];
            var parameter = argument.Name is { } name
                ? parameters.FirstOrDefault(p => string.Equals(p.Name, name, site.NameComparison))
                : i < parameters.Length ? parameters[i] : isVariadic ? parameters[^1] : null;
            if (parameter is null)
            {
                return NotApplicable;
            }

            if (argument.Expression is not { IsMissing: false } expression
                || model.GetTypeInfo(expression, cancellationToken).Type is null or { TypeKind: TypeKind.Error })
            {
                continue;
            }

            var target = parameter.IsParams && parameter.Type is IArrayTypeSymbol array && !Fits(model, expression, parameter.Type)
                ? array.ElementType
                : parameter.Type;
            score += Fits(model, expression, target) ? 1 : -10;
        }

        return score;
    }

    private static bool Fits(SemanticModel model, SyntaxNode expression, ITypeSymbol type) =>
        expression is ExpressionSyntax csharp ? model.ClassifyConversion(csharp, type).IsImplicit : FitsVisualBasic(model, expression, type);

    private static SignatureItem CreateItem(ISymbol symbol, SemanticModel model, int position, int argumentIndex, string? argumentName, CancellationToken cancellationToken)
    {
        var parameters = ParametersOf(symbol);
        var documentation = DocumentationComment.For(symbol, cancellationToken);
        var prefix = new List<TaggedRun>();
        var close = ")";

        switch (symbol)
        {
            case IMethodSymbol { MethodKind: MethodKind.Constructor } constructor:
                prefix.AddRange(Display(constructor.ContainingType, model, position, MemberFormat));
                break;
            case IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } invoke:
                prefix.AddRange(Display(invoke.ReturnType, model, position, MemberFormat));
                prefix.Add(new TaggedRun(" ", TaggedTextKind.Space));
                prefix.AddRange(Display(invoke.ContainingType, model, position, MemberFormat));
                break;
            case IPropertySymbol indexer:
                prefix.AddRange(Display(indexer.Type, model, position, MemberFormat));
                prefix.Add(new TaggedRun(" ", TaggedTextKind.Space));
                prefix.AddRange(Display(indexer.ContainingType, model, position, MemberFormat));
                close = "]";
                break;
            default:
                prefix.AddRange(Display(symbol, model, position, MemberFormat));
                break;
        }

        prefix.Add(new TaggedRun(close == "]" ? "[" : "(", TaggedTextKind.Punctuation));

        var parameterItems = parameters
            .Select(p => new SignatureParameter(
                p.Name,
                Display(p, model, position, ParameterFormat),
                documentation.ParameterText(p.Name),
                p.IsOptional || p.IsParams))
            .ToList();

        return new SignatureItem(
            prefix,
            parameterItems,
            [new TaggedRun(close, TaggedTextKind.Punctuation)],
            documentation.Summary,
            ActiveParameterOf(parameters, argumentIndex, argumentName, StringComparison.Ordinal),
            parameters.Length > 0 && parameters[^1].IsParams);
    }

    private static List<TaggedRun> Display(ISymbol symbol, SemanticModel model, int position, SymbolDisplayFormat format) =>
        TaggedTextMapper.Map(symbol.ToMinimalDisplayParts(model, position, format));

    private static int ActiveParameterOf(ImmutableArray<IParameterSymbol> parameters, int argumentIndex, string? argumentName, StringComparison nameComparison)
    {
        if (argumentName is not null)
        {
            for (var i = 0; i < parameters.Length; i++)
            {
                if (string.Equals(parameters[i].Name, argumentName, nameComparison))
                {
                    return i;
                }
            }

            return -1;
        }

        if (argumentIndex < parameters.Length)
        {
            return argumentIndex;
        }

        return parameters.Length > 0 && parameters[^1].IsParams ? parameters.Length - 1 : -1;
    }

    /// <summary>An argument as typed so far.</summary>
    /// <param name="Name">Its name, for a named argument.</param>
    /// <param name="Expression">
    /// Its expression, which is missing while the argument is still empty, or <see langword="null"/> for an argument
    /// Visual Basic leaves out, such as the first in <c>Format(, 2)</c>.
    /// </param>
    private sealed record CallArgument(string? Name, SyntaxNode? Expression);

    /// <summary>An argument list and the invocation, creation, indexer access or attribute it belongs to.</summary>
    private sealed record CallSite(SyntaxNode Owner, List<CallArgument> Arguments, List<SyntaxToken> Separators, int SpanStart, int SpanLength)
    {
        /// <summary>How argument names match parameter names: exactly in C#, ignoring case in Visual Basic.</summary>
        public StringComparison NameComparison { get; init; } = StringComparison.Ordinal;

        public static CallSite Create(SyntaxNode owner, SyntaxToken open, SyntaxToken close, IEnumerable<SyntaxToken> separators, IEnumerable<CallArgument> arguments)
        {
            var start = open.Span.End;
            var end = close.IsMissing ? Math.Max(start, owner.Span.End) : close.SpanStart;
            return new CallSite(owner, [.. arguments], [.. separators], start, end - start);
        }
    }
}
