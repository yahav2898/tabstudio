using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Embedded.Peripherals;

/// <summary>A statically known physical pin assignment. Unknown expressions produce no guessed assignment.</summary>
/// <param name="Pin">Normalized physical pin name.</param>
/// <param name="Owner">Peripheral or symbolic Arduino pin role.</param>
/// <param name="Function">Assigned signal or function.</param>
/// <param name="File">Source/configuration path.</param>
/// <param name="Line">One-based source line.</param>
public sealed record PinAssignment(string Pin, string Owner, string Function, string File, int Line);

/// <summary>A pin diagnostic suitable for the normal Error List.</summary>
/// <param name="Code">Stable diagnostic code.</param>
/// <param name="Message">Explanation, with both owners for conflicts.</param>
/// <param name="File">Source/configuration path.</param>
/// <param name="Line">One-based source line.</param>
public sealed record PinDiagnostic(string Code, string Message, string File, int Line);

/// <summary>Best-effort pin checks for explicit CubeMX, devicetree and Arduino assignments; never substitutes for a board's pin map.</summary>
public static class PinConflictChecker
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Reads CubeMX physical pin Signal entries, retaining line numbers and repeated assignments.</summary>
    public static IReadOnlyList<PinAssignment> ReadCubeIoc(string text, string file)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<PinAssignment>();
        var lines = text.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var match = Regex.Match(lines[i], @"^\s*(P[A-Z]\d+)(?:-[^.\s]+)?\.Signal\s*=\s*([^\r\n#]+)", RegexOptions.CultureInvariant, RegexTimeout);
            if (!match.Success) continue;
            var signal = match.Groups[2].Value.Trim();
            if (signal is "GPIO_Input" or "GPIO_Output" or "GPIO_Analog") signal = "GPIO";
            var separator = signal.IndexOf('_');
            var owner = separator < 0 ? signal : signal[..separator];
            result.Add(new(match.Groups[1].Value, owner, match.Groups[2].Value.Trim(), file, i + 1));
        }
        return result;
    }

    /// <summary>Reads literal NRF_PSEL and STM32_PINMUX pinctrl entries. Includes are not expanded; preprocess the DTS for full coverage.</summary>
    public static IReadOnlyList<PinAssignment> ReadDevicetree(string text, string file)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = RemoveComments(text);
        var result = new List<PinAssignment>();
        var ownerStack = new Stack<string>();
        var owner = "pinctrl";
        var lines = clean.Replace("\r", "", StringComparison.Ordinal).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var node = Regex.Match(line, @"(?:([A-Za-z_]\w*)\s*:\s*)?([A-Za-z_][\w,@-]*)\s*\{", RegexOptions.CultureInvariant, RegexTimeout);
            if (node.Success)
            {
                ownerStack.Push(owner);
                // Anonymous groupN nodes retain the enclosing pinctrl instance's label.
                if (node.Groups[1].Success) owner = node.Groups[1].Value;
                else if (!node.Groups[2].Value.StartsWith("group", StringComparison.Ordinal)) owner = node.Groups[2].Value;
            }
            foreach (Match pin in Regex.Matches(line, @"NRF_PSEL\s*\(\s*([A-Z0-9_]+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.CultureInvariant, RegexTimeout))
                result.Add(new($"P{pin.Groups[2].Value}.{pin.Groups[3].Value}", owner, pin.Groups[1].Value, file, i + 1));
            foreach (Match pin in Regex.Matches(line, @"STM32_PINMUX\s*\(\s*'([A-Z])'\s*,\s*(\d+)\s*,\s*([A-Z0-9_]+)\s*\)", RegexOptions.CultureInvariant, RegexTimeout))
                result.Add(new($"P{pin.Groups[1].Value}{pin.Groups[2].Value}", owner, pin.Groups[3].Value, file, i + 1));
            foreach (var character in line)
                if (character == '}' && ownerStack.Count != 0) owner = ownerStack.Pop();
        }
        return result;
    }

    /// <summary>Reads simple integer pin constants and literal pin API calls. Dynamic expressions/macros and library internals remain unknown.</summary>
    public static IReadOnlyList<PinAssignment> ReadArduino(string text, string file)
    {
        ArgumentNullException.ThrowIfNull(text);
        var clean = RemoveComments(text);
        var constants = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (Match constant in Regex.Matches(clean,
            @"(?:#\s*define\s+|\b(?:const\s+)?(?:unsigned\s+)?(?:int|uint8_t|byte|constexpr\s+int)\s+)([A-Za-z_]\w*)\s*(?:=\s*)?(\d+|A\d+)\b", RegexOptions.CultureInvariant, RegexTimeout))
        {
            constants[constant.Groups[1].Value] = constant.Groups[2].Value;
        }
        var result = new List<PinAssignment>();
        foreach (Match call in Regex.Matches(clean, @"\b(pinMode|digitalWrite|digitalRead|analogRead|analogWrite|attachInterrupt)\s*\(\s*([A-Za-z_]\w*|\d+)\s*[,)]", RegexOptions.CultureInvariant, RegexTimeout))
        {
            var argument = call.Groups[2].Value;
            var pin = constants.GetValueOrDefault(argument);
            if (pin is null && (uint.TryParse(argument, NumberStyles.None, CultureInfo.InvariantCulture, out _) || Regex.IsMatch(argument, @"^A\d+$", RegexOptions.CultureInvariant, RegexTimeout))) pin = argument;
            if (pin is null) continue;
            var line = 1;
            for (var j = 0; j < call.Index; j++) if (clean[j] == '\n') line++;
            var owner = constants.ContainsKey(argument) ? argument : "GPIO " + pin;
            result.Add(new(pin, owner, call.Groups[1].Value, file, line));
        }
        return result;
    }

    /// <summary>Reports distinct owners sharing a physical pin, plus pins absent from an explicitly supplied selected-chip package map.</summary>
    public static IReadOnlyList<PinDiagnostic> Check(IEnumerable<PinAssignment> assignments, IReadOnlySet<string>? validPins = null)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        var all = assignments.ToArray();
        var diagnostics = new List<PinDiagnostic>();
        if (validPins is not null)
        {
            var normalizedValid = validPins.Select(Normalize).ToHashSet(StringComparer.Ordinal);
            foreach (var assignment in all)
            {
                if (!normalizedValid.Contains(Normalize(assignment.Pin)))
                    diagnostics.Add(new("TSEMBPIN002", $"Pin {assignment.Pin} does not exist in the selected chip/package pin map.", assignment.File, assignment.Line));
            }
        }
        foreach (var group in all.GroupBy(a => Normalize(a.Pin), StringComparer.Ordinal))
        {
            var owners = group.Select(a => a.Owner).Distinct(StringComparer.Ordinal).ToArray();
            if (owners.Length < 2) continue;
            foreach (var assignment in group.DistinctBy(a => (a.Owner, a.File, a.Line)))
                diagnostics.Add(new("TSEMBPIN001", $"Pin {group.Key} is assigned to {string.Join(", ", owners)}. Confirm whether the sharing is intentional.", assignment.File, assignment.Line));
        }
        return diagnostics;
    }

    private static string Normalize(string pin) => pin.Trim().ToUpperInvariant();

    private static string RemoveComments(string text)
    {
        // Preserve every character position and newline so diagnostics still refer to the original source.
        return Regex.Replace(text, """/\*[\s\S]*?\*/|//[^\r\n]*|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'""", match =>
        {
            if (match.Value.StartsWith('"') || match.Value.StartsWith('\'')) return match.Value;
            return new string(match.Value.Select(c => c is '\r' or '\n' ? c : ' ').ToArray());
        }, RegexOptions.CultureInvariant, RegexTimeout);
    }
}
