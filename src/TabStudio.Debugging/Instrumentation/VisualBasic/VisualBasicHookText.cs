namespace TabStudio.Debugging.Instrumentation.VisualBasic;

/// <summary>
/// The Visual Basic text the instrumenter inserts. Every piece is a run of statements separated by colons, with no line
/// break, so the original statements keep their line numbers in sequence points and stack traces.
/// </summary>
internal static class VisualBasicHookText
{
    private const string Runtime = VisualBasicRuntimeSource.Runtime;

    /// <summary>
    /// What goes before a function's first statement: <c>Dim f As Object = EnterMethod(id) : Try : </c>. The function's
    /// own statements become the <c>Try</c> block.
    /// </summary>
    public static string Prologue(FunctionContext function) => FormattableString.Invariant(
        $"Dim {function.Frame} As Object = {Runtime}.{VisualBasicRuntimeSource.EnterMethod}({function.MethodId}) : Try : ");

    /// <summary>
    /// What goes before a function's <c>End</c> statement. A <c>Catch</c> whose filter reports the exception and is
    /// always <see langword="false"/>, so it never catches, then a <c>Finally</c> that leaves the frame. Iterators get no
    /// <c>Catch</c>, as in C#.
    /// </summary>
    public static string Epilogue(FunctionContext function)
    {
        var exit = $"Finally : {Runtime}.{VisualBasicRuntimeSource.ExitMethod}({function.Frame}) : End Try : ";
        if (function.IsIterator)
        {
            return exit;
        }

        var exception = function.ExceptionVariable;
        return $"Catch {exception} As Global.System.Exception When {Runtime}.{VisualBasicRuntimeSource.Fault}({function.Frame}, {exception}, {Array(function.FaultValues)}) : Throw : " + exit;
    }

    /// <summary>A statement hook, followed by a colon: <c>Hook(ShouldBreak(f, id) AndAlso BreakHere(f, id, locals)) : </c>.</summary>
    public static string Statement(FunctionContext function, int statementId, IReadOnlyList<string> values) =>
        $"{Runtime}.{VisualBasicRuntimeSource.StatementHook}({Test(function, statementId, values)}) : ";

    /// <summary>What goes before a loop or <c>ElseIf</c> condition: <c>Condition(ShouldBreak(...) AndAlso BreakHere(...), </c>.</summary>
    public static string ConditionStart(FunctionContext function, int statementId, IReadOnlyList<string> values) =>
        $"{Runtime}.{VisualBasicRuntimeSource.ConditionHook}({Test(function, statementId, values)}, ";

    /// <summary>What goes after a loop or <c>ElseIf</c> condition.</summary>
    public const string ConditionEnd = ")";

    /// <summary><c>SuspendMethod(f) : </c>, before an iterator yields.</summary>
    public static string Suspend(FunctionContext function) =>
        $"{Runtime}.{VisualBasicRuntimeSource.SuspendMethod}({function.Frame}) : ";

    /// <summary><c> : ResumeMethod(f)</c>, after an iterator's <c>Yield</c>.</summary>
    public static string Resume(FunctionContext function) =>
        $" : {Runtime}.{VisualBasicRuntimeSource.ResumeMethod}({function.Frame})";

    /// <summary>An escaped identifier, which reads a local or parameter even when its name is a keyword.</summary>
    public static string Identifier(string name) => "[" + name + "]";

    private static string Test(FunctionContext function, int statementId, IReadOnlyList<string> values) => FormattableString.Invariant(
        $"{Runtime}.{VisualBasicRuntimeSource.ShouldBreak}({function.Frame}, {statementId}) AndAlso {Runtime}.{VisualBasicRuntimeSource.BreakHere}({function.Frame}, {statementId}, {Array(values)})");

    private static string Array(IReadOnlyList<string> values) =>
        values.Count == 0 ? "Nothing" : "New Object() {" + string.Join(", ", values) + "}";
}
