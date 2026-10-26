using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Services.Toolchains;

internal static partial class ToolchainLaunch
{
    /// <summary>Delve uses the app's private Unix socket on Android, where TCP owner checks cannot use simulated UIDs.</summary>
    internal static DebugAdapterDefinition ForRuntime(DebugAdapterDefinition adapter, RuntimeKind runtime) =>
        runtime == RuntimeKind.Linux && adapter.Handles("go") && adapter.Transport == DebugAdapterTransport.Tcp
            ? adapter with
            {
                Transport = DebugAdapterTransport.UnixSocket,
                Arguments = adapter.Arguments.Select(a => a.Contains("${port}", StringComparison.Ordinal) ? "unix:${socket}" : a).ToList(),
            }
            : adapter;

    /// <summary>Delve executes the same binary built by Ctrl+F5, with optimizations disabled.</summary>
    private static bool FillGo(JsonObject properties, ProcessStartSpec run)
    {
        if (!Path.IsPathRooted(run.Program))
        {
            return false;
        }

        properties["mode"] = "exec";
        properties["program"] = run.Program;
        properties["args"] = Strings(run.Arguments);
        properties["outputMode"] = "remote";
        properties["stdinFrom"] = OperatingSystem.IsWindows() ? "CONIN$" : "/dev/stdin";
        return true;
    }
}
