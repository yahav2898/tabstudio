using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using TabStudio.App.Notebooks.Markdown;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>A part of an extension's README: Markdown text, or an image on a line of its own.</summary>
public abstract record ReadmeSegment;

/// <summary>Markdown text: headings, paragraphs, lists, code, quotes and tables.</summary>
/// <param name="Blocks">The blocks.</param>
public sealed record ReadmeText(IReadOnlyList<MarkdownBlock> Blocks) : ReadmeSegment;

/// <summary>An image on a line of its own, such as a screenshot.</summary>
/// <param name="Source">Where the image is: an address, or a path inside the extension's package.</param>
/// <param name="AltText">Its description.</param>
public sealed record ReadmeImageSource(string Source, string AltText) : ReadmeSegment;

/// <summary>
/// Reads an extension's README into segments for the details page. READMEs are written for GitHub and the VS Code
/// Marketplace, so besides Markdown they use some HTML: images (<c>&lt;img&gt;</c>) are kept, headings written as
/// <c>&lt;h1&gt;</c> become headings, and other tags are dropped. Rows of badges are left out.
/// </summary>
public static partial class ReadmeParser
{
    /// <summary>Reads a README.</summary>
    /// <param name="markdown">The README's text.</param>
    public static IReadOnlyList<ReadmeSegment> Parse(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var segments = new List<ReadmeSegment>();
        var text = new StringBuilder();
        var inFence = false;
        foreach (var raw in HtmlComment().Replace(markdown.ReplaceLineEndings("\n"), string.Empty).Split('\n'))
        {
            var line = raw;
            if (Fence().IsMatch(line))
            {
                inFence = !inFence;
                text.Append(line).Append('\n');
                continue;
            }

            if (inFence)
            {
                text.Append(line).Append('\n');
                continue;
            }

            var images = FindImages(line);
            if (images.Count > 0 && IsOnlyImages(line))
            {
                // A row of several small images is a row of badges; a single image is a picture worth showing.
                if (images.Count == 1 || !images.All(IsBadge))
                {
                    Flush(text, segments);
                    segments.AddRange(images.Where(i => images.Count == 1 || !IsBadge(i)));
                }

                continue;
            }

            text.Append(ToMarkdown(line)).Append('\n');
        }

        Flush(text, segments);
        return segments;
    }

    /// <summary>The images on a line, as Markdown (<c>![alt](src)</c>) or HTML (<c>&lt;img src alt&gt;</c>) writes them.</summary>
    private static List<ReadmeImageSource> FindImages(string line)
    {
        var images = new List<(int Position, ReadmeImageSource Image)>();
        foreach (Match match in MarkdownImage().Matches(line))
        {
            images.Add((match.Index, new ReadmeImageSource(match.Groups["src"].Value.Trim('<', '>'), match.Groups["alt"].Value)));
        }

        foreach (Match match in HtmlImage().Matches(line))
        {
            var attributes = match.Groups["attributes"].Value;
            if (Attribute(attributes, "src") is { Length: > 0 } source)
            {
                images.Add((match.Index, new ReadmeImageSource(WebUtility.HtmlDecode(source), WebUtility.HtmlDecode(Attribute(attributes, "alt") ?? string.Empty))));
            }
        }

        return [.. images.OrderBy(i => i.Position).Select(i => i.Image)];
    }

