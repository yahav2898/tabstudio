using System.Text.Json;
using System.Text.Json.Serialization;

namespace TabStudio.LanguageServers.DebugAdapters.Protocol;

// The shapes of the Debug Adapter Protocol, as TabStudio reads and writes them. Property names are converted to
// camelCase by DapJsonContext.

/// <summary>The arguments of <c>initialize</c>: who the client is and what it supports.</summary>
internal sealed record InitializeArgumentsDto(
    [property: JsonPropertyName("clientID")] string ClientId,
    string ClientName,
    [property: JsonPropertyName("adapterID")] string AdapterId,
    string Locale,
    bool LinesStartAt1,
    bool ColumnsStartAt1,
    string PathFormat,
    bool SupportsVariableType,
    bool SupportsVariablePaging,
    bool SupportsRunInTerminalRequest,
    bool SupportsMemoryReferences,
    bool SupportsProgressReporting,
    bool SupportsInvalidatedEvent,
    bool SupportsStartDebuggingRequest,
    bool SupportsArgsCanBeInterpretedByShell);

/// <summary>An exception filter the adapter offers, such as <c>uncaught</c>.</summary>
internal sealed record ExceptionBreakpointsFilterDto(string Filter, string Label, string? Description, bool? Default);

/// <summary>What the adapter can do.</summary>
internal sealed record CapabilitiesDto(
    bool? SupportsConfigurationDoneRequest,
    bool? SupportsConditionalBreakpoints,
    bool? SupportsHitConditionalBreakpoints,
    bool? SupportsLogPoints,
    bool? SupportsEvaluateForHovers,
    bool? SupportsSetVariable,
    bool? SupportsSetExpression,
    bool? SupportsExceptionInfoRequest,
    bool? SupportsTerminateRequest,
    bool? SupportTerminateDebuggee,
    bool? SupportsRestartRequest,
    bool? SupportsDelayedStackTraceLoading,
    bool? SupportsSingleThreadExecutionRequests,
    IReadOnlyList<ExceptionBreakpointsFilterDto>? ExceptionBreakpointFilters);

/// <summary>A source file.</summary>
internal sealed record SourceDto(string? Name, string? Path, int? SourceReference, string? PresentationHint);

/// <summary>A breakpoint the client sets on a line.</summary>
internal sealed record SourceBreakpointDto(int Line, int? Column, string? Condition, string? HitCondition, string? LogMessage);

/// <summary>The arguments of <c>setBreakpoints</c>: every breakpoint of one file.</summary>
internal sealed record SetBreakpointsArgumentsDto(SourceDto Source, IReadOnlyList<SourceBreakpointDto> Breakpoints, bool SourceModified);

/// <summary>A breakpoint as the adapter set it.</summary>
internal sealed record BreakpointDto(int? Id, bool Verified, string? Message, SourceDto? Source, int? Line, int? Column);

/// <summary>The body of the <c>setBreakpoints</c> response.</summary>
internal sealed record SetBreakpointsBodyDto(IReadOnlyList<BreakpointDto>? Breakpoints);

/// <summary>The arguments of <c>setExceptionBreakpoints</c>.</summary>
internal sealed record SetExceptionBreakpointsArgumentsDto(IReadOnlyList<string> Filters);

/// <summary>The arguments of requests about one thread, such as <c>continue</c> and <c>next</c>.</summary>
internal sealed record ThreadArgumentsDto(int ThreadId);

/// <summary>A thread.</summary>
internal sealed record ThreadDto(int Id, string? Name);

/// <summary>The body of the <c>threads</c> response.</summary>
internal sealed record ThreadsBodyDto(IReadOnlyList<ThreadDto>? Threads);

/// <summary>The arguments of <c>stackTrace</c>.</summary>
internal sealed record StackTraceArgumentsDto(int ThreadId, int? StartFrame, int? Levels);

/// <summary>A frame of a call stack.</summary>
internal sealed record StackFrameDto(int Id, string? Name, SourceDto? Source, int Line, int Column, int? EndLine, int? EndColumn, string? PresentationHint);

