using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using TabStudio.Forms.Design.Model;

namespace TabStudio.Forms.Design.Code;

/// <summary>
/// Reads a Visual Basic designer file, such as <c>Form1.Designer.vb</c>, as Visual Studio writes it: the
/// <c>Partial Class</c> that <c>Inherits System.Windows.Forms.Form</c>, its <c>Private Sub InitializeComponent()</c>,
/// and the <c>Friend WithEvents Button1 As Button</c> fields after it.
/// </summary>
/// <remarks>
/// <para>
/// InitializeComponent is read statement by statement, as for C#: creations (<c>Button1 = New Button()</c>),
/// properties (<c>Me.Button1.Text = "OK"</c>), collection adds (<c>Controls.Add(Button1)</c>) and the events an
/// <c>AddHandler Button1.Click, AddressOf Button1_Click</c> wires. Events are usually wired by the handlers'
/// <c>Handles</c> clauses in the code file instead, which <see cref="VisualBasicCodeBehind"/> reads.
/// </para>
/// <para>Visual Basic ignores case, so the fields are found whatever the case the statements name them in.</para>
/// </remarks>
internal static class VisualBasicDesignerFile
{
    /// <summary>The modifiers of a component's field as Visual Studio declares it.</summary>
    public const string FieldModifiers = "Friend WithEvents";

    /// <summary>The layout and initialization calls the writer writes again around the components that need them.</summary>
    private static readonly HashSet<string> LayoutCalls = new(StringComparer.OrdinalIgnoreCase)
    {
        "SuspendLayout", "ResumeLayout", "PerformLayout", "BeginInit", "EndInit",
    };

    /// <summary>Reads a Visual Basic designer file.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="rootTypeName">
    /// The designed class's base type from its code file, such as <c>Form</c>; the designer file's own
    /// <c>Inherits</c>, where Visual Studio writes it, wins.
    /// </param>
    /// <returns>The file, or <see langword="null"/> when it has no class with an InitializeComponent method.</returns>
    public static DesignerFile? Parse(string text, string rootTypeName)
    {
        ArgumentNullException.ThrowIfNull(text);
        var root = VisualBasicSyntaxTree.ParseText(text).GetRoot();
        var method = root.DescendantNodes().OfType<MethodBlockSyntax>().FirstOrDefault(m =>
            m.IsKind(SyntaxKind.SubBlock)
            && string.Equals(m.SubOrFunctionStatement.Identifier.ValueText, "InitializeComponent", StringComparison.OrdinalIgnoreCase)
            && (m.SubOrFunctionStatement.ParameterList?.Parameters.Count ?? 0) == 0);
        if (method?.Parent is not ClassBlockSyntax type)
        {
            return null;
        }

        var bodyStart = TextLines.LineEnd(text, method.SubOrFunctionStatement.Span.End);
        var bodyEnd = TextLines.LineStart(text, method.EndSubOrFunctionStatement.SpanStart);
        if (bodyEnd < bodyStart)
        {
            return null;
        }

        var inherits = type.Inherits.SelectMany(i => i.Types).Select(t => t.ToString()).FirstOrDefault();
        if (inherits is not null)
        {
            rootTypeName = inherits.StartsWith("Global.", StringComparison.OrdinalIgnoreCase) ? inherits["Global.".Length..] : inherits;
        }

        var fieldNames = type.Members.OfType<FieldDeclarationSyntax>()
            .SelectMany(f => f.Declarators.SelectMany(d => d.Names).Select(n => (Field: f, Name: n.Identifier.ValueText)))
            .ToList();
        var statements = method.Statements;
        var memberIndent = TextLines.Indentation(method.GetLeadingTrivia().ToString());
        var style = DetectStyle(text, memberIndent, statements);
        var className = type.ClassStatement.Identifier.ValueText;
        var reader = new Reader(
            new DesignDocument(new DesignComponent(className, rootTypeName, isRoot: true), style),
            fieldNames.Select(f => f.Name).ToHashSet(StringComparer.OrdinalIgnoreCase));
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
            var index = fieldNames.FindIndex(f => string.Equals(f.Name, component.Name, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && fieldNames[index].Field is { } field && field.Declarators.Count == 1 && field.Declarators[0].Names.Count == 1)
            {
                component.FieldModifiers = field.Modifiers.ToString();
                component.FieldIndex = index;
                componentFields.Add(field.Span);
            }
        }

        var tailStart = statements.Count > 0 ? TextLines.LineEnd(text, statements[^1].Span.End) : bodyStart;
        var tail = tailStart <= bodyEnd ? text[tailStart..bodyEnd] : string.Empty;
        var ns = string.Join('.', type.Ancestors().OfType<NamespaceBlockSyntax>().Reverse().Select(n => n.NamespaceStatement.Name.ToString()));
        return new DesignerFile(
            text,
            className,
            ns,
            document,
            new TextSpan(bodyStart, bodyEnd - bodyStart),
            componentFields,
            TextLines.LineEnd(text, method.EndSubOrFunctionStatement.Span.End))
        {
            MemberIndent = memberIndent,
            BodyEndsWithBlankLine = tail.Contains('\n', StringComparison.Ordinal) && string.IsNullOrWhiteSpace(tail),
            SectionComment = SectionCommentOf(text[bodyStart..bodyEnd]),
        };
    }