    /// <summary>Whether a line holds nothing but images, links around them and HTML tags.</summary>
    private static bool IsOnlyImages(string line)
    {
        var rest = HtmlImage().Replace(line, string.Empty);
        rest = LinkedImage().Replace(rest, string.Empty);
        rest = MarkdownImage().Replace(rest, string.Empty);
        rest = HtmlTag().Replace(rest, string.Empty);
        rest = rest.Replace("&nbsp;", string.Empty, StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(rest);
    }

    /// <summary>Whether an image is a badge, such as a build status or a version from shields.io.</summary>
    private static bool IsBadge(ReadmeImageSource image)
    {
        var source = image.Source;
        return source.Contains("shields.io", StringComparison.OrdinalIgnoreCase)
            || source.Contains("badge", StringComparison.OrdinalIgnoreCase)
            || source.Contains("vsmarketplacebadge", StringComparison.OrdinalIgnoreCase)
            || source.EndsWith(".svg", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A line of HTML the README uses as Markdown: <c>&lt;h2&gt;Title&lt;/h2&gt;</c> becomes <c>## Title</c>; other tags go.</summary>
    private static string ToMarkdown(string line)
    {
        if (HtmlHeading().Match(line) is { Success: true } heading)
        {
            var level = heading.Groups["level"].Value[0] - '0';
            return new string('#', level) + " " + WebUtility.HtmlDecode(HtmlTag().Replace(heading.Groups["text"].Value, string.Empty)).Trim();
        }

        if (!line.Contains('<', StringComparison.Ordinal))
        {
            return line;
        }

        var text = BreakTag().Replace(line, "  \n");
        text = HtmlImage().Replace(text, match => Attribute(match.Groups["attributes"].Value, "alt") ?? string.Empty);
        text = HtmlLink().Replace(text, match => Attribute(match.Groups["attributes"].Value, "href") is { Length: > 0 } href
            ? $"[{match.Groups["text"].Value}]({href})"
            : match.Groups["text"].Value);
        text = StrongTag().Replace(text, "**");
        text = EmphasisTag().Replace(text, "*");
        text = CodeTag().Replace(text, "`");
        return BlockTag().Replace(text, string.Empty);
    }

    private static string? Attribute(string attributes, string name)
    {
        foreach (Match match in HtmlAttribute().Matches(attributes))
        {
            if (string.Equals(match.Groups["name"].Value, name, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["value"].Value;
            }
        }

        return null;
    }

    private static void Flush(StringBuilder text, List<ReadmeSegment> segments)
    {
        if (text.Length == 0)
        {
            return;
        }

        var blocks = MarkdownParser.Parse(text.ToString());
        text.Clear();
        if (blocks.Count > 0)
        {
            segments.Add(new ReadmeText(blocks));
        }
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlComment();

    [GeneratedRegex(@"^ {0,3}(`{3,}|~{3,})")]
    private static partial Regex Fence();

    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<src><[^>]*>|[^\s)]+)(?:\s+""[^""]*"")?\)")]
    private static partial Regex MarkdownImage();

    [GeneratedRegex(@"\[\s*!\[[^\]]*\]\([^)]*\)\s*\]\([^)]*\)")]
    private static partial Regex LinkedImage();

    [GeneratedRegex(@"<img\b(?<attributes>[^>]*)/?>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlImage();

    [GeneratedRegex(@"(?<name>[A-Za-z-]+)\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)'|(?<value>[^\s>]+))")]
    private static partial Regex HtmlAttribute();

    [GeneratedRegex(@"</?[A-Za-z][^>]*>")]
    private static partial Regex HtmlTag();

    [GeneratedRegex(@"^\s*<h(?<level>[1-6])\b[^>]*>(?<text>.*?)</h\k<level>>\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlHeading();

    [GeneratedRegex(@"<br\s*/?>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakTag();

    [GeneratedRegex(@"<a\b(?<attributes>[^>]*)>(?<text>.*?)</a>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlLink();

    [GeneratedRegex(@"</?(?:b|strong)>", RegexOptions.IgnoreCase)]
    private static partial Regex StrongTag();

    [GeneratedRegex(@"</?(?:i|em)>", RegexOptions.IgnoreCase)]
    private static partial Regex EmphasisTag();

    [GeneratedRegex(@"</?code>", RegexOptions.IgnoreCase)]
    private static partial Regex CodeTag();

    [GeneratedRegex(@"</?(?:p|div|span|center|a|sub|sup|picture|source|details|summary|table|tr|td|th|tbody|thead|kbd|font)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();
}
