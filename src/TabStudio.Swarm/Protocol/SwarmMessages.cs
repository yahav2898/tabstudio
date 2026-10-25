using TabStudio.Compilation.Running;
using TabStudio.Debugging;
using TabStudio.Debugging.Symbols;

namespace TabStudio.Swarm.Protocol;

// The bodies of the swarm's messages (see SwarmMethods). Paths of source files are always the tablet's: the debug
// build was compiled there, and the device only runs it.

/// <summary>A device joining: who it is and how its screen looks.</summary>
/// <param name="Name">The device's name.</param>
/// <param name="Kind">What it is: <c>phone</c>, <c>tablet</c> or <c>computer</c>.</param>
/// <param name="Screen">Its screen.</param>
/// <param name="Platform">Its system, such as <c>Android 14</c>.</param>
internal sealed record SwarmHello(string Name, string Kind, SwarmScreenInfo Screen, string Platform);

/// <summary>The coordinator's answer to a device that joined.</summary>
/// <param name="Coordinator">The coordinator's name, which the device's overlay shows.</param>
internal sealed record SwarmWelcome(string Coordinator);

/// <summary>A file of a build, by the SHA-256 of its bytes.</summary>
/// <param name="Hash">The hash, as lowercase hexadecimal.</param>
/// <param name="Length">The length in bytes.</param>
internal sealed record SwarmFileEntry(string Hash, long Length);

/// <summary>Asks which files of a build a device does not have yet.</summary>
/// <param name="Files">Every file of the build.</param>
internal sealed record SwarmPrepareRequest(IReadOnlyList<SwarmFileEntry> Files);

/// <summary>The files a device lacks, by hash.</summary>
/// <param name="Missing">Their hashes.</param>
internal sealed record SwarmPrepareResponse(IReadOnlyList<string> Missing);

/// <summary>An assembly of the build: the hashes of its image and of its PDB.</summary>
/// <param name="Name">The assembly's name.</param>
/// <param name="Image">The hash of its image.</param>
/// <param name="Symbols">The hash of its portable PDB, if it has one.</param>
internal sealed record SwarmAssemblyEntry(string Name, string Image, string? Symbols);

/// <summary>A package assembly of the build, by file name and hash.</summary>
/// <param name="FileName">Its file name, such as <c>CommunityToolkit.Mvvm.dll</c>, by which the program finds it.</param>
/// <param name="Hash">The hash of the file.</param>
internal sealed record SwarmPackageEntry(string FileName, string Hash);

/// <summary>What a device needs to debug a build: the debugger's map of the build and the breakpoints.</summary>
/// <param name="Statements">Every statement the debugger numbered.</param>
/// <param name="Methods">Every method the debugger numbered.</param>
/// <param name="Breakpoints">The breakpoints.</param>
/// <param name="BreakOnUnhandledExceptions">Whether an exception about to go unhandled pauses the app.</param>
/// <param name="PauseAtStart">Whether the app pauses at its first statement (F10 and F11).</param>
internal sealed record SwarmDebugSetup(
    IReadOnlyList<DebugStatement> Statements,
    IReadOnlyList<DebugMethod> Methods,
    IReadOnlyList<SwarmBreakpoint> Breakpoints,
    bool BreakOnUnhandledExceptions,
    bool PauseAtStart);

/// <summary>How a device opens a web app the tablet serves.</summary>
/// <param name="Secret">The run's secret, which the device's gateway requires of every connection.</param>
/// <param name="LaunchPath">The page to open first, such as <c>/</c> or <c>/swagger</c>.</param>
internal sealed record SwarmWebSetup(string Secret, string LaunchPath);

/// <summary>Runs a build on a device. The attachments are the files the device lacked, in the order of <see cref="Attached"/>.</summary>
/// <param name="Run">The run's number, which the device's notices repeat.</param>
/// <param name="AppName">The app's name.</param>
/// <param name="Kind">How the device runs it.</param>
/// <param name="Entry">The entry assembly.</param>
/// <param name="Dependencies">The assemblies of the projects it references.</param>
/// <param name="Packages">Its package assemblies.</param>
/// <param name="Attached">The hashes of the attachments, in order.</param>
/// <param name="Arguments">Command-line arguments, for console apps.</param>
/// <param name="Debug">How to debug it, or <see langword="null"/> to run it without the debugger.</param>
/// <param name="Web">How to open it, for web apps.</param>
/// <param name="Restart">Starts the app again even when the device runs this run already, so it starts fresh.</param>
internal sealed record SwarmRunRequest(
    long Run,
    string AppName,
    SwarmAppKind Kind,
    SwarmAssemblyEntry Entry,
    IReadOnlyList<SwarmAssemblyEntry> Dependencies,
    IReadOnlyList<SwarmPackageEntry> Packages,
    IReadOnlyList<string> Attached,
    IReadOnlyList<string> Arguments,
    SwarmDebugSetup? Debug,
    SwarmWebSetup? Web,
    bool Restart = false);

/// <summary>Stops a run on a device.</summary>
/// <param name="Run">The run, or 0 for whatever runs.</param>
internal sealed record SwarmStopRequest(long Run);

/// <summary>What a device is doing with the app.</summary>
/// <param name="Run">The run it is about, or 0 when none runs.</param>
/// <param name="State">The state.</param>
/// <param name="AppName">The app's name, while one runs.</param>
/// <param name="Detail">More about the state, such as why a deploy failed.</param>
internal sealed record SwarmStateNotice(long Run, SwarmDeviceState State, string? AppName, string? Detail);

