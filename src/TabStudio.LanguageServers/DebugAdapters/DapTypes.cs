namespace TabStudio.LanguageServers.DebugAdapters;

/// <summary>An exception filter a debug adapter offers, such as "Uncaught Exceptions".</summary>
/// <param name="Filter">The identifier sent back to turn it on, such as <c>uncaught</c>.</param>
/// <param name="Label">The name shown to the user.</param>
/// <param name="Description">More about it, if any.</param>
/// <param name="IsDefault">Whether it is on unless the user turns it off.</param>
public sealed record DapExceptionFilter(string Filter, string Label, string? Description, bool IsDefault);

/// <summary>What a debug adapter can do.</summary>
public sealed record DapCapabilities
{
    /// <summary>Whether it waits for <c>configurationDone</c> before the program starts.</summary>
    public bool ConfigurationDone { get; init; }

    /// <summary>Whether breakpoints may have conditions.</summary>
    public bool ConditionalBreakpoints { get; init; }

    /// <summary>Whether breakpoints may have hit counts.</summary>
    public bool HitConditionalBreakpoints { get; init; }

    /// <summary>Whether breakpoints may log a message instead of stopping.</summary>
    public bool LogPoints { get; init; }

    /// <summary>Whether expressions can be evaluated for hovers, for DataTips.</summary>
    public bool EvaluateForHovers { get; init; }

    /// <summary>Whether variables can be changed.</summary>
    public bool SetVariable { get; init; }

    /// <summary>Whether expressions can be assigned, such as watches.</summary>
    public bool SetExpression { get; init; }

    /// <summary>Whether the adapter implements the DAP readMemory request.</summary>
    public bool ReadMemory { get; init; }

    /// <summary>Whether the adapter implements the DAP writeMemory request.</summary>
    public bool WriteMemory { get; init; }

    /// <summary>Whether the adapter implements the DAP disassemble request.</summary>
    public bool Disassemble { get; init; }

    /// <summary>Whether it tells more about the exception the program stopped at.</summary>
    public bool ExceptionInfo { get; init; }

    /// <summary>Whether it can end the program gracefully with <c>terminate</c>.</summary>
    public bool Terminate { get; init; }

    /// <summary>The exception filters it offers.</summary>
    public IReadOnlyList<DapExceptionFilter> ExceptionFilters { get; init; } = [];
}

/// <summary>
/// A program a debug adapter asks TabStudio to run (<c>runInTerminal</c>), as debugpy and js-debug do for
/// <c>"console": "integratedTerminal"</c>, so that the program reads its input from the user.
/// </summary>
/// <param name="Arguments">The program and its arguments; the program is the first.</param>
/// <param name="WorkingDirectory">The folder to run it in.</param>
/// <param name="Environment">Variables to set on top of the usual environment; a <see langword="null"/> value removes one.</param>
/// <param name="Title">The title the adapter suggests for the console, if any.</param>
/// <param name="IsExternal">Whether the adapter asked for a terminal window of its own rather than the IDE's console.</param>
public sealed record DapRunInTerminalRequest(
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    IReadOnlyDictionary<string, string?> Environment,
    string? Title,
    bool IsExternal);

/// <summary>A breakpoint TabStudio sets on a line.</summary>
/// <param name="Line">One-based line.</param>
/// <param name="Condition">When it stops, such as <c>count == 3</c>, or <see langword="null"/> to stop every time.</param>
/// <param name="HitCondition">After how many hits it stops, such as <c>5</c> or <c>&gt;= 10</c>, as the adapter understands it.</param>
/// <param name="LogMessage">A message to log instead of stopping, with expressions in braces, or <see langword="null"/>.</param>
public sealed record DapSourceBreakpoint(int Line, string? Condition = null, string? HitCondition = null, string? LogMessage = null);

/// <summary>A breakpoint as the adapter set it.</summary>
/// <param name="Id">Its identifier, which <c>breakpoint</c> events and <c>stopped</c> events name.</param>
/// <param name="IsVerified">Whether the adapter could set it, so it can stop the program.</param>
/// <param name="Line">The one-based line it was set on, which may differ from the requested one.</param>
/// <param name="Message">Why it could not be set, or more about it.</param>
public sealed record DapBreakpoint(int? Id, bool IsVerified, int? Line, string? Message);

/// <summary>A file the adapter reads some of another file's breakpoints from (<see cref="DebugAdapterSession.BreakpointSources"/>).</summary>
/// <param name="FilePath">The file to send the breakpoints for.</param>
/// <param name="Indexes">The positions, in the other file's list, of the breakpoints this file gets.</param>
public sealed record DapBreakpointSource(string FilePath, IReadOnlyList<int> Indexes);

/// <summary>A thread of the debugged program.</summary>
/// <param name="Id">Its identifier.</param>
/// <param name="Name">Its name.</param>
public sealed record DapThread(int Id, string Name);

/// <summary>A frame of a thread's call stack.</summary>
/// <param name="Id">Its identifier, for scopes and evaluation.</param>
/// <param name="Name">The function, such as <c>greet</c>.</param>
/// <param name="FilePath">The source file, when it is a file.</param>
/// <param name="Line">One-based line.</param>
/// <param name="Column">One-based column.</param>
/// <param name="EndLine">One-based end line, if known.</param>
/// <param name="EndColumn">One-based end column, if known.</param>
/// <param name="IsSubtle">Whether the adapter marks it as less important, such as library code.</param>
public sealed record DapStackFrame(int Id, string Name, string? FilePath, int Line, int Column, int? EndLine, int? EndColumn, bool IsSubtle)
{
    /// <summary>The source's name, such as <c>Main.kt</c>, which a JVM class records even when the adapter has no path for it.</summary>
    public string? SourceName { get; init; }
}

