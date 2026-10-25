namespace TabStudio.App.Services.Search;

/// <summary>Finds project templates by name and tags, and starts a new project from one.</summary>
/// <param name="templates">The templates.</param>
/// <param name="create">Starts a new project from a template, as the Start window's New project does.</param>
public sealed class TemplateSearchProvider(IEnumerable<ProjectTemplate> templates, Func<ProjectTemplate, Task> create) : ISearchProvider
{
    /// <inheritdoc />
    public SearchKind Kind => SearchKind.Template;

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        IReadOnlyList<SearchItem> items = [.. templates.Select(template =>
            new SearchItem(SearchKind.Template, template.Name, template.Description, () => create(template))
            {
                IconKey = "Icon.NewFile",
                Keywords = template.Tags,
            })];
        return Task.FromResult(items);
    }
}
