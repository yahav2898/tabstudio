using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Accessibility;
using TabStudio.App.Services;
using TabStudio.App.Services.Appearance;
using TabStudio.App.Services.Search;
using TabStudio.App.ViewModels.Search;

namespace TabStudio.App.ViewModels;

/// <summary>
/// One search for everything, as Visual Studio's Ctrl+Q (docs/design/README.md, "Search"): the solution's files, the
/// IDE's commands from its menus and key bindings, settings, the code's types and members, project templates and
/// recent solutions, ranked together. The Search button in the menu bar and Ctrl+Q open it. Also the IDE's theme, which
/// Tools &gt; Theme and the search's settings change.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private WorkspaceSymbolSearch? _symbolIndex;

    /// <summary>
    /// The IDE's commands, which the shell view gathers once from its menu bar and key bindings: every menu item with
    /// its menu path and key gesture, so the menus stay where the commands are defined.
    /// </summary>
    public CommandRegistry Commands { get; } = new();

    /// <summary>The search over the shell (Ctrl+Q).</summary>
    public UnifiedSearchViewModel Search { get; }

    /// <summary>Raised when the search opens a recent solution, after this one's documents are saved: the app opens it in place of this one.</summary>
    public event EventHandler<string>? OpenWorkspaceRequested;

    /// <summary>
    /// Raised when the search starts a new project from a template, after this solution's documents are saved: the app
    /// closes this solution and shows the Start window's New project with the template chosen.
    /// </summary>
    public event EventHandler<ProjectTemplate>? NewProjectRequested;

    /// <summary>The IDE's theme, for Tools &gt; Theme's check marks; <see langword="null"/> where the app has no theme setting, as in some tests.</summary>
    public IdeTheme? CurrentTheme => _extensions?.Appearance?.Theme;

    /// <summary>Creates the search over this workspace. Called once by the constructor.</summary>
    /// <param name="recent">The recent solutions; none when <see langword="null"/>.</param>
    private UnifiedSearchViewModel CreateSearch(RecentWorkspaces? recent)
    {
        var symbols = new WorkspaceSymbolSearch(() => Workspace, () => _language?.Features, () => _languageServers, ReadText);
        _symbolIndex = symbols;
        var providers = new List<ISearchProvider>
        {
            new FileSearchProvider(SearchableFiles, path => OpenDocumentAsync(path)),
            new CommandSearchProvider(Commands),
            new SettingSearchProvider(ProjectSetupSearchSettings()),
        };
        if (_extensions is { Appearance: { } appearance } extensions)
        {
            providers.Add(new SettingSearchProvider(SettingSearchProvider.Appearance(appearance, extensions.UseTabStudioTheme)));
            providers.Add(new SettingSearchProvider(SettingSearchProvider.TextSizes(appearance)));
            appearance.Changed += OnAppearanceChanged;
        }

        providers.Add(new SymbolSearchProvider(symbols, hit => NavigateToAsync(hit.FilePath, hit.Line, hit.Column)));
        providers.Add(new TemplateSearchProvider(ProjectTemplates.All, StartNewProjectAsync));
        if (recent is not null)
        {
            providers.Add(new WorkspaceSearchProvider(recent.GetAll, OpenRecentWorkspaceAsync));
        }

        var search = new UnifiedSearchViewModel(providers, new SearchHistory(_paths));
        search.PropertyChanged += OnSearchPropertyChanged;
        return search;
    }

    private IEnumerable<SettingEntry> ProjectSetupSearchSettings()
    {
        foreach (var enabled in new[] { true, false })
        {
            yield return new SettingEntry(
                $"Set projects up when they open: {(enabled ? "On" : "Off")}",
                enabled ? "Install toolchains and project packages when opening a folder or repository" : "Offer toolchain and package installs instead of starting them automatically",
                ["setup", "project setup", "set up projects", "automatic", "install packages", "install", "packages", "npm", "pip", "toolchain", "repository"],
                () => SetUpProjectsWhenTheyOpen == enabled,
                () =>
                {
                    SetUpProjectsWhenTheyOpen = enabled;
                    return Task.CompletedTask;
                });
        }
    }

    /// <summary>Stops following the app's theme and ends a running search. Called by <see cref="Dispose"/>.</summary>
    private void DisposeSearch()
    {
        if (_extensions?.Appearance is { } appearance)
        {
            appearance.Changed -= OnAppearanceChanged;
        }

        Search.PropertyChanged -= OnSearchPropertyChanged;
        Search.Dispose();
    }

    /// <summary>Tools &gt; Theme: TabStudio's colors in a theme of the IDE, Dark, Light or Follow the system, leaving an extension's color theme.</summary>
    /// <param name="theme">The theme.</param>
    [RelayCommand(CanExecute = nameof(CanUseTabStudioTheme))]
    private void UseTabStudioTheme(IdeTheme theme) => _extensions?.UseTabStudioTheme(theme);

    private bool CanUseTabStudioTheme() => _extensions?.Appearance is not null;

    /// <summary>The IDE's text size, for Tools &gt; Text Size's check marks; <see langword="null"/> where the app has no setting.</summary>
    public TextSize? CurrentTextSize => _extensions?.Appearance?.TextSize;

    /// <summary>Tools &gt; Text Size: the text size of the whole IDE and its editors, or Android's font size.</summary>
    /// <param name="size">The size.</param>
    [RelayCommand(CanExecute = nameof(CanUseTabStudioTheme))]
    private void UseTextSize(TextSize size) => _extensions?.Appearance?.SetTextSize(size);

    /// <summary>Tools &gt; Text Size &gt; Make Text Larger (Ctrl+Shift+.), as Visual Studio's zoom in.</summary>
    [RelayCommand(CanExecute = nameof(CanUseTabStudioTheme))]
    private void MakeTextLarger() => _extensions?.Appearance?.StepTextSize(larger: true);

    /// <summary>Tools &gt; Text Size &gt; Make Text Smaller (Ctrl+Shift+,), as Visual Studio's zoom out.</summary>
    [RelayCommand(CanExecute = nameof(CanUseTabStudioTheme))]
    private void MakeTextSmaller() => _extensions?.Appearance?.StepTextSize(larger: false);

    /// <summary>The files the search finds: those of the solution or folder that open in a tab, with their folders.</summary>
    private IEnumerable<SearchFile> SearchableFiles()
    {
        if (Workspace is not { } workspace)
        {
            return [];
        }

        var root = workspace.RootDirectory;
        return workspace.Tree.DescendantsAndSelf()
            .Where(node => node.IsOpenableFile && node.FullPath is not null)
            .Select(node => new SearchFile(node.FullPath!, FolderOf(root, node.FullPath!)));
    }

    /// <summary>A file's folder relative to the solution's, with forward slashes, or empty at the top.</summary>
    private static string FolderOf(string root, string path)
    {
        var folder = Path.GetRelativePath(root, Path.GetDirectoryName(path) ?? root);
        return folder == "." ? string.Empty : folder.Replace('\\', '/');
    }

    /// <summary>Opens a recent solution from the search, after saving this one's documents.</summary>
    private async Task OpenRecentWorkspaceAsync(string path)
    {
        var current = Workspace?.Solution?.FilePath ?? Workspace?.RootDirectory;
        if (string.Equals(current, path, StringComparison.Ordinal) || !await SaveAllCoreAsync())
        {
            return;
        }

        OpenWorkspaceRequested?.Invoke(this, path);
    }

    /// <summary>Starts a new project from a template, as the Start window's New project does, after saving this solution's documents.</summary>
    private async Task StartNewProjectAsync(ProjectTemplate template)
    {
        if (await SaveAllCoreAsync())
        {
            _sessions.Clear();
            NewProjectRequested?.Invoke(this, template);
        }
    }

    private void OnAppearanceChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CurrentTheme));
        OnPropertyChanged(nameof(CurrentTextSize));
        UseTabStudioThemeCommand.NotifyCanExecuteChanged();
    }

    private void OnSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The code may have changed since the search was last open.
        if (e.PropertyName == nameof(UnifiedSearchViewModel.IsOpen) && Search.IsOpen)
        {
            _symbolIndex?.Invalidate();
        }
    }
}
