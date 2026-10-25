using TabStudio.Core.Diagnostics;
using TabStudio.WindowsBridge.Input;

namespace TabStudio.WindowsBridge.Protocol;

// The bodies of the Windows Bridge's messages. Paths are always the tablet's: the bridge maps them to its mirror of
// the workspace on the way in and back on the way out. Window sizes and positions are the PC's pixels.

/// <summary>What the PC is, what it can build, and whether it is awake.</summary>
/// <param name="PcName">The PC's name, such as <c>DESKTOP-7Q2M</c>.</param>
/// <param name="OperatingSystem">Its Windows version, such as <c>Windows 11 (10.0.26100)</c>.</param>
/// <param name="Toolchains">The SDKs and tools it found.</param>
/// <param name="Capabilities">What those tools let it build and debug.</param>
/// <param name="State">Whether it is awake, locked or about to sleep.</param>
public sealed record BridgeInfo(
    string PcName,
    string OperatingSystem,
    IReadOnlyList<BridgeToolchain> Toolchains,
    BridgeCapabilities Capabilities,
    BridgePcState State);

/// <summary>What the bridge tells the tablet whenever it changes: the PC's state, its work and its running programs.</summary>
/// <param name="State">Whether the PC is awake, locked or about to sleep.</param>
/// <param name="Activity">What the bridge is doing, such as <c>Building HelloWpf</c>, or <see langword="null"/> when idle.</param>
/// <param name="Apps">The programs running for the tablet.</param>
public sealed record BridgeStatus(BridgePcState State, string? Activity, IReadOnlyList<BridgeAppInfo> Apps);

/// <summary>A program running on the PC for the tablet.</summary>
/// <param name="AppId">The bridge's number for it.</param>
/// <param name="Name">Its name, such as <c>HelloWpf</c>.</param>
/// <param name="ProcessId">Its process on the PC.</param>
/// <param name="ProjectPath">The project it was built from.</param>
/// <param name="IsDebugging">Whether the debugger started it.</param>
public sealed record BridgeAppInfo(int AppId, string Name, int ProcessId, string ProjectPath, bool IsDebugging);

/// <summary>Builds a project on the PC.</summary>
/// <param name="BuildId">The tablet's number for the build, which its output lines carry.</param>
/// <param name="ProjectPath">The project file.</param>
/// <param name="Configuration"><c>Debug</c> or <c>Release</c>.</param>
/// <param name="SkipReferences">Whether only this project changed, so the projects it references need no build.</param>
public sealed record BuildRequest(int BuildId, string ProjectPath, string Configuration, bool SkipReferences);

/// <summary>A line of a build's output.</summary>
/// <param name="BuildId">The build.</param>
/// <param name="Text">The line, with its paths on the tablet's.</param>
public sealed record BuildOutputLine(int BuildId, string Text);

/// <summary>How a build on the PC went.</summary>
/// <param name="Success">Whether it built.</param>
/// <param name="Diagnostics">Its errors and warnings, with their codes, on the tablet's paths.</param>
/// <param name="Duration">How long it took.</param>
/// <param name="Tool">What built it, such as <c>dotnet build</c> or <c>MSBuild 17.14</c>.</param>
/// <param name="Program">What it built, when it built a program.</param>
public sealed record BuildOutcome(bool Success, IReadOnlyList<BuildDiagnostic> Diagnostics, TimeSpan Duration, string Tool, BuiltProgram? Program);

/// <summary>A program a build made.</summary>
/// <param name="Path">The program, on the tablet's paths: the assembly of a .NET app, the executable of a native one.</param>
/// <param name="Executable">The executable that starts it: a .NET app's own launcher, or <see cref="Path"/> for a native one.</param>
/// <param name="IsDotNet">Whether it is a .NET app, which netcoredbg can debug.</param>
public sealed record BuiltProgram(string Path, string? Executable, bool IsDotNet);

/// <summary>Runs a built program on the PC.</summary>
/// <param name="ProjectPath">The project it was built from.</param>
/// <param name="Program">The program.</param>
/// <param name="Arguments">Its command-line arguments.</param>
public sealed record RunRequest(string ProjectPath, BuiltProgram Program, IReadOnlyList<string> Arguments);

/// <summary>A program that started on the PC.</summary>
/// <param name="AppId">The bridge's number for it, which its windows, output and exit carry.</param>
/// <param name="Name">Its name.</param>
/// <param name="ProcessId">Its process on the PC.</param>
/// <param name="ProjectPath">The project it was built from.</param>
/// <param name="IsDebugging">Whether the debugger started it.</param>
public sealed record AppStarted(int AppId, string Name, int ProcessId, string ProjectPath, bool IsDebugging);

/// <summary>Names a running program.</summary>
/// <param name="AppId">The program.</param>
public sealed record AppReference(int AppId);

