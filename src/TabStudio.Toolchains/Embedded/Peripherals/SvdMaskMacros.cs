using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>A source-preserving replacement of a numeric mask with a macro whose actual header definition matches the SVD.</summary>
/// <param name="Start">Start of the numeric expression.</param>
/// <param name="Length">Length of the numeric expression.</param>
/// <param name="Original">Exact expected source expression.</param>
/// <param name="Macro">Verified macro name from the loaded device header.</param>
/// <param name="Field">Corresponding SVD field name.</param>
public sealed record SvdMaskReplacement(int Start, int Length, string Original, string Macro, string Field);

/// <summary>Verifies concrete object-like device-header masks using a bounded C integer-expression subset; unsupported macros remain unavailable.</summary>
public sealed class SvdMaskMacros
{
    private readonly Dictionary<string, string> _definitions;

    private SvdMaskMacros(Dictionary<string, string> definitions) => _definitions = definitions;

    /// <summary>Reads actual #define lines, including line continuations. Conditional definitions that disagree are omitted rather than guessed.</summary>
    public static SvdMaskMacros ParseHeader(string header)
    {
        ArgumentNullException.ThrowIfNull(header);
        if (header.Length > 16 * 1024 * 1024) throw new InvalidDataException("Device header exceeds 16 MiB.");
        var definitions = new Dictionary<string, string>(StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(StringComparer.Ordinal);
        var joined = header.Replace("\\\r\n", "", StringComparison.Ordinal).Replace("\\\n", "", StringComparison.Ordinal);
        joined = Regex.Replace(joined, @"/\*[\s\S]*?\*/|//[^\r\n]*", " ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        foreach (Match macro in Regex.Matches(joined, @"^\s*#\s*define[ \t]+([A-Za-z_]\w*)([ \t]+)([^\r\n]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            var name = macro.Groups[1].Value;
            var value = macro.Groups[3].Value.Trim();
            if (definitions.TryGetValue(name, out var previous) && previous != value) ambiguous.Add(name);
            else definitions[name] = value;
        }
        foreach (var name in ambiguous) definitions.Remove(name);
        return new(definitions);
    }

    /// <summary>Offers a replacement only for a numeric expression in a literal PERIPHERAL-&gt;REGISTER assignment whose value equals one field's positioned mask.</summary>
    public IReadOnlyList<SvdMaskReplacement> FindReplacements(SvdDevice device, string source, int position)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(source);
        if (position < 0 || position > source.Length || source.Length == 0 || !SvdRegisterLookup.IsCodeAt(source, position)) return [];
        var lineStart = position == 0 ? 0 : source.LastIndexOf('\n', position - 1) + 1;
        var lineEnd = source.IndexOf('\n', position);
        if (lineEnd < 0) lineEnd = source.Length;
        if (lineEnd - lineStart > 4096) return [];
        var line = source[lineStart..lineEnd];
        var result = new List<SvdMaskReplacement>();
        foreach (Match assignment in Regex.Matches(line,
            @"\b([A-Za-z_]\w*)\s*->\s*([A-Za-z_]\w*)\s*(?:\|=|\^=|=)[ \t]*([^;\r\n]+);", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
        {
            if (position < lineStart + assignment.Index || position > lineStart + assignment.Index + assignment.Length) continue;
            var expression = assignment.Groups[3];
            // Only numeric source expressions can be refactored. Never replace a user symbol with a different symbol.
            if (!Regex.IsMatch(expression.Value, @"^\s*(?:[0-9][0-9a-fA-FxXuUlL]*|[()<>|&\s])+\s*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) continue;
            if (!TryEvaluate(expression.Value, new HashSet<string>(StringComparer.Ordinal), out var value)) continue;
            var peripheral = device.Peripherals.FirstOrDefault(p => p.Name == assignment.Groups[1].Value);
            var register = peripheral?.Registers.FirstOrDefault(r => r.Name == assignment.Groups[2].Value);
            if (peripheral is null || register is null) continue;
            foreach (var field in register.Fields.Where(field => field.Mask == value))
            {
                var prefix = peripheral.Name + "_" + register.Name + "_" + field.Name;
                foreach (var candidate in new[] { prefix + "_Msk", prefix })
                {
                    if (_definitions.TryGetValue(candidate, out var definition) && TryEvaluate(definition,
                        new HashSet<string>(StringComparer.Ordinal) { candidate }, out var actual) && actual == field.Mask)
                    {
                        result.Add(new(lineStart + expression.Index, expression.Length, expression.Value, candidate, field.Name));
                        break;
                    }
                }
            }
        }
        return result;
    }

    private bool TryEvaluate(string expression, HashSet<string> visiting, out ulong value)
    {
        value = 0;
        if (expression.Length > 4096 || visiting.Count > 16) return false;
        try
        {
            var parser = new IntegerExpression(expression, name =>
            {
                if (!visiting.Add(name) || !_definitions.TryGetValue(name, out var body)) throw new FormatException("Unresolved or recursive macro.");
                try { return TryEvaluate(body, visiting, out var nested) ? nested : throw new FormatException("Unsupported macro expression."); }
                finally { visiting.Remove(name); }
            });
            value = parser.Parse();
            return true;
        }
        catch (Exception error) when (error is FormatException or OverflowException or ArgumentOutOfRangeException) { return false; }
    }

    private sealed class IntegerExpression(string text, Func<string, ulong> resolve)
    {
        private int _position;
        private int _depth;

        public ulong Parse()
        {
            var value = Or();
            Skip();
            if (_position != text.Length) throw new FormatException("Unsupported C integer syntax.");
            return value;
        }

        private ulong Or()
        {
            var value = And();
            while (Take("|")) value |= And();
            return value;
        }

        private ulong And()
        {
            var value = Shift();
            while (Take("&")) value &= Shift();
            return value;
        }

        private ulong Shift()
        {
            var value = Atom();
            while (true)
            {
                var left = Take("<<");
                if (!left && !Take(">>")) return value;
                var count = Atom();
                if (count > 63) throw new FormatException("Invalid shift count.");
                if (left && count > 0 && value > (ulong.MaxValue >> (int)count)) throw new OverflowException();
                value = left ? value << (int)count : value >> (int)count;
            }
        }

        private ulong Atom()
        {
            Skip();
            if (++_depth > 32) throw new FormatException("Expression nesting exceeds its bound.");
            try
            {
                if (Take("("))
                {
                    var value = Or();
                    if (!Take(")")) throw new FormatException("Unbalanced parentheses.");
                    return value;
                }
                var start = _position;
                while (_position < text.Length && (char.IsAsciiLetterOrDigit(text[_position]) || text[_position] == '_')) _position++;
                if (_position == start) throw new FormatException("Expected integer or object-like macro.");
                var token = text[start.._position];
                if (char.IsAsciiLetter(token[0]) || token[0] == '_') return resolve(token);
                token = token.TrimEnd('u', 'U', 'l', 'L');
                if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) return ulong.Parse(token[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
                if (token.Length > 1 && token[0] == '0') return Convert.ToUInt64(token, 8);
                return ulong.Parse(token, NumberStyles.None, CultureInfo.InvariantCulture);
            }
            finally { _depth--; }
        }

        private bool Take(string token)
        {
            Skip();
            if (!text.AsSpan(_position).StartsWith(token, StringComparison.Ordinal)) return false;
            _position += token.Length;
            return true;
        }

        private void Skip()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position])) _position++;
        }
    }
}
