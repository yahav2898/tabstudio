using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Instrumentation;

/// <summary>
/// Rewrites one syntax tree so that every statement reports to the debugger before it runs, and every function
/// reports when it is entered and left.
/// </summary>
/// <remarks>
/// <para>
/// Every insertion goes on a line the original code already occupies, and no line break is ever added, so the
/// original statements keep their line numbers in the PDB and in exception stack traces. Only columns move; the
/// debug info map records the original positions.
/// </para>
/// <para>
/// A body <c>{ S1; S2; }</c> becomes
/// <c>{ var f = Enter(id); try { if (Step(f, 1)) Break(f, 1, locals); S1; ... } catch (Exception e) when (Fault(f, e, parameters)) { throw; } finally { Exit(f); } }</c>.
/// The filter never catches; it runs during the first pass of exception handling, while the throwing frame is still
/// on the stack, which is how the debugger breaks on exceptions the program does not handle. Iterators get no
/// <c>catch</c>, because C# forbids <c>yield</c> inside a <c>try</c> with one, and report around each
/// <c>yield return</c> that they suspend and resume.
/// </para>
/// <para>
/// The condition of a <c>while</c> or <c>for</c> loop is a step of its own on every iteration, as in Visual Studio:
/// <c>while (c) S</c> becomes <c>while (true) { hook; if (!(c)) break; S }</c>, which keeps definite assignment and
/// <c>continue</c> exactly as they were. Constant-true conditions are left alone, and so is a <c>for</c> condition
/// that declares variables, since its incrementor may use them. A <c>do</c> loop's condition is not a step.
/// </para>
/// <para>
/// Expression-bodied members, accessors and local functions are converted to block bodies so that they get a
/// frame and a statement. Lambdas with an expression body are left as they are: they appear neither on the call
/// stack nor as a step. Destructors are not instrumented, so the finalizer thread never stops.
/// </para>
/// </remarks>
internal sealed partial class InstrumentingRewriter : CSharpSyntaxRewriter
{
    private readonly SemanticModel _model;
    private readonly SyntaxTree _tree;
    private readonly DebugInfoBuilder _info;
    private readonly CancellationToken _cancellationToken;
    private readonly Stack<FunctionContext> _functions = new();

    public InstrumentingRewriter(SemanticModel model, DebugInfoBuilder info, CancellationToken cancellationToken)
    {
        _model = model;
        _tree = model.SyntaxTree;
        _info = info;
        _cancellationToken = cancellationToken;
    }

    private FunctionContext? Current => _functions.Count == 0 ? null : _functions.Peek();

    // ---- Top-level statements ----

    public override SyntaxNode? VisitCompilationUnit(CompilationUnitSyntax node)
    {
        var globals = node.Members.TakeWhile(m => m is GlobalStatementSyntax).Cast<GlobalStatementSyntax>().ToList();
        if (globals.Count == 0 ||
            _model.GetEnclosingSymbol(globals[0].Statement.SpanStart, _cancellationToken) is not IMethodSymbol entryPoint)
        {
            return base.VisitCompilationUnit(node);
        }

        var context = CreateContext(node, entryPoint, body: null);
        List<StatementSyntax> statements;
        _functions.Push(context);
        try
        {
            var originals = globals.Select(g => g.Statement).ToList();
            var updated = originals.Select(s => (StatementSyntax)Visit(s)!).ToList();
            statements = InstrumentStatements(originals, updated);
        }
        finally
        {
            _functions.Pop();
        }

        var others = node.Members.Skip(globals.Count).Select(m => (MemberDeclarationSyntax)Visit(m)!);
        return node.WithMembers(SyntaxFactory.List(WrapTopLevel(context, statements).Concat(others)));
    }

    // ---- Members with bodies ----

    public override SyntaxNode? VisitMethodDeclaration(MethodDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitMethodDeclaration(n));

    public override SyntaxNode? VisitConstructorDeclaration(ConstructorDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitConstructorDeclaration(n));

