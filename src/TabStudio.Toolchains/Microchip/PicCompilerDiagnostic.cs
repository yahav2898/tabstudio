using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Toolchains.Microchip;

/// <summary>One XC compiler diagnostic, before its guest path is mapped to the tablet.</summary>
/// <param name="File">Printed source filename.</param>
/// <param name="Line">One-based source line.</param>
/// <param name="Column">One-based column, or 1.</param>
/// <param name="Severity">Error, warning or note.</param>
/// <param name="Message">Compiler message, including its XC diagnostic number.</param>
public sealed partial record PicCompilerDiagnostic(string File, int Line, int Column, DiagnosticSeverityLevel Severity, string Message)
{
    /// <summary>Reads every diagnostic, keeping multiple errors in a single build.</summary>
    /// <param name="output">Compiler output.</param>
    public static IReadOnlyList<PicCompilerDiagnostic> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var result = new List<PicCompilerDiagnostic>();
        try
        {
            foreach (Match match in Lines().Matches(output))
            {
                if (!int.TryParse(match.Groups["line"].Value, CultureInfo.InvariantCulture, out var line))
                {
                    continue;
                }

                var column = int.TryParse(match.Groups["column"].Value, CultureInfo.InvariantCulture, out var parsed) ? parsed : 1;
                var diagnostic = new PicCompilerDiagnostic(match.Groups["file"].Value.Trim(), line, column,
                    match.Groups["severity"].Value switch { "warning" => DiagnosticSeverityLevel.Warning, "note" => DiagnosticSeverityLevel.Info, _ => DiagnosticSeverityLevel.Error },
                    match.Groups["message"].Value.Trim());
                if (!result.Contains(diagnostic))
                {
                    result.Add(diagnostic);
                }
            }
        }
        catch (RegexMatchTimeoutException)
        {
            // Keep the diagnostics already parsed from unusually large output.
        }

        return result;
    }

    [GeneratedRegex(@"^(?<file>.+?):(?<line>[1-9]\d*)(?::(?<column>[1-9]\d*))?:\s*(?<severity>fatal error|error|warning|note):\s*(?<message>.+)$", RegexOptions.Multiline, 100)]
    private static partial Regex Lines();
}
