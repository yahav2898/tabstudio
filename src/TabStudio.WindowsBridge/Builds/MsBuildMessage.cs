using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Core.Diagnostics;

namespace TabStudio.WindowsBridge.Builds;

/// <summary>
/// An error or warning as MSBuild and the tools it runs (the C# and C++ compilers, the linker, NuGet, the XAML
/// compiler) print them, in MSBuild's canonical format:
/// <c>origin(line,column): [subcategory] error|warning code: text [project]</c>.
/// </summary>
/// <param name="Origin">The file, or the tool when no file is named, such as <c>LINK</c>; empty when there is neither.</param>
/// <param name="Line">The one-based line, or 0 when none is given.</param>
/// <param name="Column">The one-based column, or 0 when none is given.</param>
/// <param name="Severity">Error or warning.</param>
/// <param name="Code">The code, such as <c>CS1002</c>, <c>C2065</c> or <c>MSB3073</c>; empty when there is none.</param>
/// <param name="Text">The message.</param>
/// <param name="Project">The project MSBuild was building, when the line names it.</param>
public sealed partial record MsBuildMessage(
    string Origin,
    int Line,
    int Column,
    DiagnosticSeverityLevel Severity,
    string Code,
    string Text,
    string? Project)
{
    /// <summary>Whether <see cref="Origin"/> is a file rather than a tool's name.</summary>
    public bool HasFile => Origin.Length > 0 && (Line > 0 || Origin.Contains('\\', StringComparison.Ordinal) || Origin.Contains('/', StringComparison.Ordinal) || Path.HasExtension(Origin));

    /// <summary>Reads a line of build output.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The error or warning, or <see langword="null"/> when the line is not one.</returns>
    public static MsBuildMessage? Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length > 4096 || (!line.Contains("error", StringComparison.OrdinalIgnoreCase) && !line.Contains("warning", StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        Match match;
        try
        {
            match = Canonical().Match(line);
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }

        if (!match.Success)
        {
            return null;
        }

        var origin = match.Groups["origin"].Value.Trim();
        var (lineNumber, column) = ParseLocation(match.Groups["location"].Value);
        var severity = match.Groups["category"].Value.EndsWith("warning", StringComparison.OrdinalIgnoreCase)
            ? DiagnosticSeverityLevel.Warning
            : DiagnosticSeverityLevel.Error;
        var project = match.Groups["project"].Success ? match.Groups["project"].Value : null;
        return new MsBuildMessage(origin, lineNumber, column, severity, match.Groups["code"].Value, match.Groups["text"].Value.Trim(), project);
    }

    /// <summary>The location part: <c>12</c>, <c>12,5</c>, <c>12-14</c>, <c>12,5-9</c> or <c>12,5,14,9</c>.</summary>
    private static (int Line, int Column) ParseLocation(string location)
    {
        if (location.Length == 0)
        {
            return (0, 0);
        }

        var parts = location.Split([',', '-']);
        var line = int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var l) ? l : 0;
        var column = location.Contains(',', StringComparison.Ordinal) && parts.Length > 1 && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var c) ? c : 0;
        return (line, column);
    }

    [GeneratedRegex(
        @"^\s*(?:(?<origin>[^\s].*?)(?:\((?<location>[0-9]+(?:[,\-][0-9]+){0,3})\))?\s*:\s*)?(?:(?<subcategory>[^:]*?)\s+)?(?<category>fatal error|error|warning)(?:\s+(?<code>[A-Za-z][A-Za-z0-9_.]*))?\s*:\s*(?<text>.*?)(?:\s+\[(?<project>[^\[\]]+)\])?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 200)]
    private static partial Regex Canonical();
}
