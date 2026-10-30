using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Core.Progress;

/// <summary>
/// The numbers build and install tools print as they go, read from their output lines: Ninja's <c>[37/120]</c>, the
/// percentage of CMake's Makefiles <c>[ 37%]</c>, Alpine's apk <c>(12/40) Installing …</c>, and the stage changes
/// CMake prints while it configures. A line without any says nothing, and the last numbers stand.
/// </summary>
public static partial class OutputProgressParser
{
    /// <summary>What a line says about progress, or <see langword="null"/> when nothing.</summary>
    /// <param name="line">One line of output.</param>
    /// <param name="stage">The stage the operation is in, kept when the line has numbers but no stage of its own.</param>
    public static OperationProgress? Parse(string line, string stage)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(stage);
        if (Ninja().Match(line) is { Success: true } ninja)
        {
            return new OperationProgress(BuildStage(stage), Number(ninja.Groups["done"].Value), Number(ninja.Groups["total"].Value));
        }

        if (Makefile().Match(line) is { Success: true } make)
        {
            return new OperationProgress(BuildStage(stage), Number(make.Groups["percent"].Value), 100, ProgressUnit.Percent);
        }

        if (Apk().Match(line) is { Success: true } apk)
        {
            return new OperationProgress("Installing packages", Number(apk.Groups["done"].Value), Number(apk.Groups["total"].Value));
        }

        if (line.StartsWith("-- Configuring done", StringComparison.Ordinal) || line.StartsWith("-- Generating done", StringComparison.Ordinal))
        {
            return new OperationProgress(line.StartsWith("-- Generating", StringComparison.Ordinal) ? "Generating build files" : "Configuring CMake");
        }

        return null;
    }

    /// <summary>The stage of a command that is about to run, from its program and arguments: CMake's configure or build, or a package install.</summary>
    /// <param name="program">The program, such as <c>cmake</c> or <c>/usr/bin/ninja</c>.</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="what">What the operation is about, such as the project's name, for the stage text.</param>
    public static string StageOf(string program, IReadOnlyList<string> arguments, string what)
    {
        ArgumentNullException.ThrowIfNull(program);
        ArgumentNullException.ThrowIfNull(arguments);
        var file = Path.GetFileName(program);
        var name = file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;
        return name switch
        {
            "cmake" when arguments.Contains("--build") => $"Building {what}",
            "cmake" when arguments.Contains("--install") => $"Installing {what}",
            "cmake" => "Configuring CMake",
            "ninja" or "make" or "gmake" or "mingw32-make" or "idf.py" or "west" or "cargo" or "pio" or "platformio" or "arduino-cli" or "gradle" or "mvn"
                or "swift" or "swiftc" or "kotlinc" => $"Building {what}",
            "npm" or "pnpm" or "yarn" when arguments is ["run", "build", ..] => $"Building {what}'s assets",
            "composer" => "Installing packages",
            "npm" or "pnpm" or "yarn" or "pip" or "pip3" or "uv" or "apk" or "bundle" => "Installing packages",
            _ => $"Running {name}",
        };
    }

    private static string BuildStage(string stage) => stage.StartsWith("Building", StringComparison.Ordinal) ? stage : "Building";

    private static double Number(string text) => double.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^\[\s*(?<done>\d+)\s*/\s*(?<total>\d+)\s*\]\s", RegexOptions.CultureInvariant)]
    private static partial Regex Ninja();

    [GeneratedRegex(@"^\[\s*(?<percent>\d{1,3})%\]\s", RegexOptions.CultureInvariant)]
    private static partial Regex Makefile();

    [GeneratedRegex(@"^\(\s*(?<done>\d+)/(?<total>\d+)\)\s+(Installing|Upgrading|Purging)\s", RegexOptions.CultureInvariant)]
    private static partial Regex Apk();
}