/// <summary>The body of the <c>stackTrace</c> response.</summary>
internal sealed record StackTraceBodyDto(IReadOnlyList<StackFrameDto>? StackFrames, int? TotalFrames);

/// <summary>The arguments of <c>scopes</c>.</summary>
internal sealed record ScopesArgumentsDto(int FrameId);

/// <summary>A scope of a frame, such as its locals.</summary>
internal sealed record ScopeDto(string? Name, string? PresentationHint, int VariablesReference, bool? Expensive);

/// <summary>The body of the <c>scopes</c> response.</summary>
internal sealed record ScopesBodyDto(IReadOnlyList<ScopeDto>? Scopes);

/// <summary>The arguments of <c>variables</c>.</summary>
internal sealed record VariablesArgumentsDto(int VariablesReference);

/// <summary>A variable, a member or an item.</summary>
internal sealed record VariableDto(string? Name, string? Value, string? Type, string? EvaluateName, int VariablesReference);

/// <summary>The body of the <c>variables</c> response.</summary>
internal sealed record VariablesBodyDto(IReadOnlyList<VariableDto>? Variables);

/// <summary>The arguments of <c>evaluate</c>.</summary>
internal sealed record EvaluateArgumentsDto(string Expression, int? FrameId, string Context);

/// <summary>The body of the <c>evaluate</c> response.</summary>
internal sealed record EvaluateBodyDto(string? Result, string? Type, int VariablesReference);

/// <summary>The arguments of <c>setVariable</c>.</summary>
internal sealed record SetVariableArgumentsDto(int VariablesReference, string Name, string Value);

/// <summary>The arguments of <c>setExpression</c>.</summary>
internal sealed record SetExpressionArgumentsDto(string Expression, string Value, int? FrameId);

/// <summary>The body of the <c>setVariable</c> and <c>setExpression</c> responses.</summary>
internal sealed record SetValueBodyDto(string? Value, string? Type, int? VariablesReference);

/// <summary>Details of an exception.</summary>
internal sealed record ExceptionDetailsDto(string? Message, string? TypeName, string? FullTypeName, string? StackTrace);

/// <summary>The body of the <c>exceptionInfo</c> response.</summary>
internal sealed record ExceptionInfoBodyDto(string? ExceptionId, string? Description, string? BreakMode, ExceptionDetailsDto? Details);

/// <summary>The arguments of <c>disconnect</c>.</summary>
internal sealed record DisconnectArgumentsDto(bool Restart, bool TerminateDebuggee);

/// <summary>The arguments of <c>terminate</c>.</summary>
internal sealed record TerminateArgumentsDto(bool Restart);

/// <summary>The body of the <c>stopped</c> event.</summary>
internal sealed record StoppedEventDto(string? Reason, string? Description, int? ThreadId, string? Text, bool? AllThreadsStopped, IReadOnlyList<int>? HitBreakpointIds);

/// <summary>The body of the <c>continued</c> event.</summary>
internal sealed record ContinuedEventDto(int ThreadId, bool? AllThreadsContinued);

/// <summary>The body of the <c>exited</c> event.</summary>
internal sealed record ExitedEventDto(int ExitCode);

/// <summary>The body of the <c>output</c> event.</summary>
internal sealed record OutputEventDto(string? Category, string? Output, SourceDto? Source, int? Line);

/// <summary>The body of the <c>thread</c> event.</summary>
internal sealed record ThreadEventDto(string? Reason, int ThreadId);

/// <summary>The body of the <c>breakpoint</c> event.</summary>
internal sealed record BreakpointEventDto(string? Reason, BreakpointDto? Breakpoint);

/// <summary>The body of the <c>process</c> event.</summary>
internal sealed record ProcessEventDto(string? Name, int? SystemProcessId);

/// <summary>The arguments of the <c>startDebugging</c> reverse request.</summary>
internal sealed record StartDebuggingArgumentsDto(JsonElement Configuration, string? Request);
