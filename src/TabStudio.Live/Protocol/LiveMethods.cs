namespace TabStudio.Live.Protocol;

/// <summary>
/// The methods of a session's messages over TabStudio Link. A guest connects to the host, so the guest is Link's client
/// and the host its server; both ask and tell. Paths are always relative to the workspace's folder.
/// </summary>
internal static class LiveMethods
{
    // ---- From a guest ----

    /// <summary>Request: a guest joins (<see cref="LiveJoinRequest"/>), answered with <see cref="LiveJoinResponse"/> and the Terminal's last output as an attachment.</summary>
    public const string Join = "live/join";

    /// <summary>Request: a guest fetches shared files (<see cref="LiveFilesRequest"/>), answered with their bytes as attachments, in order.</summary>
    public const string Files = "live/files";

    /// <summary>Request: a guest opens a shared document (<see cref="LivePathRequest"/>), answered with its edits (<see cref="LiveDocumentResponse"/>).</summary>
    public const string Open = "live/open";

    /// <summary>Request: a guest whose document went wrong loads it again (<see cref="LivePathRequest"/>), answered like <see cref="Open"/>.</summary>
    public const string Reload = "live/reload";

    /// <summary>Notification: a guest closed a document, so it gets no more edits of it (<see cref="LivePathRequest"/>).</summary>
    public const string Close = "live/close";

    /// <summary>Request: a guest saves a document on the host once the host has its edits (<see cref="LiveSaveRequest"/>).</summary>
    public const string Save = "live/save";

    /// <summary>Request: a guest drives the host's debugger (<see cref="LiveDebugCommandRequest"/>).</summary>
    public const string DebugCommand = "live/debug/command";

    /// <summary>Request: a guest expands a value of the paused program (<see cref="LiveVariablesRequest"/>), answered with <see cref="LiveVariablesResponse"/>.</summary>
    public const string Variables = "live/debug/variables";

    /// <summary>Request: a guest evaluates a Watch or DataTip expression (<see cref="LiveEvaluateRequest"/>), answered with <see cref="LiveEvaluation"/>.</summary>
    public const string Evaluate = "live/debug/evaluate";

    /// <summary>Request: a guest sets or removes a breakpoint (<see cref="LiveBreakpointRequest"/>).</summary>
    public const string Breakpoint = "live/breakpoint";

    /// <summary>Notification: a guest's input to the host's Terminal, as an attachment.</summary>
    public const string TerminalInput = "live/terminal/input";

    /// <summary>Notification: a guest raises or lowers its hand (<see cref="LiveHandNotice"/>).</summary>
    public const string Hand = "live/hand";

    /// <summary>Notification: a guest leaves on purpose, before it disconnects.</summary>
    public const string Leave = "live/leave";

    // ---- Both ways ----

    /// <summary>Notification: edits of a document (<see cref="LiveEditNotice"/>).</summary>
    public const string Edit = "live/edit";

    /// <summary>Notification: where a participant is (<see cref="LivePresence"/>); the host passes it on.</summary>
    public const string Presence = "live/presence";

    // ---- From the host ----

    /// <summary>Notification: who is in the session and what each may do (<see cref="LiveParticipantsNotice"/>).</summary>
    public const string Participants = "live/participants";

    /// <summary>Notification: the host refused a guest's edits of a document, which the guest loads again (<see cref="LivePathRequest"/>).</summary>
    public const string Rejected = "live/rejected";

    /// <summary>Notification: shared files changed, appeared or went away on the host (<see cref="LiveFilesNotice"/>).</summary>
    public const string FilesChanged = "live/files/changed";

    /// <summary>Notification: a shared document was saved on the host (<see cref="LiveFilesNotice"/>, with its new entry).</summary>
    public const string Saved = "live/saved";

    /// <summary>Notification: Output of the host (<see cref="LiveOutput"/>).</summary>
    public const string Output = "live/output";

    /// <summary>Notification: the host's Output was cleared.</summary>
    public const string OutputCleared = "live/output/cleared";

    /// <summary>Notification: output of the host's Terminal, as an attachment.</summary>
    public const string Terminal = "live/terminal";

    /// <summary>Notification: the host's Error List (<see cref="LiveErrorsNotice"/>).</summary>
    public const string Errors = "live/errors";

    /// <summary>Notification: what the host runs (<see cref="LiveRunState"/>).</summary>
    public const string Run = "live/run";

    /// <summary>Notification: where the debugged program is paused, or that it is not (<see cref="LiveDebugNotice"/>).</summary>
    public const string Debug = "live/debug";

    /// <summary>Notification: every breakpoint (<see cref="LiveBreakpointsNotice"/>).</summary>
    public const string Breakpoints = "live/breakpoints";

    /// <summary>Notification: the host removed this guest (<see cref="LiveEndNotice"/>).</summary>
    public const string Removed = "live/removed";

    /// <summary>Notification: the host ended the session (<see cref="LiveEndNotice"/>).</summary>
    public const string Ended = "live/ended";
}
