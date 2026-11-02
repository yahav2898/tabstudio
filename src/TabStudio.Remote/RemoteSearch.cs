namespace TabStudio.Remote;

public sealed record RemoteSearchHit(string FilePath, int Line, int Column, string Preview)
{
    public string Label => $"{Path.GetFileName(FilePath)}:{Line}";
}

/// <summary>Literal text search over the downloaded source cache. No remote shell command is built from a query.</summary>
public static class RemoteSearch
{
    public const int MaximumHits = 200;

    public static Task<IReadOnlyList<RemoteSearchHit>> FindAsync(string cacheRoot, string query, CancellationToken cancellationToken = default) => Task.Run<IReadOnlyList<RemoteSearchHit>>(() =>
    {
        if (string.IsNullOrEmpty(query)) { return []; }
        var results = new List<RemoteSearchHit>();
        void Walk(string directory)
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (results.Count >= MaximumHits) { return; }
                var attributes = File.GetAttributes(entry);
                if (attributes.HasFlag(FileAttributes.ReparsePoint)) { continue; }
                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    if (!TabStudio.Core.Workspaces.PathUtilities.IsIgnoredDirectory(Path.GetFileName(entry))) { Walk(entry); }
                    continue;
                }
                try
                {
                    var number = 0;
                    foreach (var line in File.ReadLines(entry))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        number++;
                        if (line.Contains('\0')) { break; }
                        var index = line.IndexOf(query, StringComparison.OrdinalIgnoreCase);
                        if (index >= 0)
                        {
                            results.Add(new RemoteSearchHit(entry, number, index + 1, line.Trim()[..Math.Min(line.Trim().Length, 180)]));
                            if (results.Count >= MaximumHits) { return; }
                        }
                    }
                }
                catch (IOException) { /* A file may have been removed by a refresh; the next search sees the new cache. */ }
                if (results.Count >= MaximumHits) { return; }
            }
        }
        Walk(cacheRoot);
        return results;
    }, cancellationToken);
}
