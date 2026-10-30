using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.Compilation.Features;

// The declarations of a Visual Basic file. It lives in its own file because the C# and Visual Basic syntax APIs
// share type names (CompilationUnitSyntax, ParameterListSyntax, SyntaxKind).
public sealed partial class DocumentSymbolFeature
{
    private static List<DocumentSymbol> CollectVisualBasic(SyntaxList<StatementSyntax> members, SyntaxNode? container)
    {
        var symbols = new List<DocumentSymbol>();
        foreach (var member in members)
        {
            AddVisualBasic(member, container, symbols);
        }

        return symbols;
    }

    private static void AddVisualBasic(StatementSyntax member, SyntaxNode? container, List<DocumentSymbol> symbols)
    {
        switch (member)
        {
            case NamespaceBlockSyntax ns:
            {
                var nameSyntax = ns.NamespaceStatement.Name;
                var name = nameSyntax.ToString();
                symbols.Add(Create(name, name, SymbolGlyph.Namespace, SymbolAccessibility.NotApplicable, ns, nameSyntax.Span, CollectVisualBasic(ns.Members, ns)));
                break;
            }

            case TypeBlockSyntax type:
            {
                // Class, Module, Structure and Interface. A module is a class whose members are all shared.
                var statement = type.BlockStatement;
                var kind = type switch
                {
                    InterfaceBlockSyntax => SymbolGlyph.Interface,
                    StructureBlockSyntax => SymbolGlyph.Struct,
                    _ => SymbolGlyph.Class,
                };
                var name = statement.Identifier.ValueText;
                var accessibility = VisualBasicAccessibilityOf(statement.Modifiers, container, isField: false);
                var display = name + VisualBasicTypeParameters(statement.TypeParameterList);
                symbols.Add(Create(name, display, kind, accessibility, type, statement.Identifier.Span, CollectVisualBasic(type.Members, type)));
                break;
            }

            case EnumBlockSyntax enumBlock:
            {
                var statement = enumBlock.EnumStatement;
                var children = enumBlock.Members
                    .OfType<EnumMemberDeclarationSyntax>()
                    .Select(m => Create(m.Identifier.ValueText, m.Identifier.ValueText, SymbolGlyph.EnumMember, SymbolAccessibility.NotApplicable, m, m.Identifier.Span, []))
                    .ToList();
                var name = statement.Identifier.ValueText;
                var accessibility = VisualBasicAccessibilityOf(statement.Modifiers, container, isField: false);
                symbols.Add(Create(name, name, SymbolGlyph.Enum, accessibility, enumBlock, statement.Identifier.Span, children));
                break;
            }

            case DelegateStatementSyntax type:
            {
                var name = type.Identifier.ValueText;
                var display = name + VisualBasicTypeParameters(type.TypeParameterList) + VisualBasicParameters(type.ParameterList);
                var accessibility = VisualBasicAccessibilityOf(type.Modifiers, container, isField: false);
                symbols.Add(Create(name, display, SymbolGlyph.Delegate, accessibility, type, type.Identifier.Span, []));
                break;
            }

            case MethodBlockSyntax method:
                AddVisualBasicMethod(method.SubOrFunctionStatement, method, container, symbols);
                break;

            case MethodStatementSyntax method:
                // A method without a body: an interface member, a MustOverride method or a partial method.
                AddVisualBasicMethod(method, method, container, symbols);
                break;

            case ConstructorBlockSyntax constructor:
            {
                var statement = constructor.SubNewStatement;
                var accessibility = VisualBasicAccessibilityOf(statement.Modifiers, container, isField: false);
                symbols.Add(Create("New", "New" + VisualBasicParameters(statement.ParameterList), SymbolGlyph.Method, accessibility, constructor, statement.NewKeyword.Span, []));
                break;
            }

            case OperatorBlockSyntax op:
            {
                var statement = op.OperatorStatement;
                var name = "Operator " + statement.OperatorToken.Text;
                var accessibility = VisualBasicAccessibilityOf(statement.Modifiers, container, isField: false);
                symbols.Add(Create(name, name + VisualBasicParameters(statement.ParameterList), SymbolGlyph.Method, accessibility, op, statement.OperatorToken.Span, []));
                break;
            }

            case DeclareStatementSyntax declare:
            {
                var name = declare.Identifier.ValueText;
                var accessibility = VisualBasicAccessibilityOf(declare.Modifiers, container, isField: false);
                symbols.Add(Create(name, name + VisualBasicParameters(declare.ParameterList), SymbolGlyph.Method, accessibility, declare, declare.Identifier.Span, []));
                break;
            }

            case PropertyBlockSyntax property:
                AddVisualBasicProperty(property.PropertyStatement, property, container, symbols);
                break;

            case PropertyStatementSyntax property:
                AddVisualBasicProperty(property, property, container, symbols);
                break;

            case EventBlockSyntax e:
                AddVisualBasicEvent(e.EventStatement, e, container, symbols);
                break;

            case EventStatementSyntax e:
                AddVisualBasicEvent(e, e, container, symbols);
                break;

            case FieldDeclarationSyntax field:
            {
                var kind = field.Modifiers.Any(SyntaxKind.ConstKeyword) ? SymbolGlyph.Constant : SymbolGlyph.Field;
                var accessibility = VisualBasicAccessibilityOf(field.Modifiers, container, isField: true);
                var names = field.Declarators.SelectMany(d => d.Names).ToList();
                foreach (var identifier in names)
                {
                    var name = identifier.Identifier.ValueText;
                    SyntaxNode node = names.Count == 1 ? field : identifier;
                    symbols.Add(Create(name, name, kind, accessibility, node, identifier.Identifier.Span, []));
                }

                break;
            }
        }
    }

