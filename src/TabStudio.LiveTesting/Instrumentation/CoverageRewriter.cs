using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TabStudio.Debugging.Instrumentation;

namespace TabStudio.LiveTesting.Instrumentation;

/// <summary>
/// Rewrites one syntax tree so that every statement, and the start of every function body, marks a coverage point
/// as hit before it runs.
/// </summary>
/// <remarks>
/// <para>
/// It works like the debugger's <c>InstrumentingRewriter</c> and keeps the same promise: every insertion goes on
/// a line the original code already occupies and no line break is ever added, so statements keep their line numbers
/// in the PDB and in the stack traces of failing tests. Only columns move.
/// </para>
/// <para>
/// The hooks are much lighter than the debugger's: a single call per statement, with no frames, no locals and no
/// <c>try</c>, and no semantic model is needed to place them. A body <c>{ S1; S2; }</c> becomes
/// <c>{ Hit(e); Hit(1); S1; Hit(2); S2; }</c>, where <c>e</c> is the member's entry point. Embedded statements get
/// braces on the lines they occupy (<c>if (c) S</c> becomes <c>if (c) { Hit(n); S }</c>), and expression bodies of
/// members and local functions become block bodies. Lambdas with an expression body are left as they are; their
/// line is covered by the statement around them.
/// </para>
/// <para>
/// Only the bodies of tracked members (see <see cref="MemberKeys"/>) are instrumented, including the lambdas and
/// local functions inside them. Field initializers and destructors are not.
/// </para>
/// </remarks>
internal sealed class CoverageRewriter : CSharpSyntaxRewriter
{
    private readonly SyntaxTree _tree;
    private readonly CoverageModuleBuilder _module;
    private readonly CancellationToken _cancellationToken;
    private readonly MemberKeys.KeyCounter _keys = new();
    private int? _member;

    /// <summary>Creates a rewriter for one tree of a project.</summary>
    /// <param name="tree">The tree to rewrite.</param>
    /// <param name="module">Numbers the project's members and points.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    public CoverageRewriter(SyntaxTree tree, CoverageModuleBuilder module, CancellationToken cancellationToken)
    {
        _tree = tree;
        _module = module;
        _cancellationToken = cancellationToken;
    }

    // ---- Top-level statements ----

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var globals = MemberKeys.TopLevelStatements(node);
        if (globals.Count == 0)
        {
            return base.VisitCompilationUnit(node);
        }

        var member = AddMember(MemberKeys.TopLevelTypeName, MemberKeys.TopLevelName, MemberKeys.TopLevelTypeName + "." + MemberKeys.TopLevelName, globals[0]);
        List<StatementSyntax> statements;
        _member = member;
        try
        {
            var originals = globals.Select(g => g.Statement).ToList();
            var updated = originals.Select(s => (StatementSyntax)Visit(s)!).ToList();
            statements = InstrumentStatements(originals, updated);
        }
        finally
        {
            _member = null;
        }

        // The entry point goes in front of the first statement, on its line.
        var entry = CoverageHooks.Hit(_module.AddEntry(member))
            .WithLeadingTrivia(statements[0].GetLeadingTrivia())
            .WithTrailingTrivia(HookSyntax.Space);
        statements[0] = statements[0].WithoutLeadingTrivia();
        statements.Insert(0, entry);

