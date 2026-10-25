using System.Globalization;

namespace TabStudio.LanguageServers.Loopback;

/// <summary>What a line of a loopback script does.</summary>
public enum LoopbackStatementKind
{
    /// <summary>Nothing: a blank line or a comment that starts with <c>#</c>.</summary>
    None,

    /// <summary><c>let name = value</c>: gives a name a value.</summary>
    Let,

    /// <summary><c>print value</c>: writes a value to the output.</summary>
    Print,

    /// <summary>Something that is not a statement.</summary>
    Unknown,
}

/// <summary>A line of a loopback script.</summary>
/// <param name="Line">Zero-based line.</param>
/// <param name="Kind">What it does.</param>
/// <param name="Name">The name a <c>let</c> sets, or the name or value a <c>print</c> writes.</param>
/// <param name="NameColumn">Zero-based column of <paramref name="Name"/>.</param>
/// <param name="Value">The value a <c>let</c> gives, as written.</param>
/// <param name="ValueColumn">Zero-based column of <paramref name="Value"/>.</param>
/// <param name="Text">The whole line.</param>
public sealed record LoopbackStatement(int Line, LoopbackStatementKind Kind, string Name, int NameColumn, string Value, int ValueColumn, string Text)
{
    /// <summary>Zero-based column where the statement starts.</summary>
    public int Column => Text.Length - Text.TrimStart().Length;
}

/// <summary>
/// The tiny language TabStudio's built-in loopback language server and debug adapter understand, so the self test
/// can prove the editor and debugger on a device that has no real servers installed:
/// <code>
/// # Lines that start with # are comments.
/// let greeting = "Hello"
/// let count = 3
/// print greeting
/// </code>
/// A value is a number, a string in double quotes, or a name given a value on an earlier line.
/// </summary>
public static class LoopbackScript
{
    /// <summary>The language's id, as the loopback settings name it.</summary>
    public const string LanguageId = "loopback";

    /// <summary>The extension of loopback scripts.</summary>
    public const string Extension = ".loopback";

    /// <summary>Reads a script into its statements, one per line.</summary>
    /// <param name="text">The script.</param>
    public static List<LoopbackStatement> Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var statements = new List<LoopbackStatement>();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            statements.Add(ParseLine(i, lines[i].TrimEnd('\r')));
        }

        return statements;
    }

    /// <summary>Whether a text is a name: letters, digits and underscores, not starting with a digit.</summary>
    /// <param name="text">The text.</param>
    public static bool IsName(string text) =>
        text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.All(c => char.IsLetterOrDigit(c) || c == '_');

    /// <summary>The value of an expression: a number, a string, or a name's value; <see langword="null"/> when it has none.</summary>
    /// <param name="expression">The expression, as written.</param>
    /// <param name="variables">The names given values so far.</param>
    public static string? Evaluate(string expression, IReadOnlyDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(variables);
        var trimmed = expression.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed;
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return trimmed;
        }

        return variables.TryGetValue(trimmed, out var value) ? value : null;
    }

    /// <summary>The type of a value, as the Locals panel shows it.</summary>
    /// <param name="value">The value.</param>
    public static string TypeOf(string value) => value.StartsWith('"') ? "string" : "number";

    /// <summary>A value as <c>print</c> writes it: strings without their quotes.</summary>
    /// <param name="value">The value.</param>
    public static string Display(string value) => value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;

    private static LoopbackStatement ParseLine(int line, string text)
    {
        var trimmed = text.Trim();
        var start = text.Length - text.TrimStart().Length;
        if (trimmed.Length == 0 || trimmed.StartsWith('#'))
        {
            return new LoopbackStatement(line, LoopbackStatementKind.None, string.Empty, start, string.Empty, start, text);
        }

        if (trimmed.StartsWith("let ", StringComparison.Ordinal))
        {
            var nameStart = start + 4;
            while (nameStart < text.Length && text[nameStart] == ' ')
            {
                nameStart++;
            }

            var nameEnd = nameStart;
            while (nameEnd < text.Length && (char.IsLetterOrDigit(text[nameEnd]) || text[nameEnd] == '_'))
            {
                nameEnd++;
            }

            var equals = text.IndexOf('=', nameEnd);
            var valueStart = equals < 0 ? text.Length : equals + 1;
            while (valueStart < text.Length && text[valueStart] == ' ')
            {
                valueStart++;
            }

            return new LoopbackStatement(line, LoopbackStatementKind.Let, text[nameStart..nameEnd], nameStart, equals < 0 ? string.Empty : text[valueStart..].TrimEnd(), valueStart, text);
        }

        if (trimmed.StartsWith("print ", StringComparison.Ordinal) || trimmed == "print")
        {
            var valueStart = Math.Min(text.Length, start + 5);
            while (valueStart < text.Length && text[valueStart] == ' ')
            {
                valueStart++;
            }

            var value = text[valueStart..].TrimEnd();
            return new LoopbackStatement(line, LoopbackStatementKind.Print, value, valueStart, value, valueStart, text);
        }

        return new LoopbackStatement(line, LoopbackStatementKind.Unknown, trimmed, start, string.Empty, start, text);
    }
}
