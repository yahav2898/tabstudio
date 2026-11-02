using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// A form's designer file, such as <c>Form1.Designer.cs</c> or <c>Form1.Designer.vb</c>, read with Roslyn: the class,
/// its InitializeComponent and the fields of its components, turned into a <see cref="DesignDocument"/>. Nothing in it
/// is compiled or run.
/// </summary>
/// <remarks>
/// InitializeComponent is read statement by statement, as Visual Studio's designer reads it: creations
/// (<c>button1 = new Button();</c>), properties (<c>button1.Text = "OK";</c>), events (<c>button1.Click += button1_Click;</c>)
/// and collection adds (<c>Controls.Add(button1);</c>). The layout calls Visual Studio writes are left for the writer
/// to write again; any other statement is kept as it is, with the component it is about. A Visual Basic file is read
/// by <see cref="VisualBasicDesignerFile"/>.
/// </remarks>
internal sealed class DesignerFile
{
    /// <summary>Creates a read designer file.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="className">The designed class's name.</param>
    /// <param name="ns">The designed class's namespace, or an empty string.</param>
    /// <param name="document">The form the file designs.</param>
    /// <param name="body">What the writer writes InitializeComponent's statements over.</param>
    /// <param name="componentFields">The field declarations of the components, which the writer writes again.</param>
    /// <param name="fieldsInsertionPoint">Where the fields go when the file has none.</param>
    internal DesignerFile(string text, string className, string ns, DesignDocument document, TextSpan body, IReadOnlyList<TextSpan> componentFields, int fieldsInsertionPoint)
    {
        Text = text;
        ClassName = className;
        Namespace = ns;
        Document = document;
        Body = body;
        ComponentFields = componentFields;
        FieldsInsertionPoint = fieldsInsertionPoint;
    }

    /// <summary>The file's text.</summary>
    public string Text { get; }

    /// <summary>The designed class's name.</summary>
    public string ClassName { get; }

    /// <summary>The designed class's namespace, or an empty string.</summary>
    public string Namespace { get; }

    /// <summary>The form the file designs.</summary>
    public DesignDocument Document { get; }

    /// <summary>The file's language.</summary>
    public DesignerLanguage Language => Document.Style.Language;

    /// <summary>
    /// What the writer writes InitializeComponent's statements over: in C#, the method's body from its brace to its
    /// brace; in Visual Basic, the whole lines between <c>Private Sub InitializeComponent()</c> and <c>End Sub</c>.
    /// </summary>
    internal TextSpan Body { get; }

    /// <summary>The field declarations of the components, which the writer writes again.</summary>
    internal IReadOnlyList<TextSpan> ComponentFields { get; }

    /// <summary>
    /// Where the components' fields go when the file has none: in C#, after the <c>#endregion</c> that follows
    /// InitializeComponent or before the class's closing brace; in Visual Basic, after InitializeComponent's <c>End Sub</c> line.
    /// </summary>
    internal int FieldsInsertionPoint { get; }

    /// <summary>The indentation of the class's members, such as InitializeComponent's.</summary>
    internal string MemberIndent { get; init; } = string.Empty;

    /// <summary>Whether InitializeComponent's statements end with a blank line, as older Visual Basic designer files have them.</summary>
    internal bool BodyEndsWithBlankLine { get; init; }

    /// <summary>
    /// The comment lines around a component's name in its section, as the file writes them: Visual Basic's <c>' </c>,
    /// or the <c>'</c> of older files.
    /// </summary>
    internal string SectionComment { get; init; } = "// ";

    /// <summary>The language of a designer or code file, by its extension: Visual Basic for <c>.vb</c>, otherwise C#.</summary>
    /// <param name="path">The file's path.</param>
    public static DesignerLanguage LanguageOf(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        return path.EndsWith(".vb", StringComparison.OrdinalIgnoreCase) ? DesignerLanguage.VisualBasic : DesignerLanguage.CSharp;
    }

    /// <summary>Reads a designer file.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="rootTypeName">The designed class's base type, such as <c>Form</c> or <c>UserControl</c>, from its code file.</param>
    /// <param name="language">The file's language.</param>
    /// <returns>The file, or <see langword="null"/> when it has no class with an InitializeComponent method.</returns>
    public static DesignerFile? Parse(string text, string rootTypeName = "Form", DesignerLanguage language = DesignerLanguage.CSharp)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (language == DesignerLanguage.VisualBasic)
        {
            return VisualBasicDesignerFile.Parse(text, rootTypeName);
        }

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
        var componentFields = new List<TextSpan>();
        foreach (var component in document.Components)
        {
            component.Type = TypeResolver.Resolve(component.TypeName);
            // "private Button a, b;" declares two components: the writer writes a field for each, so the shared
            // declaration goes, or both were declared twice. One that also declares another field stays as it is.
            if (fieldNames.Find(f => f.Name == component.Name) is { Field: { } field }
                && field.Declaration.Variables.All(v => document.Find(v.Identifier.ValueText) is not null))
            {
                component.FieldModifiers = field.Modifiers.ToString();
                if (!componentFields.Contains(field.Span))
                {
                    componentFields.Add(field.Span);
                }
            }
        }

        var ns = string.Join('.', type.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(n => n.Name.ToString()));
        return new DesignerFile(text, type.Identifier.ValueText, ns, document, method.Body!.Span, componentFields, FindFieldsInsertionPoint(text, type, method))
        {
            MemberIndent = TextLines.Indentation(method.GetLeadingTrivia().ToString()),
        };
    }

    /// <summary>Where new fields go: after the <c>#endregion</c> that follows InitializeComponent, or before the class's closing brace.</summary>
    private static int FindFieldsInsertionPoint(string text, ClassDeclarationSyntax type, MethodDeclarationSyntax method)
    {
        var region = type.DescendantTrivia().FirstOrDefault(t => t.IsKind(SyntaxKind.EndRegionDirectiveTrivia) && t.SpanStart > method.Span.End);
        if (region != default)
        {
            var end = text.IndexOf('\n', region.Span.End);
            return end < 0 ? text.Length : end + 1;
        }

        return TextLines.LineStart(text, type.CloseBraceToken.SpanStart);
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
        var newLine = TextLines.NewLineOf(text);
        var methodIndent = TextLines.Indentation(method.GetLeadingTrivia().ToString());
        var indent = statements.Count > 0 ? TextLines.Indentation(statements[0].GetLeadingTrivia().ToString()) : methodIndent + "    ";
        if (indent.Length <= methodIndent.Length)
        {
            indent = methodIndent + "    ";
        }

        return new CodeStyle(qualified, fullNames, indent, newLine);
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

            // A field the designer did not create with new (a user field, or one a factory method returns) is not a
            // component: its statements are kept as written, not stored on a component nothing writes back.
            if (!Known(component))
            {
                Keep(statement, null);
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

            if (handler is null || Target(subscription.Left) is not (var component, var path) || !Known(component) || path.Length == 0 || path.Contains('.', StringComparison.Ordinal))
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
                && Target(receiver) is (var component, var path) && Known(component) && path.Length > 0)
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
        private bool Known(DesignComponent component) => component.IsRoot || Document.Components.Contains(component);

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