    public override SyntaxNode? VisitOperatorDeclaration(OperatorDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitOperatorDeclaration(n));

    public override SyntaxNode? VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node) =>
        VisitBaseMethod(node, n => base.VisitConversionOperatorDeclaration(n));

    public override SyntaxNode? VisitDestructorDeclaration(DestructorDeclarationSyntax node) => node;

    public override SyntaxNode? VisitAccessorDeclaration(AccessorDeclarationSyntax node)
    {
        if (node.Body is null && node.ExpressionBody is null)
        {
            return base.VisitAccessorDeclaration(node);
        }

        if (_model.GetDeclaredSymbol(node, _cancellationToken) is not IMethodSymbol symbol)
        {
            return base.VisitAccessorDeclaration(node);
        }

        var context = CreateContext(node, symbol, (SyntaxNode?)node.Body ?? node.ExpressionBody);
        var updated = (AccessorDeclarationSyntax)Within(context, () => base.VisitAccessorDeclaration(node))!;
        if (updated.Body is { } body)
        {
            return updated.WithBody(WrapBody(context, body));
        }

        return updated
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(BodyFromArrow(context, symbol, node.ExpressionBody!, updated.ExpressionBody!, updated.SemicolonToken));
    }

    public override SyntaxNode? VisitPropertyDeclaration(PropertyDeclarationSyntax node)
    {
        if (node.ExpressionBody is not { } arrow ||
            _model.GetDeclaredSymbol(node, _cancellationToken) is not { GetMethod: { } getter })
        {
            return base.VisitPropertyDeclaration(node);
        }

        var context = CreateContext(arrow, getter, arrow);
        var updated = (PropertyDeclarationSyntax)Within(context, () => base.VisitPropertyDeclaration(node))!;
        var accessors = GetterFromArrow(context, getter, arrow, updated.ExpressionBody!, updated.SemicolonToken);
        return updated.WithExpressionBody(null).WithSemicolonToken(default).WithAccessorList(accessors);
    }

    public override SyntaxNode? VisitIndexerDeclaration(IndexerDeclarationSyntax node)
    {
        if (node.ExpressionBody is not { } arrow ||
            _model.GetDeclaredSymbol(node, _cancellationToken) is not { GetMethod: { } getter })
        {
            return base.VisitIndexerDeclaration(node);
        }

        var context = CreateContext(arrow, getter, arrow);
        var updated = (IndexerDeclarationSyntax)Within(context, () => base.VisitIndexerDeclaration(node))!;
        var accessors = GetterFromArrow(context, getter, arrow, updated.ExpressionBody!, updated.SemicolonToken);
        return updated.WithExpressionBody(null).WithSemicolonToken(default).WithAccessorList(accessors);
    }

    // ---- Local functions and lambdas ----

    public override SyntaxNode? VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        if ((node.Body is null && node.ExpressionBody is null) ||
            _model.GetDeclaredSymbol(node, _cancellationToken) is not IMethodSymbol symbol)
        {
            return base.VisitLocalFunctionStatement(node);
        }

        var context = CreateContext(node, symbol, (SyntaxNode?)node.Body ?? node.ExpressionBody);
        var updated = (LocalFunctionStatementSyntax)Within(context, () => base.VisitLocalFunctionStatement(node))!;
        if (updated.Body is { } body)
        {
            return updated.WithBody(WrapBody(context, body));
        }

        return updated
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(BodyFromArrow(context, symbol, node.ExpressionBody!, updated.ExpressionBody!, updated.SemicolonToken));
    }

    public override SyntaxNode? VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node)
    {
        if (node.Block is null || LambdaSymbol(node) is not { } symbol)
        {
            return base.VisitParenthesizedLambdaExpression(node);
        }

        var context = CreateContext(node, symbol, node.Block);
        var updated = (ParenthesizedLambdaExpressionSyntax)Within(context, () => base.VisitParenthesizedLambdaExpression(node))!;
        return updated.WithBlock(WrapBody(context, updated.Block!));
    }

    public override SyntaxNode? VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node)
    {
        if (node.Block is null || LambdaSymbol(node) is not { } symbol)
        {
            return base.VisitSimpleLambdaExpression(node);
        }

        var context = CreateContext(node, symbol, node.Block);
        var updated = (SimpleLambdaExpressionSyntax)Within(context, () => base.VisitSimpleLambdaExpression(node))!;
        return updated.WithBlock(WrapBody(context, updated.Block!));
    }

    public override SyntaxNode? VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node)
    {
        if (LambdaSymbol(node) is not { } symbol)
        {
            return base.VisitAnonymousMethodExpression(node);
        }

        var context = CreateContext(node, symbol, node.Block);
        var updated = (AnonymousMethodExpressionSyntax)Within(context, () => base.VisitAnonymousMethodExpression(node))!;
        return updated.WithBlock(WrapBody(context, updated.Block));
    }

    // ---- Statement lists ----

    public override SyntaxNode? VisitBlock(BlockSyntax node)
    {
        var updated = (BlockSyntax)base.VisitBlock(node)!;
        return Current is null ? updated : updated.WithStatements(SyntaxFactory.List(InstrumentStatements(node.Statements, updated.Statements)));
    }

    public override SyntaxNode? VisitSwitchSection(SwitchSectionSyntax node)
    {
        var updated = (SwitchSectionSyntax)base.VisitSwitchSection(node)!;
        return Current is null ? updated : updated.WithStatements(SyntaxFactory.List(InstrumentStatements(node.Statements, updated.Statements)));
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
        if (Current is not { } context || !HasConditionHook(node))
        {
            return updated.WithStatement(Embed(node.Statement, updated.Statement));
        }

        // while (c) S  becomes  while (true) { hook; if (!(c)) break; S }, so the condition is a step on every
        // iteration. Definite assignment is unchanged: after the loop it is what holds when c is false.
        var body = LoopBody(context, node.Condition, updated.Condition, updated.CloseParenToken, node.Statement, updated.Statement);
        return updated
            .WithCondition(SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))
            .WithCloseParenToken(updated.CloseParenToken.WithTrailingTrivia(HookSyntax.Space))
            .WithStatement(body);
    }

    public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
    {
        var updated = (DoStatementSyntax)base.VisitDoStatement(node)!;
        return updated.WithStatement(Embed(node.Statement, updated.Statement));
    }

    public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
    {
        var updated = (ForStatementSyntax)base.VisitForStatement(node)!;
        if (Current is not { } context || node.Condition is null || updated.Condition is null || !IsSteppableCondition(node.Condition) ||
            DeclaresVariables(node.Condition))
        {
            return updated.WithStatement(Embed(node.Statement, updated.Statement));
        }

        // for (i; c; n) S  becomes  for (i; ; n) { hook; if (!(c)) break; S }: continue still runs n, then c.
        // A condition that declares variables stays, because the incrementor may use them.
        var body = LoopBody(context, node.Condition, updated.Condition, updated.CloseParenToken, node.Statement, updated.Statement);
        return updated
            .WithCondition(null)
            .WithCloseParenToken(updated.CloseParenToken.WithTrailingTrivia(HookSyntax.Space))
            .WithStatement(body);
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

    private SyntaxNode? VisitBaseMethod<T>(T node, Func<T, SyntaxNode?> visitChildren)
        where T : BaseMethodDeclarationSyntax
    {
        if ((node.Body is null && node.ExpressionBody is null) ||
            _model.GetDeclaredSymbol(node, _cancellationToken) is not IMethodSymbol symbol)
        {
            return visitChildren(node);
        }

        var context = CreateContext(node, symbol, (SyntaxNode?)node.Body ?? node.ExpressionBody);
        var updated = (BaseMethodDeclarationSyntax)Within(context, () => visitChildren(node))!;
        if (updated.Body is { } body)
        {
            return updated.WithBody(WrapBody(context, body));
        }

        return updated
            .WithExpressionBody(null)
            .WithSemicolonToken(default)
            .WithBody(BodyFromArrow(context, symbol, node.ExpressionBody!, updated.ExpressionBody!, updated.SemicolonToken));
    }

    private IMethodSymbol? LambdaSymbol(AnonymousFunctionExpressionSyntax node) =>
        _model.GetSymbolInfo(node, _cancellationToken).Symbol as IMethodSymbol;

    private SyntaxNode? Within(FunctionContext context, Func<SyntaxNode?> visit)
    {
        _functions.Push(context);
        try
        {
            return visit();
        }
        finally
        {
            _functions.Pop();
        }
    }

    /// <summary>Registers a function in the debug info map and decides what its hooks may read.</summary>
    private FunctionContext CreateContext(SyntaxNode declaration, IMethodSymbol symbol, SyntaxNode? body)
    {
        var parent = Current;
        var isNested = symbol.MethodKind is MethodKind.AnonymousFunction or MethodKind.LocalFunction;
        ImmutableHashSet<TextSpan> scopes;
        bool thisAllowed;
        if (isNested)
        {
            var isolated = parent is null || symbol.MethodKind == MethodKind.LocalFunction || symbol.IsStatic;
            scopes = isolated ? ImmutableHashSet.Create(declaration.Span) : parent!.Scopes.Add(declaration.Span);
            thisAllowed = parent?.ThisType is not null &&
                !symbol.IsStatic &&
                symbol.ContainingType is { IsValueType: false } &&
                !declaration.Ancestors().Any(a => a is ConstructorInitializerSyntax or PrimaryConstructorBaseTypeSyntax);
        }
        else
        {
            scopes = ImmutableHashSet.Create(declaration.Span);
            thisAllowed = CanUseThis(symbol);
        }

        var thisType = thisAllowed ? symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) : null;
        var parameters = LocalsCollector.ParameterSlots(symbol, thisType);
        var methodId = _info.AddMethod(MethodNames.Display(symbol), Locate(declaration.Span), parameters);
        var faultValues = parameters.Select(p => p.Name == "this" && thisType is not null ? "this" : "@" + p.Name).ToList();
        var isIterator = body is not null && ContainsYield(body);

        return new FunctionContext(methodId, _tree, declaration.Span, scopes, isIterator, thisType, faultValues);
    }

    private static bool CanUseThis(IMethodSymbol member) =>
        !member.IsStatic &&
        member.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.PropertyGet or MethodKind.PropertySet
            or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.ExplicitInterfaceImplementation &&
        member.ContainingType is { IsRefLikeType: false, TypeKind: TypeKind.Class or TypeKind.Struct or TypeKind.Interface };

    private static bool ContainsYield(SyntaxNode body) =>
        body.DescendantNodes(n => n == body || n is not (AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            .OfType<YieldStatementSyntax>()
            .Any();

    private static bool NeedsHook(StatementSyntax statement) =>
        statement is not (BlockSyntax or LocalFunctionStatementSyntax or EmptyStatementSyntax);

    /// <summary>Puts a hook before each statement of a list, on the statement's own line.</summary>
    private List<StatementSyntax> InstrumentStatements(IReadOnlyList<StatementSyntax> originals, IReadOnlyList<StatementSyntax> updated)
    {
        var result = new List<StatementSyntax>(updated.Count * 2);
        var context = Current;
        for (var i = 0; i < updated.Count; i++)
        {
            if (context is null)
            {
                result.Add(updated[i]);
            }
            else
            {
                AddInstrumented(context, result, originals[i], updated[i]);
            }
        }

        return result;
    }

    private void AddInstrumented(FunctionContext context, List<StatementSyntax> result, StatementSyntax original, StatementSyntax updated)
    {
        if (!NeedsHook(original))
        {
            result.Add(updated);
            return;
        }

        if (original is LabeledStatementSyntax labeled)
        {
            // L: S becomes L: hook; S, so a goto to L still runs the hook.
            if (updated is not LabeledStatementSyntax updatedLabeled || !NeedsHook(labeled.Statement) || labeled.Statement is LabeledStatementSyntax)
            {
                result.Add(updated);
                return;
            }

            var inner = new List<StatementSyntax>();
            AddInstrumented(context, inner, labeled.Statement, updatedLabeled.Statement);
            result.Add(updatedLabeled.WithStatement(inner[0]));
            result.AddRange(inner.Skip(1));
            return;
        }

        if (original is WhileStatementSyntax loop && HasConditionHook(loop))
        {
            // Its condition, evaluated first, is its step.
            result.Add(updated);
            return;
        }

        var hook = CreateStepHook(context, original);
        result.Add(hook.WithLeadingTrivia(updated.GetLeadingTrivia()).WithTrailingTrivia(HookSyntax.Space));

        if (context.IsIterator && original.IsKind(SyntaxKind.YieldReturnStatement))
        {
            var trailing = updated.GetTrailingTrivia();
            result.Add(HookSyntax.Suspend(context.Frame).WithTrailingTrivia(HookSyntax.Space));
            result.Add(updated.WithoutLeadingTrivia().WithTrailingTrivia(HookSyntax.Space));
            result.Add(HookSyntax.Resume(context.Frame).WithTrailingTrivia(trailing));
            return;
        }

        result.Add(updated.WithoutLeadingTrivia());
    }

    /// <summary>Instruments the body of an <c>if</c>, loop or similar statement, adding braces when it has none.</summary>
    private StatementSyntax Embed(StatementSyntax original, StatementSyntax updated)
    {
        if (Current is not { } context || !NeedsHook(original) || original is LabeledStatementSyntax)
        {
            return updated;
        }

        var statements = new List<StatementSyntax>();
        AddInstrumented(context, statements, original, updated);
        return HookSyntax.Block(statements);
    }

    private bool HasConditionHook(WhileStatementSyntax loop) => Current is not null && IsSteppableCondition(loop.Condition);

    /// <summary>A loop condition gets its own step unless it is the constant <c>true</c>, which marks an infinite loop.</summary>
    private bool IsSteppableCondition(ExpressionSyntax condition) =>
        _model.GetConstantValue(condition, _cancellationToken) is not { HasValue: true, Value: true };

    private static bool DeclaresVariables(ExpressionSyntax condition) =>
        condition.DescendantNodesAndSelf().Any(n => n is SingleVariableDesignationSyntax or DeclarationExpressionSyntax);

    /// <summary>
    /// The body of a rewritten loop: <c>{ hook; if (!(c)) break; S }</c>, with the hook and the check on the line of
    /// the condition and <c>S</c> on its own lines.
    /// </summary>
    private BlockSyntax LoopBody(
        FunctionContext context,
        ExpressionSyntax originalCondition,
        ExpressionSyntax updatedCondition,
        SyntaxToken closeParen,
        StatementSyntax originalBody,
        StatementSyntax updatedBody)
    {
        var hook = CreateStepHook(context, originalCondition).WithTrailingTrivia(HookSyntax.Space);
        var check = SyntaxFactory.IfStatement(
                SyntaxFactory.PrefixUnaryExpression(
                    SyntaxKind.LogicalNotExpression,
                    SyntaxFactory.ParenthesizedExpression(updatedCondition.WithoutLeadingTrivia().WithoutTrailingTrivia())),
                SyntaxFactory.BreakStatement())
            .WithTrailingTrivia(closeParen.TrailingTrivia);

        var statements = new List<StatementSyntax> { hook, check };
        AddInstrumented(context, statements, originalBody, updatedBody);
        var trailing = statements[^1].GetTrailingTrivia();
        statements[^1] = statements[^1].WithoutTrailingTrivia();

        return SyntaxFactory.Block(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithTrailingTrivia(HookSyntax.Space),
            SyntaxFactory.List(statements),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(HookSyntax.Space).WithTrailingTrivia(trailing));
    }

    private StatementSyntax CreateStepHook(FunctionContext context, SyntaxNode original)
    {
        var locals = LocalsCollector.Collect(_model, original, context, _cancellationToken);
        var id = _info.AddStatement(context.MethodId, Locate(HighlightSpan(original)), locals.Select(l => l.Slot).ToList(),
            ExceptionOperand(original, division: true), ExceptionOperand(original, division: false));
        return HookSyntax.Step(context.Frame, id, locals.Select(l => l.Expression).ToList());
    }

    /// <summary>Turns <c>{ S... }</c> into <c>{ var f = Enter(id); try { S... } catch ... finally { Exit(f); } }</c>.</summary>
    private static BlockSyntax WrapBody(FunctionContext context, BlockSyntax body)
    {
        var enter = HookSyntax.Enter(context.Frame, context.MethodId).WithTrailingTrivia(HookSyntax.Space);
        var tryStatement = CreateTry(context);
        var tryBlock = tryStatement.Block
            .WithOpenBraceToken(tryStatement.Block.OpenBraceToken.WithTrailingTrivia(body.OpenBraceToken.TrailingTrivia))
            .WithStatements(body.Statements)
            .WithCloseBraceToken(tryStatement.Block.CloseBraceToken.WithLeadingTrivia(body.CloseBraceToken.LeadingTrivia));
        tryStatement = tryStatement.WithBlock(tryBlock).WithTrailingTrivia(HookSyntax.Space);

        return body
            .WithOpenBraceToken(body.OpenBraceToken.WithTrailingTrivia(HookSyntax.Space))
            .WithStatements(SyntaxFactory.List<StatementSyntax>([enter, tryStatement]))
            .WithCloseBraceToken(body.CloseBraceToken.WithLeadingTrivia());
    }

    /// <summary>Wraps the top-level statements the same way as a body, as two global statements.</summary>
    private static IEnumerable<MemberDeclarationSyntax> WrapTopLevel(FunctionContext context, List<StatementSyntax> statements)
    {
        var leading = statements[0].GetLeadingTrivia();
        statements[0] = statements[0].WithoutLeadingTrivia();
        var trailing = statements[^1].GetTrailingTrivia();
        statements[^1] = statements[^1].WithoutTrailingTrivia();

        var enter = HookSyntax.Enter(context.Frame, context.MethodId).WithLeadingTrivia(leading).WithTrailingTrivia(HookSyntax.Space);
        var tryStatement = CreateTry(context);
        var tryBlock = tryStatement.Block
            .WithOpenBraceToken(tryStatement.Block.OpenBraceToken.WithTrailingTrivia(HookSyntax.Space))
            .WithStatements(SyntaxFactory.List(statements))
            .WithCloseBraceToken(tryStatement.Block.CloseBraceToken.WithLeadingTrivia(HookSyntax.Space));
        tryStatement = tryStatement.WithBlock(tryBlock).WithTrailingTrivia(trailing);

        return [SyntaxFactory.GlobalStatement(enter), SyntaxFactory.GlobalStatement(tryStatement)];
    }

    private static TryStatementSyntax CreateTry(FunctionContext context) =>
        HookSyntax.Try(context.Frame, context.IsIterator ? null : context.ExceptionVariable, context.FaultValues);

    /// <summary>Turns <c>=&gt; expr;</c> into a block body that returns or evaluates <c>expr</c>, on the same lines.</summary>
    private BlockSyntax BodyFromArrow(
        FunctionContext context,
        IMethodSymbol symbol,
        ArrowExpressionClauseSyntax original,
        ArrowExpressionClauseSyntax updated,
        SyntaxToken semicolon)
    {
        var expression = updated.Expression;
        var hook = CreateStepHook(context, original.Expression)
            .WithLeadingTrivia(expression.GetLeadingTrivia())
            .WithTrailingTrivia(HookSyntax.Space);
        var bare = expression.WithoutLeadingTrivia();

        StatementSyntax statement = bare switch
        {
            ThrowExpressionSyntax thrown => SyntaxFactory.ThrowStatement(
                thrown.ThrowKeyword, thrown.Expression, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            _ when ReturnsValue(symbol) => SyntaxFactory.ReturnStatement(
                SyntaxFactory.Token(SyntaxKind.ReturnKeyword).WithTrailingTrivia(HookSyntax.Space), bare, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
            _ => SyntaxFactory.ExpressionStatement(bare, SyntaxFactory.Token(SyntaxKind.SemicolonToken)),
        };

        var block = SyntaxFactory.Block(
            SyntaxFactory.Token(SyntaxKind.OpenBraceToken).WithLeadingTrivia(updated.ArrowToken.LeadingTrivia).WithTrailingTrivia(updated.ArrowToken.TrailingTrivia),
            SyntaxFactory.List<StatementSyntax>([hook, statement.WithTrailingTrivia(HookSyntax.Space)]),
            SyntaxFactory.Token(SyntaxKind.CloseBraceToken).WithLeadingTrivia(semicolon.LeadingTrivia).WithTrailingTrivia(semicolon.TrailingTrivia));
        return WrapBody(context, block);
    }

    /// <summary>Turns a property's <c>=&gt; expr;</c> into <c>{ get { ... } }</c>, on the same lines.</summary>
    private AccessorListSyntax GetterFromArrow(
        FunctionContext context,
        IMethodSymbol getter,
        ArrowExpressionClauseSyntax original,
        ArrowExpressionClauseSyntax updated,
        SyntaxToken semicolon)
    {
        var body = BodyFromArrow(context, getter, original, updated, semicolon);
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

    private static bool ReturnsValue(IMethodSymbol method)
    {
        if (method.ReturnsVoid)
        {
            return false;
        }

        // An async method returning Task or ValueTask has a statement body; Task<T> returns a value.
        return !method.IsAsync || method.ReturnType is INamedTypeSymbol { IsGenericType: true };
    }

    /// <summary>The part of a statement the editor highlights: the header of compound statements, otherwise all of it.</summary>
    private static TextSpan HighlightSpan(SyntaxNode node)
    {
        SyntaxToken? end = node switch
        {
            IfStatementSyntax s => s.CloseParenToken,
            WhileStatementSyntax s => s.CloseParenToken,
            ForStatementSyntax s => s.CloseParenToken,
            CommonForEachStatementSyntax s => s.CloseParenToken,
            UsingStatementSyntax s => s.CloseParenToken,
            LockStatementSyntax s => s.CloseParenToken,
            FixedStatementSyntax s => s.CloseParenToken,
            SwitchStatementSyntax s => s.CloseParenToken,
            DoStatementSyntax s => s.DoKeyword,
            TryStatementSyntax s => s.TryKeyword,
            CheckedStatementSyntax s => s.Keyword,
            UnsafeStatementSyntax s => s.UnsafeKeyword,
            _ => null,
        };

        return end is { } token && !token.IsMissing && token.Span.End > node.SpanStart
            ? TextSpan.FromBounds(node.SpanStart, token.Span.End)
            : node.Span;
    }

    private DebugSourceSpan Locate(TextSpan span)
    {
        var lines = _tree.GetLineSpan(span, _cancellationToken);
        return new DebugSourceSpan(
            _tree.FilePath,
            lines.StartLinePosition.Line + 1,
            lines.StartLinePosition.Character + 1,
            lines.EndLinePosition.Line + 1,
            lines.EndLinePosition.Character + 1,
            span.Start,
            span.Length);
    }
}