    private static void AddVisualBasicMethod(MethodStatementSyntax statement, SyntaxNode node, SyntaxNode? container, List<DocumentSymbol> symbols)
    {
        var name = statement.Identifier.ValueText;
        var display = name + VisualBasicTypeParameters(statement.TypeParameterList) + VisualBasicParameters(statement.ParameterList);
        var kind = statement.AttributeLists.SelectMany(l => l.Attributes).Any(a => IsExtensionAttribute(a.Name.ToString()))
            ? SymbolGlyph.ExtensionMethod
            : SymbolGlyph.Method;
        var accessibility = VisualBasicAccessibilityOf(statement.Modifiers, container, isField: false);
        symbols.Add(Create(name, display, kind, accessibility, node, statement.Identifier.Span, []));
    }

    private static void AddVisualBasicProperty(PropertyStatementSyntax statement, SyntaxNode node, SyntaxNode? container, List<DocumentSymbol> symbols)
    {
        var name = statement.Identifier.ValueText;
        var display = statement.ParameterList is { Parameters.Count: > 0 } parameters ? name + VisualBasicParameters(parameters) : name;
        var accessibility = VisualBasicAccessibilityOf(statement.Modifiers, container, isField: false);
        symbols.Add(Create(name, display, SymbolGlyph.Property, accessibility, node, statement.Identifier.Span, []));
    }

    private static void AddVisualBasicEvent(EventStatementSyntax statement, SyntaxNode node, SyntaxNode? container, List<DocumentSymbol> symbols)
    {
        var name = statement.Identifier.ValueText;
        var accessibility = VisualBasicAccessibilityOf(statement.Modifiers, container, isField: false);
        symbols.Add(Create(name, name, SymbolGlyph.Event, accessibility, node, statement.Identifier.Span, []));
    }

    /// <summary>Whether an attribute name is <c>Extension</c>, which makes a module's method an extension method.</summary>
    private static bool IsExtensionAttribute(string name)
    {
        var simpleName = name[(name.LastIndexOf('.') + 1)..];
        return string.Equals(simpleName, "Extension", StringComparison.OrdinalIgnoreCase)
            || string.Equals(simpleName, "ExtensionAttribute", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Type parameters as Visual Basic writes them: <c>(Of TKey, TValue)</c>.</summary>
    private static string VisualBasicTypeParameters(TypeParameterListSyntax? list) =>
        list is null || list.Parameters.Count == 0
            ? string.Empty
            : "(Of " + string.Join(", ", list.Parameters.Select(p => p.Identifier.ValueText)) + ")";

    /// <summary>A parameter list as the navigation bar shows it: the parameter types, such as <c>(ByRef Integer, String)</c>.</summary>
    private static string VisualBasicParameters(ParameterListSyntax? list) =>
        "(" + (list is null ? string.Empty : string.Join(", ", list.Parameters.Select(VisualBasicParameterText))) + ")";

    /// <summary>
    /// A parameter as the navigation bar shows it: its modifiers but <c>ByVal</c>, and its type, such as
    /// <c>ByRef Integer</c> or <c>ParamArray Object()</c>. A parameter without <c>As</c> shows its name.
    /// </summary>
    private static string VisualBasicParameterText(ParameterSyntax parameter)
    {
        var identifier = parameter.Identifier;
        var type = parameter.AsClause?.Type?.ToString() ?? identifier.Identifier.ValueText;
        if (identifier.Nullable.IsKind(SyntaxKind.QuestionToken))
        {
            type += "?";
        }

        type += string.Concat(identifier.ArrayRankSpecifiers.Select(r => r.ToString()));
        var modifiers = parameter.Modifiers.Where(m => !m.IsKind(SyntaxKind.ByValKeyword)).Select(m => m.Text).ToList();
        return modifiers.Count == 0 ? type : string.Join(' ', modifiers) + " " + type;
    }

    /// <summary>
    /// The declared accessibility, or the one Visual Basic gives a declaration without modifiers: <c>Friend</c> for a
    /// type in a namespace, <c>Private</c> for a field of a class or module, and <c>Public</c> for anything else.
    /// </summary>
    private static SymbolAccessibility VisualBasicAccessibilityOf(SyntaxTokenList modifiers, SyntaxNode? container, bool isField)
    {
        var isPublic = modifiers.Any(SyntaxKind.PublicKeyword);
        var isFriend = modifiers.Any(SyntaxKind.FriendKeyword);
        var isProtected = modifiers.Any(SyntaxKind.ProtectedKeyword);
        var isPrivate = modifiers.Any(SyntaxKind.PrivateKeyword);

        return (isPublic, isFriend, isProtected, isPrivate) switch
        {
            (true, _, _, _) => SymbolAccessibility.Public,
            (_, true, true, _) => SymbolAccessibility.ProtectedInternal,
            (_, _, true, true) => SymbolAccessibility.PrivateProtected,
            (_, true, _, _) => SymbolAccessibility.Internal,
            (_, _, true, _) => SymbolAccessibility.Protected,
            (_, _, _, true) => SymbolAccessibility.Private,
            _ => container switch
            {
                null or NamespaceBlockSyntax => SymbolAccessibility.Internal,
                InterfaceBlockSyntax or StructureBlockSyntax => SymbolAccessibility.Public,
                _ => isField ? SymbolAccessibility.Private : SymbolAccessibility.Public,
            },
        };
    }
}
