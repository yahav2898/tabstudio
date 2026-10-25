using TabStudio.Debugging;
using TabStudio.Debugging.Symbols;
using TabStudio.Link;
using TabStudio.Swarm.Protocol;

namespace TabStudio.Swarm.Coordinator;

/// <summary>
/// Where a debugged app is paused on a device of the swarm: its call stack and why it stopped, and requests for its
/// locals, the members of its values and Watch expressions, which the device evaluates in its paused program.
/// </summary>
/// <remarks>Requests about a pause that ended fail with <see cref="LinkRemoteException"/>. Thread-safe.</remarks>
public sealed class SwarmPause
{
    internal SwarmPause(SwarmDevice device, SwarmPauseNotice notice)
    {
        Device = device;
        Id = notice.Pause;
        RunId = notice.Run;
        Reason = notice.Reason;
        Frames = notice.Frames;
        Exception = notice.Exception;
        ConditionError = notice.ConditionError;
        BreakpointFile = notice.BreakpointFile;
        BreakpointLine = notice.BreakpointLine;
    }

    /// <summary>The device the app is paused on.</summary>
    public SwarmDevice Device { get; }

    /// <summary>The pause's number on the device.</summary>
    public long Id { get; }

    /// <summary>The run the app belongs to.</summary>
    public long RunId { get; }

    /// <summary>Why it paused.</summary>
    public PauseReason Reason { get; }

    /// <summary>The call stack, innermost first.</summary>
    public IReadOnlyList<SwarmFrame> Frames { get; }

    /// <summary>The statement about to run, or for an exception the statement that threw.</summary>
    public DebugSourceSpan? Location => Frames.Count > 0 ? Frames[0].Location : null;

    /// <summary>The exception, when it paused at one about to go unhandled.</summary>
    public SwarmFault? Exception { get; }

    /// <summary>Why the condition of the breakpoint it hit could not be evaluated, if it could not.</summary>
    public string? ConditionError { get; }

    /// <summary>The file of the breakpoint it hit, if any.</summary>
    public string? BreakpointFile { get; }

    /// <summary>The line of the breakpoint it hit, or 0.</summary>
    public int BreakpointLine { get; }

    /// <summary>Whether the app is still paused here.</summary>
    public bool IsCurrent => ReferenceEquals(Device.Pause, this);

    /// <summary>Reads the variables of the top frame: <c>this</c>, the parameters and the locals in scope.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<SwarmVariable>> GetLocalsAsync(CancellationToken cancellationToken) => GetVariablesAsync(0, cancellationToken);

    /// <summary>Reads the members or items of a value.</summary>
    /// <param name="variable">The value, from this pause.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task<IReadOnlyList<SwarmVariable>> GetChildrenAsync(SwarmVariable variable, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(variable);
        return variable.HasChildren ? GetVariablesAsync(variable.Reference, cancellationToken) : Task.FromResult<IReadOnlyList<SwarmVariable>>([]);
    }

    /// <summary>Evaluates a Watch or DataTip expression: a local, then member names and indexes, such as <c>order.Items[0]</c>.</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The value, or why there is none.</returns>
    public async Task<SwarmEvaluation> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var connection = Device.Connection ?? throw Device.Offline();
        var answer = await connection.RequestAsync(
            SwarmMethods.Evaluate,
            SwarmJson.Body(new SwarmEvaluateRequest(Id, expression), SwarmJsonContext.Default.SwarmEvaluateRequest),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var result = answer.Read(SwarmJsonContext.Default.SwarmEvaluateResponse);
        return new SwarmEvaluation(result.Value, result.Error);
    }

    /// <summary>Continues or steps the app from this pause.</summary>
    /// <param name="mode">How it continues.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether it was still paused here and continued.</returns>
    public Task<bool> ResumeAsync(SwarmResumeMode mode, CancellationToken cancellationToken) => Device.ResumeAsync(Id, mode, cancellationToken);

    private async Task<IReadOnlyList<SwarmVariable>> GetVariablesAsync(int reference, CancellationToken cancellationToken)
    {
        var connection = Device.Connection ?? throw Device.Offline();
        var answer = await connection.RequestAsync(
            SwarmMethods.Variables,
            SwarmJson.Body(new SwarmVariablesRequest(Id, reference), SwarmJsonContext.Default.SwarmVariablesRequest),
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return answer.Read(SwarmJsonContext.Default.SwarmVariablesResponse).Variables;
    }
}

/// <summary>The value of a Watch or DataTip expression in a paused device, or why there is none.</summary>
/// <param name="Value">The value, when the expression found one.</param>
/// <param name="Error">Why not, otherwise.</param>
public sealed record SwarmEvaluation(SwarmVariable? Value, string? Error);
