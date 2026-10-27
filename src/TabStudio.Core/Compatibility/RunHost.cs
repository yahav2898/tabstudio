namespace TabStudio.Core.Compatibility;

/// <summary>
/// Where a program runs when the user presses F5.
/// </summary>
public enum RunHost
{
    /// <summary>Nothing to run, for example a class library.</summary>
    None,

    /// <summary>Compiled in memory and run inside TabStudio, with output in the Output panel.</summary>
    InProcess,

    /// <summary>Served locally and shown in a WebView next to the editor.</summary>
    WebView,

    /// <summary>Rendered as a native Android view next to the editor.</summary>
    AndroidView,

    /// <summary>Run on the user's own Windows PC and streamed back to the tablet.</summary>
    WindowsBridge,

    /// <summary>Run by a toolchain from the Toolchain Store, such as Python, with its output in the Output panel.</summary>
    Toolchain,
}

/// <summary>
/// Whether TabStudio can run a project today.
/// </summary>
public enum CompatibilityStatus
{
    /// <summary>Runs on the tablet today.</summary>
    Ready,

    /// <summary>Supported by the architecture and on the roadmap, but not shipped yet.</summary>
    Planned,

    /// <summary>Can run on the tablet after some code changes, which the agent can help with.</summary>
    NeedsChanges,

    /// <summary>Needs real Windows: runs through the Windows Bridge on the user's PC.</summary>
    NeedsWindows,

    /// <summary>Not an application; it is built as part of the projects that use it.</summary>
    NotRunnable,

    /// <summary>TabStudio does not know how to run this project.</summary>
    Unsupported,

    /// <summary>Runs on the tablet once its toolchain is installed from the Toolchain Store.</summary>
    NeedsToolchain,

    /// <summary>
    /// Builds and runs on the tablet, but its database cannot run here and none it can reach answers, such as a SQL
    /// Server (see <see cref="SqlServerDetector"/>).
    /// </summary>
    NeedsDatabase,
}