/// <summary>What a program wrote to its console.</summary>
/// <param name="AppId">The program.</param>
/// <param name="Text">The text, a line or more.</param>
/// <param name="IsError">Whether it wrote it to standard error.</param>
public sealed record AppOutput(int AppId, string Text, bool IsError);

/// <summary>A program's top-level windows and dialogs, the newest last.</summary>
/// <param name="AppId">The program.</param>
/// <param name="Windows">Its windows.</param>
public sealed record AppWindows(int AppId, IReadOnlyList<BridgeWindowInfo> Windows);

/// <summary>A top-level window or dialog of a program on the PC.</summary>
/// <param name="Id">The window's handle on the PC.</param>
/// <param name="Title">Its title.</param>
/// <param name="Width">Its width in the PC's pixels.</param>
/// <param name="Height">Its height in the PC's pixels.</param>
/// <param name="IsDialog">Whether another window of the program owns it, as a dialog or message box.</param>
/// <param name="Scale">The PC's pixels per device-independent pixel where the window is, such as 1.5 at 144 DPI.</param>
public sealed record BridgeWindowInfo(long Id, string Title, int Width, int Height, bool IsDialog, double Scale);

/// <summary>A program exited.</summary>
/// <param name="AppId">The program.</param>
/// <param name="ExitCode">Its exit code, when it has one.</param>
public sealed record AppExited(int AppId, int? ExitCode);

/// <summary>Starts streaming a window to the tablet.</summary>
/// <param name="AppId">The program.</param>
/// <param name="WindowId">The window.</param>
/// <param name="MaxWidth">The most pixels wide the tablet shows it; a wider window is scaled down before it is sent.</param>
/// <param name="MaxHeight">The most pixels high the tablet shows it.</param>
public sealed record StreamRequest(int AppId, long WindowId, int MaxWidth, int MaxHeight);

/// <summary>A stream that started.</summary>
/// <param name="StreamId">The bridge's number for it, which its frames carry.</param>
/// <param name="CaptureMethod">How the PC captures the window, such as <c>Windows.Graphics.Capture</c>.</param>
public sealed record StreamStarted(int StreamId, string CaptureMethod);

/// <summary>Names a stream.</summary>
/// <param name="StreamId">The stream.</param>
public sealed record StreamReference(int StreamId);

/// <summary>A frame of a stream; the encoded frame is the notification's attachment.</summary>
/// <param name="StreamId">The stream.</param>
/// <param name="Sequence">The frame's number in the stream, from 1.</param>
/// <param name="WindowWidth">The window's width in the PC's pixels, which input positions are in.</param>
/// <param name="WindowHeight">The window's height in the PC's pixels.</param>
/// <param name="Level">The quality level of the frame's changed tiles, 0 for exact.</param>
public sealed record FrameHeader(int StreamId, long Sequence, int WindowWidth, int WindowHeight, int Level);

/// <summary>The tablet drew a frame.</summary>
/// <param name="StreamId">The stream.</param>
/// <param name="Sequence">The frame's number.</param>
public sealed record FrameShown(int StreamId, long Sequence);

/// <summary>Input for a program's window, in the order it happened.</summary>
/// <param name="AppId">The program.</param>
/// <param name="WindowId">The window.</param>
/// <param name="Events">The input.</param>
public sealed record InputBatch(int AppId, long WindowId, IReadOnlyList<BridgeInputEvent> Events);

/// <summary>Resizes a program's window.</summary>
/// <param name="AppId">The program.</param>
/// <param name="WindowId">The window.</param>
/// <param name="Width">The new width in the PC's pixels.</param>
/// <param name="Height">The new height in the PC's pixels.</param>
public sealed record ResizeRequest(int AppId, long WindowId, int Width, int Height);

/// <summary>Text on a clipboard.</summary>
/// <param name="Text">The text, or <see langword="null"/> when the clipboard holds none.</param>
public sealed record ClipboardText(string? Text);

/// <summary>Starts the debug adapter on the PC for a tunnel.</summary>
/// <param name="TunnelId">The tablet's number for the tunnel, which its data carries.</param>
/// <param name="WorkingDirectory">The folder the adapter starts in, on the tablet's paths.</param>
/// <param name="ProjectPath">The project whose program it debugs, on the tablet's paths.</param>
public sealed record DebuggerOpen(int TunnelId, string WorkingDirectory, string ProjectPath);

/// <summary>Bytes of the debug adapter's messages; they are the notification's attachment.</summary>
/// <param name="TunnelId">The tunnel.</param>
public sealed record DebuggerData(int TunnelId);

/// <summary>A debug adapter's tunnel closed.</summary>
/// <param name="TunnelId">The tunnel.</param>
/// <param name="Reason">Why, when it was not closed on purpose.</param>
public sealed record DebuggerClosed(int TunnelId, string? Reason);
