using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// A form's designer file, such as <c>Form1.Designer.cs</c>, read with Roslyn: the class, its InitializeComponent and
/// the fields of its components, turned into a <see cref="DesignDocument"/>. Nothing in it is compiled or run.
/// </summary>
/// <remarks>
/// InitializeComponent is read statement by statement, as Visual Studio's designer reads it: creations
/// (<c>button1 = new Button();</c>), properties (<c>button1.Text = "OK";</c>), events (<c>button1.Click += button1_Click;</c>)
/// and collection adds (<c>Controls.Add(button1);</c>). The layout calls Visual Studio writes are left for the writer
/// to write again; any other statement is kept as it is, with the component it is about.
/// </remarks>
internal sealed class DesignerFile
{
    private DesignerFile(string text, ClassDeclarationSyntax type, MethodDeclarationSyntax method, DesignDocument document, IReadOnlyList<FieldDeclarationSyntax> fields)
    {
        Text = text;
        Class = type;
        InitializeComponent = method;
        Document = document;
        ComponentFields = fields;
    }

    /// <summary>The file's text.</summary>
    public string Text { get; }

    /// <summary>The designed class's declaration.</summary>
    public ClassDeclarationSyntax Class { get; }

    /// <summary>The InitializeComponent method.</summary>
    public MethodDeclarationSyntax InitializeComponent { get; }

    /// <summary>The form the file designs.</summary>
    public DesignDocument Document { get; }

    /// <summary>The field declarations of the components, which the writer writes again.</summary>
    public IReadOnlyList<FieldDeclarationSyntax> ComponentFields { get; }

    /// <summary>The designed class's name.</summary>
    public string ClassName => Class.Identifier.ValueText;

