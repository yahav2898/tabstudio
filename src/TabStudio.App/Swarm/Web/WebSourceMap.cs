using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.App.Swarm.Web;

/// <summary>Where an element of a web app's page is written: the file, the element's start tag, and its source.</summary>
/// <param name="FilePath">The page's file.</param>
/// <param name="Start">Where the element's start tag starts, as an offset in the file.</param>
/// <param name="Length">The start tag's length.</param>
/// <param name="Line">The start tag's line, from 1.</param>
/// <param name="Column">The start tag's column, from 1.</param>
/// <param name="Snippet">The element's source, or its start tag when the element is long.</param>
public sealed record WebSourceLocation(string FilePath, int Start, int Length, int Line, int Column, string Snippet);

/// <summary>
/// Finds the element a web screen's path names in the app's HTML: by the deepest id in the path, which names one
/// element wherever it is, then by tag and index among the children below it, as the page's script counts them.
/// The page shown is searched first, then the project's other pages.
/// </summary>
public static partial class WebSourceMap
{
    private const int SnippetLength = 600;

    private static readonly string[] PageFiles = ["*.html", "*.htm", "*.cshtml", "*.razor"];

    private static readonly HashSet<string> VoidElements = new(StringComparer.OrdinalIgnoreCase)
    {
        "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "source", "track", "wbr",
    };

    /// <summary>Finds an element in the project's pages.</summary>
    /// <param name="projectDirectory">The web project's folder.</param>
    /// <param name="page">The page shown, whose file is searched first.</param>
    /// <param name="elementPath">The element's path, such as <c>body/main[0]/form#form/button[0]</c>.</param>
    /// <returns>Where it is written, or <see langword="null"/> when no page has it.</returns>
    public static WebSourceLocation? Find(string projectDirectory, Uri? page, string elementPath)
    {
        ArgumentNullException.ThrowIfNull(projectDirectory);
        ArgumentNullException.ThrowIfNull(elementPath);
        var segments = elementPath.Split('/');
        if (segments.Length == 0 || segments[0] != "body" || !Directory.Exists(projectDirectory))
        {
            return null;
        }

        foreach (var file in PagesOf(projectDirectory, page))
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (IOException)
            {
                continue;
            }

            if (Find(text, segments) is { } start)
            {
                var tagEnd = text.IndexOf('>', start);
                var length = (tagEnd < 0 ? text.Length : tagEnd + 1) - start;
                var (line, column) = LineAndColumn(text, start);
                return new WebSourceLocation(file, start, length, line, column, Snippet(text, start, length));
            }
        }

