using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>A category the Browse list can be narrowed to, shown as a chip.</summary>
public sealed partial class ExtensionCategoryViewModel : ObservableObject
{
    private readonly ExtensionsViewModel _owner;

    /// <summary>Creates a category chip.</summary>
    /// <param name="owner">The Extensions window.</param>
    /// <param name="label">What the chip says.</param>
    /// <param name="category">The registry's category, or <see langword="null"/> for every extension.</param>
    public ExtensionCategoryViewModel(ExtensionsViewModel owner, string label, string? category)
    {
        _owner = owner;
        Label = label;
        Category = category;
    }

    /// <summary>What the chip says.</summary>
    public string Label { get; }

    /// <summary>The registry's category, or <see langword="null"/> for every extension.</summary>
    public string? Category { get; }

    /// <summary>Whether Browse is narrowed to this category.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [RelayCommand]
    private void Select() => _owner.SelectCategory(this);
}

/// <summary>Browse: searching the registry, with popular extensions when nothing is typed.</summary>
public sealed partial class ExtensionsViewModel
{
    /// <summary>How many results a page of Browse has.</summary>
    public const int PageSize = 30;

    private static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(350);

    // The categories that matter on a tablet, where extensions add languages, themes and snippets.
    private static readonly (string Label, string? Category)[] CategoryChoices =
    [
        ("All", null),
        ("Languages", "Programming Languages"),
        ("Themes", "Themes"),
        ("Snippets", "Snippets"),
    ];

    private CancellationTokenSource? _search;
    private bool _browseLoaded;
    private string? _category;

    /// <summary>The categories Browse can be narrowed to.</summary>
    public IReadOnlyList<ExtensionCategoryViewModel> Categories { get; }

    /// <summary>What was typed in the search box: a search on Browse, a filter on Installed.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>The Browse results.</summary>
    public RangeObservableCollection<ExtensionItemViewModel> BrowseResults { get; } = [];

    /// <summary>Whether a search is running.</summary>
    [ObservableProperty]
    public partial bool IsSearching { get; private set; }

    /// <summary>What the results are, such as "Popular" or "1,204 results".</summary>
    [ObservableProperty]
    public partial string ResultsHeading { get; private set; } = "Popular";

    /// <summary>Why there are no results, such as the registry being unreachable, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBrowseMessage))]
    public partial string? BrowseMessage { get; private set; }

    /// <summary>Whether <see cref="BrowseMessage"/> is shown.</summary>
    public bool HasBrowseMessage => BrowseMessage is not null;

    /// <summary>Whether the registry cannot be reached, so Retry is offered.</summary>
    [ObservableProperty]
    public partial bool CanRetry { get; private set; }

    /// <summary>Whether the results were saved earlier because the registry cannot be reached now.</summary>
    [ObservableProperty]
    public partial bool IsOffline { get; private set; }

    /// <summary>Whether more results can be loaded.</summary>
    [ObservableProperty]
    public partial bool CanLoadMore { get; private set; }

    /// <summary>Narrows Browse to a category, as tapping its chip does.</summary>
    /// <param name="category">The category.</param>
    public void SelectCategory(ExtensionCategoryViewModel category)
    {
        ArgumentNullException.ThrowIfNull(category);
        foreach (var chip in Categories)
        {
            chip.IsSelected = ReferenceEquals(chip, category);
        }

        _category = category.Category;
        SelectedTab = ExtensionsTab.Browse;
        _ = SearchAsync(append: false);
    }

    /// <summary>Searches the registry now, as Enter in the search box does.</summary>
    [RelayCommand]
    private Task SearchNowAsync() => SelectedTab == ExtensionsTab.Browse ? SearchAsync(append: false) : Task.CompletedTask;

    [RelayCommand]
    private Task LoadMoreAsync() => SearchAsync(append: true);

    [RelayCommand]
    private Task RetryAsync() => SearchAsync(append: false);

    partial void OnSearchTextChanged(string value)
    {
        if (SelectedTab == ExtensionsTab.Installed)
        {
            FilterInstalled();
            return;
        }

        if (SelectedTab == ExtensionsTab.Updates)
        {
            SelectedTab = ExtensionsTab.Browse;
        }

        _ = SearchSoonAsync();
    }

    /// <summary>Searches once the user stops typing for a moment.</summary>
    private async Task SearchSoonAsync()
    {
        var search = Restart();
        try
        {
            await Task.Delay(SearchDelay, search.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchAsync(append: false, search);
    }

    /// <summary>Searches the registry, or loads the next page of results.</summary>
    private async Task SearchAsync(bool append, CancellationTokenSource? search = null)
    {
        search ??= Restart();
        var text = SearchText.Trim();
        var query = new OpenVsxQuery(
            text,
            _category,
            text.Length == 0 ? OpenVsxSort.Downloads : OpenVsxSort.Relevance,
            append ? BrowseResults.Count : 0,
            PageSize);
        IsSearching = true;
        OpenVsxSearchResult result;
        try
        {
            result = await Client.SearchAsync(query, search.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (OpenVsxException ex)
        {
            if (ReferenceEquals(search, _search))
            {
                if (!append)
                {
                    BrowseResults.ReplaceAll([]);
                    SyncListSelection();
                }

                BrowseMessage = ex.Message;
                CanRetry = true;
                CanLoadMore = false;
                IsSearching = false;
            }

            return;
        }

        if (!ReferenceEquals(search, _search) || _disposed)
        {
            return;
        }

        _browseLoaded = true;
        IsSearching = false;
        CanRetry = false;
        IsOffline = result.IsOffline;
        var items = result.Extensions.Select(e => WithState(ExtensionItemViewModel.FromSummary(this, e))).ToList();
        if (append)
        {
            foreach (var item in items.Where(i => BrowseResults.All(r => r.Id != i.Id)))
            {
                BrowseResults.Add(item);
            }
        }
        else
        {
            BrowseResults.ReplaceAll(items);
        }

        SyncListSelection();

        CanLoadMore = result.Extensions.Count > 0 && result.Offset + result.Extensions.Count < result.TotalSize;
        ResultsHeading = text.Length > 0
            ? (result.TotalSize == 1 ? "1 result" : string.Create(CultureInfo.CurrentCulture, $"{result.TotalSize:N0} results"))
            : Categories.FirstOrDefault(c => c.IsSelected && c.Category is not null) is { } category ? $"Popular {category.Label.ToLower(CultureInfo.CurrentCulture)}" : "Popular";
        BrowseMessage = BrowseResults.Count > 0 ? null
            : text.Length > 0 ? $"No extensions match “{text}”."
            : "The registry has no extensions in this category.";
    }

    /// <summary>Cancels the search that is running and starts another. A cancelled search's source is left to the collector, since its request may still be finishing.</summary>
    private CancellationTokenSource Restart()
    {
        _search?.Cancel();
        _search = new CancellationTokenSource();
        return _search;
    }
}
