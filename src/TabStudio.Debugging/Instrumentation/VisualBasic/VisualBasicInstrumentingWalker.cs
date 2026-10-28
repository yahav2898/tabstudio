using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Debugging.Instrumentation.VisualBasic;

/// <summary>
/// Instruments one Visual Basic syntax tree, the twin of the C# <see cref="InstrumentingRewriter"/>: every statement
/// reports to the debugger before it runs, and every function reports when it is entered and left.
/// </summary>
/// <remarks>
/// <para>
/// Visual Basic separates statements on one line with colons, so the instrumenter only inserts text: hooks go in front
/// of statements, on their own line, and no line break is ever added. The original statements keep their line
/// numbers in the PDB and in exception stack traces; the debug info map records their original positions.
/// </para>
/// <para>
/// A body <c>S1 ... Sn</c> becomes
/// <c>Dim f As Object = EnterMethod(id) : Try : Hook(...) : S1 ... Catch e As Exception When Fault(f, e, parameters) : Throw : Finally : ExitMethod(f) : End Try : End Sub</c>.
/// The filter never catches; it runs while the exception searches for a handler, before anything unwinds, which is how
/// the debugger breaks on exceptions the program does not handle. Iterators get no <c>Catch</c>, as in C#, and report
/// around each <c>Yield</c> that they suspend and resume. In a constructor that starts with <c>MyBase.New(...)</c>,
/// <c>Me.New(...)</c> or <c>MyClass.New(...)</c>, that call stays first and runs before the frame exists.
/// </para>
/// <para>
/// The condition of a <c>While</c> loop, a <c>Do While</c> or <c>Do Until</c> loop and an <c>ElseIf</c> is a step of its own
/// every time it is evaluated, as in Visual Studio: <c>While c</c> becomes <c>While Condition(hook, c)</c>, which passes
/// the value of <c>c</c> through unchanged. A constant condition is left alone, and a <c>Loop While</c> condition is not a
/// step, like a C# <c>do</c> loop's. <c>For</c> and <c>For Each</c> loops are a step once, before the loop starts.
/// </para>
/// <para>
/// Single-line lambdas are left as they are, like C# expression lambdas: they appear neither on the call stack nor as a
/// step. Finalizers are not instrumented, so the finalizer thread never stops, and neither are functions that use
/// <c>On Error</c> or <c>Resume</c>, which cannot contain a <c>Try</c>, nor functions whose first statement is a label,
/// which has to start its line.
/// </para>
/// </remarks>
internal sealed class VisualBasicInstrumentingWalker
{
    private readonly SemanticModel _model;
    private readonly SyntaxTree _tree;
    private readonly DebugInfoBuilder _info;
    private readonly CancellationToken _cancellationToken;
    private readonly List<Insertion> _insertions = [];

    /// <summary>Creates a walker for the tree of a semantic model.</summary>
    /// <param name="model">The semantic model of the original tree.</param>
    /// <param name="info">Numbers statements and methods; shared by every project of a build.</param>
    /// <param name="cancellationToken">Cancels instrumentation.</param>
    public VisualBasicInstrumentingWalker(SemanticModel model, DebugInfoBuilder info, CancellationToken cancellationToken)
    {
        _model = model;
        _tree = model.SyntaxTree;
        _info = info;
        _cancellationToken = cancellationToken;
    }

    /// <summary>Instruments the tree.</summary>
    /// <returns>The instrumented source text, or <see langword="null"/> when the tree has nothing to instrument.</returns>
    public string? Rewrite()
    {
        Visit(_tree.GetRoot(_cancellationToken), null);
        if (_insertions.Count == 0)
        {
            return null;
        }

        var original = _tree.GetText(_cancellationToken).ToString();
        var builder = new StringBuilder(original.Length + _insertions.Sum(i => i.Text.Length));
        var copied = 0;
        foreach (var insertion in _insertions.OrderBy(i => i.Position).ThenBy(i => i.Order))
        {
            builder.Append(original, copied, insertion.Position - copied).Append(insertion.Text);
            copied = insertion.Position;
        }

        return builder.Append(original, copied, original.Length - copied).ToString();
    }

    // ---- Traversal ----

