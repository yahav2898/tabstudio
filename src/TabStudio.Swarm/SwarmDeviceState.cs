namespace TabStudio.Swarm;

/// <summary>What a device in a swarm is doing with the app being built.</summary>
public enum SwarmDeviceState
{
    /// <summary>Joined and waiting for an app to run.</summary>
    Ready,

    /// <summary>Receiving a build.</summary>
    Deploying,

    /// <summary>Starting the app it received.</summary>
    Starting,

    /// <summary>Running the app.</summary>
    Running,

    /// <summary>Asked to pause (Break All), and waiting for the app's next statement.</summary>
    Pausing,

    /// <summary>Paused by the debugger, at a breakpoint, a step or an exception.</summary>
    Paused,

    /// <summary>The app ended by itself, as a console app does.</summary>
    Exited,

    /// <summary>The app stopped because of an unhandled exception.</summary>
    Crashed,

    /// <summary>The build could not be received or started.</summary>
    Failed,

    /// <summary>The device is not connected: it left, or the coordinator lost it and waits for it to come back.</summary>
    Offline,
}