    /// <summary>
    /// How the file writes code: <c>Me.Button1</c> or <c>Button1</c>; <c>System.Drawing.Point</c> or <c>Point</c>, as
    /// older Visual Studio writes every type, the template's <c>New System.Drawing.Size(800, 450)</c> included.
    /// </summary>
    private static CodeStyle DetectStyle(string text, string memberIndent, SyntaxList<StatementSyntax> statements)
    {
        var qualified = statements.Any(s => s.DescendantNodes().OfType<MemberAccessExpressionSyntax>().Any(m => m.Expression is MeExpressionSyntax));
        var fullNames = statements.Any(s => s.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().Any(c => IsQualified(c.Type.ToString())));
        var indent = statements.Count > 0 ? TextLines.Indentation(statements[0].GetLeadingTrivia().ToString()) : memberIndent + "    ";
        if (indent.Length <= memberIndent.Length)
        {
            indent = memberIndent + "    ";
        }

        return new CodeStyle(qualified, fullNames, indent, TextLines.NewLineOf(text)) { Language = DesignerLanguage.VisualBasic };

        static bool IsQualified(string typeName) =>
            typeName.StartsWith("System.Windows.Forms.", StringComparison.Ordinal)
            || typeName.StartsWith("System.Drawing.", StringComparison.Ordinal)
            || typeName.StartsWith("Global.System.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The comment lines around a section's name, as the file writes them: <c>' </c> as Visual Studio writes them now,
    /// or <c>'</c> as it used to.
    /// </summary>
    private static string SectionCommentOf(string body)
    {
        foreach (var line in body.Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n'))
        {
            var comment = line.TrimStart();
            if (comment.StartsWith('\'') && string.IsNullOrWhiteSpace(comment[1..]))
            {
                return comment;
            }
        }

        return "' ";
    }

    /// <summary>Reads InitializeComponent's statements into a document.</summary>
    private sealed class Reader(DesignDocument document, HashSet<string> fields)
    {
        public DesignDocument Document { get; } = document;

        public void Read(StatementSyntax statement)
        {
            switch (statement)
            {
                case AssignmentStatementSyntax assignment when assignment.IsKind(SyntaxKind.SimpleAssignmentStatement):
                    ReadAssignment(statement, assignment);
                    break;
                case AddRemoveHandlerStatementSyntax handler when handler.IsKind(SyntaxKind.AddHandlerStatement):
                    ReadEvent(statement, handler);
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

        private static bool Is(string? name, string expected) => string.Equals(name, expected, StringComparison.OrdinalIgnoreCase);

        private static string? MethodName(ExpressionSyntax expression) => expression switch
        {
            IdentifierNameSyntax name => name.Identifier.ValueText,
            MemberAccessExpressionSyntax { Expression: MeExpressionSyntax or MyClassExpressionSyntax, Name: var name } => name.Identifier.ValueText,
            _ => null,
        };

        /// <summary>The handler <c>AddressOf Button1_Click</c> or <c>New EventHandler(AddressOf Me.Button1_Click)</c> names.</summary>
        private static string? HandlerOf(ExpressionSyntax expression) => expression switch
        {
            UnaryExpressionSyntax { Operand: var operand } addressOf when addressOf.IsKind(SyntaxKind.AddressOfExpression) => MethodName(operand),
            ObjectCreationExpressionSyntax { ArgumentList.Arguments: [SimpleArgumentSyntax { Expression: var argument }] } => HandlerOf(argument),
            _ => null,
        };

        private void ReadAssignment(StatementSyntax statement, AssignmentStatementSyntax assignment)
        {
            if (Target(assignment.Left) is not (var component, var path))
            {
                Keep(statement, null);
                return;
            }

            if (path.Length == 0 && assignment.Right is ObjectCreationExpressionSyntax creation)
            {
                if (Is(component.Name, "components") && creation.Type.ToString().EndsWith("Container", StringComparison.Ordinal))
                {
                    Document.HasContainer = true;
                    Document.ContainerStatement = statement.ToString();
                    return;
                }

                component.TypeName = creation.Type.ToString();

                // As written, Me.components included, which the writer writes back as it was.
                component.ConstructorArguments = string.Join(", ", creation.ArgumentList?.Arguments.Select(a => a.ToString()) ?? []);
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

        private void ReadEvent(StatementSyntax statement, AddRemoveHandlerStatementSyntax handler)
        {
            var method = HandlerOf(handler.DelegateExpression);
            if (method is null || Target(handler.EventExpression) is not (var component, var path) || path.Length == 0 || path.Contains('.', StringComparison.Ordinal))
            {
                Keep(statement, null);
                return;
            }

            component.Events.Add(new DesignEvent(path, method));
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
            if (method is not null && LayoutCalls.Contains(method))
            {
                return;
            }

            if ((Is(method, "Add") || Is(method, "AddRange")) && invocation.Expression is MemberAccessExpressionSyntax { Expression: { } receiver }
                && Target(receiver) is (var component, var path) && path.Length > 0)
            {
                var arguments = invocation.ArgumentList?.Arguments ?? default;
                if (Is(method, "AddRange") && arguments is [SimpleArgumentSyntax { Expression: ArrayCreationExpressionSyntax array }])
                {
                    var range = component.Collection(path);
                    range.IsRange = true;
                    range.RangeType = array.Type.ToString();
                    range.Items.AddRange(array.Initializer?.Initializers.Select(Simplify) ?? []);
                    return;
                }

                if (Is(method, "Add") && arguments.Count > 0)
                {
                    component.Collection(path).Items.Add(string.Join(", ", arguments.Select(Argument)));
                    return;
                }
            }

            Keep(statement, null);
        }

        /// <summary>
        /// Keeps a statement the designer does not read with the component it is about, or the form. An extender's
        /// call, such as <c>ToolTip1.SetToolTip(Button1, "...")</c>, is about the control it extends, whose section
        /// Visual Studio writes it in.
        /// </summary>
        private void Keep(StatementSyntax statement, DesignComponent? about)
        {
            if (about is null
                && statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: var method }, ArgumentList.Arguments: [SimpleArgumentSyntax first, ..] } }
                && method.StartsWith("Set", StringComparison.OrdinalIgnoreCase) && Target(first.Expression) is (var extended, "") && !extended.IsRoot && Document.Components.Contains(extended))
            {
                about = extended;
            }

            var component = about ?? statement.DescendantNodes().OfType<ExpressionSyntax>()
                .Select(e => e switch
                {
                    MemberAccessExpressionSyntax { Expression: MeExpressionSyntax, Name: var name } when fields.Contains(name.Identifier.ValueText) => Document.Find(name.Identifier.ValueText),
                    IdentifierNameSyntax name when fields.Contains(name.Identifier.ValueText) => Document.Find(name.Identifier.ValueText),
                    _ => null,
                })
                .FirstOrDefault(c => c is not null);
            (component ?? Document.Root).OtherStatements.Add(statement.ToString());
        }

        /// <summary>
        /// The component an expression starts with, and the property path after it: <c>Me.Button1.Location</c> is
        /// Button1's Location, <c>Text</c> and <c>Me.Text</c> are the form's Text, and <c>Button1</c> is Button1 itself.
        /// </summary>
        private (DesignComponent Component, string Path)? Target(ExpressionSyntax expression)
        {
            var parts = new List<string>();
            var current = expression;
            while (current is MemberAccessExpressionSyntax { Expression: { } inner } member)
            {
                parts.Insert(0, member.Name.Identifier.ValueText);
                current = inner;
            }

            switch (current)
            {
                case MeExpressionSyntax or MyBaseExpressionSyntax or MyClassExpressionSyntax:
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
                var component = Document.Find(name) ?? new DesignComponent(name, string.Empty) { FieldModifiers = FieldModifiers };
                return (component, string.Join('.', parts.Skip(1)));
            }

            return parts.Count == 0 ? null : (Document.Root, string.Join('.', parts));
        }

        /// <summary>An argument as the document keeps it: a component as its name, as <c>Button1</c> for <c>Me.Button1</c>.</summary>
        private string Argument(ArgumentSyntax argument) =>
            argument is SimpleArgumentSyntax { NameColonEquals: null, Expression: var expression } ? Simplify(expression) : argument.ToString();

        /// <summary>A component's reference as its name, such as <c>Button1</c> for <c>Me.Button1</c>; any other expression as written.</summary>
        private string Simplify(ExpressionSyntax expression) => expression switch
        {
            MemberAccessExpressionSyntax { Expression: MeExpressionSyntax, Name: var name } when fields.Contains(name.Identifier.ValueText) => name.Identifier.ValueText,
            MeExpressionSyntax => "Me",
            _ => expression.ToString(),
        };
    }
}
