namespace TabStudio.Live;

/// <summary>What the host's IDE is doing, as its status bar says it.</summary>
/// <param name="Phase">What its program is doing.</param>
/// <param name="StatusText">Its status bar's message, such as <c>Paused at a breakpoint · Program.cs, line 12</c>.</param>
public sealed record LiveRunState(LiveRunPhase Phase, string StatusText)
{
    /// <summary>Nothing runs, nothing said.</summary>
    public static LiveRunState Idle { get; } = new(LiveRunPhase.Idle, string.Empty);
}
