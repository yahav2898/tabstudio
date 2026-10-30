using System.Text.Json.Nodes;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services.Toolchains;

internal static partial class ToolchainLaunch
{
    /// <summary>The launch configuration type of the PHP Debug adapter, for Xdebug.</summary>
    public const string PhpType = "php";

    /// <summary>
    /// What F5 runs for a PHP project, as the PHP Debug adapter launches it: <c>php script args</c> as
    /// <c>program</c> and <c>args</c>, <c>php artisan serve</c> as the <c>artisan</c> program, and PHP's built-in web
    /// server (<c>php -S</c>) or <c>composer start</c> as a runtime with its arguments and no program. A script runs
    /// in the Output panel, which answers its input; a server's output comes as the adapter's output events.
    /// </summary>
    private static bool FillPhp(JsonObject properties, ProcessStartSpec run, string folder)
    {
        var arguments = run.Arguments;
        properties["runtimeExecutable"] = run.Program;
        properties["externalConsole"] = false;
        var index = 0;
        var options = new JsonArray();
        while (index < arguments.Count && arguments[index].StartsWith('-') && arguments[index] != "-S")
        {
            options.Add((JsonNode?)arguments[index++]);
        }

        if (Path.GetFileNameWithoutExtension(run.Program) != "php" || index >= arguments.Count || arguments[index] == "-S")
        {
            // A web server or a tool: the adapter starts the runtime with the arguments and waits for Xdebug's connection.
            properties["runtimeArgs"] = Strings(arguments);
            properties["program"] = string.Empty;
            return true;
        }

        if (options.Count > 0)
        {
            properties["runtimeArgs"] = options;
        }

        // A script reads its input from the Output panel: the adapter asks TabStudio to run it (runInTerminal).
        properties["console"] = "integratedTerminal";
        properties["program"] = Path.GetFullPath(Path.Combine(folder, arguments[index]));
        properties["args"] = Strings(arguments.Skip(index + 1));
        return true;
    }
}
