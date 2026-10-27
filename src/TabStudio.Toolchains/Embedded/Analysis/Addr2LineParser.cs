using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Analysis;

/// <summary>A source frame reported by GNU addr2line for the matching ELF.</summary>
/// <param name="Address">Captured instruction address.</param>
/// <param name="Function">Demangled function name.</param>
/// <param name="File">Compiler-reported source path.</param>
/// <param name="Line">One-based source line.</param>
public sealed record FirmwareSourceFrame(ulong Address, string Function, string File, int Line);

/// <summary>Parses GNU addr2line -a -f -C output without inventing unknown locations.</summary>
public static partial class Addr2LineParser
{
    /// <summary>Reads three-line address/function/location records. Unresolved locations are omitted.</summary>
    public static IReadOnlyList<FirmwareSourceFrame> Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        var lines = output.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        var result = new List<FirmwareSourceFrame>();
        for (var i = 0; i + 2 < lines.Length; i++)
        {
            var address = lines[i].Trim();
            if (!address.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                || !ulong.TryParse(address.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var pc))
            {
                continue;
            }
            var function = lines[++i].Trim();
            var source = LocationLine().Match(lines[++i].Trim());
            if (source.Success && source.Groups[1].Value != "??"
                && int.TryParse(source.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var line) && line > 0)
            {
                result.Add(new(pc, function, source.Groups[1].Value, line));
            }
        }
        return result;
    }

    [GeneratedRegex(@"^(.+):(\d+)(?:\s+\(discriminator\s+\d+\))?$", RegexOptions.CultureInvariant)]
    private static partial Regex LocationLine();
}
