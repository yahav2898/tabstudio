namespace TabStudio.Swarm.Protocol;

/// <summary>
/// The methods of the swarm's messages over TabStudio Link. The device connects to the coordinator, so the device is
/// Link's client and the coordinator its host; both ask and tell.
/// </summary>
internal static class SwarmMethods
{
    // ---- From the device ----

    /// <summary>Request: the device joins with its name and screen (<see cref="SwarmHello"/>), answered with <see cref="SwarmWelcome"/>.</summary>
    public const string Join = "swarm/join";

    /// <summary>Notification: the device leaves on purpose, before it disconnects.</summary>
    public const string Leave = "swarm/leave";

    /// <summary>Notification: what the device is doing with the app (<see cref="SwarmStateNotice"/>).</summary>
    public const string State = "swarm/state";

    /// <summary>Notification: a console app's output (<see cref="SwarmOutputNotice"/>).</summary>
    public const string Output = "swarm/output";

    /// <summary>Notification: the app ended (<see cref="SwarmExitNotice"/>).</summary>
    public const string Exited = "swarm/exited";

    /// <summary>Notification: the device's screen changed, for example when it was turned (<see cref="SwarmScreenInfo"/>).</summary>
    public const string Screen = "swarm/screen";

    /// <summary>Notification: a person's input on the device's screen (<see cref="SwarmInput"/>), for mirroring and recording.</summary>
    public const string Captured = "swarm/captured";

    /// <summary>Notification: the debugged app paused (<see cref="SwarmPauseNotice"/>).</summary>
    public const string Paused = "swarm/debug/paused";

    /// <summary>Notification: the debugged app continued (<see cref="SwarmResumedNotice"/>).</summary>
    public const string Resumed = "swarm/debug/resumed";

    /// <summary>Request: the device's web view opened a connection to the web app (<see cref="SwarmStreamNotice"/>), answered with <see cref="SwarmConnectResponse"/>.</summary>
    public const string WebConnect = "swarm/web/connect";

    // ---- From the coordinator ----

    /// <summary>Request: which of these files the device lacks (<see cref="SwarmPrepareRequest"/>, <see cref="SwarmPrepareResponse"/>).</summary>
    public const string Prepare = "swarm/prepare";

    /// <summary>Request: run a build, whose missing files are the attachments (<see cref="SwarmRunRequest"/>).</summary>
    public const string Run = "swarm/run";

    /// <summary>Request: stop the app (<see cref="SwarmStopRequest"/>).</summary>
    public const string Stop = "swarm/stop";

    /// <summary>Notification: a line typed for the console app, or the end of its input (<see cref="SwarmConsoleInput"/>).</summary>
    public const string ConsoleInput = "swarm/stdin";

    /// <summary>Notification: the tablet removed the device from the swarm; it disconnects and forgets the swarm.</summary>
    public const string Removed = "swarm/removed";

    /// <summary>Notification: a short message to show on the device's screen for a moment (<see cref="SwarmNotice"/>).</summary>
    public const string Notice = "swarm/notice";

    /// <summary>Request: the breakpoints, all of them (<see cref="SwarmBreakpointsRequest"/>).</summary>
    public const string Breakpoints = "swarm/debug/breakpoints";

    /// <summary>Request: continue or step the paused app (<see cref="SwarmResumeRequest"/>, <see cref="SwarmResumeResponse"/>).</summary>
    public const string Resume = "swarm/debug/resume";

    /// <summary>Request: pause the app at its next statement (Break All).</summary>
    public const string Pause = "swarm/debug/pause";

    /// <summary>Request: the locals of the paused app, or the members of one of its values (<see cref="SwarmVariablesRequest"/>).</summary>
    public const string Variables = "swarm/debug/variables";

    /// <summary>Request: a Watch or DataTip expression in the paused app (<see cref="SwarmEvaluateRequest"/>).</summary>
    public const string Evaluate = "swarm/debug/evaluate";

    /// <summary>Request: replay an input on the device's screen (<see cref="SwarmInput"/>, <see cref="SwarmInputResponse"/>).</summary>
    public const string Input = "swarm/screen/input";

    /// <summary>Request: a picture of the device's screen, as the answer's attachment (<see cref="SwarmCaptureResponse"/>).</summary>
    public const string Capture = "swarm/screen/capture";

    /// <summary>Request: the layout checks of the device's screen (<see cref="SwarmLayoutResponse"/>).</summary>
    public const string Layout = "swarm/screen/layout";

    /// <summary>Request: the text an element of the device's screen shows (<see cref="SwarmTextRequest"/>, <see cref="SwarmTextResponse"/>).</summary>
    public const string ReadText = "swarm/screen/text";

    // ---- Both ways ----

    /// <summary>Notification: bytes of a web connection, as the attachment (<see cref="SwarmStreamNotice"/>).</summary>
    public const string WebData = "swarm/web/data";

    /// <summary>Notification: a web connection closed (<see cref="SwarmStreamNotice"/>).</summary>
    public const string WebClose = "swarm/web/close";
}
