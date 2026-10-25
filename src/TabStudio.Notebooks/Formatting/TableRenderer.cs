using System.Globalization;
using System.Text;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Formatting;

/// <summary>Renders a <see cref="TableOutput"/> as HTML and as aligned plain text.</summary>
internal static class TableRenderer
{
    private const int MaxTextColumnWidth = 40;

    /// <summary>An HTML table, one row per line so that saved notebooks diff well.</summary>
    public static string ToHtml(TableOutput table)
    {
        var html = new StringBuilder("<table>\n<thead>\n<tr>");
        foreach (var column in table.Columns)
        {
            html.Append("<th>").Append(Escape(column)).Append("</th>");
        }

        html.Append("</tr>\n</thead>\n<tbody>\n");
        foreach (var row in table.Rows)
        {
            html.Append("<tr>");
            foreach (var cell in row)
            {
                html.Append("<td>").Append(Escape(cell)).Append("</td>");
            }

            html.Append("</tr>\n");
        }

        html.Append("</tbody>\n");
        if (table.IsTruncated)
        {
            html.Append("<tfoot>\n<tr><td colspan=\"")
                .Append(table.Columns.Count.ToString(CultureInfo.InvariantCulture))
                .Append("\">")
                .Append(Escape(DescribeMoreRows(table)))
                .Append("</td></tr>\n</tfoot>\n");
        }

        return html.Append("</table>").ToString();
    }

    /// <summary>A plain-text table with aligned columns, for front ends that cannot show HTML.</summary>
    public static string ToText(TableOutput table)
    {
        var widths = table.Columns.Select(c => Math.Clamp(c.Length, 1, MaxTextColumnWidth)).ToArray();
        foreach (var row in table.Rows)
        {
            for (var i = 0; i < widths.Length && i < row.Count; i++)
            {
                widths[i] = Math.Max(widths[i], Math.Min(Flatten(row[i]).Length, MaxTextColumnWidth));
            }
        }

        var text = new StringBuilder();
        AppendLine(text, table.Columns, widths);
        AppendLine(text, widths.Select(w => new string('-', w)).ToList(), widths);
        foreach (var row in table.Rows)
        {
            AppendLine(text, row, widths);
        }

        if (table.IsTruncated)
        {
            text.Append(DescribeMoreRows(table)).Append('\n');
        }

        return text.ToString().TrimEnd('\n');
    }

    /// <summary>Escapes text for HTML content and attribute values.</summary>
    public static string Escape(string text) => text
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&#39;", StringComparison.Ordinal);

    private static string DescribeMoreRows(TableOutput table) => table.TotalRowCount is { } total
        ? string.Create(CultureInfo.InvariantCulture, $"({total - table.Rows.Count} more rows)")
        : "(more rows)";

    private static void AppendLine(StringBuilder text, IReadOnlyList<string> cells, int[] widths)
    {
        var line = new StringBuilder();
        for (var i = 0; i < widths.Length; i++)
        {
            if (i > 0)
            {
                line.Append("  ");
            }

            var cell = Fit(i < cells.Count ? Flatten(cells[i]) : string.Empty, widths[i]);
            line.Append(cell.PadRight(widths[i]));
        }

        text.Append(line.ToString().TrimEnd()).Append('\n');
    }

    private static string Flatten(string text) => text.ReplaceLineEndings(" ");

    private static string Fit(string text, int width) =>
        text.Length <= width ? text : string.Concat(text.AsSpan(0, width - 1), "…");
}
