using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Search;

namespace TabStudio.App.ViewModels.Search;

/// <summary>
/// One search for everything (Ctrl+Q), as Visual Studio's: files, commands with their key gestures, settings, symbols,
/// templates and recent solutions, ranked together. With nothing typed it offers what was opened recently; typed, it
/// shows the best match first and then each kind of result in a group, best group first, with a row that shows a whole
/// group. Chips above the results narrow it to one kind, as do ">" (commands) and "#" (symbols) typed first.
/// </summary>
public sealed partial class UnifiedSearchViewModel : ObservableObject, IDisposable
{
    /// <summary>How many results a group shows before its Show all row.</summary>
    public const int GroupLimit = 4;

    /// <summary>How many results one kind shows at most.</summary>
    public const int ScopeLimit = 100;

    /// <summary>How many recent items show before anything is typed.</summary>
    public const int RecentLimit = 8;

    private readonly IReadOnlyList<ISearchProvider> _providers;
    private readonly SearchHistory _history;
    private CancellationTokenSource? _search;

    /// <summary>Creates the search over its providers.</summary>
    /// <param name="providers">Where results come from.</param>
    /// <param name="history">What was opened before.</param>
    public UnifiedSearchViewModel(IEnumerable<ISearchProvider> providers, SearchHistory history)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(history);
        _providers = [.. providers];
        _history = history;
        Scopes =
        [
            new SearchScopeChip(null, "All") { IsSelected = true },
            .. Enum.GetValues<SearchKind>()
                .Where(kind => _providers.Any(provider => provider.Kind == kind))
                .Select(kind => new SearchScopeChip(kind, ScopeName(kind))),
        ];
        Completion = Task.CompletedTask;
    }

    /// <summary>What the user typed.</summary>
    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    /// <summary>The kind of results shown, or <see langword="null"/> for all.</summary>
    [ObservableProperty]
    public partial SearchKind? Scope { get; set; }

    /// <summary>Whether the search is open.</summary>
    [ObservableProperty]
    public partial bool IsOpen { get; private set; }

    /// <summary>Whether a search is running, as one for symbols may.</summary>
    [ObservableProperty]
    public partial bool IsSearching { get; private set; }

    /// <summary>The highlighted row, which Enter opens.</summary>
    [ObservableProperty]
    public partial SearchRow? SelectedRow { get; set; }

    /// <summary>The results, in groups.</summary>
    public ObservableCollection<SearchRow> Rows { get; } = [];

    /// <summary>The scope chips.</summary>
    public IReadOnlyList<SearchScopeChip> Scopes { get; }

    /// <summary>Whether there is nothing to show.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    /// <summary>What the empty search says: a hint before typing, or that nothing matched.</summary>
    [ObservableProperty]
    public partial string EmptyTitle { get; private set; } = string.Empty;

    /// <summary>The empty search's second line.</summary>
    [ObservableProperty]
    public partial string EmptyMessage { get; private set; } = string.Empty;

    /// <summary>The search that runs now, for tests and the self test.</summary>
    public Task Completion { get; private set; }

    /// <summary>A kind's name in a chip, such as "Files".</summary>
    /// <param name="kind">The kind.</param>
    public static string ScopeName(SearchKind kind) => kind switch
    {
        SearchKind.File => "Files",
        SearchKind.Command => "Commands",
        SearchKind.Setting => "Settings",
        SearchKind.Symbol => "Symbols",
        SearchKind.Template => "Templates",
        _ => "Recent",
    };

    /// <summary>A kind's name in a sentence, such as "files".</summary>
    /// <param name="kind">The kind.</param>
    public static string PluralName(SearchKind kind) => kind switch
    {
        SearchKind.File => "files",
        SearchKind.Command => "commands",
        SearchKind.Setting => "settings",
        SearchKind.Symbol => "symbols",
        SearchKind.Template => "templates",
        _ => "recent solutions",
    };

    /// <summary>Moves the highlight, wrapping around the ends.</summary>
    /// <param name="delta">How many rows to move; negative moves up.</param>
    public void MoveSelection(int delta)
    {
        if (Rows.Count == 0)
        {
            return;
        }

        var index = SelectedRow is { } selected ? Rows.IndexOf(selected) : -1;
        index = index < 0 ? (delta > 0 ? 0 : Rows.Count - 1) : ((index + delta) % Rows.Count + Rows.Count) % Rows.Count;
        SelectedRow = Rows[index];
    }

    /// <summary>Searches again now, with the query and scope as they are.</summary>
    public Task RefreshAsync()
    {
        // The previous search may still be using its token, so it is cancelled but not disposed.
        _search?.Cancel();
        _search = new CancellationTokenSource();
        Completion = SearchAsync(Query, Scope, _search.Token);
        return Completion;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _search?.Cancel();
        _search?.Dispose();
        _search = null;
    }

    /// <summary>Opens the search, optionally on one kind of result.</summary>
    /// <param name="scope">The kind, or <see langword="null"/> for all.</param>
    [RelayCommand]
    private void Open(SearchKind? scope = null)
    {
        Scope = scope;
        IsOpen = true;
        _ = RefreshAsync();
    }

    /// <summary>Closes the search.</summary>
    [RelayCommand]
    private void Close() => IsOpen = false;

    /// <summary>Opens or closes the search, as Ctrl+Q does.</summary>
    [RelayCommand]
    private void Toggle()
    {
        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open(null);
        }
    }

    /// <summary>Shows one kind of result, or all.</summary>
    /// <param name="chip">The chip tapped.</param>
    [RelayCommand]
    private void ChooseScope(SearchScopeChip? chip) => Scope = chip?.Kind;

    /// <summary>Opens a result, or shows the rest of a group.</summary>
    /// <param name="row">The row, or <see langword="null"/> for the highlighted one.</param>
    [RelayCommand]
    private async Task AcceptAsync(SearchRow? row)
    {
        switch (row ?? SelectedRow)
        {
            case SearchMoreRow more:
                Scope = more.Kind;
                break;
            case SearchResultRow { Item.IsEnabled: true } result:
                IsOpen = false;
                _history.Record(result.Item);
                await result.Item.Open();
                break;
        }
    }

    partial void OnQueryChanged(string value) => _ = RefreshAsync();

    partial void OnScopeChanged(SearchKind? value)
    {
        foreach (var chip in Scopes)
        {
            chip.IsSelected = chip.Kind == value;
        }

        _ = RefreshAsync();
    }

    private async Task SearchAsync(string query, SearchKind? scope, CancellationToken cancellationToken)
    {
        var text = query.Trim();
        if (text.StartsWith('>'))
        {
            scope = SearchKind.Command;
            text = text[1..].Trim();
        }
        else if (text.StartsWith('#'))
        {
            scope = SearchKind.Symbol;
            text = text[1..].Trim();
        }

        IsSearching = true;
        var providers = _providers.Where(provider => scope is null || provider.Kind == scope).ToList();
        var found = await Task.WhenAll(providers.Select(provider => SearchOneAsync(provider, text, cancellationToken)));
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        IsSearching = false;
        var items = found.SelectMany(list => list).ToList();
        var rows = text.Length == 0 ? Suggestions(items, scope) : Results(SearchRanker.Rank(text, items, _history), scope);
        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(row);
        }

        SelectedRow = Rows.OfType<SearchResultRow>().FirstOrDefault(row => row.Item.IsEnabled) ?? Rows.FirstOrDefault();
        IsEmpty = Rows.Count == 0;
        (EmptyTitle, EmptyMessage) = text.Length == 0
            ? ("Search everything", "Type to find files, commands, settings, symbols, templates and recent solutions.")
            : ($"No results for “{text}”", scope is null
                ? "Check the spelling, or try fewer letters."
                : $"Nothing in {PluralName(scope.Value)} matches. Tap All to search everything.");
        CountByScope(text, items);
    }

    private static async Task<IReadOnlyList<SearchItem>> SearchOneAsync(ISearchProvider provider, string text, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SearchAsync(text, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return [];
        }
#pragma warning disable CA1031 // A provider that fails, such as a language server that stopped, must not break the others.
        catch (Exception)
#pragma warning restore CA1031
        {
            return [];
        }
    }

    /// <summary>Before anything is typed: what was opened recently, or, in a scope, everything of that kind.</summary>
    private List<SearchRow> Suggestions(List<SearchItem> items, SearchKind? scope)
    {
        if (scope is { } kind)
        {
            return [.. items
                .Where(item => item.Kind == kind)
                .OrderByDescending(_history.Boost)
                .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
                .Take(ScopeLimit)
                .Select((item, index) => new SearchResultRow(new RankedItem(item, 0, [])) { Group = index == 0 ? ScopeName(kind) : null })];
        }

        // Recent items are shown as they are now (a command may have become unavailable), when the providers still offer them.
        var current = new Dictionary<string, SearchItem>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            current.TryAdd(item.Id, item);
        }

        return [.. _history.Recent
            .Select(item => current.GetValueOrDefault(item.Id))
            .OfType<SearchItem>()
            .Take(RecentLimit)
            .Select((item, index) => new SearchResultRow(new RankedItem(item, 0, [])) { Group = index == 0 ? "Recently opened" : null })];
    }

    /// <summary>Typed: in a scope, one ranked list; otherwise the best match, then each kind's group, best group first.</summary>
    private static List<SearchRow> Results(IReadOnlyList<RankedItem> ranked, SearchKind? scope)
    {
        if (scope is { } kind)
        {
            return [.. ranked.Take(ScopeLimit).Select((item, index) => new SearchResultRow(item) { Group = index == 0 ? ScopeName(kind) : null })];
        }

        var rows = new List<SearchRow>();
        if (ranked.Count == 0)
        {
            return rows;
        }

        rows.Add(new SearchResultRow(ranked[0], isBestMatch: true) { Group = "Best match" });
        foreach (var group in ranked.Skip(1).GroupBy(item => item.Item.Kind).OrderByDescending(group => group.First().Score))
        {
            var shown = group.Take(GroupLimit).ToList();
            rows.AddRange(shown.Select((item, index) => new SearchResultRow(item) { Group = index == 0 ? ScopeName(group.Key) : null }));
            var total = group.Count() + (ranked[0].Item.Kind == group.Key ? 1 : 0);
            if (group.Count() > GroupLimit)
            {
                rows.Add(new SearchMoreRow(group.Key, total));
            }
        }

        return rows;
    }

    private void CountByScope(string text, List<SearchItem> items)
    {
        foreach (var chip in Scopes)
        {
            chip.Count = text.Length == 0 || chip.Kind is null || Scope is not null
                ? null
                : items.Count(item => item.Kind == chip.Kind && SearchRanker.Rank(text, [item]).Count > 0);
        }
    }
}
