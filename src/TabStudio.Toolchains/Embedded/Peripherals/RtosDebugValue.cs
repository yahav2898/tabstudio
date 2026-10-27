using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>Parses the unsigned scalar result of a fixed RTOS debugger expression, not arbitrary log text.</summary>
public static class RtosDebugValue
{
    private static readonly Regex Scalar = new(@"^\s*(?:\([^()\r\n]*\)\s*)?(?<value>0x[0-9a-fA-F]+|\d+)(?:\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    /// <summary>Accepts an unsigned decimal/hex debugger scalar, with an optional type cast and symbol annotation.</summary>
    public static ulong ParseUnsigned(string result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Length > 4096) throw new InvalidDataException("RTOS debugger scalar exceeds 4 KiB.");
        var match = Scalar.Match(result);
        if (!match.Success) throw new InvalidDataException("The debugger did not return an unsigned RTOS scalar.");
        var value = match.Groups["value"].Value;
        return value.StartsWith("0x", StringComparison.Ordinal)
            ? ulong.Parse(value[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
            : ulong.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
    }
}
