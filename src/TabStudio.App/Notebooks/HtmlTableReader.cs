using System.Globalization;
using System.Text.RegularExpressions;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.App.Notebooks;

/// <summary>
/// Reads a simple HTML table back into a <see cref="TableOutput"/>, so tables saved in a notebook file (which
/// keeps only their HTML and text) are shown as a native grid when the notebook is opened again.
/// </summary>
/// <remarks>
/// It understands the tables TabStudio's kernel writes and the flat tables of Polyglot Notebooks and pandas.
/// Anything more elaborate, such as a table nested in a cell, is left to the plain-text view.
/// </remarks>
public static partial class HtmlTableReader
{
    /// <summary>Reads the HTML of an output as a table.</summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The table, or <see langword="null"/> when the HTML is not one simple table.</returns>
    public static TableOutput? TryRead(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var tables = TableStart().Count(html);
        if (tables != 1)
        {
            return null;
        }

        var footer = Footer().Match(html);
        var body = footer.Success ? html.Remove(footer.Index, footer.Length) : html;

        List<string>? columns = null;
        var rows = new List<IReadOnlyList<string>>();
        foreach (Match row in Row().Matches(body))
        {
            var cells = Cell().Matches(row.Groups["cells"].Value);
            if (cells.Count == 0)
            {
                continue;
            }

            var texts = cells.Select(c => HtmlText.ToSingleLine(c.Groups["content"].Value)).ToList();
            if (columns is null && cells.All(c => c.Groups["tag"].Value.Equals("th", StringComparison.OrdinalIgnoreCase)))
            {
                columns = texts;
            }
            else
            {
                rows.Add(texts);
            }
        }

        if (rows.Count == 0 && columns is null)
        {
            return null;
        }

        var width = Math.Max(columns?.Count ?? 0, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
        var normalized = rows.Select(r => (IReadOnlyList<string>)Pad(r, width)).ToList();
        var more = footer.Success ? MoreRows().Match(HtmlText.ToSingleLine(footer.Value)) : Match.Empty;
        return new TableOutput(Pad(columns ?? [], width), normalized)
        {
            IsTruncated = footer.Success,
            TotalRowCount = more.Success ? normalized.Count + int.Parse(more.Groups["count"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture) : null,
        };
    }

    private static List<string> Pad(IReadOnlyList<string> cells, int width)
    {
        var padded = new List<string>(Math.Max(width, cells.Count));
        padded.AddRange(cells);
        while (padded.Count < width)
        {
            padded.Add(string.Empty);
        }

        return padded;
    }

    [GeneratedRegex(@"<table\b", RegexOptions.IgnoreCase)]
    private static partial Regex TableStart();

    [GeneratedRegex(@"<tfoot\b.*?</tfoot\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Footer();

    [GeneratedRegex(@"<tr\b[^>]*>(?<cells>.*?)</tr\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Row();

    [GeneratedRegex(@"<(?<tag>td|th)\b[^>]*>(?<content>.*?)</\k<tag>\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex Cell();

    [GeneratedRegex(@"(?<count>\d{1,9}) more rows")]
    private static partial Regex MoreRows();
}
