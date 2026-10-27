using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>An exact literal CMSIS peripheral/register reference in source, with its corresponding SVD metadata.</summary>
/// <param name="Start">Register identifier start offset.</param>
/// <param name="Length">Register identifier length.</param>
/// <param name="Peripheral">Exact matching peripheral instance.</param>
/// <param name="Register">Exact matching register.</param>
public sealed record SvdRegisterReference(int Start, int Length, SvdPeripheral Peripheral, SvdRegister Register);

/// <summary>Finds literal CMSIS register accesses without guessing a pointer variable's target or a vendor macro name.</summary>
public static class SvdRegisterLookup
{
    /// <summary>Matches PERIPHERAL-&gt;REGISTER on the current bounded C/C++ source line, rejecting comments and quoted/raw strings.</summary>
    public static SvdRegisterReference? FindReference(SvdDevice device, string source, int position)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(source);
        if (position < 0 || position > source.Length || source.Length == 0 || !IsCodeAt(source, position)) return null;
        var start = position == 0 ? 0 : source.LastIndexOf('\n', position - 1) + 1;
        var end = source.IndexOf('\n', position);
        if (end < 0) end = source.Length;
        if (end - start > 4096) return null;
        var line = source[start..end];
        foreach (Match match in Regex.Matches(line, @"\b([A-Za-z_]\w*)\s*->\s*([A-Za-z_]\w*)\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var identifier = match.Groups[2];
            var absolute = start + identifier.Index;
            if (position < absolute || position > absolute + identifier.Length) continue;
            var peripheral = device.Peripherals.FirstOrDefault(p => p.Name == match.Groups[1].Value);
            var register = peripheral?.Registers.FirstOrDefault(r => r.Name == identifier.Value);
            if (peripheral is not null && register is not null) return new(absolute, identifier.Length, peripheral, register);
        }
        return null;
    }

    /// <summary>Returns false inside C/C++ comments, character literals, escaped strings and standard raw-string literals.</summary>
    public static bool IsCodeAt(string source, int position)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (position < 0 || position > source.Length || position > 4 * 1024 * 1024) return false;
        for (var i = 0; i < position; i++)
        {
            var character = source[i];
            if (character == '/' && i + 1 < source.Length && source[i + 1] == '/')
            {
                var end = source.IndexOf('\n', i + 2);
                while (end > 0 && (source[end - 1] == '\\' || end > 1 && source[end - 1] == '\r' && source[end - 2] == '\\'))
                    end = source.IndexOf('\n', end + 1);
                if (end < 0 || position <= end) return false;
                i = end;
            }
            else if (character == '/' && i + 1 < source.Length && source[i + 1] == '*')
            {
                var end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                if (end < 0 || position < end + 2) return false;
                i = end + 1;
            }
            else if (character == 'R' && i + 1 < source.Length && source[i + 1] == '"')
            {
                var parenthesis = source.IndexOf('(', i + 2);
                if (parenthesis < 0 || parenthesis - i - 2 > 16) continue;
                var delimiter = source[(i + 2)..parenthesis];
                if (delimiter.Any(c => char.IsWhiteSpace(c) || c is ')' or '\\')) continue;
                var closing = ")" + delimiter + "\"";
                var end = source.IndexOf(closing, parenthesis + 1, StringComparison.Ordinal);
                if (end < 0 || position < end + closing.Length) return false;
                i = end + closing.Length - 1;
            }
            else if (character is '"' or '\'')
            {
                var quote = character;
                var end = i + 1;
                for (; end < source.Length; end++)
                {
                    if (source[end] == '\\') { end++; continue; }
                    if (source[end] == quote) break;
                }
                if (end >= source.Length || position <= end) return false;
                i = end;
            }
        }
        return true;
    }
}