        return null;
    }

    /// <summary>Where the element a path names starts in a page's source.</summary>
    internal static int? Find(string text, IReadOnlyList<string> segments)
    {
        // The deepest id names the element below which the rest of the path is counted.
        var from = 0;
        int? element = null;
        for (var i = segments.Count - 1; i > 0; i--)
        {
            if (Segment().Match(segments[i]) is { Success: true, Groups: var groups } && groups["id"].Success)
            {
                var tag = Regex.Escape(groups["tag"].Value);
                var id = Regex.Escape(groups["id"].Value);
                var match = Regex.Match(text, $"""<{tag}\b[^>]*\bid\s*=\s*["']{id}["']""", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                if (!match.Success)
                {
                    return null;
                }

                element = match.Index;
                from = i + 1;
                break;
            }
        }

        if (element is null)
        {
            var body = Regex.Match(text, @"<body\b", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
            if (!body.Success)
            {
                return null;
            }

            element = body.Index;
            from = 1;
        }

        for (var i = from; i < segments.Count && element is not null; i++)
        {
            if (Segment().Match(segments[i]) is not { Success: true, Groups: var groups } || !groups["index"].Success)
            {
                return null;
            }

            element = Child(text, element.Value, groups["tag"].Value, int.Parse(groups["index"].Value, CultureInfo.InvariantCulture));
        }

        return element;
    }

    /// <summary>The start of an element's child with a tag, counted among the children with that tag.</summary>
    private static int? Child(string text, int parent, string tag, int index)
    {
        var open = text.IndexOf('>', parent);
        if (open < 0 || text[open - 1] == '/')
        {
            return null;
        }

        var depth = 0;
        for (var token = Tag().Match(text, open + 1); token.Success; token = Tag().Match(text, token.Index + token.Length))
        {
            // Comments hold no elements.
            if (!token.Groups["name"].Success)
            {
                continue;
            }

            var name = token.Groups["name"].Value;
            if (token.Groups["close"].Success)
            {
                if (depth == 0)
                {
                    return null;
                }

                depth--;
                continue;
            }

            if (depth == 0 && name.Equals(tag, StringComparison.OrdinalIgnoreCase) && index-- == 0)
            {
                return token.Index;
            }

            if (token.Groups["self"].Success || VoidElements.Contains(name))
            {
                continue;
            }

            // Scripts and styles hold text, whatever it looks like.
            if (name.Equals("script", StringComparison.OrdinalIgnoreCase) || name.Equals("style", StringComparison.OrdinalIgnoreCase))
            {
                var close = text.IndexOf($"</{name}", token.Index + token.Length, StringComparison.OrdinalIgnoreCase);
                if (close < 0)
                {
                    return null;
                }

                token = Tag().Match(text, close);
                continue;
            }

            depth++;
        }

        return null;
    }

    private static IEnumerable<string> PagesOf(string projectDirectory, Uri? page)
    {
        var shown = page is null ? null : ShownFile(projectDirectory, page);
        if (shown is not null)
        {
            yield return shown;
        }

        var others = PageFiles
            .SelectMany(pattern => Directory.EnumerateFiles(projectDirectory, pattern, SearchOption.AllDirectories))
            .Where(f => !IsBuildOutput(projectDirectory, f) && !string.Equals(f, shown, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal);
        foreach (var file in others)
        {
            yield return file;
        }
    }

    /// <summary>The static file a page's address serves from <c>wwwroot</c>: <c>/</c> is <c>index.html</c>.</summary>
    private static string? ShownFile(string projectDirectory, Uri page)
    {
        var path = Uri.UnescapeDataString(page.AbsolutePath).TrimStart('/');
        if (path.Length == 0 || path.EndsWith('/'))
        {
            path += "index.html";
        }

        var file = Path.GetFullPath(Path.Combine(projectDirectory, "wwwroot", path.Replace('/', Path.DirectorySeparatorChar)));
        return file.StartsWith(Path.GetFullPath(projectDirectory), StringComparison.Ordinal) && File.Exists(file) ? file : null;
    }

    private static bool IsBuildOutput(string projectDirectory, string file)
    {
        var relative = Path.GetRelativePath(projectDirectory, file).Replace('\\', '/');
        return relative.StartsWith("bin/", StringComparison.Ordinal) || relative.StartsWith("obj/", StringComparison.Ordinal)
            || relative.Contains("node_modules/", StringComparison.Ordinal);
    }

    private static string Snippet(string text, int start, int tagLength)
    {
        var tag = text.Substring(start, tagLength);
        var name = Tag().Match(tag).Groups["name"].Value;
        var end = text.IndexOf($"</{name}>", start + tagLength, StringComparison.OrdinalIgnoreCase);
        return end > 0 && end + name.Length + 3 - start <= SnippetLength ? text[start..(end + name.Length + 3)] : tag;
    }

    private static (int Line, int Column) LineAndColumn(string text, int offset)
    {
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < offset; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, offset - lineStart + 1);
    }

    /// <summary>A path segment: <c>tag#id</c> or <c>tag[index]</c>.</summary>
    [GeneratedRegex(@"^(?<tag>[^#\[\]/]+)(?:#(?<id>[^/]+)|\[(?<index>\d+)\])$")]
    private static partial Regex Segment();

    /// <summary>A start or end tag, skipping comments; a self-closing tag ends with <c>/&gt;</c>.</summary>
    [GeneratedRegex(@"<!--.*?-->|<(?<close>/)?(?<name>[a-zA-Z][\w-]*)\b(?:[^>""']|""[^""]*""|'[^']*')*?(?<self>/)?>", RegexOptions.Singleline)]
    private static partial Regex Tag();
}
