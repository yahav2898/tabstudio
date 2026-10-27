using System.Text.RegularExpressions;

namespace TabStudio.Toolchains.Python;

/// <summary>A package a requirements file pins to one version, such as <c>psycopg2==2.9.9</c>.</summary>
/// <param name="Name">The package's normalized name (PEP 503), such as <c>flask-sqlalchemy</c>.</param>
/// <param name="Version">The pinned version.</param>
public sealed record PythonPin(string Name, string Version);

/// <summary>Reads the packages a project's requirements pin, as pip reads <c>requirements.txt</c>.</summary>
public static partial class PythonRequirements
{
    /// <summary>
    /// The packages a requirements file pins with <c>==</c>, including those of the files it includes with
    /// <c>-r</c>. Lines with environment markers, options and unpinned requirements are skipped.
    /// </summary>
    /// <param name="file">The requirements file, such as <c>requirements.txt</c>.</param>
    public static IReadOnlyList<PythonPin> Pins(string file)
    {
        ArgumentNullException.ThrowIfNull(file);
        var result = new List<PythonPin>();
        Read(Path.GetFullPath(file), result, new HashSet<string>(StringComparer.Ordinal));
        return [.. result.DistinctBy(p => p.Name)];
    }

    /// <summary>The packages a requirements file's text pins, without following <c>-r</c>.</summary>
    /// <param name="text">The file's text.</param>
    public static IReadOnlyList<PythonPin> PinsOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var result = new List<PythonPin>();
        foreach (var line in Lines(text))
        {
            if (Pin().Match(line) is { Success: true } pin)
            {
                result.Add(new(Normalize(pin.Groups["name"].Value), pin.Groups["version"].Value));
            }
        }

        return result;
    }

    /// <summary>The names a requirements file's text lists, pinned or not, normalized.</summary>
    /// <param name="text">The file's text.</param>
    public static IReadOnlyList<string> NamesOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. Lines(text).Select(l => Name().Match(l)).Where(m => m.Success).Select(m => Normalize(m.Groups["name"].Value)).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>A package name as PyPI compares names: lower case, with runs of <c>-</c>, <c>_</c> and <c>.</c> as <c>-</c>.</summary>
    /// <param name="name">The name, such as <c>Flask_SQLAlchemy</c>.</param>
    public static string Normalize(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Separators().Replace(name.Trim(), "-").ToLowerInvariant();
    }

    private static void Read(string file, List<PythonPin> result, HashSet<string> visited)
    {
        if (!visited.Add(file) || visited.Count > 20)
        {
            return;
        }

        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var line in Lines(text))
        {
            if (Include().Match(line) is { Success: true } include)
            {
                Read(Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!, include.Groups["file"].Value)), result, visited);
            }
        }

        result.AddRange(PinsOf(text));
    }

    /// <summary>The logical lines: comments removed and continuations joined.</summary>
    private static IEnumerable<string> Lines(string text)
    {
        var current = string.Empty;
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = Comment().Replace(raw, string.Empty).TrimEnd();
            if (line.EndsWith('\\'))
            {
                current += line[..^1] + " ";
                continue;
            }

            var whole = (current + line).Trim();
            current = string.Empty;
            if (whole.Length > 0)
            {
                yield return whole;
            }
        }
    }

    [GeneratedRegex(@"(^|\s)#.*$", RegexOptions.CultureInvariant)]
    private static partial Regex Comment();

    [GeneratedRegex(@"^(-r|--requirement)[=\s]\s*(?<file>\S+)", RegexOptions.CultureInvariant)]
    private static partial Regex Include();

    /// <summary>A pin without markers, such as <c>Flask-Login==0.6.3</c> or <c>uvicorn[standard]==0.30.1</c>.</summary>
    [GeneratedRegex(@"^(?<name>[A-Za-z0-9][A-Za-z0-9._-]*)\s*(\[[^\]]*\])?\s*==\s*(?<version>[A-Za-z0-9._+!-]+)\s*(--hash\S*\s*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pin();

    [GeneratedRegex(@"^(?<name>[A-Za-z0-9][A-Za-z0-9._-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex Name();

    [GeneratedRegex(@"[-_.]+", RegexOptions.CultureInvariant)]
    private static partial Regex Separators();
}
