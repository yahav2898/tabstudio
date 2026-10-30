using System.Text.RegularExpressions;

namespace TabStudio.Live;

/// <summary>
/// What each participant may do, in one place: the host checks every request from a guest against these rules, and
/// a guest's IDE uses the same rules to show only what it may do. A guest's own device is never trusted to enforce
/// them: the host refuses what they forbid.
/// </summary>
public static partial class LivePermissions
{
    /// <summary>Whether a participant may change the shared documents: the host, and guests the host made editors.</summary>
    /// <param name="participant">The participant.</param>
    public static bool CanEdit(LiveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return participant.Role is LiveRole.Host or LiveRole.Editor;
    }

    /// <summary>Whether a participant may save a shared document on the host: whoever may edit it.</summary>
    /// <param name="participant">The participant.</param>
    public static bool CanSave(LiveParticipant participant) => CanEdit(participant);

    /// <summary>Whether a participant may set and remove breakpoints: whoever may edit, since breakpoints change the run.</summary>
    /// <param name="participant">The participant.</param>
    public static bool CanSetBreakpoints(LiveParticipant participant) => CanEdit(participant);

    /// <summary>
    /// Whether a participant may continue, step and pause the debugged program: the host, and guests the host allowed
    /// to. Everyone sees the same pause either way.
    /// </summary>
    /// <param name="participant">The participant.</param>
    public static bool CanControlDebugger(LiveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return participant.Role == LiveRole.Host || participant.CanStep;
    }

    /// <summary>
    /// Whether a participant may evaluate an expression in the paused program: anyone may read a variable, such as
    /// <c>order.Lines[0].Price</c>, and whoever may control the debugger may evaluate anything. A debug adapter's
    /// evaluation runs code (debugpy's is Python's <c>eval</c>), so a viewer's Watch could otherwise run any program
    /// on the host.
    /// </summary>
    /// <param name="participant">The participant.</param>
    /// <param name="expression">The Watch or DataTip expression.</param>
    public static bool CanEvaluate(LiveParticipant participant, string expression)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(expression);
        return CanControlDebugger(participant) || VariablePath().IsMatch(expression.Trim());
    }

    /// <summary>Whether a participant may type in the host's Terminal: the host, and guests the host allowed to.</summary>
    /// <param name="participant">The participant.</param>
    public static bool CanUseTerminal(LiveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return participant.Role == LiveRole.Host || participant.CanUseTerminal;
    }

    /// <summary>Whether a participant may build and run (F5, Ctrl+F5): the host only, since everything runs on the host.</summary>
    /// <param name="participant">The participant.</param>
    public static bool CanRun(LiveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return participant.Role == LiveRole.Host;
    }

    /// <summary>Whether a participant may change what others may do, remove them and end the session: the host only.</summary>
    /// <param name="participant">The participant.</param>
    public static bool CanManage(LiveParticipant participant)
    {
        ArgumentNullException.ThrowIfNull(participant);
        return participant.Role == LiveRole.Host;
    }

    /// <summary>
    /// Whether a participant may read a file of the workspace: anyone in the session, for a file the host shares. The
    /// path must be one the host listed, which never leads outside the workspace's folder.
    /// </summary>
    /// <param name="participant">The participant.</param>
    /// <param name="relativePath">The file's path in the workspace.</param>
    /// <param name="isShared">Whether the host shares that path.</param>
    public static bool CanRead(LiveParticipant participant, string? relativePath, Func<string, bool> isShared)
    {
        ArgumentNullException.ThrowIfNull(participant);
        ArgumentNullException.ThrowIfNull(isShared);
        return participant.IsConnected && Files.LiveWorkspacePaths.Normalize(relativePath) is { } path && isShared(path);
    }

    [GeneratedRegex(@"^[\p{L}_$][\p{L}\p{Nd}_$]*(?:\.[\p{L}_$][\p{L}\p{Nd}_$]*|\[\d{1,9}\])*$", RegexOptions.CultureInvariant)]
    private static partial Regex VariablePath();
}
