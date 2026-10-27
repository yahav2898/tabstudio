using TabStudio.Swarm;

namespace TabStudio.Live;

/// <summary>
/// Where the host's debugged program is paused, as every participant's debugger shows it: the same statement, call
/// stack and locals. Frames and variables are Swarm's records, with the frames' files on the workspace's relative
/// paths; the members of a value are read from the host when a participant expands it.
/// </summary>
/// <param name="PauseId">The pause's number, which requests about its values name.</param>
/// <param name="StatusText">Why it paused, as the host's status bar says it.</param>
/// <param name="Frames">The call stack, innermost first.</param>
/// <param name="Locals">The variables of the paused frame.</param>
/// <param name="Exception">The exception, when it paused at one about to go unhandled.</param>
public sealed record LiveDebugState(long PauseId, string StatusText, IReadOnlyList<SwarmFrame> Frames, IReadOnlyList<SwarmVariable> Locals, string? Exception);
