using System.Text.RegularExpressions;

namespace TabStudio.App.Notebooks;

/// <summary>
/// Removes terminal escape sequences, such as the colors in Python tracebacks, from notebook output text.
/// </summary>
public static partial class AnsiText
{
    /// <summary>Returns the text without ANSI escape sequences.</summary>
    /// <param name="text">Console output.</param>
    public static string Strip(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Contains('\u001b', StringComparison.Ordinal) ? Escape().Replace(text, string.Empty) : text;
    }

    [GeneratedRegex(@"\u001b(?:\[[0-?]*[ -/]*[@-~]|\][^\u0007\u001b]*(?:\u0007|\u001b\\)|[@-Z\\-_])")]
    private static partial Regex Escape();
}
