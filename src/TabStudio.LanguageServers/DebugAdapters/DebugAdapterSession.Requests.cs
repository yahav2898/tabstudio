using System.Text.Json;
using TabStudio.LanguageServers.DebugAdapters.Protocol;

namespace TabStudio.LanguageServers.DebugAdapters;

/// <summary>The requests of a debug session: breakpoints, execution, and inspecting the paused program.</summary>
public sealed partial class DebugAdapterSession
{
    /// <summary>Applies saved Flutter code over the adapter's VM service, optionally resetting app state.</summary>
    public Task<System.Text.Json.JsonElement> ReloadFlutterAsync(bool fullRestart, string reason, CancellationToken cancellationToken) =>
        _connection.SendRequestAsync(fullRestart ? "hotRestart" : "hotReload", new System.Text.Json.Nodes.JsonObject { ["reason"] = reason }, cancellationToken);

    /// <summary>Replaces the breakpoints of a file. Conditions, hit counts and log messages the adapter does not support are left out.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="breakpoints">Every breakpoint of the file.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The breakpoints as the adapter set them, in the same order.</returns>
    public async Task<IReadOnlyList<DapBreakpoint>> SetBreakpointsAsync(string filePath, IReadOnlyList<DapSourceBreakpoint> breakpoints, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(breakpoints);
        if (BreakpointSources?.Invoke(filePath, breakpoints) is { } sources)
        {
            return await SetMappedBreakpointsAsync(filePath, breakpoints, sources, cancellationToken).ConfigureAwait(false);
        }

        return await SendBreakpointsAsync(filePath, breakpoints, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a file's breakpoints for the files <see cref="BreakpointSources"/> named, clearing those it named before
    /// and not now, and puts the results back in the order of the file's breakpoints.
    /// </summary>
    private async Task<IReadOnlyList<DapBreakpoint>> SetMappedBreakpointsAsync(string filePath, IReadOnlyList<DapSourceBreakpoint> breakpoints,
        IReadOnlyList<DapBreakpointSource> sources, CancellationToken cancellationToken)
    {
        var results = new DapBreakpoint?[breakpoints.Count];
        var sent = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            var subset = source.Indexes.Select(i => breakpoints[i]).ToList();
            var set = await SendBreakpointsAsync(source.FilePath, subset, cancellationToken).ConfigureAwait(false);
            for (var i = 0; i < source.Indexes.Count && i < set.Count; i++)
            {
                // The adapter knows the line in its source; it is the same line of the file the breakpoint is in.
                results[source.Indexes[i]] = set[i] with { Line = set[i].Line ?? breakpoints[source.Indexes[i]].Line };
            }

            sent.Add(source.FilePath);
        }

        lock (_mappedSources)
        {
            if (_mappedSources.TryGetValue(filePath, out var before))
            {
                before.ExceptWith(sent);
                sent.UnionWith(before);
            }
        }

        foreach (var stale in sent.Where(path => !sources.Any(s => string.Equals(s.FilePath, path, StringComparison.Ordinal))).ToList())
        {
            await SendBreakpointsAsync(stale, [], cancellationToken).ConfigureAwait(false);
            sent.Remove(stale);
        }

        lock (_mappedSources)
        {
            _mappedSources[filePath] = sent;
        }

        return [.. results.Select((result, index) => result ?? new DapBreakpoint(null, false, breakpoints[index].Line, null))];
    }

    private async Task<IReadOnlyList<DapBreakpoint>> SendBreakpointsAsync(string filePath, IReadOnlyList<DapSourceBreakpoint> breakpoints, CancellationToken cancellationToken)
    {
        var capabilities = Capabilities;
        var arguments = new SetBreakpointsArgumentsDto(
            new SourceDto(Path.GetFileName(filePath), filePath, null, null),
            breakpoints.Select(b => new SourceBreakpointDto(
                b.Line,
                null,
                capabilities.ConditionalBreakpoints ? b.Condition : null,
                capabilities.HitConditionalBreakpoints ? b.HitCondition : null,
                capabilities.LogPoints ? b.LogMessage : null)).ToList(),
            false);
        var body = await _connection.SendRequestAsync("setBreakpoints", arguments, DapJsonContext.Default.SetBreakpointsArgumentsDto, DapJsonContext.Default.SetBreakpointsBodyDto, cancellationToken).ConfigureAwait(false);
        return (body?.Breakpoints ?? []).Select(ToBreakpoint).ToList();
    }

    /// <summary>
    /// Where the adapter reads a source file's breakpoints from, when that is not the file itself: given a file and
    /// its breakpoints, the files to send them for, each with the indexes of the breakpoints it gets, such as the Java
    /// sources that tell Java's debugger which class each line of a Kotlin file compiles into. A <see langword="null"/>
    /// answer sends them for the file itself.
    /// </summary>
    public Func<string, IReadOnlyList<DapSourceBreakpoint>, IReadOnlyList<DapBreakpointSource>?>? BreakpointSources { get; set; }

    private readonly Dictionary<string, HashSet<string>> _mappedSources = new(StringComparer.Ordinal);

    /// <summary>Turns on the exception filters given, and off the others.</summary>
    /// <param name="filters">The filters, from <see cref="DapCapabilities.ExceptionFilters"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task SetExceptionBreakpointsAsync(IReadOnlyList<string> filters, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filters);
        return _connection.SendRequestAsync("setExceptionBreakpoints", new SetExceptionBreakpointsArgumentsDto(filters), DapJsonContext.Default.SetExceptionBreakpointsArgumentsDto, cancellationToken);
    }

