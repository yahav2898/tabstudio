using TabStudio.Live.Files;
using TabStudio.Live.Text;
using TabStudio.Swarm;

namespace TabStudio.Live.Protocol;

// The bodies of a session's messages (see LiveMethods). Paths are relative to the workspace's folder, with '/'.

/// <summary>A guest joining: who it is, and, when it reconnects, its number and the documents it had open.</summary>
/// <param name="Name">The guest's name.</param>
/// <param name="Kind">What it is: <c>phone</c>, <c>tablet</c> or <c>computer</c>.</param>
/// <param name="Previous">Its number in the session before its connection dropped, when it reconnects.</param>
/// <param name="Documents">The documents it had open, with what it has of each, when it reconnects.</param>
internal sealed record LiveJoinRequest(string Name, string Kind, int? Previous, IReadOnlyList<LiveDocumentVersion>? Documents);

/// <summary>Which edits of a document a guest has.</summary>
/// <param name="Path">The document.</param>
/// <param name="Version">How many operations of each participant it applied.</param>
internal sealed record LiveDocumentVersion(string Path, Dictionary<int, long> Version);

/// <summary>The host's answer to a guest that joined.</summary>
/// <param name="Session">The session.</param>
/// <param name="You">The guest's number, which its edits carry.</param>
/// <param name="Participants">Everyone in the session.</param>
/// <param name="Files">The shared files.</param>
/// <param name="Run">What the host runs and shows.</param>
/// <param name="Documents">For a guest that reconnects, the edits it lacks of the documents it had open.</param>
internal sealed record LiveJoinResponse(
    LiveSessionInfo Session,
    int You,
    IReadOnlyList<LiveParticipant> Participants,
    IReadOnlyList<LiveFileEntry> Files,
    LiveRunSnapshot Run,
    IReadOnlyList<LiveDocumentResponse> Documents);

/// <summary>A session.</summary>
/// <param name="Workspace">The shared workspace's name.</param>
/// <param name="Host">The host's name.</param>
/// <param name="Entry">What a guest opens: the solution's path, or <see langword="null"/> for the folder.</param>
/// <param name="Classroom">Whether it is in classroom mode.</param>
/// <param name="Presenter">Who presents, in classroom mode.</param>
internal sealed record LiveSessionInfo(string Workspace, string Host, string? Entry, bool Classroom, int Presenter);

/// <summary>What the host runs and shows, for a guest that joins.</summary>
/// <param name="State">What its program does.</param>
/// <param name="Output">The last of its Output.</param>
/// <param name="Errors">Its Error List.</param>
/// <param name="Debug">Where its debugged program is paused, if it is.</param>
/// <param name="Breakpoints">Every breakpoint.</param>
internal sealed record LiveRunSnapshot(
    LiveRunState State,
    IReadOnlyList<LiveOutput> Output,
    IReadOnlyList<LiveDiagnostic> Errors,
    LiveDebugState? Debug,
    IReadOnlyList<SwarmBreakpoint> Breakpoints);

/// <summary>Shared files a guest fetches.</summary>
/// <param name="Paths">Their paths.</param>
internal sealed record LiveFilesRequest(IReadOnlyList<string> Paths);

/// <summary>The shared files a guest asked for, in its order: each one's entry, or <see langword="null"/> when it is not shared; their bytes are the attachments.</summary>
/// <param name="Files">The entries.</param>
internal sealed record LiveFilesResponse(IReadOnlyList<LiveFileEntry?> Files);

/// <summary>A request about one document.</summary>
/// <param name="Path">The document.</param>
internal sealed record LivePathRequest(string Path);

/// <summary>A document's edits: all of them for a guest that opens it, those it lacks for one that reconnects.</summary>
/// <param name="Path">The document.</param>
/// <param name="Operations">The edits, in an order they can be applied.</param>
/// <param name="Version">Which edits the host has, so the guest sends the ones it lacks.</param>
internal sealed record LiveDocumentResponse(string Path, IReadOnlyList<TextOperation> Operations, Dictionary<int, long> Version);

/// <summary>A save of a document, once the host has the guest's edits up to a version.</summary>
/// <param name="Path">The document.</param>
/// <param name="Version">The edits the guest had when it saved.</param>
internal sealed record LiveSaveRequest(string Path, Dictionary<int, long> Version);

/// <summary>What a guest asks the debugger to do.</summary>
/// <param name="Command">The command.</param>
internal sealed record LiveDebugCommandRequest(LiveDebugCommand Command);

/// <summary>The members or items of a value of the paused program.</summary>
/// <param name="Pause">The pause.</param>
/// <param name="Path">The value's path, as its <see cref="SwarmVariable.Path"/> says it.</param>
internal sealed record LiveVariablesRequest(long Pause, string Path);

/// <summary>Members or items of a value.</summary>
/// <param name="Variables">Them.</param>
internal sealed record LiveVariablesResponse(IReadOnlyList<SwarmVariable> Variables);

/// <summary>A Watch or DataTip expression to evaluate in the paused program.</summary>
/// <param name="Pause">The pause.</param>
/// <param name="Expression">The expression.</param>
internal sealed record LiveEvaluateRequest(long Pause, string Expression);

/// <summary>A breakpoint a guest sets or removes.</summary>
/// <param name="Path">The file.</param>
/// <param name="Line">The one-based line.</param>
/// <param name="Condition">When it stops, or <see langword="null"/> for every time.</param>
/// <param name="Remove">Whether it removes the breakpoint on that line instead.</param>
internal sealed record LiveBreakpointRequest(string Path, int Line, string? Condition, bool Remove);

/// <summary>A guest's hand.</summary>
/// <param name="Raised">Whether it is up.</param>
internal sealed record LiveHandNotice(bool Raised);

/// <summary>Edits of a document.</summary>
/// <param name="Path">The document.</param>
/// <param name="Operations">The edits, in order.</param>
internal sealed record LiveEditNotice(string Path, IReadOnlyList<TextOperation> Operations);

/// <summary>Who is in the session.</summary>
/// <param name="Participants">Everyone, the host first.</param>
/// <param name="Classroom">Whether it is in classroom mode.</param>
/// <param name="Presenter">Who presents.</param>
internal sealed record LiveParticipantsNotice(IReadOnlyList<LiveParticipant> Participants, bool Classroom, int Presenter);

/// <summary>Shared files that changed.</summary>
/// <param name="Changed">Files that changed or appeared.</param>
/// <param name="Removed">Files that went away.</param>
internal sealed record LiveFilesNotice(IReadOnlyList<LiveFileEntry> Changed, IReadOnlyList<string> Removed);

/// <summary>The host's Error List.</summary>
/// <param name="Errors">Its rows.</param>
internal sealed record LiveErrorsNotice(IReadOnlyList<LiveDiagnostic> Errors);

/// <summary>Where the debugged program is paused.</summary>
/// <param name="State">The pause, or <see langword="null"/> when it is not paused.</param>
internal sealed record LiveDebugNotice(LiveDebugState? State);

/// <summary>Every breakpoint.</summary>
/// <param name="Breakpoints">Them, on the workspace's relative paths.</param>
internal sealed record LiveBreakpointsNotice(IReadOnlyList<SwarmBreakpoint> Breakpoints);

/// <summary>Why a guest's session ended.</summary>
/// <param name="Message">In words for the guest.</param>
internal sealed record LiveEndNotice(string Message);
