using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Microchip;

/// <summary>Memory occupied by the linked firmware, in bytes; a missing capacity remains unknown.</summary>
/// <param name="UsedBytes">Bytes used, as reported by the linker.</param>
/// <param name="CapacityBytes">Device capacity, when the report states it in bytes.</param>
public sealed record PicMemoryRegion(long UsedBytes, long? CapacityBytes);

/// <summary>Program and data memory reported by XC8 or XC32. Missing reports are not reported as zero usage.</summary>
/// <param name="Program">Program memory.</param>
/// <param name="Data">Data memory.</param>
public sealed partial record PicMemoryUsage(PicMemoryRegion? Program, PicMemoryRegion? Data)
{
    /// <summary>Reads the final memory summaries from the build output, without counting configuration words as bytes.</summary>
    /// <param name="output">The compiler and linker's output.</param>
    public static PicMemoryUsage Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);
        PicMemoryRegion? program = null, data = null;
        foreach (Match match in Xc8().Matches(output))
        {
            var region = new PicMemoryRegion(
                long.Parse(match.Groups["used"].Value, CultureInfo.InvariantCulture),
                long.Parse(match.Groups["capacity"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (match.Groups["kind"].Value.Equals("Program", StringComparison.OrdinalIgnoreCase))
            {
                program = region;
            }
            else
            {
                data = region;
            }
        }

        foreach (Match match in Xc32().Matches(output))
        {
            var region = new PicMemoryRegion(long.Parse(match.Groups["used"].Value, CultureInfo.InvariantCulture), null);
            if (match.Groups["kind"].Value.Equals("program", StringComparison.OrdinalIgnoreCase))
            {
                program = region;
            }
            else
            {
                data = region;
            }
        }

        // XC32 6's linker uses aligned columns, without the older "(bytes)"
        // and decimal parentheses. Read its totals, not individual sections or
        // the module report (which can count linker-generated data differently).
        foreach (Match match in Xc32Columns().Matches(output))
        {
            var region = new PicMemoryRegion(
                long.Parse(match.Groups["used"].Value, CultureInfo.InvariantCulture),
                long.Parse(match.Groups["capacity"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
            if (match.Groups["kind"].Value.Equals("program", StringComparison.OrdinalIgnoreCase))
            {
                program = region;
            }
            else
            {
                data = region;
            }
        }

        return new PicMemoryUsage(program, data);
    }

    [GeneratedRegex(@"^\s*(?<kind>Program|Data)\s+space\s+used\s+[0-9a-f]+h\s*\(\s*(?<used>\d+)\)\s+of\s+(?<capacity>[0-9a-f]+)h\s+bytes", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Xc8();

    [GeneratedRegex(@"^\s*Total\s+(?<kind>program|data)\s+memory\s+used\s*\(bytes\)\s*:\s*0x[0-9a-f]+\s*\(\s*(?<used>\d+)\)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Xc32();

    [GeneratedRegex(@"^\s*Total\s+(?<kind>Program|Data)\s+Memory\s+used\s*:\s*(?:0x)?[0-9a-f]+\s+(?<used>\d+)\s+<?[\d.]+%\s+of\s+0x(?<capacity>[0-9a-f]+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex Xc32Columns();
}
