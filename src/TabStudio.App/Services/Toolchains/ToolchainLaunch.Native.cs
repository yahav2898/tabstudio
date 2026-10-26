using System.Text.Json.Nodes;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services.Toolchains;

internal static partial class ToolchainLaunch
{
    /// <summary>LLDB runs the executable in the Output panel through DAP's runInTerminal request.</summary>
    private static bool FillNative(JsonObject properties, ProcessStartSpec run)
    {
        if (!Path.IsPathRooted(run.Program))
        {
            return false;
        }

        properties["program"] = run.Program;
        properties["args"] = Strings(run.Arguments);
        properties["runInTerminal"] = true;
        properties["disableASLR"] = false;
        return true;
    }
}
