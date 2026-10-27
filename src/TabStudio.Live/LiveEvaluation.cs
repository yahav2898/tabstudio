using TabStudio.Swarm;

namespace TabStudio.Live;

/// <summary>The value of a Watch or DataTip expression in the host's paused program, or why there is none.</summary>
/// <param name="Value">The value, when the expression found one.</param>
/// <param name="Error">Why not, otherwise.</param>
public sealed record LiveEvaluation(SwarmVariable? Value, string? Error);
