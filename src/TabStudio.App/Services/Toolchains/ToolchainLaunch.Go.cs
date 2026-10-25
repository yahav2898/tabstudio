using System.Text.Json.Nodes;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services.Toolchains;

internal static partial class ToolchainLaunch
{
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
