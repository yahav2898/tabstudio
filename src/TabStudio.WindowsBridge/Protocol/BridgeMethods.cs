namespace TabStudio.WindowsBridge.Protocol;

/// <summary>
/// The methods of the Windows Bridge over TabStudio Link. Requests go from the tablet to the bridge unless marked;
/// notifications are marked as such. Paths in every message are the tablet's: the bridge maps them to its mirror of
/// the workspace and back.
/// </summary>
public static class BridgeMethods
{
    /// <summary>What the PC is and what it can build; answered with <see cref="BridgeInfo"/>.</summary>
    public const string Info = "bridge/info";

    /// <summary>Sends the workspace's file list; answered with the files the bridge lacks.</summary>
    public const string PrepareWorkspace = "bridge/workspace/prepare";

    /// <summary>Sends a batch of the files the bridge lacks, as attachments.</summary>
    public const string UploadFiles = "bridge/workspace/upload";

    /// <summary>Removes the files the tablet no longer has and makes the workspace current.</summary>
    public const string CommitWorkspace = "bridge/workspace/commit";

    /// <summary>Builds a project with the PC's own tools; answered with <see cref="BuildOutcome"/>.</summary>
    public const string Build = "bridge/build";

    /// <summary>Notification, bridge to tablet: a line of a build's output.</summary>
    public const string BuildOutput = "bridge/build/output";

    /// <summary>Runs a built program on the PC; answered with <see cref="AppStarted"/>.</summary>
    public const string Run = "bridge/app/run";

    /// <summary>Stops a running program.</summary>
    public const string Stop = "bridge/app/stop";

    /// <summary>Notification, bridge to tablet: a program started, also when the debugger started it.</summary>
    public const string AppStarted = "bridge/app/started";

    /// <summary>Notification, bridge to tablet: what a running program wrote to its console.</summary>
    public const string AppOutput = "bridge/app/output";

    /// <summary>Notification, bridge to tablet: a program's windows changed: one opened, closed, resized or was renamed.</summary>
    public const string AppWindows = "bridge/app/windows";

    /// <summary>Notification, bridge to tablet: a program exited.</summary>
    public const string AppExited = "bridge/app/exited";

    /// <summary>Starts streaming a window; answered with <see cref="StreamStarted"/>.</summary>
    public const string StartStream = "bridge/stream/start";

    /// <summary>Notification: stops streaming a window.</summary>
    public const string StopStream = "bridge/stream/stop";

    /// <summary>Notification, bridge to tablet: a stream ended on its own, as when its window closed or could not be captured.</summary>
    public const string StreamEnded = "bridge/stream/ended";

    /// <summary>Notification, bridge to tablet: an encoded frame of a streamed window, as an attachment.</summary>
    public const string Frame = "bridge/stream/frame";

    /// <summary>Notification: the tablet drew a frame, which paces the stream.</summary>
    public const string FrameShown = "bridge/stream/shown";

    /// <summary>Notification: mouse and keyboard input for a program's window.</summary>
    public const string Input = "bridge/input";

    /// <summary>Resizes a program's window, such as to fit the tablet's pane.</summary>
    public const string Resize = "bridge/window/resize";

    /// <summary>The PC's clipboard text; answered with <see cref="ClipboardText"/>.</summary>
    public const string GetClipboard = "bridge/clipboard/get";

    /// <summary>Puts text on the PC's clipboard.</summary>
    public const string SetClipboard = "bridge/clipboard/set";

    /// <summary>Notification: starts the debug adapter on the PC for a tunnel the tablet named.</summary>
    public const string OpenDebugger = "bridge/debug/open";

    /// <summary>Notification, both ways: bytes of the debug adapter's messages, as an attachment.</summary>
    public const string DebuggerData = "bridge/debug/data";

    /// <summary>Notification, both ways: the debug adapter's tunnel closed.</summary>
    public const string CloseDebugger = "bridge/debug/close";

    /// <summary>Notification, bridge to tablet: what the PC is doing and whether it is about to sleep.</summary>
    public const string Status = "bridge/status";
}
