using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.Services.Toolchains;

/// <summary>
/// The launch configuration that debugs what F5 runs for a Python or Node.js project: the same program, arguments,
/// folder and environment as its run plan, in the terms of the toolchain's debug adapter (debugpy or js-debug).
/// </summary>
/// <remarks>
/// <para>
/// For debugpy, the plan's interpreter and its options become <c>python</c> and <c>pythonArgs</c>, and the script
/// (or <c>-m module</c>) <c>program</c> (or <c>module</c>). For js-debug, <c>node file</c> becomes <c>program</c>,
/// and anything else, such as <c>npm start</c>, <c>runtimeExecutable</c> and <c>runtimeArgs</c>; a <c>start</c>
/// script that only runs <c>node file</c> is debugged as that file, without npm in between.
/// </para>
/// <para>
/// The configurations leave <c>console</c> to the adapter's terminal defaults, so the program runs in the Output
/// panel and reads its input there, as it does without the debugger. The IDE starts that program with the run
/// plan's environment, under what the adapter adds.
/// </para>
/// </remarks>
internal static partial class ToolchainLaunch
{
    /// <summary>The launch configuration type of debugpy.</summary>
    public const string PythonType = "debugpy";

    /// <summary>The launch configuration type of js-debug.</summary>
    public const string NodeType = "pwa-node";

    /// <summary>Whether a configuration type is one this class writes configurations for.</summary>
    /// <param name="type">The debug adapter's type, from the toolchain catalog.</param>
    public static bool Supports(string? type) => type is PythonType or NodeType or PhpType or "go" or "lldb-dap" or "java" or "flutter" or "dart";

    /// <summary>The configuration that debugs a plan's program.</summary>
    /// <param name="type">The debug adapter's type: <see cref="PythonType"/> or <see cref="NodeType"/>.</param>
    /// <param name="plan">What F5 runs.</param>
    /// <returns>The configuration, or <see langword="null"/> when the plan's command is not one the adapter launches.</returns>
    public static LaunchConfiguration? Create(string type, RunPlan plan)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(plan);
        var run = plan.Run;
        var folder = run.WorkingDirectory ?? Directory.GetCurrentDirectory();
        var properties = new JsonObject
        {
            ["name"] = plan.ProgramName,
            ["type"] = type,
            ["request"] = "launch",
            ["cwd"] = folder,
            ["env"] = Environment(run),
        };

        var filled = type switch
        {
            PythonType => FillPython(properties, run, folder),
            NodeType => FillNode(properties, run, folder),
            "go" => FillGo(properties, run),

            "lldb-dap" => FillNative(properties, run),

            "java" => FillJava(properties, run),

            PhpType => FillPhp(properties, run, folder),

            "flutter" or "dart" => FillDart(properties, run, folder),
            _ => false,
        };
        return filled
            ? new LaunchConfiguration { Name = plan.ProgramName, Type = properties["type"]!.GetValue<string>(), Request = "launch", Properties = properties }
            : null;
    }

    /// <summary><c>python3 -u main.py a b</c>: the interpreter, its options, and the script or module with its arguments.</summary>
    private static bool FillPython(JsonObject properties, ProcessStartSpec run, string folder)
    {
        var arguments = run.Arguments;
        var index = 0;
        var options = new JsonArray();
        while (index < arguments.Count && arguments[index].StartsWith('-') && arguments[index] is not ("-m" or "-c" or "-"))
        {
            options.Add((JsonNode?)arguments[index++]);
        }

        if (index >= arguments.Count || arguments[index] == "-c" || arguments[index] == "-")
        {
            return false;
        }

        if (arguments[index] == "-m")
        {
            if (index + 1 >= arguments.Count)
            {
                return false;
            }

            properties["module"] = arguments[index + 1];
            index += 2;
        }
        else
        {
            properties["program"] = Path.GetFullPath(Path.Combine(folder, arguments[index]));
            index++;
        }

        properties["python"] = run.Program;
        if (options.Count > 0)
        {
            properties["pythonArgs"] = options;
        }

        properties["args"] = Strings(arguments.Skip(index));
        return true;
    }

    /// <summary><c>node index.js</c> as a program, or another command, such as <c>npm start</c>, as the runtime.</summary>
    private static bool FillNode(JsonObject properties, ProcessStartSpec run, string folder)
    {
        properties["skipFiles"] = new JsonArray("<node_internals>/**");
        var program = run.Program;
        var arguments = run.Arguments;
        if (IsNpmStart(run) && NodeStartScript(folder) is { } script)
        {
            program = "node";
            arguments = [script];
        }

        if (Path.GetFileNameWithoutExtension(program) == "node" && arguments.Any(a => !a.StartsWith('-')))
        {
            var index = 0;
            var options = new JsonArray();
            while (arguments[index].StartsWith('-'))
            {
                options.Add((JsonNode?)arguments[index++]);
            }

            properties["runtimeExecutable"] = program;
            if (options.Count > 0)
            {
                properties["runtimeArgs"] = options;
            }

            properties["program"] = Path.GetFullPath(Path.Combine(folder, arguments[index]));
            properties["args"] = Strings(arguments.Skip(index + 1));
            return true;
        }

        properties["runtimeExecutable"] = program;
        properties["runtimeArgs"] = Strings(arguments);
        return true;
    }

    private static bool FillDart(JsonObject properties, ProcessStartSpec run, string folder)
    {
        var flutter = Path.GetFileNameWithoutExtension(run.Program) == "flutter";
        properties["type"] = flutter ? "flutter" : "dart";
        properties["program"] = Path.GetFullPath(Path.Combine(folder, flutter ? "lib/main.dart" : (run.Arguments.Count > 0 ? run.Arguments[^1] : "bin/main.dart")));
        if (flutter)
        {
            properties["toolArgs"] = Strings(run.Arguments.Skip(1));
        }
        else
        {
            properties["console"] = "terminal";
        }
        return true;
    }

    private static bool IsNpmStart(ProcessStartSpec run) =>
        Path.GetFileNameWithoutExtension(run.Program) == "npm" && run.Arguments is ["start"] or ["run", "start"] or ["run-script", "start"];

    /// <summary>The file a <c>start</c> script of exactly <c>node file</c> runs, or <see langword="null"/>.</summary>
    private static string? NodeStartScript(string folder)
    {
        var manifest = Path.Combine(folder, "package.json");
        return File.Exists(manifest) && RunPlanner.ReadJson(manifest, "scripts.start") is { } start && NodeStartPattern().Match(start.Trim()) is { Success: true } match
            ? match.Groups["file"].Value
            : null;
    }

    /// <summary>
    /// The run's variables for the program. One that extends another, such as <c>PATH</c> with a <c>.venv</c>'s
    /// <c>bin</c> before <c>$PATH</c>, is left out: the adapters would pass <c>$PATH</c> on as it is, so the IDE
    /// applies it when it starts the program for the adapter, with the runtime's own <c>PATH</c> in its place.
    /// </summary>
    private static JsonObject Environment(ProcessStartSpec run)
    {
        var environment = new JsonObject();
        foreach (var (name, value) in run.Environment)
        {
            if (value?.Contains("$PATH", StringComparison.Ordinal) != true)
            {
                environment[name] = value;
            }
        }

        return environment;
    }

    private static JsonArray Strings(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add((JsonNode?)value);
        }

        return array;
    }

    [GeneratedRegex(@"^node\s+(?<file>[\w./-]+\.(?:js|mjs|cjs|ts|mts|cts))$", RegexOptions.CultureInvariant)]
    private static partial Regex NodeStartPattern();
}
