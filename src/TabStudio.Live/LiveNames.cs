using System.Globalization;
using System.Text;

namespace TabStudio.Live;

/// <summary>How participants' names are shown.</summary>
public static class LiveNames
{
    private static readonly char[] WordSeparators = [' ', '-', '_'];

    /// <summary>A name as a session shows it: trimmed, without control characters, and at most <see cref="LiveProtocol.MaxNameLength"/> characters.</summary>
    /// <param name="name">The name a device gave.</param>
    public static string Clean(string? name)
    {
        var text = new StringBuilder();
        foreach (var c in name ?? string.Empty)
        {
            if (!char.IsControl(c) && text.Length < LiveProtocol.MaxNameLength)
            {
                text.Append(c);
            }
        }

        var cleaned = text.ToString().Trim();
        return cleaned.Length == 0 ? "Guest" : cleaned;
    }

    /// <summary>The initials of a name: the first letter of its first two words, upper case.</summary>
    /// <param name="name">The name.</param>
    public static string Initials(string? name)
    {
        var words = (name ?? string.Empty).Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 0 && char.IsLetterOrDigit(w[0]))
            .Take(2)
            .Select(w => char.ToUpper(w[0], CultureInfo.InvariantCulture));
        var initials = string.Concat(words);
        return initials.Length == 0 ? "?" : initials;
    }
}
