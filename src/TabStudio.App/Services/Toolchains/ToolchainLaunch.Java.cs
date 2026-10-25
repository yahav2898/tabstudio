using System.Text.Json.Nodes;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services.Toolchains;

internal static partial class ToolchainLaunch
{
    private static bool FillJava(JsonObject properties, ProcessStartSpec run)
    {
        if (run.Arguments is not ["-cp", var classpath, var mainClass, ..])
        {
            return false;
        }

        properties["mainClass"] = mainClass;
        properties["classPaths"] = Strings(classpath.Split(Path.PathSeparator));
        properties["args"] = Strings(run.Arguments.Skip(3));
        properties["console"] = "integratedTerminal";
        return true;
    }
}