    /// <summary>The designed class's namespace, or an empty string.</summary>
    public string Namespace => string.Join('.', Class.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));

    /// <summary>Reads a designer file.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="rootTypeName">The designed class's base type, such as <c>Form</c> or <c>UserControl</c>, from its code file.</param>
    /// <returns>The file, or <see langword="null"/> when it has no class with an InitializeComponent method.</returns>
    public static DesignerFile? Parse(string text, string rootTypeName = "Form")
    {
        ArgumentNullException.ThrowIfNull(text);
        var root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
        var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.ValueText == "InitializeComponent" && m.ParameterList.Parameters.Count == 0 && m.Body is not null);
        if (method?.Parent is not ClassDeclarationSyntax type)
        {
            return null;
        }

        var fieldNames = type.Members.OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declaration.Variables.Select(v => (Field: f, Name: v.Identifier.ValueText)))
            .ToList();
        var statements = method.Body!.Statements;
        var style = DetectStyle(text, method, statements);
        var reader = new Reader(new DesignDocument(new DesignComponent(type.Identifier.ValueText, rootTypeName, isRoot: true), style), fieldNames.Select(f => f.Name).ToHashSet(StringComparer.Ordinal));
        foreach (var statement in statements)
        {
            reader.Read(statement);
        }

        var document = reader.Document;
        document.Root.Type = TypeResolver.Resolve(rootTypeName) ?? typeof(System.Windows.Forms.Form);
        var componentFields = new List<FieldDeclarationSyntax>();
        foreach (var component in document.Components)
        {
            component.Type = TypeResolver.Resolve(component.TypeName);
            if (fieldNames.Find(f => f.Name == component.Name) is { Field: { } field } && field.Declaration.Variables.Count == 1)
            {
                component.FieldModifiers = field.Modifiers.ToString();
                componentFields.Add(field);
            }
        }

        return new DesignerFile(text, type, method, document, componentFields);
    }

    private static CodeStyle DetectStyle(string text, MethodDeclarationSyntax method, SyntaxList<StatementSyntax> statements)
    {
        var qualified = statements.Any(s => s.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(m => m.Expression is ThisExpressionSyntax));
        var fullNames = statements.OfType<ExpressionStatementSyntax>()
            .Select(s => s.Expression)
            .OfType<AssignmentExpressionSyntax>()
            .Select(a => a.Right)
            .OfType<ObjectCreationExpressionSyntax>()
            .Any(c => c.Type.ToString().StartsWith("System.Windows.Forms.", StringComparison.Ordinal));
        var newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var methodIndent = Indentation(method.GetLeadingTrivia().ToString());
        var indent = statements.Count > 0 ? Indentation(statements[0].GetLeadingTrivia().ToString()) : methodIndent + "    ";
        if (indent.Length <= methodIndent.Length)
        {
            indent = methodIndent + "    ";
        }

        return new CodeStyle(qualified, fullNames, indent, newLine);
    }

    /// <summary>The whitespace before the last line's text.</summary>
    private static string Indentation(string leadingTrivia)
    {
        var lastLine = leadingTrivia.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n')[^1];
        return new string([.. lastLine.TakeWhile(char.IsWhiteSpace)]);
    }

    /// <summary>Reads InitializeComponent's statements into a document.</summary>
    private sealed class Reader(DesignDocument document, HashSet<string> fields)
    {
        public DesignDocument Document { get; } = document;

        public void Read(StatementSyntax statement)
        {
            switch (statement)
            {
                case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax assignment } when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
                    ReadAssignment(statement, assignment);
                    break;
                case ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax subscription } when subscription.IsKind(SyntaxKind.AddAssignmentExpression):
                    ReadEvent(statement, subscription);
                    break;
                case ExpressionStatementSyntax { Expression: InvocationExpressionSyntax invocation }:
                    ReadInvocation(statement, invocation);
                    break;
                case LocalDeclarationStatementSyntax when Document.Components.Count == 0:
                    Document.LeadingStatements.Add(statement.ToString());
                    break;
                default:
                    Keep(statement, null);
                    break;
            }
        }

        private void ReadAssignment(StatementSyntax statement, AssignmentExpressionSyntax assignment)
        {
            if (Target(assignment.Left) is not (var component, var path))
            {
                Keep(statement, null);
                return;
            }

            if (path.Length == 0 && assignment.Right is ObjectCreationExpressionSyntax creation)
            {
                if (component.Name == "components" && creation.Type.ToString().EndsWith("Container", StringComparison.Ordinal))
                {
                    Document.HasContainer = true;
                    return;
                }

                component.TypeName = creation.Type.ToString();
                component.ConstructorArguments = string.Join(", ", creation.ArgumentList?.Arguments.Select(a => Simplify(a.Expression)) ?? []);
                if (!Document.Components.Contains(component))
                {
                    Document.Components.Add(component);
                }

                return;
            }

            if (path.Length == 0)
            {
                Keep(statement, component);
                return;
            }

            component.SetCode(path, assignment.Right.ToString());
        }

        private void ReadEvent(StatementSyntax statement, AssignmentExpressionSyntax subscription)
        {
            var handler = subscription.Right switch
            {
                IdentifierNameSyntax name => name.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var name } => name.Identifier.ValueText,
                ObjectCreationExpressionSyntax { ArgumentList.Arguments: [{ Expression: var argument }] } => argument switch
                {
                    IdentifierNameSyntax name => name.Identifier.ValueText,
                    MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var name } => name.Identifier.ValueText,
                    _ => null,
                },
                _ => null,
            };

            if (handler is null || Target(subscription.Left) is not (var component, var path) || path.Length == 0 || path.Contains('.', StringComparison.Ordinal))
            {
                Keep(statement, null);
                return;
            }

            component.Events.Add(new DesignEvent(path, handler));
        }

        private void ReadInvocation(StatementSyntax statement, InvocationExpressionSyntax invocation)
        {
            var method = invocation.Expression switch
            {
                MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
                IdentifierNameSyntax name => name.Identifier.ValueText,
                _ => null,
            };

            // The layout and initialization calls are written again around the components that need them.
            if (method is "SuspendLayout" or "ResumeLayout" or "PerformLayout" or "BeginInit" or "EndInit")
            {
                return;
            }

            if (method is "Add" or "AddRange" && invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiver }
                && Target(receiver) is (var component, var path) && path.Length > 0)
            {
                var arguments = invocation.ArgumentList.Arguments;
                if (method == "AddRange" && arguments is [{ Expression: ArrayCreationExpressionSyntax array }])
                {
                    var range = component.Collection(path);
                    range.IsRange = true;
                    range.RangeType = array.Type.ElementType.ToString();
                    range.Items.AddRange(array.Initializer?.Expressions.Select(Simplify) ?? []);
                    return;
                }

                if (method == "Add" && arguments.Count > 0)
                {
                    component.Collection(path).Items.Add(string.Join(", ", arguments.Select(a => Simplify(a.Expression))));
                    return;
                }
            }

            Keep(statement, null);
        }

        /// <summary>
        /// Keeps a statement the designer does not read with the component it is about, or the form. An extender's
        /// call, such as <c>toolTip1.SetToolTip(button1, "...")</c>, is about the control it extends, whose section
        /// Visual Studio writes it in.
        /// </summary>
        private void Keep(StatementSyntax statement, DesignComponent? about)
        {
            if (about is null && statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var method }, ArgumentList.Arguments: [var first, ..] } }
                && method.StartsWith("Set", StringComparison.Ordinal) && Target(first.Expression) is (var extended, "") && !extended.IsRoot && Document.Components.Contains(extended))
            {
                about = extended;
            }

            var component = about ?? statement.DescendantNodes().OfType<ExpressionSyntax>()
                .Select(e => e switch
                {
                    MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var name } when fields.Contains(name.Identifier.ValueText) => Document.Find(name.Identifier.ValueText),
                    IdentifierNameSyntax name when fields.Contains(name.Identifier.ValueText) => Document.Find(name.Identifier.ValueText),
                    _ => null,
                })
                .FirstOrDefault(c => c is not null);
            (component ?? Document.Root).OtherStatements.Add(statement.ToString());
        }

        /// <summary>
        /// The component an expression starts with, and the property path after it: <c>this.button1.Location</c>
        /// is button1's Location, <c>Text</c> and <c>this.Text</c> are the form's Text, and <c>button1</c> is button1 itself.
        /// </summary>
        private (DesignComponent Component, string Path)? Target(ExpressionSyntax expression)
        {
            var parts = new List<string>();
            var current = expression;
            while (current is MemberAccessExpressionSyntax member)
            {
                parts.Insert(0, member.Name.Identifier.ValueText);
                current = member.Expression;
            }

            switch (current)
            {
                case ThisExpressionSyntax:
                    break;
                case IdentifierNameSyntax identifier:
                    parts.Insert(0, identifier.Identifier.ValueText);
                    break;
                default:
                    return null;
            }

            if (parts.Count > 0 && fields.Contains(parts[0]))
            {
                var name = parts[0];
                var component = Document.Find(name) ?? new DesignComponent(name, string.Empty);
                return (component, string.Join('.', parts.Skip(1)));
            }

            return parts.Count == 0 ? null : (Document.Root, string.Join('.', parts));
        }

        /// <summary>A component's reference as its name, such as <c>button1</c> for <c>this.button1</c>; any other expression as written.</summary>
        private string Simplify(ExpressionSyntax expression) => expression switch
        {
            MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax, Name: var name } when fields.Contains(name.Identifier.ValueText) => name.Identifier.ValueText,
            ThisExpressionSyntax => "this",
            _ => expression.ToString(),
        };
    }
}