/// <summary>Output of a console app on a device.</summary>
/// <param name="Run">The run.</param>
/// <param name="Text">The text, which may hold several lines.</param>
/// <param name="IsError">Whether it was written to standard error.</param>
internal sealed record SwarmOutputNotice(long Run, string Text, bool IsError);

/// <summary>How an app ended on a device.</summary>
/// <param name="Run">The run.</param>
/// <param name="Outcome">How it ended.</param>
/// <param name="ExitCode">Its exit code.</param>
/// <param name="Fault">The exception it crashed with, if it did.</param>
internal sealed record SwarmExitNotice(long Run, RunOutcome Outcome, int ExitCode, SwarmFault? Fault);

/// <summary>A line typed on the tablet for the console app, or the end of its input.</summary>
/// <param name="Line">The line, or <see langword="null"/> for the end of input.</param>
internal sealed record SwarmConsoleInput(string? Line);

/// <summary>A debugged app paused on a device.</summary>
/// <param name="Run">The run.</param>
/// <param name="Pause">The pause's number on the device, which requests about it repeat.</param>
/// <param name="Reason">Why it paused.</param>
/// <param name="Frames">Its call stack, innermost first.</param>
/// <param name="Exception">The exception, when it paused at one.</param>
/// <param name="ConditionError">Why a breakpoint's condition could not be evaluated, if it could not.</param>
/// <param name="BreakpointFile">The file of the breakpoint it hit, if any.</param>
/// <param name="BreakpointLine">The line of the breakpoint it hit, or 0.</param>
internal sealed record SwarmPauseNotice(
    long Run,
    long Pause,
    PauseReason Reason,
    IReadOnlyList<SwarmFrame> Frames,
    SwarmFault? Exception,
    string? ConditionError,
    string? BreakpointFile,
    int BreakpointLine);

/// <summary>A debugged app continued on a device.</summary>
/// <param name="Run">The run.</param>
/// <param name="Pause">The pause that ended.</param>
internal sealed record SwarmResumedNotice(long Run, long Pause);

/// <summary>Continues or steps a paused app; continuing an app that is still pausing cancels the pause.</summary>
/// <param name="Pause">The pause it is about, or 0 for whatever is paused or pausing.</param>
/// <param name="Mode">How it continues.</param>
internal sealed record SwarmResumeRequest(long Pause, SwarmResumeMode Mode);

/// <summary>Whether the app continued.</summary>
/// <param name="Resumed">Whether it was paused, or pausing, and continued.</param>
internal sealed record SwarmResumeResponse(bool Resumed);

/// <summary>The breakpoints, all of them.</summary>
/// <param name="Breakpoints">The breakpoints.</param>
internal sealed record SwarmBreakpointsRequest(IReadOnlyList<SwarmBreakpoint> Breakpoints);

/// <summary>Asks for the locals of a pause (reference 0) or the members and items of one of its values.</summary>
/// <param name="Pause">The pause.</param>
/// <param name="Reference">0 for the locals, or a <see cref="SwarmVariable.Reference"/>.</param>
internal sealed record SwarmVariablesRequest(long Pause, int Reference);

/// <summary>Variables of a pause.</summary>
/// <param name="Variables">The variables, in order.</param>
internal sealed record SwarmVariablesResponse(IReadOnlyList<SwarmVariable> Variables);

/// <summary>Evaluates an expression in a pause, as the Watch window and DataTips do.</summary>
/// <param name="Pause">The pause.</param>
/// <param name="Expression">The expression: a local and member names or indexes, such as <c>order.Items[0]</c>.</param>
internal sealed record SwarmEvaluateRequest(long Pause, string Expression);

/// <summary>An expression's value, or why there is none.</summary>
/// <param name="Value">The value, when the expression found one.</param>
/// <param name="Error">Why not, otherwise.</param>
internal sealed record SwarmEvaluateResponse(SwarmVariable? Value, string? Error);

/// <summary>Whether a replayed input reached its element.</summary>
/// <param name="Delivered">Whether the element was found and the input delivered.</param>
internal sealed record SwarmInputResponse(bool Delivered);

/// <summary>Whether a device sent a picture of its screen, as the answer's first attachment.</summary>
/// <param name="HasImage">Whether there is one; there is none while no app runs.</param>
internal sealed record SwarmCaptureResponse(bool HasImage);

/// <summary>The layout problems on a device's screen.</summary>
/// <param name="Issues">The problems, most serious first.</param>
internal sealed record SwarmLayoutResponse(IReadOnlyList<SwarmLayoutIssue> Issues);

/// <summary>Asks for the text an element shows.</summary>
/// <param name="ElementPath">The element.</param>
internal sealed record SwarmTextRequest(string ElementPath);

/// <summary>The text an element shows.</summary>
/// <param name="Text">The text, or <see langword="null"/> when there is no such element.</param>
internal sealed record SwarmTextResponse(string? Text);

/// <summary>A short message for a device's screen.</summary>
/// <param name="Text">The message.</param>
internal sealed record SwarmNotice(string Text);

/// <summary>A connection of the web tunnel, by its number on the device.</summary>
/// <param name="Stream">The connection's number.</param>
internal sealed record SwarmStreamNotice(int Stream);

/// <summary>Whether the coordinator reached the web app for a device's connection.</summary>
/// <param name="Connected">Whether it did.</param>
/// <param name="Error">Why not, otherwise.</param>
internal sealed record SwarmConnectResponse(bool Connected, string? Error);