    /// <summary>Lets the program run again (F5).</summary>
    /// <param name="threadId">The thread that was stopped.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task ContinueAsync(int threadId, CancellationToken cancellationToken) => ThreadRequestAsync("continue", threadId, cancellationToken);

    /// <summary>Runs the current line and stops at the next one in the same function (F10).</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task StepOverAsync(int threadId, CancellationToken cancellationToken) => ThreadRequestAsync("next", threadId, cancellationToken);

    /// <summary>Steps into the call on the current line (F11).</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task StepIntoAsync(int threadId, CancellationToken cancellationToken) => ThreadRequestAsync("stepIn", threadId, cancellationToken);

    /// <summary>Runs to the end of the current function and stops in its caller (Shift+F11).</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task StepOutAsync(int threadId, CancellationToken cancellationToken) => ThreadRequestAsync("stepOut", threadId, cancellationToken);

    /// <summary>Pauses a running thread (Break All).</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task PauseAsync(int threadId, CancellationToken cancellationToken) => ThreadRequestAsync("pause", threadId, cancellationToken);

    /// <summary>The program's threads.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<DapThread>> GetThreadsAsync(CancellationToken cancellationToken)
    {
        var body = await _connection.SendRequestAsync("threads", null, cancellationToken).ConfigureAwait(false);
        var threads = Read(body, DapJsonContext.Default.ThreadsBodyDto)?.Threads ?? [];
        return threads.Select(t => new DapThread(t.Id, string.IsNullOrEmpty(t.Name) ? $"Thread {t.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}" : t.Name)).ToList();
    }

    /// <summary>A thread's call stack, innermost frame first.</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<DapStackFrame>> GetStackTraceAsync(int threadId, CancellationToken cancellationToken)
    {
        var body = await _connection.SendRequestAsync("stackTrace", new StackTraceArgumentsDto(threadId, 0, 200), DapJsonContext.Default.StackTraceArgumentsDto, DapJsonContext.Default.StackTraceBodyDto, cancellationToken).ConfigureAwait(false);
        return (body?.StackFrames ?? [])
            .Select(f => new DapStackFrame(
                f.Id,
                f.Name ?? "?",
                f.Source?.Path,
                Math.Max(1, f.Line),
                Math.Max(1, f.Column),
                f.EndLine,
                f.EndColumn,
                f.PresentationHint is "subtle" or "label" || f.Source?.PresentationHint == "deemphasize")
            {
                SourceName = f.Source?.Name,
            })
            .ToList();
    }

    /// <summary>The scopes of a frame, such as its locals and the globals.</summary>
    /// <param name="frameId">The frame.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<DapScope>> GetScopesAsync(int frameId, CancellationToken cancellationToken)
    {
        var body = await _connection.SendRequestAsync("scopes", new ScopesArgumentsDto(frameId), DapJsonContext.Default.ScopesArgumentsDto, DapJsonContext.Default.ScopesBodyDto, cancellationToken).ConfigureAwait(false);
        return (body?.Scopes ?? [])
            .Select((s, index) => new DapScope(
                s.Name ?? "Scope",
                s.VariablesReference,
                s.Expensive == true,
                s.PresentationHint == "locals" || (index == 0 && s.PresentationHint is null && s.Name is not null && s.Name.StartsWith("Local", StringComparison.OrdinalIgnoreCase))))
            .ToList();
    }

    /// <summary>The children of a scope, variable or evaluated value.</summary>
    /// <param name="variablesReference">Their reference.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<DapVariable>> GetVariablesAsync(int variablesReference, CancellationToken cancellationToken)
    {
        var body = await _connection.SendRequestAsync("variables", new VariablesArgumentsDto(variablesReference), DapJsonContext.Default.VariablesArgumentsDto, DapJsonContext.Default.VariablesBodyDto, cancellationToken).ConfigureAwait(false);
        return (body?.Variables ?? [])
            .Select(v => new DapVariable(v.Name ?? string.Empty, v.Value ?? string.Empty, string.IsNullOrEmpty(v.Type) ? null : v.Type, v.EvaluateName, v.VariablesReference))
            .ToList();
    }

