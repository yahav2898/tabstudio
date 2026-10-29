using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>
/// Rewrites one Visual Basic syntax tree so that every statement, and the start of every member body, marks a
/// coverage point as hit before it runs: the Visual Basic twin of <see cref="CoverageRewriter"/>.
/// </summary>
/// <remarks>
/// <para>
/// It keeps the same promise: no line break is ever added, so statements keep their line numbers in the PDB and in
/// the stack traces of failing tests. Hooks are joined to the line they mark with the statement separator, so
/// <c>S1</c> on its line becomes <c>Hit(1) : S1</c>, and a body's entry point goes at the end of the line that opens
/// it: <c>Sub Run() : Hit(e)</c>. Statements of single-line <c>If</c>s, of <c>Else</c> clauses and of multi-line
/// lambdas get hooks too; single-line lambdas are left as they are, their line covered by the statement around them.
/// </para>
/// <para>
/// A constructor's <c>MyBase.New(...)</c>, <c>MyClass.New(...)</c> or <c>Me.New(...)</c> must stay its first
/// statement, so its hook and the constructor's entry go after it, on its line. Labels get no hook; the statement after
/// a label gets one after the label, so a <c>GoTo</c> still records it.
/// </para>
/// <para>
/// Only the bodies of tracked members (see <see cref="VisualBasicMemberKeys"/>) are instrumented, including the
/// lambdas inside them. Field initializers are not.
/// </para>
/// </remarks>
internal sealed class VisualBasicCoverageRewriter : VisualBasicSyntaxRewriter
{
    private readonly SyntaxTree _tree;
    private readonly CoverageModuleBuilder _module;
    private readonly string _rootNamespace;
    private readonly CancellationToken _cancellationToken;
    private readonly MemberKeys.KeyCounter _keys = new();
    private int? _member;

    /// <summary>Creates a rewriter for one tree of a project.</summary>
    /// <param name="tree">The tree to rewrite.</param>
    /// <param name="module">Numbers the project's members and points.</param>
    /// <param name="rootNamespace">The project's root namespace, which starts the type names of its members.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    public VisualBasicCoverageRewriter(SyntaxTree tree, CoverageModuleBuilder module, string rootNamespace, CancellationToken cancellationToken)
    {
        _tree = tree;
        _module = module;
        _rootNamespace = rootNamespace;
        _cancellationToken = cancellationToken;
    }

    // ---- Members with bodies ----

    public override SyntaxNode? VisitMethodBlock(MethodBlockSyntax node) =>
        VisitMethod(node, n => base.VisitMethodBlock(n));

    public override SyntaxNode? VisitConstructorBlock(ConstructorBlockSyntax node) =>
        VisitMethod(node, n => base.VisitConstructorBlock(n));

    public override SyntaxNode? VisitOperatorBlock(OperatorBlockSyntax node) =>
        VisitMethod(node, n => base.VisitOperatorBlock(n));

    public override SyntaxNode? VisitPropertyBlock(PropertyBlockSyntax node) =>
        VisitAccessors(node, n => base.VisitPropertyBlock(n));

    public override SyntaxNode? VisitEventBlock(EventBlockSyntax node) =>
        VisitAccessors(node, n => base.VisitEventBlock(n));

    public override SyntaxNode? VisitAccessorBlock(AccessorBlockSyntax node)
    {
        var updated = (AccessorBlockSyntax)base.VisitAccessorBlock(node)!;
        return _member is { } member ? AddEntry(member, node, updated) : updated;
    }

