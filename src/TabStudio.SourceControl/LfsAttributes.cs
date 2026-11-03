using System.Text;

namespace TabStudio.SourceControl;

/// <summary>
/// Which files <c>.gitattributes</c> sends to Git LFS (<c>filter=lfs</c>), as Git LFS's clean filter decides when a file is
/// committed: the root's file first, deeper ones after, and the last matching line winning.
/// </summary>
internal static class LfsAttributes
{
    /// <summary>The paths among <paramref name="paths"/> that are stored with Git LFS.</summary>
    /// <param name="root">The repository's folder.</param>
    /// <param name="paths">Paths relative to the root, with <c>/</c> separators.</param>
    public static List<string> Select(string root, IEnumerable<string> paths)
    {
        var list = paths.ToList();
        var folders = new SortedSet<string>(Comparer<string>.Create((a, b) =>
        {
            var depth = a.Count(c => c == '/').CompareTo(b.Count(c => c == '/'));
            return depth != 0 ? depth : string.CompareOrdinal(a, b);
        }));
        foreach (var path in list)
        {
            folders.Add(string.Empty);
            var parts = path.Split('/');
            for (var i = 1; i < parts.Length; i++)
            {
                folders.Add(string.Join('/', parts.Take(i)));
            }
        }

        var rules = new GitIgnore();
        var any = false;
        foreach (var folder in folders)
        {
            var file = Path.Combine(root, folder.Replace('/', Path.DirectorySeparatorChar), ".gitattributes");
            if (!File.Exists(file))
            {
                continue;
            }

            var converted = new StringBuilder();
            foreach (var raw in File.ReadAllLines(file))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('['))
                {
                    continue;
                }

                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var attributes = parts.Skip(1).ToList();
                if (attributes.Contains("filter=lfs"))
                {
                    converted.Append(parts[0]).Append('\n');
                    any = true;
                }
                else if (attributes.Any(a => a is "-filter" or "!filter" || (a.StartsWith("filter=", StringComparison.Ordinal) && a != "filter=lfs")))
                {
                    converted.Append('!').Append(parts[0]).Append('\n');
                }
            }

            rules.Add(converted.ToString(), folder);
        }

        return any ? [.. list.Where(p => rules.IsIgnored(p, isDirectory: false))] : [];
    }
}
