using System.Globalization;

namespace TabStudio.Containers.Runtime;

/// <summary>Linux signal names and numbers, as <c>STOPSIGNAL</c> and <c>docker kill -s</c> write them.</summary>
public static class Signals
{
    /// <summary>SIGKILL.</summary>
    public const int Kill = 9;

    private static readonly Dictionary<string, int> Numbers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HUP"] = 1, ["INT"] = 2, ["QUIT"] = 3, ["ABRT"] = 6, ["KILL"] = 9, ["USR1"] = 10, ["USR2"] = 12,
        ["PIPE"] = 13, ["ALRM"] = 14, ["TERM"] = 15, ["CHLD"] = 17, ["CONT"] = 18, ["STOP"] = 19, ["TSTP"] = 20, ["WINCH"] = 28,
        ["RTMIN+3"] = 37,
    };

    /// <summary>Parses <c>SIGTERM</c>, <c>TERM</c> or <c>15</c>.</summary>
    /// <exception cref="FormatException">It is not a signal.</exception>
    public static int Parse(string text)
    {
        text = text.Trim();
        if (int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number is > 0 and < 65) return number;
        var name = text.StartsWith("SIG", StringComparison.OrdinalIgnoreCase) ? text[3..] : text;
        return Numbers.TryGetValue(name, out number) ? number : throw new FormatException($"'{text}' is not a signal.");
    }
}
