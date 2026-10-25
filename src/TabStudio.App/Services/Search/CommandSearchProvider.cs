using CommunityToolkit.Mvvm.Input;

namespace TabStudio.App.Services.Search;

/// <summary>Finds the IDE's commands, from the menus and key bindings (see <see cref="CommandRegistry"/>).</summary>
/// <param name="registry">The commands.</param>
public sealed class CommandSearchProvider(CommandRegistry registry) : ISearchProvider
{
    /// <inheritdoc />
    public SearchKind Kind => SearchKind.Command;

    /// <inheritdoc />
    public Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        IReadOnlyList<SearchItem> items = [.. registry.Commands.Select(Item)];
        return Task.FromResult(items);
    }

    private static SearchItem Item(CommandEntry entry) =>
        new(SearchKind.Command, entry.Title, entry.Path, () => RunAsync(entry))
        {
            Gesture = entry.Gesture,
            IconKey = "Icon.Command",
            IsEnabled = entry.IsAvailable(),
        };

    private static Task RunAsync(CommandEntry entry)
    {
        if (!entry.Command.CanExecute(entry.Parameter))
        {
            return Task.CompletedTask;
        }

        if (entry.Command is IAsyncRelayCommand asynchronous)
        {
            return asynchronous.ExecuteAsync(entry.Parameter);
        }

        entry.Command.Execute(entry.Parameter);
        return Task.CompletedTask;
    }
}