    private void Visit(SyntaxNode node, InstrumentedFunction? current)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        switch (node)
        {
            case MethodBlockBaseSyntax method:
                VisitMethod(method);
                return;
            case MultiLineLambdaExpressionSyntax lambda:
                VisitLambda(lambda, current);
                return;
            case SingleLineLambdaExpressionSyntax:
                return;
            case ElseIfBlockSyntax elseIf when current is not null:
                HookCondition(current, elseIf.ElseIfStatement.Condition, elseIf.ElseIfStatement);
                break;
        }

        if (current is not null && StatementsOf(node) is { } statements)
        {
            foreach (var statement in statements)
            {
                HookStatement(current, statement);
            }
        }

        foreach (var child in node.ChildNodes())
        {
            Visit(child, current);
        }
    }

    private void VisitMethod(MethodBlockBaseSyntax method)
    {
        if (_model.GetDeclaredSymbol(method, _cancellationToken) is not { } symbol ||
            symbol.MethodKind == MethodKind.Destructor ||
            UsesUnstructuredErrorHandling(method))
        {
            return;
        }

        var statements = method.Statements;
        var first = StartsWithConstructorCall(statements) ? 1 : 0;
        if (first < statements.Count && statements[first] is LabelStatementSyntax)
        {
            return;
        }

        Instrument(CreateFunction(method, symbol, parent: null, statements), statements, first, method.EndBlockStatement);
    }

    private void VisitLambda(MultiLineLambdaExpressionSyntax lambda, InstrumentedFunction? parent)
    {
        var statements = lambda.Statements;
        if (_model.GetSymbolInfo(lambda, _cancellationToken).Symbol is not IMethodSymbol symbol ||
            statements.FirstOrDefault() is LabelStatementSyntax ||
            UsesUnstructuredErrorHandling(lambda))
        {
            return;
        }

        Instrument(CreateFunction(lambda, symbol, parent, statements), statements, 0, lambda.EndSubOrFunctionStatement);
    }

    /// <summary>Wraps a function's statements in its frame and puts a hook before each of them.</summary>
    private void Instrument(InstrumentedFunction function, SyntaxList<StatementSyntax> statements, int first, EndBlockStatementSyntax end)
    {
        // A constructor call has to stay the first statement, so it runs outside the frame, without hooks.
        for (var i = 0; i < first; i++)
        {
            Visit(statements[i], null);
        }

        Insert(first < statements.Count ? statements[first].SpanStart : end.SpanStart, VisualBasicHookText.Prologue(function.Context));
        for (var i = first; i < statements.Count; i++)
        {
            HookStatement(function, statements[i]);
            Visit(statements[i], function);
        }

        Insert(end.SpanStart, VisualBasicHookText.Epilogue(function.Context));
    }

    // ---- Hooks ----

    /// <summary>Puts a hook before a statement of a list, on the statement's own line.</summary>
    private void HookStatement(InstrumentedFunction function, StatementSyntax statement)
    {
        switch (statement)
        {
            case LabelStatementSyntax or EmptyStatementSyntax:
                // A label has to start its line; the statement after it gets the hook, so a GoTo runs it.
                return;
            case WhileBlockSyntax loop when IsSteppable(loop.WhileStatement.Condition):
                // Its condition, evaluated first and on every iteration, is its step.
                HookCondition(function, loop.WhileStatement.Condition, loop.WhileStatement);
                return;
            case DoLoopBlockSyntax { DoStatement.WhileOrUntilClause: { } clause } loop when IsSteppable(clause.Condition):
                HookCondition(function, clause.Condition, loop.DoStatement);
                return;
        }

        var locals = Collect(function, statement.SpanStart);
        var id = _info.AddStatement(function.Context.MethodId, Locate(HighlightSpan(statement)), locals.Select(l => l.Slot).ToList());
        Insert(statement.SpanStart, VisualBasicHookText.Statement(function.Context, id, locals.Select(l => l.Expression).ToList()));

        if (function.Context.IsIterator && statement is YieldStatementSyntax)
        {
            Insert(statement.SpanStart, VisualBasicHookText.Suspend(function.Context));
            Insert(statement.Span.End, VisualBasicHookText.Resume(function.Context));
        }
    }

    /// <summary>Makes a condition a step of its own: <c>c</c> becomes <c>Condition(hook, c)</c>.</summary>
    private void HookCondition(InstrumentedFunction function, ExpressionSyntax condition, StatementSyntax header)
    {
        var locals = Collect(function, condition.SpanStart);
        var id = _info.AddStatement(function.Context.MethodId, Locate(header.Span), locals.Select(l => l.Slot).ToList());
        Insert(condition.SpanStart, VisualBasicHookText.ConditionStart(function.Context, id, locals.Select(l => l.Expression).ToList()));
        Insert(condition.Span.End, VisualBasicHookText.ConditionEnd);
    }

    private IReadOnlyList<CollectedLocal> Collect(InstrumentedFunction function, int position) =>
        VisualBasicLocalsCollector.Collect(_model, position, function.Context, function.Candidates, _cancellationToken);

    private void Insert(int position, string text) => _insertions.Add(new Insertion(position, _insertions.Count, text));

    // ---- Functions ----

    /// <summary>Registers a function in the debug info map and decides what its hooks may read.</summary>
    private InstrumentedFunction CreateFunction(SyntaxNode declaration, IMethodSymbol symbol, InstrumentedFunction? parent, SyntaxList<StatementSyntax> statements)
    {
        var span = DeclarationSpan(declaration, symbol);
        ImmutableHashSet<TextSpan> scopes;
        string? thisType;
        if (symbol.MethodKind == MethodKind.AnonymousFunction)
        {
            // A lambda reads the variables of the functions around it; in a Structure it cannot read Me.
            scopes = parent is null ? ImmutableHashSet.Create(span) : parent.Context.Scopes.Add(span);
            thisType = symbol.ContainingType is { IsValueType: false } ? parent?.Context.ThisType : null;
        }
        else
        {
            scopes = ImmutableHashSet.Create(span);
            thisType = CanUseMe(symbol) ? symbol.ContainingType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) : null;
        }

        var (parameters, faultValues) = VisualBasicLocalsCollector.Parameters(symbol, thisType);
        var methodId = _info.AddMethod(MethodNames.Display(symbol), Locate(declaration.Span), parameters);
        var context = new FunctionContext(methodId, _tree, span, scopes, symbol.IsIterator, thisType, faultValues);

        List<ISymbol> candidates = [.. symbol.Parameters, .. DeclaredLocals(statements)];
        if (parent is not null)
        {
            candidates.AddRange(parent.Candidates);
        }

        var ordered = candidates
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(s => s is ILocalSymbol ? 1 : 0)
            .ThenBy(s => s.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0)
            .ToList();
        return new InstrumentedFunction(context, ordered);
    }

    /// <summary>The span the symbol's declaring syntax reference has in this tree, which is how its variables name their owner.</summary>
    private TextSpan DeclarationSpan(SyntaxNode declaration, IMethodSymbol symbol)
    {
        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (reference.SyntaxTree == _tree && declaration.Span.Contains(reference.Span))
            {
                return reference.Span;
            }
        }

        return declaration.Span;
    }

    /// <summary>The locals declared in a function's statements, including those of the lambdas inside it.</summary>
    private IEnumerable<ISymbol> DeclaredLocals(SyntaxList<StatementSyntax> statements)
    {
        if (statements.Count == 0)
        {
            return [];
        }

        try
        {
            var flow = _model.AnalyzeDataFlow(statements[0], statements[^1]);
            return flow is { Succeeded: true } ? flow.VariablesDeclared.OfType<ILocalSymbol>() : [];
        }
        catch (ArgumentException)
        {
            // Roslyn refuses regions it cannot analyze; the hooks then report only the parameters.
            return [];
        }
    }

    private static bool CanUseMe(IMethodSymbol member) =>
        !member.IsStatic &&
        member.MethodKind is MethodKind.Ordinary or MethodKind.Constructor or MethodKind.PropertyGet or MethodKind.PropertySet
            or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise &&
        member.ContainingType is { TypeKind: TypeKind.Class or TypeKind.Struct } type &&
        !(type.IsValueType && (member.IsAsync || member.IsIterator));

    /// <summary>
    /// Whether the first statement calls another constructor (<c>MyBase.New(...)</c>, <c>Me.New(...)</c> or
    /// <c>MyClass.New(...)</c>), which Visual Basic requires to come first.
    /// </summary>
    private bool StartsWithConstructorCall(SyntaxList<StatementSyntax> statements)
    {
        var target = statements.FirstOrDefault() switch
        {
            ExpressionStatementSyntax statement => statement.Expression,
            CallStatementSyntax call => call.Invocation,
            _ => null,
        };

        var callee = target is InvocationExpressionSyntax invocation ? invocation.Expression : target;
        if (target is null ||
            callee is not MemberAccessExpressionSyntax { Expression: MyBaseExpressionSyntax or MeExpressionSyntax or MyClassExpressionSyntax } access)
        {
            return false;
        }

        return string.Equals(access.Name.Identifier.ValueText, "New", StringComparison.OrdinalIgnoreCase) ||
            _model.GetSymbolInfo(target, _cancellationToken).Symbol is IMethodSymbol { MethodKind: MethodKind.Constructor };
    }

    /// <summary><c>On Error</c> and <c>Resume</c> cannot share a method with <c>Try</c>.</summary>
    private static bool UsesUnstructuredErrorHandling(SyntaxNode function) =>
        function.DescendantNodes().Any(n => n is OnErrorGoToStatementSyntax or OnErrorResumeNextStatementSyntax or ResumeStatementSyntax);

    /// <summary>A condition is a step unless it is a constant, which marks an endless loop or dead code.</summary>
    private bool IsSteppable(ExpressionSyntax condition) => !_model.GetConstantValue(condition, _cancellationToken).HasValue;

    /// <summary>The statements a block holds directly, which get hooks; <see langword="null"/> for other nodes.</summary>
    private static SyntaxList<StatementSyntax>? StatementsOf(SyntaxNode node) => node switch
    {
        MultiLineIfBlockSyntax block => block.Statements,
        ElseIfBlockSyntax block => block.Statements,
        ElseBlockSyntax block => block.Statements,
        SingleLineIfStatementSyntax statement => statement.Statements,
        SingleLineElseClauseSyntax clause => clause.Statements,
        ForOrForEachBlockSyntax block => block.Statements,
        WhileBlockSyntax block => block.Statements,
        DoLoopBlockSyntax block => block.Statements,
        UsingBlockSyntax block => block.Statements,
        SyncLockBlockSyntax block => block.Statements,
        WithBlockSyntax block => block.Statements,
        TryBlockSyntax block => block.Statements,
        CatchBlockSyntax block => block.Statements,
        FinallyBlockSyntax block => block.Statements,
        CaseBlockSyntax block => block.Statements,
        _ => null,
    };

    /// <summary>The part of a statement the editor highlights: the first line of a block, otherwise all of it.</summary>
    private static TextSpan HighlightSpan(StatementSyntax statement) => statement switch
    {
        MultiLineIfBlockSyntax s => s.IfStatement.Span,
        SingleLineIfStatementSyntax s => TextSpan.FromBounds(s.SpanStart, s.ThenKeyword.Span.End),
        ForOrForEachBlockSyntax s => s.ForOrForEachStatement.Span,
        WhileBlockSyntax s => s.WhileStatement.Span,
        DoLoopBlockSyntax s => s.DoStatement.Span,
        UsingBlockSyntax s => s.UsingStatement.Span,
        SyncLockBlockSyntax s => s.SyncLockStatement.Span,
        WithBlockSyntax s => s.WithStatement.Span,
        TryBlockSyntax s => s.TryStatement.Span,
        SelectBlockSyntax s => s.SelectStatement.Span,
        _ => statement.Span,
    };

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

    /// <summary>A function being instrumented and the variables its hooks may read.</summary>
    /// <param name="Context">Its frame, id and scopes.</param>
    /// <param name="Candidates">Its parameters and locals and those of the functions around it, parameters first.</param>
    private sealed record InstrumentedFunction(FunctionContext Context, IReadOnlyList<ISymbol> Candidates);

    /// <summary>Text to insert into the original source.</summary>
    /// <param name="Position">Where, in the original text.</param>
    /// <param name="Order">The order of insertions at the same position.</param>
    /// <param name="Text">What.</param>
    private readonly record struct Insertion(int Position, int Order, string Text);
}
