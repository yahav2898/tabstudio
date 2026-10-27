using System.Globalization;
using System.Net;
using System.Text;
using TabStudio.Toolchains.Embedded.Analysis;

namespace TabStudio.App.Services;

/// <summary>A local, script-free memory report. Rectangle areas are proportional to section byte sizes.</summary>
internal static class EmbeddedMemoryReport
{
    internal static string Create(FirmwareMemoryReport report, FirmwareMemoryDelta? delta,
        IReadOnlyList<FirmwareObjectContribution>? objects = null)
    {
        var html = new StringBuilder("""
            <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
            <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'; img-src 'none'">
            <title>Firmware memory</title><style>
            :root{color-scheme:light dark;font:16px system-ui;background:Canvas;color:CanvasText}
            body{margin:24px;max-width:1100px}h1{font-size:28px}h2{font-size:22px;margin-top:32px}
            table{border-collapse:collapse;width:100%;font-variant-numeric:tabular-nums}td,th{text-align:left;padding:10px;border-bottom:1px solid GrayText;overflow-wrap:anywhere}
            svg{width:100%;height:auto;border:1px solid GrayText}rect{stroke:Canvas;stroke-width:2;fill:color-mix(in srgb,Highlight 35%,Canvas)}
            .tile1{fill:color-mix(in srgb,Highlight 48%,Canvas)}.tile2{fill:color-mix(in srgb,Highlight 60%,Canvas)}
            text{fill:CanvasText;font:16px system-ui}.note{color:GrayText}code{overflow-wrap:anywhere}
            </style><body><h1>Firmware memory</h1>
            """);
        html.Append(CultureInfo.InvariantCulture, $"<p><strong>Flash: {report.FlashBytes:N0} B · RAM: {report.RamBytes:N0} B</strong></p>");
        if (delta is not null)
            html.Append(CultureInfo.InvariantCulture, $"<p>Since previous build: flash {delta.FlashBytes:+#,0;-#,0;0} B · RAM {delta.RamBytes:+#,0;-#,0;0} B</p>");
        if (report.PlacementInferred)
            html.Append("<p class=note>Some RAM placement is inferred from ELF writable-section flags. Configure the selected chip's memory regions for precise placement.</p>");
        html.Append("<p class=note>Flash includes initialized RAM's load image. RAM excludes runtime heap/stack growth unless reserved by ELF sections. Debug sections are excluded. Symbol aliases are listed separately and never added to section totals.</p>");
        AddMap(html, "Flash load image", report.Sections.Where(s => s.FileSize > 0).Select(s => (s.Name, s.FileSize)).ToArray(), "flash");
        AddMap(html, "RAM at runtime", report.Sections.Where(s => s.IsRam).Select(s => (s.Name, s.Size)).ToArray(), "ram");
        html.Append("<h2>Allocated sections</h2><table><thead><tr><th>Section</th><th>Runtime address</th><th>Runtime size</th><th>Stored bytes</th><th>Region</th></tr></thead><tbody>");
        foreach (var section in report.Sections.OrderByDescending(s => s.Size))
            html.Append(CultureInfo.InvariantCulture, $"<tr><td>{E(section.Name)}</td><td><code>0x{section.Address:X}</code></td><td>{section.Size:N0} B</td><td>{section.FileSize:N0} B</td><td>{E(section.Region ?? (section.IsRam ? "RAM (inferred)" : "Flash (inferred)"))}</td></tr>");
        html.Append("</tbody></table>");
        if (objects is { Count: > 0 })
        {
            html.Append("<h2>Object files from GNU linker map</h2><p class=note>Input-section contributions exclude discarded sections and fill. Archive members remain separate. Overlay contributions can overlap; these rows are not added to ELF totals. Largest 1,000 files shown.</p><table><thead><tr><th>Object or archive member</th><th>Input-section bytes</th></tr></thead><tbody>");
            foreach (var file in objects.GroupBy(o => o.ObjectFile, StringComparer.Ordinal)
                .Select(group => (Name: group.Key, Size: group.Aggregate(0ul, (sum, row) => checked(sum + row.Size))))
                .OrderByDescending(file => file.Size).Take(1000))
            {
                html.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{E(file.Name)}</code></td><td>{file.Size:N0} B</td></tr>");
            }
            html.Append("</tbody></table>");
        }
        html.Append("<h2>Largest symbols</h2><table><thead><tr><th>Symbol</th><th>Size</th><th>Section</th><th>Source ownership</th></tr></thead><tbody>");
        foreach (var symbol in report.Symbols.Take(100))
            html.Append(CultureInfo.InvariantCulture, $"<tr><td><code>{E(symbol.Name)}</code></td><td>{symbol.Size:N0} B</td><td>{E(symbol.Section)}</td><td>{E(symbol.SourceFile ?? "Not recorded in ELF")}</td></tr>");
        return html.Append("</tbody></table></body></html>").ToString();
    }

    private static void AddMap(StringBuilder html, string heading, (string Name, ulong Size)[] items, string id)
    {
        html.Append("<h2>").Append(E(heading)).Append("</h2>");
        if (items.Length == 0) { html.Append("<p>No allocated sections.</p>"); return; }
        html.Append("<svg viewBox=\"0 0 1000 400\" role=img aria-label=\"").Append(E(heading)).Append(" section treemap\">");
        var index = 0;
        void Draw((string Name, ulong Size)[] part, double x, double y, double width, double height)
        {
            if (part.Length == 1)
            {
                var item = part[0];
                var tileId = id + index.ToString(CultureInfo.InvariantCulture);
                html.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"{tileId}\"><rect x=\"{x:F3}\" y=\"{y:F3}\" width=\"{width:F3}\" height=\"{height:F3}\"/></clipPath><g><title>{E(item.Name)}: {item.Size} B</title><rect class=\"tile{index % 3}\" x=\"{x:F3}\" y=\"{y:F3}\" width=\"{width:F3}\" height=\"{height:F3}\"/><text x=\"{x + 5:F3}\" y=\"{y + 20:F3}\" clip-path=\"url(#{tileId})\">{E(item.Name)} · {item.Size} B</text></g>");
                index++;
                return;
            }
            var total = part.Sum(p => (double)p.Size);
            double first = 0;
            var cut = 0;
            while (cut < part.Length - 1 && first < total / 2) first += part[cut++].Size;
            var fraction = first / total;
            if (width >= height)
            {
                Draw(part[..cut], x, y, width * fraction, height);
                Draw(part[cut..], x + width * fraction, y, width * (1 - fraction), height);
            }
            else
            {
                Draw(part[..cut], x, y, width, height * fraction);
                Draw(part[cut..], x, y + height * fraction, width, height * (1 - fraction));
            }
        }
        Draw(items.OrderByDescending(p => p.Size).ToArray(), 0, 0, 1000, 400);
        html.Append("</svg>");
    }

    private static string E(string text) => WebUtility.HtmlEncode(text);
}
