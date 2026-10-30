using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.Compilation.Features;

// Signature help in Visual Basic files. It lives in its own file because the C# and Visual Basic syntax APIs share
// type and extension method names (ArgumentListSyntax, InvocationExpressionSyntax, ClassifyConversion).
public sealed partial class SignatureHelpFeature
{
    /// <summary>
    /// A Visual Basic member without its parameters or type: <c>Greeter.Greet</c>. Visual Basic writes the type
    /// after the parameters, so <see cref="CreateVisualBasicItem"/> adds it to the suffix.
    /// </summary>
    private static readonly SymbolDisplayFormat VisualBasicMemberFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypes,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeRef | SymbolDisplayMemberOptions.IncludeContainingType,
        miscellaneousOptions: MiscellaneousOptions);

    /// <summary>
    /// Finds the innermost argument list around the caret in Visual Basic: of a call, a <c>New</c> expression or an
    /// attribute. A call to a default property, such as <c>items(0)</c>, is a call too.
    /// </summary>
    private static CallSite? FindVisualBasicCallSite(SyntaxNode root, int position)
    {
        // The token just left of the caret: for "Greet(|" at the end of a line it is the parenthesis.
        var token = root.FindToken(position > 0 ? position - 1 : 0);
        for (var node = token.Parent; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case ArgumentListSyntax { Parent: { } owner } list
                    when owner is InvocationExpressionSyntax or ObjectCreationExpressionSyntax or AttributeSyntax
                    && IsInside(list.OpenParenToken, list.CloseParenToken, position):
                {
                    var site = CallSite.Create(owner, list.OpenParenToken, list.CloseParenToken, list.Arguments.GetSeparators(), list.Arguments.Select(ToVisualBasicArgument));
                    return site with { NameComparison = StringComparison.OrdinalIgnoreCase };
                }

                case StatementSyntax:
                    // Statements include lambda headers: help belongs to the call being typed, not to one around a lambda.
                    return null;
            }
        }

        return null;
    }

    private static CallArgument ToVisualBasicArgument(ArgumentSyntax argument) => argument switch
    {
        SimpleArgumentSyntax simple => new(simple.NameColonEquals?.Name.Identifier.ValueText, simple.Expression),
        RangeArgumentSyntax range => new(null, range.UpperBound),
        _ => new(null, null),
    };

    /// <summary>The overloads that can be called at a Visual Basic call site, and the one the compiler picked, if any.</summary>
    private static (List<ISymbol> Candidates, ISymbol? Resolved) GetVisualBasicCandidates(SemanticModel model, SyntaxNode owner, int position, CancellationToken cancellationToken)
    {
        switch (owner)
        {
            case InvocationExpressionSyntax invocation:
            {
                var info = model.GetSymbolInfo(invocation, cancellationToken);
                var group = invocation.Expression is { } expression
                    ? model.GetMemberGroup(expression, cancellationToken).Where(s => s is IMethodSymbol or IPropertySymbol).ToList()
                    : [];
                if (group.Count == 0)
                {
                    if (invocation.Expression is { } callee
                        && model.GetTypeInfo(callee, cancellationToken).Type is INamedTypeSymbol { DelegateInvokeMethod: { } invoke })
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

            case ObjectCreationExpressionSyntax creation:
            {
                var info = model.GetSymbolInfo(creation, cancellationToken);
                var type = model.GetTypeInfo(creation, cancellationToken).Type as INamedTypeSymbol ?? ContainingTypeOf(info);
                return (Constructors(type, model, position, exclude: null), info.Symbol);
            }

            case AttributeSyntax attribute:
            {
                var info = model.GetSymbolInfo(attribute, cancellationToken);
                var type = ContainingTypeOf(info) ?? model.GetTypeInfo(attribute, cancellationToken).Type as INamedTypeSymbol;
                return (Constructors(type, model, position, exclude: null), info.Symbol);
            }

            default:
                return ([], null);
        }
    }

    /// <summary>
    /// Whether an argument converts to a parameter's type without narrowing, which Visual Basic would allow only with
    /// <c>Option Strict Off</c>.
    /// </summary>
    private static bool FitsVisualBasic(SemanticModel model, SyntaxNode expression, ITypeSymbol type)
    {
        if (expression is not ExpressionSyntax basic)
        {
            return false;
        }

        var conversion = model.ClassifyConversion(basic, type);
        return conversion.IsIdentity || conversion.IsWidening;
    }

    /// <summary>
    /// One overload as Visual Basic writes it: <c>Greeter.Greet(name As String) As String</c>,
    /// <c>New Point(x As Integer, y As Integer)</c> or <c>Console.WriteLine(format As String, ParamArray arg As Object())</c>.
    /// </summary>
    private static SignatureItem CreateVisualBasicItem(
        ISymbol symbol,
        SemanticModel model,
        int position,
        int argumentIndex,
        string? argumentName,
        StringComparison nameComparison,
        CancellationToken cancellationToken)
    {
        var parameters = ParametersOf(symbol);
        var documentation = DocumentationComment.For(symbol, cancellationToken);
        var prefix = new List<TaggedRun>();
        ITypeSymbol? type = null;

        switch (symbol)
        {
            case IMethodSymbol { MethodKind: MethodKind.Constructor } constructor:
                prefix.Add(new TaggedRun("New", TaggedTextKind.Keyword));
                prefix.Add(new TaggedRun(" ", TaggedTextKind.Space));
                prefix.AddRange(Display(constructor.ContainingType, model, position, VisualBasicMemberFormat));
                break;
            case IMethodSymbol { MethodKind: MethodKind.DelegateInvoke } invoke:
                prefix.AddRange(Display(invoke.ContainingType, model, position, VisualBasicMemberFormat));
                type = invoke.ReturnsVoid ? null : invoke.ReturnType;
                break;
            case IMethodSymbol method:
                prefix.AddRange(Display(method, model, position, VisualBasicMemberFormat));
                type = method.ReturnsVoid ? null : method.ReturnType;
                break;
            case IPropertySymbol property:
                prefix.AddRange(Display(property, model, position, VisualBasicMemberFormat));
                type = property.Type;
                break;
            default:
                prefix.AddRange(Display(symbol, model, position, VisualBasicMemberFormat));
                break;
        }

        prefix.Add(new TaggedRun("(", TaggedTextKind.Punctuation));

        var suffix = new List<TaggedRun> { new(")", TaggedTextKind.Punctuation) };
        if (type is not null)
        {
            suffix.Add(new TaggedRun(" ", TaggedTextKind.Space));
            suffix.Add(new TaggedRun("As", TaggedTextKind.Keyword));
            suffix.Add(new TaggedRun(" ", TaggedTextKind.Space));
            suffix.AddRange(Display(type, model, position, VisualBasicMemberFormat));
        }

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
            suffix,
            documentation.Summary,
            ActiveParameterOf(parameters, argumentIndex, argumentName, nameComparison),
            parameters.Length > 0 && parameters[^1].IsParams);
    }
}
