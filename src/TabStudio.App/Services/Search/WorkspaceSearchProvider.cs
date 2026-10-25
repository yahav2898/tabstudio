namespace TabStudio.App.Services.Search;

/// <summary>Finds solutions and folders opened recently, and opens one.</summary>
/// <param name="recent">The recent workspaces, most recent first.</param>
/// <param name="open">Opens a solution, project or folder.</param>
public sealed class WorkspaceSearchProvider(Func<IReadOnlyList<RecentWorkspace>> recent, Func<string, Task> open) : ISearchProvider
{
    /// <inheritdoc />
    public SearchKind Kind => SearchKind.Workspace;

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        IReadOnlyList<SearchItem> items = [.. recent().Select(workspace =>
            new SearchItem(SearchKind.Workspace, workspace.Name, workspace.Path, () => open(workspace.Path))
            {
                IconKey = "Icon.Solution",
                Note = workspace.Description,
            })];
        return Task.FromResult(items);
    }
}
