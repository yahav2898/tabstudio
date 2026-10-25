using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.CSharp;

namespace TabStudio.Porting.Forms;

/// <summary>
/// Reads a form's InitializeComponent, as the Windows Forms designer writes it (with or without <c>this.</c> and
/// full type names): which components it creates, the properties it sets, how it nests controls, and the events it
/// connects.
/// </summary>
internal static class DesignerReader
{
    /// <summary>Statements that only manage layout passes and initialization, which Avalonia needs none of.</summary>
    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
    {
        "SuspendLayout", "ResumeLayout", "PerformLayout", "BeginInit", "EndInit",
    };

    /// <summary>Reads the designer file of a form.</summary>
    /// <param name="text">The file, such as Form1.Designer.cs.</param>
    /// <returns>The form, or <see langword="null"/> when the file has no InitializeComponent.</returns>
    public static FormsDesign? Read(string text)
    {
        var root = CSharpSyntaxTree.ParseText(text, CodeFile.Options).GetCompilationUnitRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent" && m.Body is not null);
        if (method?.Parent is not ClassDeclarationSyntax type)
        {
            return null;
        }

        var ns = type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault();
        var className = ns is null ? type.Identifier.ValueText : $"{ns.Name}.{type.Identifier.ValueText}";
        var form = new FormsComponent("this", "Form");
        var components = new Dictionary<string, FormsComponent>(StringComparer.Ordinal) { ["this"] = form };

        // Fields first: they say the type of components the method creates with object initializers or not at all.
        foreach (var field in type.Members.OfType<FieldDeclarationSyntax>())
        {
            foreach (var variable in field.Declaration.Variables)
            {
                var name = variable.Identifier.ValueText;
                if (name != "components")
                {
                    components[name] = new FormsComponent(name, SimpleName(field.Declaration.Type));
                }
            }
        }

        var unread = new List<StatementSyntax>();
        foreach (var statement in method.Body!.Statements)
        {
            if (!ReadStatement(statement, components))
            {
                unread.Add(statement);
            }
        }

        return new FormsDesign(className, form, components, unread)
        {
            BlockNamespace = ns is NamespaceDeclarationSyntax,
            BaseType = type.BaseList?.Types.FirstOrDefault() is { } baseType ? SimpleName(baseType.Type) : "Form",
        };
    }

    /// <summary>The type's name without its namespace: <c>System.Windows.Forms.Button</c> is <c>Button</c>.</summary>
    public static string SimpleName(TypeSyntax type) => type switch
    {
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        AliasQualifiedNameSyntax alias => alias.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        NullableTypeSyntax nullable => SimpleName(nullable.ElementType),
        _ => type.ToString(),
    };

