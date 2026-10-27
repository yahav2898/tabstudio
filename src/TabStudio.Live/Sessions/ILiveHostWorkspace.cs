using TabStudio.Swarm;

namespace TabStudio.Live.Sessions;

/// <summary>
/// What a host's IDE does for its guests, once the session has checked that they may: open documents' text, saving,
/// the debugger and the Terminal. The session calls it on its thread with full paths inside the shared folder only.
/// </summary>
public interface ILiveHostWorkspace
{
    /// <summary>The editor's text of a file open in the IDE, unsaved edits included; <see langword="null"/> when it is not open.</summary>
    /// <param name="fullPath">The file.</param>
    string? GetOpenText(string fullPath);

    /// <summary>Saves a shared document to its file, as the IDE's Save does.</summary>
    /// <param name="document">The document.</param>
    Task SaveAsync(LiveDocument document);

    /// <summary>Continues, steps or pauses the debugged program, as the debug toolbar does.</summary>
    /// <param name="command">What to do.</param>
    /// <param name="participant">Who asked.</param>
    /// <returns>Whether the debugger could do it now.</returns>
    Task<bool> ControlDebuggerAsync(LiveDebugCommand command, LiveParticipant participant);

    /// <summary>Sets or removes a breakpoint.</summary>
    /// <param name="fullPath">The file.</param>
    /// <param name="line">The one-based line.</param>
    /// <param name="condition">When it stops, or <see langword="null"/> for every time.</param>
    /// <param name="remove">Whether to remove the breakpoint on that line instead.</param>
    /// <param name="participant">Who asked.</param>
    void SetBreakpoint(string fullPath, int line, string? condition, bool remove, LiveParticipant participant);

    /// <summary>Reads the members or items of a value of the paused program.</summary>
    /// <param name="pauseId">The pause, as <see cref="LiveDebugState.PauseId"/> numbers it.</param>
    /// <param name="path">The value's path.</param>
    /// <param name="cancellationToken">Gives up.</param>
    Task<IReadOnlyList<SwarmVariable>> GetVariablesAsync(long pauseId, string path, CancellationToken cancellationToken);

    /// <summary>Evaluates a Watch or DataTip expression in the paused program.</summary>
    /// <param name="pauseId">The pause.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="cancellationToken">Gives up.</param>
    Task<LiveEvaluation> EvaluateAsync(long pauseId, string expression, CancellationToken cancellationToken);

    /// <summary>Types into the Terminal.</summary>
    /// <param name="input">What a guest typed, as the Terminal's keys encode it.</param>
    /// <param name="participant">Who typed it.</param>
    void WriteTerminal(ReadOnlyMemory<byte> input, LiveParticipant participant);
}
