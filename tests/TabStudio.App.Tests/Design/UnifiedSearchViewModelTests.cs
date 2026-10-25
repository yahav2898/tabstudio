using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Services.Search;
using TabStudio.App.ViewModels.Search;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Design;

/// <summary>One search for everything: ranked groups, scopes, history, and opening the right thing.</summary>
public sealed class UnifiedSearchViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [AvaloniaFact]
    public async Task Typing_shows_the_best_match_then_each_kind_in_a_group()
    {
        using var search = DesignSamples.Search(new AppPaths(_temp.Path));

        search.Query = "greet";
        await search.Completion;

        var rows = search.Rows.OfType<SearchResultRow>().ToList();
        Assert.True(rows[0].IsBestMatch);
        Assert.Equal("Best match", rows[0].Group);
        Assert.Contains(rows, row => row.Group == "Files");
        Assert.Contains(rows, row => row.Group == "Symbols");
        Assert.Same(rows[0], search.SelectedRow);
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, rows[0].Highlights);
        Assert.Equal(3, search.Scopes.Single(chip => chip.Kind == SearchKind.File).Count);
    }

    [AvaloniaFact]
    public async Task A_scope_or_a_prefix_shows_one_kind()
    {
        using var search = DesignSamples.Search(new AppPaths(_temp.Path));

        search.Query = ">save";
        await search.Completion;
        Assert.All(search.Rows.OfType<SearchResultRow>(), row => Assert.Equal(SearchKind.Command, row.Item.Kind));
        Assert.Equal("Save", ((SearchResultRow)search.Rows[0]).Title);

        search.Query = string.Empty;
        search.ChooseScopeCommand.Execute(search.Scopes.Single(chip => chip.Kind == SearchKind.Template));
        await search.Completion;
        Assert.All(search.Rows.OfType<SearchResultRow>(), row => Assert.Equal(SearchKind.Template, row.Item.Kind));
        Assert.Equal(ProjectTemplates.All.Count, search.Rows.Count);
        Assert.True(search.Scopes.Single(chip => chip.Kind == SearchKind.Template).IsSelected);
    }

    [AvaloniaFact]
    public async Task A_group_with_more_results_offers_to_show_them_all()
    {
        using var search = DesignSamples.Search(new AppPaths(_temp.Path));
        search.Query = "s";
        await search.Completion;

        var more = search.Rows.OfType<SearchMoreRow>().First();
        await search.AcceptCommand.ExecuteAsync(more);
        await search.Completion;

        Assert.Equal(more.Kind, search.Scope);
        Assert.True(search.Rows.Count > UnifiedSearchViewModel.GroupLimit);
    }

    [AvaloniaFact]
    public async Task Opening_a_result_runs_it_closes_the_search_and_remembers_it()
    {
        var opened = new List<string>();
        var history = new SearchHistory(new AppPaths(_temp.Path));
        using var search = new UnifiedSearchViewModel(
            [new FileSearchProvider(() => DesignSamples.Files, path => { opened.Add(path); return Task.CompletedTask; })],
            history);
        search.OpenCommand.Execute(null);
        search.Query = "readme";
        await search.Completion;

        await search.AcceptCommand.ExecuteAsync(null);

        Assert.Equal(new[] { "/w/README.md" }, opened);
        Assert.False(search.IsOpen);
        search.Query = string.Empty;
        await search.Completion;
        var recent = Assert.IsType<SearchResultRow>(Assert.Single(search.Rows));
        Assert.Equal(("README.md", "Recently opened"), (recent.Title, recent.Group));
        Assert.True(new SearchHistory(new AppPaths(_temp.Path)).Boost(recent.Item) > 0);
    }

    [AvaloniaFact]
    public async Task A_command_that_cannot_run_shows_dimmed_and_does_not_run()
    {
        using var search = DesignSamples.Search(new AppPaths(_temp.Path));
        search.Query = "rebuild";
        await search.Completion;

        var rebuild = search.Rows.OfType<SearchResultRow>().Single(row => row.Title == "Rebuild Solution");
        Assert.True(rebuild.IsDisabled);
        search.OpenCommand.Execute(null);
        await search.Completion;
        await search.AcceptCommand.ExecuteAsync(rebuild);

        Assert.True(search.IsOpen);
    }

    [AvaloniaFact]
    public async Task A_provider_that_fails_does_not_stop_the_others()
    {
        using var search = new UnifiedSearchViewModel([new FailingProvider(), new FileSearchProvider(() => DesignSamples.Files, _ => Task.CompletedTask)], new SearchHistory());

        search.Query = "program";
        await search.Completion;

        Assert.Equal("Program.cs", ((SearchResultRow)search.Rows[0]).Title);
    }

    [AvaloniaFact]
    public async Task Nothing_found_says_so_and_how_to_find_more()
    {
        using var search = DesignSamples.Search(new AppPaths(_temp.Path));
        search.ChooseScopeCommand.Execute(search.Scopes.Single(chip => chip.Kind == SearchKind.Setting));

        search.Query = "greeter";
        await search.Completion;

        Assert.True(search.IsEmpty);
        Assert.Equal("Nothing in settings matches. Tap All to search everything.", search.EmptyMessage);
    }

    [AvaloniaFact]
    public async Task The_arrow_keys_move_through_the_results_and_wrap()
    {
        using var search = DesignSamples.Search(new AppPaths(_temp.Path));
        search.Query = "greet";
        await search.Completion;

        search.MoveSelection(-1);

        Assert.Same(search.Rows[^1], search.SelectedRow);
        search.MoveSelection(1);
        Assert.Same(search.Rows[0], search.SelectedRow);
    }

    [Fact]
    public void Key_gestures_split_into_key_caps()
    {
        Assert.Equal(new[] { "Ctrl", "Shift", "B" }, SearchResultRow.SplitGesture("Ctrl+Shift+B"));
        Assert.Equal(new[] { "Ctrl", "R", ",", "A" }, SearchResultRow.SplitGesture("Ctrl+R, A"));
        Assert.Equal(new[] { "Ctrl", "+" }, SearchResultRow.SplitGesture("Ctrl++"));
    }

    public void Dispose() => _temp.Dispose();

    private sealed class FailingProvider : ISearchProvider
    {
        public SearchKind Kind => SearchKind.Symbol;

        public Task<IReadOnlyList<SearchItem>> SearchAsync(string query, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The language server stopped.");
    }
}