    /// <summary>The field an assignment's target names: <c>button1</c> or <c>this.button1</c>.</summary>
    private static string? FieldName(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var name } => name.Identifier.ValueText,
        _ => null,
    };

    /// <summary>The component an expression names: <c>button1</c>, <c>this.button1</c> or <c>this</c>.</summary>
    private static FormsComponent? Component(ExpressionSyntax expression, Dictionary<string, FormsComponent> components) => expression switch
    {
        ThisExpressionSyntax => components["this"],
        IdentifierNameSyntax identifier => components.GetValueOrDefault(identifier.Identifier.ValueText),
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: IdentifierNameSyntax name } => components.GetValueOrDefault(name.Identifier.ValueText),
        CastExpressionSyntax cast => Component(cast.Expression, components),
        ParenthesizedExpressionSyntax parenthesized => Component(parenthesized.Expression, components),
        _ => null,
    };

    /// <summary>Splits a target such as <c>this.button1.FlatAppearance.BorderSize</c> into its component and its property path.</summary>
    private static (FormsComponent? Component, string Path) Target(ExpressionSyntax expression, Dictionary<string, FormsComponent> components)
    {
        var names = new List<string>();
        var current = expression;
        while (current is MemberAccessExpressionSyntax access)
        {
            if (Component(current, components) is { } component)
            {
                names.Reverse();
                return (component, string.Join('.', names));
            }

            names.Add(access.Name.Identifier.ValueText);
            current = access.Expression;
        }

        if (Component(current, components) is { } found)
        {
            names.Reverse();
            return (found, string.Join('.', names));
        }

        // An unqualified property of the form: "ClientSize = ...".
        if (current is IdentifierNameSyntax { Identifier.ValueText: var first } && !components.ContainsKey(first))
        {
            names.Add(first);
            names.Reverse();
            return (components["this"], string.Join('.', names));
        }

        return (null, string.Empty);
    }

    private static bool ReadStatement(StatementSyntax statement, Dictionary<string, FormsComponent> components)
    {
        if (statement is not ExpressionStatementSyntax { Expression: var expression })
        {
            return statement is EmptyStatementSyntax;
        }

        switch (expression)
        {
            case AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression, Right: ObjectCreationExpressionSyntax creation } assignment
                when FieldName(assignment.Left) is { } name && (name == "components" || components.ContainsKey(name)):
            {
                if (name == "components")
                {
                    return true;
                }

                var component = components[name];
                component.Type = SimpleName(creation.Type);
                component.Arguments.AddRange(creation.ArgumentList?.Arguments.Select(a => a.Expression) ?? []);
                return true;
            }

            case AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.SimpleAssignmentExpression } assignment:
            {
                var (component, path) = Target(assignment.Left, components);
                if (component is null || path.Length == 0)
                {
                    return false;
                }

                component.Properties[path] = assignment.Right;
                return true;
            }

            case AssignmentExpressionSyntax { RawKind: (int)SyntaxKind.AddAssignmentExpression } subscription:
            {
                var (component, path) = Target(subscription.Left, components);
                if (component is null || path.Length == 0 || path.Contains('.', StringComparison.Ordinal) || Handler(subscription.Right) is not { } handler)
                {
                    return false;
                }

                component.Events.Add(new FormsEvent(path, handler));
                return true;
            }

            case InvocationExpressionSyntax invocation:
                return ReadCall(invocation, components);

            default:
                return false;
        }
    }

    private static bool ReadCall(InvocationExpressionSyntax invocation, Dictionary<string, FormsComponent> components)
    {
        var arguments = invocation.ArgumentList.Arguments.Select(a => a.Expression).ToList();
        if (invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: var bare } && Ignored.Contains(bare))
        {
            return true;
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: var method } call)
        {
            return false;
        }

        if (Ignored.Contains(method))
        {
            return true;
        }

        var (owner, path) = Target(call.Expression, components);
        if (owner is null)
        {
            return false;
        }

        switch (path, method)
        {
            case ("Controls" or "Panel1.Controls" or "TabPages", "Add") when arguments.Count >= 1 && Component(arguments[0], components) is { } child:
                Attach(owner, child, owner.Children);
                if (arguments.Count == 3 && Number(arguments[1]) is { } column && Number(arguments[2]) is { } row)
                {
                    child.Cell = ((int)column, (int)row);
                }

                return true;
            case ("Panel2.Controls", "Add") when arguments.Count == 1 && Component(arguments[0], components) is { } second:
                Attach(owner, second, owner.SecondPanel);
                return true;
            case ("Controls" or "Panel1.Controls" or "Panel2.Controls" or "TabPages", "AddRange") when arguments.Count == 1:
                foreach (var element in Elements(arguments[0]))
                {
                    if (Component(element, components) is { } added)
                    {
                        Attach(owner, added, path == "Panel2.Controls" ? owner.SecondPanel : owner.Children);
                    }
                }

                return true;
            case ("Items" or "DropDownItems", "AddRange") when Elements(arguments.FirstOrDefault()).ToList() is { } elements:
                foreach (var element in elements)
                {
                    if (Component(element, components) is { } item)
                    {
                        Attach(owner, item, owner.Items);
                    }
                    else
                    {
                        owner.ListItems.Add(element);
                    }
                }

                return true;
            case ("Items" or "DropDownItems", "Add") when arguments.Count == 1:
                if (Component(arguments[0], components) is { } single)
                {
                    Attach(owner, single, owner.Items);
                }
                else
                {
                    owner.ListItems.Add(arguments[0]);
                }

                return true;
            case ("Columns", "AddRange"):
                foreach (var element in arguments.SelectMany(Elements))
                {
                    if (Component(element, components) is { } addedColumn)
                    {
                        addedColumn.Parent = owner;
                        owner.Columns.Add(addedColumn);
                    }
                }

                return true;
            case ("Columns", "Add") when arguments.Count == 1 && Component(arguments[0], components) is { } oneColumn:
                oneColumn.Parent = owner;
                owner.Columns.Add(oneColumn);
                return true;
            case ("ColumnStyles" or "RowStyles", "Add") when arguments.Count == 1 && arguments[0] is ObjectCreationExpressionSyntax style:
                var styleArguments = style.ArgumentList?.Arguments.Select(a => a.Expression).ToList() ?? [];
                var sizeType = styleArguments.Count > 0 ? FormsValues.EnumMember(styleArguments[0]) ?? "AutoSize" : "AutoSize";
                var value = styleArguments.Count > 1 ? Number(styleArguments[1]) ?? 0 : 0;
                (path == "ColumnStyles" ? owner.ColumnStyles : owner.RowStyles).Add(new FormsTrack(sizeType, value));
                return true;
            case ("", "SetColumnSpan" or "SetRowSpan") when arguments.Count == 2 && Component(arguments[0], components) is { } spanned && Number(arguments[1]) is { } span:
                if (method == "SetColumnSpan")
                {
                    spanned.ColumnSpan = (int)span;
                }
                else
                {
                    spanned.RowSpan = (int)span;
                }

                return true;
            case ("", "SetToolTip") when arguments.Count == 2 && Component(arguments[0], components) is { } tipped:
                tipped.ToolTip = FormsValues.String(arguments[1]);
                return true;
            default:
                return false;
        }
    }

    private static void Attach(FormsComponent owner, FormsComponent child, List<FormsComponent> list)
    {
        child.Parent = owner;
        if (!list.Contains(child))
        {
            list.Add(child);
        }
    }

    /// <summary>The elements of an array creation, such as <c>new Control[] { a, b }</c> or a collection expression.</summary>
    private static IEnumerable<ExpressionSyntax> Elements(ExpressionSyntax? expression) => expression switch
    {
        ArrayCreationExpressionSyntax { Initializer: { } initializer } => initializer.Expressions,
        ImplicitArrayCreationExpressionSyntax implicitArray => implicitArray.Initializer.Expressions,
        CollectionExpressionSyntax collection => collection.Elements.OfType<ExpressionElementSyntax>().Select(e => e.Expression),
        null => [],
        _ => [expression],
    };

    /// <summary>The method a handler expression names: <c>button1_Click</c>, <c>this.button1_Click</c> or <c>new EventHandler(this.button1_Click)</c>.</summary>
    public static string? Handler(ExpressionSyntax expression) => expression switch
    {
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var name } => name.Identifier.ValueText,
        ObjectCreationExpressionSyntax { ArgumentList.Arguments: [var inner] } => Handler(inner.Expression),
        _ => null,
    };

    private static double? Number(ExpressionSyntax expression) => FormsValues.Number(expression);
}
