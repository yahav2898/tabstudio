using System.Text.Json.Nodes;
using TabStudio.LanguageServers.Configuration;
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

    /// <summary>
    /// The LLDB commands that load Rust's own formatters from the toolchain, as <c>rust-lldb</c> and VS Code's Rust
    /// debugging do: LLDB has no Rust plugin, so without them a <c>&amp;str</c> shows as its address and a
    /// <c>String</c> or <c>Vec</c> as its fields. The toolchain's sysroot is asked of <c>rustc</c> where LLDB runs; when
    /// LLDB has no Python or the files are missing, nothing changes.
    /// </summary>
    public static readonly string RustFormattersCommand =
        "script exec(\"try:\\n import subprocess, os\\n s = subprocess.run(['rustc', '--print', 'sysroot'], capture_output=True, text=True, timeout=20).stdout.strip()\\n"
        + " e = os.path.join(s, 'lib', 'rustlib', 'etc')\\n"
        + " if os.path.isfile(os.path.join(e, 'lldb_lookup.py')):\\n"
        + "  lldb.debugger.HandleCommand('command script import \\\"' + os.path.join(e, 'lldb_lookup.py') + '\\\"')\\n"
        + "  lldb.debugger.HandleCommand('command source -s 1 \\\"' + os.path.join(e, 'lldb_commands') + '\\\"')\\n"
        + "except Exception:\\n pass\")";

    /// <summary>A Rust program's LLDB configuration, with <see cref="RustFormattersCommand"/> run before the program starts.</summary>
    /// <param name="configuration">The configuration <see cref="Create"/> made.</param>
    public static LaunchConfiguration WithRustFormatters(LaunchConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var properties = (JsonObject)configuration.Properties.DeepClone();
        var commands = properties["initCommands"] as JsonArray ?? [];
        commands.Add((JsonNode?)RustFormattersCommand);
        properties["initCommands"] = commands;
        return configuration with { Properties = properties };
    }
}
