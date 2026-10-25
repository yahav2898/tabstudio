namespace TabStudio.App.Services.Search;

/// <summary>A file the search can open.</summary>
/// <param name="Path">Its full path.</param>
/// <param name="Folder">Its folder, relative to the solution's, or empty at the top.</param>
public sealed record SearchFile(string Path, string Folder);

/// <summary>Finds the open solution's files by name, and by folder when the name does not match.</summary>
/// <param name="files">The files of the open solution or folder.</param>
/// <param name="open">Opens a file in the editor.</param>
public sealed class FileSearchProvider(Func<IEnumerable<SearchFile>> files, Func<string, Task> open) : ISearchProvider
{
    /// <inheritdoc />
    public SearchKind Kind => SearchKind.File;

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        IReadOnlyList<SearchItem> items = [.. files().Select(file =>
            new SearchItem(SearchKind.File, Path.GetFileName(file.Path), file.Folder, () => open(file.Path)) { IconKey = "Icon.File" })];
        return Task.FromResult(items);
    }
}