/// <summary>A scope of a frame, such as its locals or the globals.</summary>
/// <param name="Name">Its name, such as <c>Locals</c>.</param>
/// <param name="VariablesReference">The reference for <c>variables</c>.</param>
/// <param name="IsExpensive">Whether reading it is slow, so it should be read only when asked.</param>
/// <param name="IsLocals">Whether it holds the frame's locals.</param>
public sealed record DapScope(string Name, int VariablesReference, bool IsExpensive, bool IsLocals);

/// <summary>A variable, member or item of the paused program.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Value">Its value, as the adapter formats it.</param>
/// <param name="Type">Its type, if known.</param>
/// <param name="EvaluateName">An expression that finds it again, for Add Watch, if known.</param>
/// <param name="VariablesReference">The reference of its children; 0 when it has none.</param>
public sealed record DapVariable(string Name, string Value, string? Type, string? EvaluateName, int VariablesReference)
{
    /// <summary>Whether it has members or items to show.</summary>
    public bool HasChildren => VariablesReference > 0;
}

/// <summary>The value of an evaluated expression.</summary>
/// <param name="Result">The value, as the adapter formats it.</param>
/// <param name="Type">Its type, if known.</param>
/// <param name="VariablesReference">The reference of its children; 0 when it has none.</param>
public sealed record DapEvaluation(string Result, string? Type, int VariablesReference);

/// <summary>More about the exception the program stopped at.</summary>
/// <param name="ExceptionId">Its identifier, usually its type, such as <c>ZeroDivisionError</c>.</param>
/// <param name="Description">Its description, usually its message.</param>
/// <param name="TypeName">Its full type name, if known.</param>
/// <param name="Message">Its message, if known.</param>
/// <param name="StackTrace">Its stack trace as text, if known.</param>
public sealed record DapExceptionInfo(string ExceptionId, string? Description, string? TypeName, string? Message, string? StackTrace);

/// <summary>Why an expression is evaluated, which adapters may treat differently.</summary>
public enum DapEvaluateContext
{
    /// <summary>For the Watch panel.</summary>
    Watch,

    /// <summary>Typed by the user, as in a debug console; may have side effects.</summary>
    Repl,

    /// <summary>For a DataTip.</summary>
    Hover,
}

/// <summary>The program stopped.</summary>
/// <param name="reason">Why, such as <c>breakpoint</c>, <c>step</c>, <c>exception</c>, <c>pause</c> or <c>entry</c>.</param>
/// <param name="threadId">The thread that stopped, if the adapter says.</param>
/// <param name="allThreadsStopped">Whether every thread stopped.</param>
/// <param name="description">The adapter's description, if any.</param>
/// <param name="text">More about it, such as the exception's message.</param>
/// <param name="hitBreakpointIds">The breakpoints that were hit.</param>
public sealed class DapStoppedEventArgs(string reason, int? threadId, bool allThreadsStopped, string? description, string? text, IReadOnlyList<int> hitBreakpointIds) : EventArgs
{
    /// <summary>Why it stopped.</summary>
    public string Reason { get; } = reason;

    /// <summary>The thread that stopped, if the adapter says.</summary>
    public int? ThreadId { get; } = threadId;

    /// <summary>Whether every thread stopped.</summary>
    public bool AllThreadsStopped { get; } = allThreadsStopped;

    /// <summary>The adapter's description, if any.</summary>
    public string? Description { get; } = description;

    /// <summary>More about it, if any.</summary>
    public string? Text { get; } = text;

    /// <summary>The breakpoints that were hit.</summary>
    public IReadOnlyList<int> HitBreakpointIds { get; } = hitBreakpointIds;
}

/// <summary>Output of the debugged program or the adapter, for the Output panel.</summary>
/// <param name="category">Where it comes from: <c>stdout</c>, <c>stderr</c>, <c>console</c> or <c>important</c>.</param>
/// <param name="output">The text, with its line breaks.</param>
public sealed class DapOutputEventArgs(string category, string output) : EventArgs
{
    /// <summary>Where it comes from.</summary>
    public string Category { get; } = category;

    /// <summary>The text.</summary>
    public string Output { get; } = output;
}

/// <summary>A breakpoint the adapter changed, such as one it verified once the code it is in loaded.</summary>
/// <param name="reason">What happened: <c>changed</c>, <c>new</c> or <c>removed</c>.</param>
/// <param name="breakpoint">The breakpoint.</param>
public sealed class DapBreakpointEventArgs(string reason, DapBreakpoint breakpoint) : EventArgs
{
    /// <summary>What happened.</summary>
    public string Reason { get; } = reason;

    /// <summary>The breakpoint.</summary>
    public DapBreakpoint Breakpoint { get; } = breakpoint;
}

/// <summary>The debugged program exited.</summary>
/// <param name="exitCode">Its exit code.</param>
public sealed class DapExitedEventArgs(int exitCode) : EventArgs
{
    /// <summary>Its exit code.</summary>
    public int ExitCode { get; } = exitCode;
}
