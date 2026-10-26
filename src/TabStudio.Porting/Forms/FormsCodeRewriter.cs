using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Porting.CSharp;
using TabStudio.Porting.Output;

namespace TabStudio.Porting.Forms;

/// <summary>Rewrites Forms user code against the controls created by the converted designer.</summary>
internal sealed class FormsCodeRewriter(PortContext context, CodeFile file, Dictionary<string, FormsClassInfo> classes, HashSet<string> declaredTypes) : CSharpSyntaxRewriter
{
    private FormsClassInfo? _class;

    public override SyntaxNode? VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previous = _class;
        var ns = node.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString();
        _class = classes.GetValueOrDefault(ns is null ? node.Identifier.ValueText : ns + "." + node.Identifier.ValueText);
        var result = base.VisitClassDeclaration(node);
        _class = previous;
        return result;
    }

    public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
    {
        var name = node.Identifier.ValueText;
        if (!declaredTypes.Contains(name) && SyntaxContext.IsTypeOrStatic(node))
        {
            var mapped = name switch
            {
                "Form" => "AppForm",
                "DataGridViewRow" => "GridRow",
                "DataGridViewCell" => "GridCell",
                "DataGridView" => "DataGrid",
                "Label" => "TextBlock",
                "CheckBox" or "RadioButton" or "Button" or "TextBox" or "ComboBox" or "ListBox" or "UserControl" => name,
                _ => null,
            };
            if (mapped is not null && mapped != name)
            {
                file.Report.Converted(name + " → " + mapped);
                return SyntaxFactory.IdentifierName(mapped).WithTriviaFrom(node);
            }
        }

        return base.VisitIdentifierName(node);
    }

    public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
    {
        if (node.Left.ToString() == "System.Windows.Forms")
        {
            return VisitIdentifierName(node.Right is IdentifierNameSyntax id ? id : SyntaxFactory.IdentifierName(node.Right.Identifier))!.WithTriviaFrom(node);
        }

        return base.VisitQualifiedName(node);
    }

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var result = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node)!;
        if (_class?.HandlerArguments.TryGetValue(node.Identifier.ValueText, out var args) is true && result.ParameterList.Parameters.Count >= 2)
        {
            var parameter = result.ParameterList.Parameters[1];
            result = result.ReplaceNode(parameter, parameter.WithType(SyntaxFactory.ParseTypeName(args).WithTriviaFrom(parameter.Type!)));
        }

        if (_class?.AsyncClosingHandlers.Contains(node.Identifier.ValueText) is true)
        {
            result = result.WithReturnType(SyntaxFactory.ParseTypeName("System.Threading.Tasks.Task").WithTriviaFrom(result.ReturnType));
            if (!result.Modifiers.Any(SyntaxKind.AsyncKeyword))
            {
                result = result.AddModifiers(SyntaxFactory.Token(SyntaxKind.AsyncKeyword).WithTrailingTrivia(SyntaxFactory.Space));
            }
        }

        return result;
    }

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        var result = (ConstructorDeclarationSyntax)base.VisitConstructorDeclaration(node)!;
        if (_class is null)
        {
            return result;
        }

        var body = result.Body;
        if (body is null && result.ExpressionBody is { } expression)
        {
            body = SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(expression.Expression));
            result = result.WithExpressionBody(null).WithSemicolonToken(default);
        }

        if (body is not null)
        {
            var statements = body.Statements.SelectMany(statement =>
                statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }
                && invocation.Expression.ToString() is "InitializeComponent" or "this.InitializeComponent"
                    ? new[] { statement, SyntaxFactory.ParseStatement("InitializePortedEvents();").WithTriviaFrom(statement) }
                    : new[] { statement });
            result = result.WithBody(body.WithStatements(SyntaxFactory.List(statements)));
        }

        return result;
    }

    public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
    {
        var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node)!;
        var owner = node.Expression.ToString().Replace("this.", string.Empty, StringComparison.Ordinal);
        var member = node.Name.Identifier.ValueText;
        var control = _class?.Components.GetValueOrDefault(owner);
        if (member == "Rows" && _class?.GridRows.TryGetValue(owner, out var rows) is true)
        {
            file.Report.Converted("DataGridView.Rows → observable GridRows");
            return SyntaxFactory.IdentifierName(rows).WithTriviaFrom(node);
        }

        if (member == "SelectedRows" && control?.Forms == "DataGridView")
        {
            file.Report.Converted("DataGridView.SelectedRows → snapshot of selected GridRows");
            return SyntaxFactory.ParseExpression(owner + ".SelectedItems.Cast<GridRow>().ToList()").WithTriviaFrom(node);
        }

        var mapped = member switch
        {
            "Enabled" when control is not null => "IsEnabled",
            "Visible" when control is not null => "IsVisible",
            "Checked" when control is not null => "IsChecked",
            "Text" when control?.Avalonia is "Button" or "CheckBox" or "RadioButton" => "Content",
            "Text" when control?.Avalonia is "MenuItem" or "TabItem" => "Header",
            "DataSource" when control is not null => "ItemsSource",
            _ => null,
        };
        if (mapped is not null)
        {
            file.Report.Converted(member + " → " + mapped);
            var access = visited.WithName(SyntaxFactory.IdentifierName(mapped).WithTriviaFrom(visited.Name));
            if (member == "Checked" && !(node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node))
            {
                return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.EqualsExpression, access.WithoutTrivia(), SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))).WithTriviaFrom(node);
            }

            return access;
        }

        if (member == "Value" && control?.Forms == "NumericUpDown" && node.Parent is CastExpressionSyntax)
        {
            return SyntaxFactory.ParenthesizedExpression(SyntaxFactory.BinaryExpression(SyntaxKind.CoalesceExpression, visited.WithoutTrivia(), SyntaxFactory.LiteralExpression(SyntaxKind.NumericLiteralExpression, SyntaxFactory.Literal(0)))).WithTriviaFrom(node);
        }

        return visited;
    }

    public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
        if (node.Expression is MemberAccessExpressionSyntax access)
        {
            var owner = access.Expression.ToString();
            var method = access.Name.Identifier.ValueText;
            if (owner is "MessageBox" or "System.Windows.Forms.MessageBox" && method == "Show")
            {
                context.Needs(SupportNeeds.FormsMessageBox);
                file.Report.Converted("MessageBox.Show → await MessageBox.ShowAsync");
                return AsyncCalls.Await(visited.WithExpression(SyntaxFactory.ParseExpression("MessageBox.ShowAsync").WithTriviaFrom(visited.Expression)));
            }

            if (method == "AddRange" && access.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Items" })
            {
                file.Report.Converted("Items.AddRange → observable item collection helper");
                return SyntaxFactory.InvocationExpression(SyntaxFactory.ParseExpression("FormsItems.AddRange").WithTriviaFrom(visited.Expression),
                    visited.ArgumentList.WithArguments(visited.ArgumentList.Arguments.Insert(0, SyntaxFactory.Argument(visited.Expression is MemberAccessExpressionSyntax call ? call.Expression.WithoutTrivia() : access.Expression))));
            }

            if (owner == "Application" && method == "Exit")
            {
                return visited.WithExpression(SyntaxFactory.ParseExpression("AppForm.Exit"));
            }

            if (method == "ShowDialog" && node.ArgumentList.Arguments.Count == 0)
            {
                return AsyncCalls.Await(visited.WithExpression(((MemberAccessExpressionSyntax)visited.Expression).WithName(SyntaxFactory.IdentifierName("ShowDialogAsync"))));
            }
        }

        return visited;
    }
}