    // ---- Members without bodies: nothing inside them is instrumented ----

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) => node;

    public override SyntaxNode? VisitPropertyStatement(PropertyStatementSyntax node) => node;

    public override SyntaxNode? VisitEnumBlock(EnumBlockSyntax node) => node;

    public override SyntaxNode? VisitDelegateStatement(DelegateStatementSyntax node) => node;

    // ---- Statement lists ----

    /// <summary>Puts a hook before each statement of a body, on the statement's own line.</summary>
    public override SyntaxList<TNode> VisitList<TNode>(SyntaxList<TNode> list)
    {
        var updated = base.VisitList(list);
        if (_member is not { } member || typeof(TNode) != typeof(StatementSyntax) || list.Count == 0 || !IsBody(list[0].Parent))
        {
            return updated;
        }

        var originals = (SyntaxList<StatementSyntax>)(object)list;
        var statements = (SyntaxList<StatementSyntax>)(object)updated;
        return (SyntaxList<TNode>)(object)SyntaxFactory.List(InstrumentStatements(member, originals, statements));
    }

    // ---- Helpers ----

    /// <summary>Whether a node's statements run as code: a member or lambda body, or a block inside one.</summary>
    private static bool IsBody(SyntaxNode? parent) =>
        parent is MethodBlockBaseSyntax or MultiLineLambdaExpressionSyntax or MultiLineIfBlockSyntax or ElseIfBlockSyntax
            or ElseBlockSyntax or WhileBlockSyntax or DoLoopBlockSyntax or ForOrForEachBlockSyntax or UsingBlockSyntax
            or SyncLockBlockSyntax or WithBlockSyntax or TryBlockSyntax or CatchBlockSyntax or FinallyBlockSyntax
            or CaseBlockSyntax
        || (parent is SingleLineIfStatementSyntax or SingleLineElseClauseSyntax && !InSingleLineLambda(parent));

    /// <summary>Whether a node is part of a single-line lambda, whose one statement cannot take a separator.</summary>
    private static bool InSingleLineLambda(SyntaxNode node) =>
        node.Ancestors().FirstOrDefault(a => a is LambdaExpressionSyntax or MethodBlockBaseSyntax) is SingleLineLambdaExpressionSyntax;

    /// <summary>
    /// Whether a statement calls another constructor, which must stay the first statement of a constructor:
    /// <c>MyBase.New(...)</c>, <c>MyClass.New(...)</c> or <c>Me.New(...)</c>.
    /// </summary>
    private static bool IsConstructorCall(StatementSyntax statement)
    {
        var expression = statement switch
        {
            ExpressionStatementSyntax expressionStatement => expressionStatement.Expression,
            CallStatementSyntax call => call.Invocation,
            _ => null,
        };

        return statement.Parent is ConstructorBlockSyntax
            && expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: InstanceExpressionSyntax } access }
            && string.Equals(access.Name.Identifier.ValueText, "New", StringComparison.OrdinalIgnoreCase);
    }

    private SyntaxNode? VisitMethod<T>(T node, Func<T, SyntaxNode?> visitChildren)
        where T : MethodBlockBaseSyntax
    {
        if (!VisualBasicMemberKeys.IsTracked(node))
        {
            return node;
        }

        var member = AddMember(node);
        var updated = (MethodBlockBaseSyntax)Within(member, () => visitChildren(node))!;
        return AddEntry(member, node, updated);
    }

    private SyntaxNode? VisitAccessors<T>(T node, Func<T, SyntaxNode?> visitChildren)
        where T : StatementSyntax
    {
        if (!VisualBasicMemberKeys.IsTracked(node))
        {
            return node;
        }

        var member = AddMember(node);
        return Within(member, () => visitChildren(node));
    }

    private SyntaxNode? Within(int member, Func<SyntaxNode?> visit)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        var previous = _member;
        _member = member;
        try
        {
            return visit();
        }
        finally
        {
            _member = previous;
        }
    }

    private int AddMember(StatementSyntax member)
    {
        var (typeName, name, key) = VisualBasicMemberKeys.Describe(member, _rootNamespace);
        return _module.AddMember(_tree.FilePath, typeName, name, _keys.Unique(key), LineOf(member));
    }

    private List<StatementSyntax> InstrumentStatements(int member, SyntaxList<StatementSyntax> originals, SyntaxList<StatementSyntax> updated)
    {
        var result = new List<StatementSyntax>(updated.Count * 2);
        for (var i = 0; i < updated.Count; i++)
        {
            var original = originals[i];
            var statement = updated[i];
            if (original is LabelStatementSyntax or EmptyStatementSyntax)
            {
                result.Add(statement);
                continue;
            }

            var hook = VisualBasicCoverageHooks.Hit(_module.AddStatement(member, LineOf(original)));
            if (i == 0 && IsConstructorCall(original))
            {
                // MyBase.New(...) : Hit(n), with the line's trailing trivia after the hook.
                result.Add(statement.WithTrailingTrivia(VisualBasicCoverageHooks.Separator));
                result.Add(hook.WithTrailingTrivia(statement.GetTrailingTrivia()));
                continue;
            }

            result.Add(hook.WithLeadingTrivia(statement.GetLeadingTrivia()).WithTrailingTrivia(VisualBasicCoverageHooks.Separator));
            result.Add(statement.WithoutLeadingTrivia());
        }

        return result;
    }

    /// <summary>
    /// Adds the entry point of a body at the end of the line that opens it, <c>Sub Run() : Hit(e)</c>, or, in a
    /// constructor that calls another one first, after that call and its hook.
    /// </summary>
    private MethodBlockBaseSyntax AddEntry(int member, MethodBlockBaseSyntax original, MethodBlockBaseSyntax updated)
    {
        var entry = VisualBasicCoverageHooks.Hit(_module.AddEntry(member));
        var statements = updated.Statements.ToList();
        if (original.Statements.Count > 0 && IsConstructorCall(original.Statements[0]) && statements.Count > 1)
        {
            var callHook = statements[1];
            statements[1] = callHook.WithTrailingTrivia(VisualBasicCoverageHooks.Separator);
            statements.Insert(2, entry.WithTrailingTrivia(callHook.GetTrailingTrivia()));
            return updated.WithStatements(SyntaxFactory.List(statements));
        }

        var begin = updated.BlockStatement;
        statements.Insert(0, entry.WithTrailingTrivia(begin.GetTrailingTrivia()));
        return updated
            .WithBlockStatement(begin.WithTrailingTrivia(VisualBasicCoverageHooks.Separator))
            .WithStatements(SyntaxFactory.List(statements));
    }

    /// <summary>The one-based line where a node of the original tree starts.</summary>
    private int LineOf(SyntaxNode node) => _tree.GetLineSpan(node.Span, _cancellationToken).StartLinePosition.Line + 1;
}
