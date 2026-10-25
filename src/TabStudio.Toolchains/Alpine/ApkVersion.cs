using System.Globalization;

namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// Compares Alpine package versions the way <c>apk</c> does: <c>1.2.10 &gt; 1.2.9</c>, <c>1.0_rc1 &lt; 1.0</c>,
/// <c>1.0 &lt; 1.0_p1</c>, <c>1.0-r1 &lt; 1.0-r2</c>.
/// </summary>
/// <remarks>
/// A version is dot-separated numbers, an optional letter, any number of suffixes (<c>_alpha</c>, <c>_beta</c>,
/// <c>_pre</c> and <c>_rc</c> come before the plain version; <c>_cvs</c>, <c>_svn</c>, <c>_git</c>, <c>_hg</c> and
/// <c>_p</c> after it), each with an optional number, an optional <c>~hash</c>, and a <c>-rN</c> revision.
/// </remarks>
public static class ApkVersion
{
    private static readonly string[] Suffixes = ["alpha", "beta", "pre", "rc", "", "cvs", "svn", "git", "hg", "p"];
    private static readonly int PlainSuffix = Array.IndexOf(Suffixes, "");

    /// <summary>Compares two versions.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    /// <returns>Less than zero, zero or more than zero, as <paramref name="left"/> is older, equal or newer.</returns>
    public static int Compare(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var a = Parse(left);
        var b = Parse(right);

        for (var i = 0; i < Math.Max(a.Numbers.Count, b.Numbers.Count); i++)
        {
            // A version with more numbers is newer: 1.2.1 > 1.2.
            if (i >= a.Numbers.Count)
            {
                return -1;
            }

            if (i >= b.Numbers.Count)
            {
                return 1;
            }

            var number = a.Numbers[i].CompareTo(b.Numbers[i]);
            if (number != 0)
            {
                return number;
            }
        }

        var letter = a.Letter.CompareTo(b.Letter);
        if (letter != 0)
        {
            return letter;
        }

        for (var i = 0; i < Math.Max(a.Suffixes.Count, b.Suffixes.Count); i++)
        {
            var (suffixA, numberA) = i < a.Suffixes.Count ? a.Suffixes[i] : (PlainSuffix, 0L);
            var (suffixB, numberB) = i < b.Suffixes.Count ? b.Suffixes[i] : (PlainSuffix, 0L);
            var suffix = suffixA.CompareTo(suffixB);
            if (suffix != 0)
            {
                return suffix;
            }

            var suffixNumber = numberA.CompareTo(numberB);
            if (suffixNumber != 0)
            {
                return suffixNumber;
            }
        }

        return a.Revision.CompareTo(b.Revision);
    }

    /// <summary>Whether a version satisfies a constraint such as <c>&gt;=3.14</c>.</summary>
    /// <param name="version">The version.</param>
    /// <param name="operation">The operator: <c>=</c>, <c>&lt;</c>, <c>&lt;=</c>, <c>&gt;</c>, <c>&gt;=</c> or <c>~</c> (a version that starts with the constraint's).</param>
    /// <param name="constraint">The version in the constraint.</param>
    public static bool Satisfies(string version, string operation, string constraint)
    {
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(constraint);
        if (operation.Contains('~', StringComparison.Ordinal))
        {
            // Fuzzy: 3.14 matches 3.14, 3.14.8 and 3.14.8-r0, but not 3.140.
            return version == constraint || version.StartsWith(constraint + ".", StringComparison.Ordinal)
                || version.StartsWith(constraint + "-", StringComparison.Ordinal) || version.StartsWith(constraint + "_", StringComparison.Ordinal);
        }

        var comparison = Compare(version, constraint);
        return operation switch
        {
            "=" => comparison == 0,
            "<" => comparison < 0,
            "<=" or "=<" => comparison <= 0,
            ">" => comparison > 0,
            ">=" or "=>" => comparison >= 0,
            _ => true,
        };
    }

    /// <summary>The version without its revision, for the UI: <c>3.14.8-r0</c> becomes <c>3.14.8</c>.</summary>
    /// <param name="version">A package version.</param>
    public static string Display(string version)
    {
        ArgumentNullException.ThrowIfNull(version);
        var revision = version.LastIndexOf("-r", StringComparison.Ordinal);
        return revision > 0 ? version[..revision] : version;
    }

    private static Parsed Parse(string version)
    {
        var numbers = new List<long>();
        var suffixes = new List<(int, long)>();
        var letter = '\0';
        long revision = 0;
        var i = 0;

        // Numbers, separated by dots.
        while (i < version.Length && char.IsAsciiDigit(version[i]))
        {
            numbers.Add(ReadNumber(version, ref i));
            if (i < version.Length && version[i] == '.' && i + 1 < version.Length && char.IsAsciiDigit(version[i + 1]))
            {
                i++;
            }
            else
            {
                break;
            }
        }

        if (i < version.Length && char.IsAsciiLetterLower(version[i]))
        {
            letter = version[i++];
        }

        while (i < version.Length && version[i] == '_')
        {
            var start = ++i;
            while (i < version.Length && char.IsAsciiLetter(version[i]))
            {
                i++;
            }

            var name = version[start..i];
            var index = Array.IndexOf(Suffixes, name);
            suffixes.Add((index < 0 ? PlainSuffix : index, i < version.Length && char.IsAsciiDigit(version[i]) ? ReadNumber(version, ref i) : 0));
        }

        if (i < version.Length && version[i] == '~')
        {
            while (i < version.Length && version[i] != '-')
            {
                i++;
            }
        }

        if (i + 1 < version.Length && version[i] == '-' && version[i + 1] == 'r')
        {
            i += 2;
            revision = i < version.Length && char.IsAsciiDigit(version[i]) ? ReadNumber(version, ref i) : 0;
        }

        return new Parsed(numbers, letter, suffixes, revision);
    }

    private static long ReadNumber(string text, ref int i)
    {
        var start = i;
        while (i < text.Length && char.IsAsciiDigit(text[i]))
        {
            i++;
        }

        // Numbers beyond long's range compare by their first 18 digits, which no real version reaches.
        var digits = text[start..i];
        return long.Parse(digits.Length > 18 ? digits[..18] : digits, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private sealed record Parsed(List<long> Numbers, char Letter, List<(int Suffix, long Number)> Suffixes, long Revision);
}