        var others = node.Members.Skip(globals.Count).Select(m => (MemberDeclarationSyntax)Visit(m)!);
        var members = statements.Select(s => (MemberDeclarationSyntax)SyntaxFactory.GlobalStatement(s)).Concat(others);
        return node.WithMembers(SyntaxFactory.List(members));
    }

    // ---- Members with bodies ----

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitMethodDeclaration(n), MemberKeys.ReturnsValue(node.ReturnType, node.Modifiers));

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitConstructorDeclaration(n), returnsValue: false);

    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitOperatorDeclaration(n), returnsValue: true);

    public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitConversionOperatorDeclaration(n), returnsValue: true);

    public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) => node;

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (!MemberKeys.IsTracked(node))
        {
            return node;
        }

        var member = AddMember(node);
        var updated = (PropertyDeclarationSyntax)Within(member, () => base.VisitPropertyDeclaration(node))!;
        if (node.ExpressionBody is not { } arrow)
        {
            return updated;
        }

        var accessors = GetterFromArrow(member, arrow, updated.ExpressionBody!, updated.SemicolonToken);
        return updated.WithExpressionBody(null).WithSemicolonToken(default).WithAccessorList(accessors);
    }

    public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        if (!MemberKeys.IsTracked(node))
        {
            return node;
        }

        var member = AddMember(node);
        var updated = (IndexerDeclarationSyntax)Within(member, () => base.VisitIndexerDeclaration(node))!;
        if (node.ExpressionBody is not { } arrow)
        {
            return updated;
        }

        var accessors = GetterFromArrow(member, arrow, updated.ExpressionBody!, updated.SemicolonToken);
        return updated.WithExpressionBody(null).WithSemicolonToken(default).WithAccessorList(accessors);
    }

    public override SyntaxNode? VisitEventDeclaration(EventDeclarationSyntax node)
    {
        if (!MemberKeys.IsTracked(node))
        {
            return node;
        }

        var member = AddMember(node);
        return Within(member, () => base.VisitEventDeclaration(node));
    }

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        if (_member is not { } member || (node.Body is null && node.ExpressionBody is null))
        {
            return base.VisitAccessorDeclaration(node);
        }

        var updated = (AccessorDeclarationSyntax)base.VisitAccessorDeclaration(node)!;
        if (updated.Body is { } body)
        {
            return updated.WithBody(AddEntry(member, body));
        }

        var returnsValue = node.Keyword.IsKind(SyntaxKind.GetKeyword);
        return updated
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(BodyFromArrow(member, node.ExpressionBody!, updated.ExpressionBody!, updated.SemicolonToken, returnsValue));
    }

    // ---- Fields and other members without bodies: nothing inside them is instrumented ----

    public override SyntaxNode? VisitFieldDeclaration(FieldDeclarationSyntax node) => node;

    public override SyntaxNode? VisitEventFieldDeclaration(EventFieldDeclarationSyntax node) => node;

    public override SyntaxNode? VisitEnumDeclaration(EnumDeclarationSyntax node) => node;

    public override SyntaxNode? VisitDelegateDeclaration(DelegateDeclarationSyntax node) => node;

    // ---- Local functions ----

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        var updated = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node)!;
        if (_member is not { } member || node.ExpressionBody is null || updated.ExpressionBody is null)
        {
            return updated;
        }

        // An expression-bodied local function gets a block body, so its expression is a statement with a marker.
        var returnsValue = MemberKeys.ReturnsValue(node.ReturnType, node.Modifiers);
        return updated
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(BodyFromArrow(member, node.ExpressionBody, updated.ExpressionBody, updated.SemicolonToken, returnsValue, withEntry: false));
    }

    // ---- Statement lists ----

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var updated = (BlockSyntax)base.VisitBlock(node)!;
        return _member is null ? updated : updated.WithStatements(SyntaxFactory.List(InstrumentStatements(node.Statements, updated.Statements)));
    }

    public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
    {
        var updated = (SwitchSectionSyntax)base.VisitSwitchSection(node)!;
        return _member is null ? updated : updated.WithStatements(SyntaxFactory.List(InstrumentStatements(node.Statements, updated.Statements)));
    }

    // ---- Embedded statements, which get braces so a hook can go in front of them ----

    public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
    {
        var updated = (IfStatementSyntax)base.VisitIfStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitElseClause(ElseClauseSyntax node)
    {
        var updated = (ElseClauseSyntax)base.VisitElseClause(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
    {
        var updated = (WhileStatementSyntax)base.VisitWhileStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var updated = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var updated = (ForStatementSyntax)base.VisitForStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
    {
        var updated = (ForEachStatementSyntax)base.VisitForEachStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
    {
        var updated = (ForEachVariableStatementSyntax)base.VisitForEachVariableStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
    {
        var updated = (UsingStatementSyntax)base.VisitUsingStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitLockStatement(LockStatementSyntax node)
    {
        var updated = (LockStatementSyntax)base.VisitLockStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitFixedStatement(FixedStatementSyntax node)
    {
        var updated = (FixedStatementSyntax)base.VisitFixedStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    // ---- Helpers ----

    private SyntaxNode? VisitBaseMethod<T>(T node, Func<T, SyntaxNode?> visitChildren, bool returnsValue)
        where T : BaseMethodDeclarationSyntax
    {
        if (!MemberKeys.IsTracked(node))
        {
            return node;
        }

        var member = AddMember(node);
        var updated = (BaseMethodDeclarationSyntax)Within(member, () => visitChildren(node))!;
        if (updated.Body is { } body)
        {
            return updated.WithBody(AddEntry(member, body));
        }

        return updated
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(BodyFromArrow(member, node.ExpressionBody!, updated.ExpressionBody!, updated.SemicolonToken, returnsValue));
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

    private int AddMember(MemberDeclarationSyntax member)
    {
        var (typeName, name, key) = MemberKeys.Describe(member);
        return AddMember(typeName, name, key, member);
    }

    private int AddMember(string typeName, string name, string key, SyntaxNode start) =>
        _module.AddMember(_tree.FilePath, typeName, name, _keys.Unique(key), LineOf(start));

    private static bool NeedsHook(StatementSyntax statement) =>
        statement is not (BlockSyntax or LocalFunctionStatementSyntax or EmptyStatementSyntax);

    /// <summary>Puts a hook before each statement of a list, on the statement's own line.</summary>
    private List<StatementSyntax> InstrumentStatements(IReadOnlyList<StatementSyntax> originals, IReadOnlyList<StatementSyntax> updated)
    {
        var result = new List<StatementSyntax>(updated.Count * 2);
        for (var i = 0; i < updated.Count; i++)
        {
            if (_member is { } member)
            {
                AddInstrumented(member, result, originals[i], updated[i]);
            }
            else
            {
                result.Add(updated[i]);
            }
        }

        return result;
    }

    private void AddInstrumented(int member, List<StatementSyntax> result, StatementSyntax original, StatementSyntax updated)
    {
        if (!NeedsHook(original))
        {
            result.Add(updated);
            return;
        }

        if (original is LabeledStatementSyntax labeled)
        {
            // L: S becomes L: hook; S, so a goto to L still records S.
            if (updated is not LabeledStatementSyntax updatedLabeled || !NeedsHook(labeled.Statement) || labeled.Statement is LabeledStatementSyntax)
            {
                result.Add(updated);
                return;
            }

            var inner = new List<StatementSyntax>();
            AddInstrumented(member, inner, labeled.Statement, updatedLabeled.Statement);
            result.Add(updatedLabeled.WithStatement(inner[0]));
            result.AddRange(inner.Skip(1));
            return;
        }

        var hook = StatementHook(member, original);
        result.Add(hook.WithLeadingTrivia(updated.GetLeadingTrivia()).WithTrailingTrivia(HookSyntax.Space));
        result.Add(updated.WithoutLeadingTrivia());
    }

    /// <summary>Instruments the body of an <c>if</c>, loop or similar statement, adding braces when it has none.</summary>
    private StatementSyntax Embed(StatementSyntax original, StatementSyntax updated)
    {
        if (_member is not { } member || !NeedsHook(original) || original is LabeledStatementSyntax)
        {
            return updated;
        }

        var statements = new List<StatementSyntax>();
        AddInstrumented(member, statements, original, updated);
        return HookSyntax.Block(statements);
    }

    private StatementSyntax StatementHook(int member, SyntaxNode original) =>
        CoverageHooks.Hit(_module.AddStatement(member, LineOf(original)));

    /// <summary>Turns <c>{ S... }</c> into <c>{ Hit(entry); S... }</c>, the entry on the line of the brace.</summary>
    private BlockSyntax AddEntry(int member, BlockSyntax body)
    {
        var entry = CoverageHooks.Hit(_module.AddEntry(member)).WithTrailingTrivia(body.OpenBraceToken.TrailingTrivia);
        return body
            .WithOpenBraceToken(body.OpenBraceToken.WithTrailingTrivia(HookSyntax.Space))
            .WithStatements(body.Statements.Insert(0, entry));
    }

    /// <summary>Turns <c>=&gt; expr;</c> into a block body that returns or evaluates <c>expr</c>, on the same lines.</summary>
    private BlockSyntax BodyFromArrow(
        int member,
        ArrowExpressionClauseSyntax original,
        ArrowExpressionClauseSyntax updated,
        SyntaxToken semicolon,
        bool returnsValue,
        bool withEntry = true)
    {
        var expression = updated.Expression;
        var hooks = new List<StatementSyntax>();
        if (withEntry)
        {
            hooks.Add(CoverageHooks.Hit(_module.AddEntry(member)).WithTrailingTrivia(HookSyntax.Space));
        }

        hooks.Add(StatementHook(member, original.Expression).WithTrailingTrivia(HookSyntax.Space));
        hooks[0] = hooks[0].WithLeadingTrivia(expression.GetLeadingTrivia());
        var bare = expression.WithoutLeadingTrivia();

        StatementSyntax statement = bare switch
        {
            ThrowExpressionSyntax thrown => SyntaxFactory.ThrowStatement(
                thrown.ThrowKeyword, thrown.Expression, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            _ when returnsValue => SyntaxFactory.ReturnStatement(
                SyntaxFactory.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(HookSyntax.Space), bare, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            _ => SyntaxFactory.ExpressionStatement(bare, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
        };

        hooks.Add(statement.WithTrailingTrivia(HookSyntax.Space));
        return SyntaxFactory.Block(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithLeadingTrivia(updated.ArrowToken.LeadingTrivia).WithTrailingTrivia(updated.ArrowToken.TrailingTrivia),
            SyntaxFactory.List(hooks),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(semicolon.LeadingTrivia).WithTrailingTrivia(semicolon.TrailingTrivia));
    }

    /// <summary>Turns a property's <c>=&gt; expr;</c> into <c>{ get { ... } }</c>, on the same lines.</summary>
    private AccessorListSyntax GetterFromArrow(int member, ArrowExpressionClauseSyntax original, ArrowExpressionClauseSyntax updated, SyntaxToken semicolon)
    {
        var body = BodyFromArrow(member, original, updated, semicolon, returnsValue: true);
        var open = SyntaxFactory.Token(SyntaxKind.OpenBraceToken)
            .WithLeadingTrivia(body.OpenBraceToken.LeadingTrivia)
            .WithTrailingTrivia(HookSyntax.Space);
        var close = SyntaxFactory.Token(SyntaxKind.CloseBraceToken)
            .WithLeadingTrivia(HookSyntax.Space)
            .WithTrailingTrivia(body.CloseBraceToken.TrailingTrivia);
        body = body
            .WithOpenBraceToken(body.OpenBraceToken.WithLeadingTrivia())
            .WithCloseBraceToken(body.CloseBraceToken.WithTrailingTrivia());

        var accessor = SyntaxFactory.AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
            .WithKeyword(SyntaxFactory.Token(SyntaxKind.GetKeyword).WithTrailingTrivia(HookSyntax.Space))
            .WithBody(body);
        return SyntaxFactory.AccessorList(open, SyntaxFactory.SingletonList(accessor), close);
    }

    /// <summary>The one-based line where a node of the original tree starts.</summary>
    private int LineOf(SyntaxNode node) => _tree.GetLineSpan(node.Span, _cancellationToken).StartLinePosition.Line + 1;
}
