using System.Globalization;
using System.Text.RegularExpressions;

namespace TabStudio.App.Editing;

/// <summary>How Tab indents a file: with a tab character or with spaces, and how wide a level is.</summary>
/// <param name="UsesTabs">Whether Tab inserts a tab character.</param>
/// <param name="Size">The width of one level, in columns.</param>
public sealed record IndentationStyle(bool UsesTabs, int Size)
{
    private const int MaxEditorConfigBytes = 64 * 1024;

    /// <summary>Four spaces, which the editor used for every file.</summary>
    public static IndentationStyle Default { get; } = new(UsesTabs: false, Size: 4);

    /// <summary>
    /// The indentation of a file, as other editors would choose it: the repository's <c>.editorconfig</c> first, then
    /// what the file's language requires (tabs in a Makefile and in Go), then what the file itself already uses.
    /// Tab always inserted four spaces, which broke Makefiles ("missing separator") and tab-indented Python
    /// (<c>TabError</c>), and ignored the repository's style.
    /// </summary>
    /// <param name="path">The file.</param>
    /// <param name="text">Its text.</param>
    public static IndentationStyle For(string path, string text)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(text);
        var (usesTabs, size) = FromEditorConfig(path);
        var name = Path.GetFileName(path);
        usesTabs ??= IsTabLanguage(name) ? true : Detect(text);
        return new IndentationStyle(usesTabs ?? Default.UsesTabs, size ?? Default.Size);
    }

    /// <summary>Whether a file's language indents with tabs: Makefiles need them, and gofmt writes them.</summary>
    private static bool IsTabLanguage(string name) =>
        name is "Makefile" or "makefile" or "GNUmakefile"
        || name.EndsWith(".mk", StringComparison.OrdinalIgnoreCase)
        || name.EndsWith(".go", StringComparison.OrdinalIgnoreCase);

    /// <summary>Tabs when more indented lines start with a tab than with spaces; nothing when no line is indented.</summary>
    private static bool? Detect(string text)
    {
        int tabs = 0, spaces = 0, lines = 0;
        var start = 0;
        while (start < text.Length && lines < 2000)
        {
            var c = text[start];
            if (c == '\t')
            {
                tabs++;
            }
            else if (c == ' ' && start + 1 < text.Length && text[start + 1] == ' ')
            {
                spaces++;
            }

            lines++;
            var end = text.IndexOf('\n', start);
            if (end < 0)
            {
                break;
            }

            start = end + 1;
        }

        return tabs == 0 && spaces == 0 ? null : tabs > spaces;
    }

    /// <summary>
    /// <c>indent_style</c> and <c>indent_size</c> (or <c>tab_width</c>) from the <c>.editorconfig</c> files above a
    /// file, nearest last, up to one that says <c>root = true</c>. Sections match by <c>*</c>, <c>**</c>, <c>?</c>
    /// and <c>{a,b}</c>, as EditorConfig defines them.
    /// </summary>
    private static (bool? UsesTabs, int? Size) FromEditorConfig(string path)
    {
        var configs = new List<(string Folder, string Text)>();
        try
        {
            path = Path.GetFullPath(path);
            for (var folder = Path.GetDirectoryName(path); folder is not null; folder = Path.GetDirectoryName(folder))
            {
                var file = Path.Combine(folder, ".editorconfig");
                if (File.Exists(file) && new FileInfo(file).Length <= MaxEditorConfigBytes)
                {
                    var text = File.ReadAllText(file);
                    configs.Add((folder, text));
                    if (Regex.IsMatch(text, @"^\s*root\s*=\s*true\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or RegexMatchTimeoutException)
        {
            return (null, null);
        }

        bool? usesTabs = null;
        int? size = null;
        int? tabWidth = null;
        configs.Reverse();
        foreach (var (folder, text) in configs)
        {
            var relative = Path.GetRelativePath(folder, path).Replace('\\', '/');
            var applies = false;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] is '#' or ';')
                {
                    continue;
                }

                if (line[0] == '[' && line[^1] == ']')
                {
                    applies = Matches(line[1..^1], relative);
                    continue;
                }

                var equals = line.IndexOf('=', StringComparison.Ordinal);
                if (!applies || equals < 0)
                {
                    continue;
                }

                var key = line[..equals].Trim().ToLowerInvariant();
                var value = line[(equals + 1)..].Trim().ToLowerInvariant();
                switch (key)
                {
                    case "indent_style" when value is "tab" or "space":
                        usesTabs = value == "tab";
                        break;
                    case "indent_size" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n is > 0 and <= 16:
                        size = n;
                        break;
                    case "indent_size" when value == "tab":
                        size = null;
                        break;
                    case "tab_width" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var w) && w is > 0 and <= 16:
                        tabWidth = w;
                        break;
                }
            }
        }

        return (usesTabs, size ?? tabWidth);
    }

    /// <summary>Whether an EditorConfig section's glob matches a path relative to its file's folder.</summary>
    internal static bool Matches(string glob, string relativePath)
    {
        // A glob without a slash matches a file name in any folder.
        var pattern = glob.Contains('/', StringComparison.Ordinal) ? glob.TrimStart('/') : "**/" + glob;
        var regex = new System.Text.StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            switch (c)
            {
                case '*' when i + 1 < pattern.Length && pattern[i + 1] == '*':
                    i++;
                    if (i + 1 < pattern.Length && pattern[i + 1] == '/')
                    {
                        i++;
                        regex.Append("(?:.*/)?");
                    }
                    else
                    {
                        regex.Append(".*");
                    }

                    break;
                case '*':
                    regex.Append("[^/]*");
                    break;
                case '?':
                    regex.Append("[^/]");
                    break;
                case '{':
                    var close = pattern.IndexOf('}', i);
                    if (close < 0)
                    {
                        regex.Append(@"\{");
                        break;
                    }

                    regex.Append("(?:").Append(string.Join('|', pattern[(i + 1)..close].Split(',').Select(Regex.Escape))).Append(')');
                    i = close;
                    break;
                default:
                    regex.Append(Regex.Escape(new string(c, 1)));
                    break;
            }
        }

        regex.Append('$');
        try
        {
            return Regex.IsMatch(relativePath, regex.ToString(), RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        }
        catch (Exception ex) when (ex is ArgumentException or RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
