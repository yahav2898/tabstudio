using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using VBSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.Compilation.Features;

/// <summary>
/// The namespaces, types and members declared in a file, for the navigation bar above the editor, the breadcrumb
/// and an outline.
/// </summary>
/// <remarks>
/// Works from syntax alone, so it is fast and also works while the file has errors. Serves C# and Visual Basic
/// files; a Visual Basic module shows as a class.
/// </remarks>
public sealed partial class DocumentSymbolFeature
{
    private readonly RoslynWorkspaceHost _host;

    internal DocumentSymbolFeature(RoslynWorkspaceHost host) => _host = host;

    /// <summary>Returns the declarations of a file as a tree.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The top-level declarations in document order, or an empty list when the file is not loaded.</returns>
    public async Task<IReadOnlyList<DocumentSymbol>> GetDocumentSymbolsAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        if (_host.GetDocument(filePath) is not { } document
            || await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false) is not { } root)
        {
            return [];
        }

        return root switch
        {
            CompilationUnitSyntax unit => Collect(unit.Members, container: null),
            VBSyntax.CompilationUnitSyntax unit => CollectVisualBasic(unit.Members, container: null),
            _ => [],
        };
    }

    /// <summary>
    /// Returns the declarations that contain a position, outermost first, such as namespace, type and member.
    /// This is the breadcrumb: <c>Lib › Greeter › Greet(string)</c>.
    /// </summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="position">Zero-based offset of the caret.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The path, which is empty outside every declaration.</returns>
    public async Task<IReadOnlyList<DocumentSymbol>> GetSymbolPathAsync(string filePath, int position, CancellationToken cancellationToken = default)
    {
        var path = new List<DocumentSymbol>();
        var level = await GetDocumentSymbolsAsync(filePath, cancellationToken).ConfigureAwait(false);
        while (level.FirstOrDefault(s => s.Contains(position)) is { } match)
        {
            path.Add(match);
            level = match.Children;
        }

        return path;
    }

    private static List<DocumentSymbol> Collect(SyntaxList<MemberDeclarationSyntax> members, SyntaxNode? container)
    {
        var symbols = new List<DocumentSymbol>();
        foreach (var member in members)
        {
            Add(member, container, symbols);
        }

        return symbols;
    }

    private static void Add(MemberDeclarationSyntax member, SyntaxNode? container, List<DocumentSymbol> symbols)
    {
        var accessibility = AccessibilityOf(member.Modifiers, container);
        switch (member)
        {
            case BaseNamespaceDeclarationSyntax ns:
            {
                var name = ns.Name.ToString();
                symbols.Add(Create(name, name, SymbolGlyph.Namespace, SymbolAccessibility.NotApplicable, ns, ns.Name.Span, Collect(ns.Members, ns)));
                break;
            }

            case EnumDeclarationSyntax type:
            {
                var children = type.Members
                    .Select(m => Create(m.Identifier.ValueText, m.Identifier.ValueText, SymbolGlyph.EnumMember, SymbolAccessibility.NotApplicable, m, m.Identifier.Span, []))
                    .ToList();
                var name = type.Identifier.ValueText;
                symbols.Add(Create(name, name, SymbolGlyph.Enum, accessibility, type, type.Identifier.Span, children));
                break;
            }

            case TypeDeclarationSyntax type:
            {
                var kind = type switch
                {
                    InterfaceDeclarationSyntax => SymbolGlyph.Interface,
                    StructDeclarationSyntax => SymbolGlyph.Struct,
                    RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind(SyntaxKind.StructKeyword) => SymbolGlyph.Struct,
                    _ => SymbolGlyph.Class,
                };
                var name = type.Identifier.ValueText;
                symbols.Add(Create(name, name + TypeParameters(type.TypeParameterList), kind, accessibility, type, type.Identifier.Span, Collect(type.Members, type)));
                break;
            }

            case DelegateDeclarationSyntax type:
            {
                var name = type.Identifier.ValueText;
                var display = name + TypeParameters(type.TypeParameterList) + Parameters(type.ParameterList);
                symbols.Add(Create(name, display, SymbolGlyph.Delegate, accessibility, type, type.Identifier.Span, []));
                break;
            }

            case MethodDeclarationSyntax method:
            {
                var name = method.Identifier.ValueText;
                var isExtension = method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true;
                var display = name + TypeParameters(method.TypeParameterList) + Parameters(method.ParameterList);
                symbols.Add(Create(name, display, isExtension ? SymbolGlyph.ExtensionMethod : SymbolGlyph.Method, accessibility, method, method.Identifier.Span, []));
                break;
            }

            case ConstructorDeclarationSyntax constructor:
            {
                var name = constructor.Identifier.ValueText;
                symbols.Add(Create(name, name + Parameters(constructor.ParameterList), SymbolGlyph.Method, accessibility, constructor, constructor.Identifier.Span, []));
                break;
            }

            case DestructorDeclarationSyntax destructor:
            {
                var name = "~" + destructor.Identifier.ValueText;
                symbols.Add(Create(name, name + "()", SymbolGlyph.Method, SymbolAccessibility.NotApplicable, destructor, destructor.Identifier.Span, []));
                break;
            }

            case OperatorDeclarationSyntax op:
            {
                var name = "operator " + op.OperatorToken.Text;
                symbols.Add(Create(name, name + Parameters(op.ParameterList), SymbolGlyph.Method, accessibility, op, op.OperatorToken.Span, []));
                break;
            }

            case ConversionOperatorDeclarationSyntax conversion:
            {
                var name = $"{conversion.ImplicitOrExplicitKeyword.Text} operator {conversion.Type}";
                symbols.Add(Create(name, name + Parameters(conversion.ParameterList), SymbolGlyph.Method, accessibility, conversion, conversion.Type.Span, []));
                break;
            }

            case PropertyDeclarationSyntax property:
            {
                var name = property.Identifier.ValueText;
                symbols.Add(Create(name, name, SymbolGlyph.Property, accessibility, property, property.Identifier.Span, []));
                break;
            }

            case IndexerDeclarationSyntax indexer:
            {
                var parameters = string.Join(", ", indexer.ParameterList.Parameters.Select(ParameterText));
                symbols.Add(Create("this[]", $"this[{parameters}]", SymbolGlyph.Property, accessibility, indexer, indexer.ThisKeyword.Span, []));
                break;
            }

            case EventDeclarationSyntax e:
            {
                var name = e.Identifier.ValueText;
                symbols.Add(Create(name, name, SymbolGlyph.Event, accessibility, e, e.Identifier.Span, []));
                break;
            }

            case BaseFieldDeclarationSyntax field:
            {
                var kind = field switch
                {
                    EventFieldDeclarationSyntax => SymbolGlyph.Event,
                    _ when field.Modifiers.Any(SyntaxKind.ConstKeyword) => SymbolGlyph.Constant,
                    _ => SymbolGlyph.Field,
                };
                foreach (var variable in field.Declaration.Variables)
                {
                    var name = variable.Identifier.ValueText;
                    SyntaxNode node = field.Declaration.Variables.Count == 1 ? field : variable;
                    symbols.Add(Create(name, name, kind, accessibility, node, variable.Identifier.Span, []));
                }

                break;
            }

            case GlobalStatementSyntax { Statement: LocalFunctionStatementSyntax function }:
            {
                var name = function.Identifier.ValueText;
                var display = name + TypeParameters(function.TypeParameterList) + Parameters(function.ParameterList);
                symbols.Add(Create(name, display, SymbolGlyph.Method, SymbolAccessibility.NotApplicable, function, function.Identifier.Span, []));
                break;
            }
        }
    }

    private static DocumentSymbol Create(
        string name,
        string displayName,
        SymbolGlyph kind,
        SymbolAccessibility accessibility,
        SyntaxNode node,
        TextSpan nameSpan,
        List<DocumentSymbol> children) =>
        new(name, displayName, kind, accessibility, node.SpanStart, node.Span.Length, nameSpan.Start, nameSpan.Length, children);

    private static string TypeParameters(TypeParameterListSyntax? list) =>
        list is null ? string.Empty : "<" + string.Join(", ", list.Parameters.Select(p => p.Identifier.ValueText)) + ">";

    private static string Parameters(ParameterListSyntax list) =>
        "(" + string.Join(", ", list.Parameters.Select(ParameterText)) + ")";

    /// <summary>A parameter as the navigation bar shows it: modifiers and type, such as <c>ref int</c>.</summary>
    private static string ParameterText(ParameterSyntax parameter)
    {
        var type = parameter.Type?.ToString() ?? parameter.Identifier.ValueText;
        return parameter.Modifiers.Count == 0 ? type : parameter.Modifiers.ToString() + " " + type;
    }

    /// <summary>The declared accessibility, or the one C# gives a declaration without modifiers.</summary>
    private static SymbolAccessibility AccessibilityOf(SyntaxTokenList modifiers, SyntaxNode? container)
    {
        var isPublic = modifiers.Any(SyntaxKind.PublicKeyword);
        var isInternal = modifiers.Any(SyntaxKind.InternalKeyword);
        var isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
        var isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);

        return (isPublic, isInternal, isProtected, isPrivate) switch
        {
            (true, _, _, _) => SymbolAccessibility.Public,
            (_, true, true, _) => SymbolAccessibility.ProtectedInternal,
            (_, _, true, true) => SymbolAccessibility.PrivateProtected,
            (_, true, _, _) => SymbolAccessibility.Internal,
            (_, _, true, _) => SymbolAccessibility.Protected,
            (_, _, _, true) => SymbolAccessibility.Private,
            _ => container switch
            {
                InterfaceDeclarationSyntax => SymbolAccessibility.Public,
                TypeDeclarationSyntax => SymbolAccessibility.Private,
                _ => SymbolAccessibility.Internal,
            },
        };
    }
}
