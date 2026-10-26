using System.Text;
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
        // java-debug's wire protocol accepts one command line; VS Code converts arrays before sending it.
        properties["args"] = string.Join(' ', run.Arguments.Skip(3).Select(JavaArgument));
        properties["console"] = "integratedTerminal";
        return true;
    }

    private static string JavaArgument(string argument)
    {
        if (!OperatingSystem.IsWindows())
        {
            return "\"" + argument.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        var quoted = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            quoted.Append('\\', character == '"' ? (backslashes * 2) + 1 : backslashes);
            backslashes = 0;
            quoted.Append(character);
        }

        return quoted.Append('\\', backslashes * 2).Append('"').ToString();
    }
}
