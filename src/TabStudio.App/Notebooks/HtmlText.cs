using System.Net;
using System.Text.RegularExpressions;

namespace TabStudio.App.Notebooks;

/// <summary>
/// Turns HTML output into readable text, for notebook outputs that have no plain-text version. Avalonia has no
/// HTML renderer, so this is how TabStudio shows HTML it cannot draw natively.
/// </summary>
public static partial class HtmlText
{
    /// <summary>Converts HTML to plain text: tags removed, entities decoded, blocks on lines of their own.</summary>
    /// <param name="html">The HTML.</param>
    public static string ToPlainText(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var text = Hidden().Replace(html, string.Empty);
        text = LineBreak().Replace(text, "\n");
        text = CellEnd().Replace(text, "\t");
        text = Tag().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        text = SpaceRun().Replace(text, " ");
        text = BlankLines().Replace(text, "\n\n");
        return string.Join('\n', text.Split('\n').Select(line => line.Trim())).Trim();
    }

    /// <summary>Converts a fragment of HTML, such as a table cell, to one line of text.</summary>
    /// <param name="html">The HTML fragment.</param>
    public static string ToSingleLine(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return SpaceRun().Replace(ToPlainText(html).Replace('\n', ' ').Replace('\t', ' '), " ").Trim();
    }

    [GeneratedRegex(@"<(script|style|head)\b[^>]*>.*?</\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Hidden();

    [GeneratedRegex(@"<br\s*/?>|</(p|div|tr|li|h[1-6]|pre|table|thead|tbody|tfoot|ul|ol|blockquote|details|summary)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex LineBreak();

    [GeneratedRegex(@"</(td|th)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex CellEnd();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[  ]{2,}")]
    private static partial Regex SpaceRun();

    [GeneratedRegex(@"\n[ \t]*\n(?:[ \t]*\n)+")]
    private static partial Regex BlankLines();
}