    /// <summary>Evaluates an expression in a frame.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="frameId">The frame, or <see langword="null"/> for the global scope.</param>
    /// <param name="context">Why it is evaluated.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <exception cref="DebugAdapterException">The expression could not be evaluated, with the adapter's reason.</exception>
    public async Task<DapEvaluation> EvaluateAsync(string expression, int? frameId, DapEvaluateContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var name = context switch
        {
            DapEvaluateContext.Hover => Capabilities.EvaluateForHovers ? "hover" : "watch",
            DapEvaluateContext.Repl => "repl",
            _ => "watch",
        };
        var body = await _connection.SendRequestAsync("evaluate", new EvaluateArgumentsDto(expression, frameId, name), DapJsonContext.Default.EvaluateArgumentsDto, DapJsonContext.Default.EvaluateBodyDto, cancellationToken).ConfigureAwait(false);
        return new DapEvaluation(body?.Result ?? string.Empty, body?.Type is { Length: > 0 } type ? type : null, body?.VariablesReference ?? 0);
    }

    /// <summary>Changes the value of a variable.</summary>
    /// <param name="containerReference">The reference of the scope or value the variable belongs to.</param>
    /// <param name="name">The variable's name.</param>
    /// <param name="value">The new value, as an expression.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The variable with its new value.</returns>
    /// <exception cref="DebugAdapterException">The value could not be set, with the adapter's reason.</exception>
    public async Task<DapVariable> SetVariableAsync(int containerReference, string name, string value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        var body = await _connection.SendRequestAsync("setVariable", new SetVariableArgumentsDto(containerReference, name, value), DapJsonContext.Default.SetVariableArgumentsDto, DapJsonContext.Default.SetValueBodyDto, cancellationToken).ConfigureAwait(false);
        return new DapVariable(name, body?.Value ?? value, body?.Type, null, body?.VariablesReference ?? 0);
    }

    /// <summary>Assigns a value to an expression, such as a watch.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="value">The new value, as an expression.</param>
    /// <param name="frameId">The frame, or <see langword="null"/> for the global scope.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The expression's new value.</returns>
    /// <exception cref="DebugAdapterException">The value could not be set, with the adapter's reason.</exception>
    public async Task<DapEvaluation> SetExpressionAsync(string expression, string value, int? frameId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(value);
        var body = await _connection.SendRequestAsync("setExpression", new SetExpressionArgumentsDto(expression, value, frameId), DapJsonContext.Default.SetExpressionArgumentsDto, DapJsonContext.Default.SetValueBodyDto, cancellationToken).ConfigureAwait(false);
        return new DapEvaluation(body?.Value ?? value, body?.Type, body?.VariablesReference ?? 0);
    }

    /// <summary>More about the exception a thread stopped at, when the adapter can tell.</summary>
    /// <param name="threadId">The thread.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<DapExceptionInfo?> GetExceptionInfoAsync(int threadId, CancellationToken cancellationToken)
    {
        if (!Capabilities.ExceptionInfo)
        {
            return null;
        }

        var body = await _connection.SendRequestAsync("exceptionInfo", new ThreadArgumentsDto(threadId), DapJsonContext.Default.ThreadArgumentsDto, DapJsonContext.Default.ExceptionInfoBodyDto, cancellationToken).ConfigureAwait(false);
        return body is null
            ? null
            : new DapExceptionInfo(body.ExceptionId ?? "Exception", body.Description, body.Details?.FullTypeName ?? body.Details?.TypeName, body.Details?.Message, body.Details?.StackTrace);
    }

    /// <summary>Stops the program (Shift+F5): gracefully when the adapter can, then disconnects.</summary>
    /// <param name="cancellationToken">Gives up waiting for the program to end.</param>
    public async Task TerminateAsync(CancellationToken cancellationToken)
    {
        if (HasEnded)
        {
            return;
        }

        try
        {
            if (Capabilities.Terminate)
            {
                await _connection.SendRequestAsync("terminate", new TerminateArgumentsDto(false), DapJsonContext.Default.TerminateArgumentsDto, cancellationToken).ConfigureAwait(false);
                await Task.WhenAny(_terminated.Task, Task.Delay(TimeSpan.FromSeconds(3), cancellationToken)).ConfigureAwait(false);
            }

            await _connection.SendRequestAsync("disconnect", new DisconnectArgumentsDto(false, true), DapJsonContext.Default.DisconnectArgumentsDto, cancellationToken).ConfigureAwait(false);
        }
        catch (DebugAdapterException)
        {
            // The adapter is gone already, or refused: disposing the session ends it.
        }

        End();
    }

    private Task<JsonElement> ThreadRequestAsync(string command, int threadId, CancellationToken cancellationToken) =>
        _connection.SendRequestAsync(command, new ThreadArgumentsDto(threadId), DapJsonContext.Default.ThreadArgumentsDto, cancellationToken);
}
