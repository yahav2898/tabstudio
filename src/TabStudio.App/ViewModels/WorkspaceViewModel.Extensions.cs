using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Editing.Extensions;
using TabStudio.App.Services.Extensions;
using TabStudio.App.ViewModels.Extensions;
using TabStudio.Extensions.Snippets;

namespace TabStudio.App.ViewModels;

/// <summary>
/// VS Code extensions in the IDE: the Extensions window (Extensions &gt; Manage Extensions, Ctrl+Shift+X) in a tab of
/// its own, Install from VSIX, the color theme and file icon theme pickers, Check for Updates, and Edit &gt;
/// IntelliSense &gt; Insert Snippet (Ctrl+K, Ctrl+X). When installed extensions change, open files and the Solution
/// Explorer take the languages they now claim.
/// </summary>
public sealed partial class WorkspaceViewModel
{
    private const string ExtensionsTabName = "Extensions";

    private ExtensionsService? _extensions;
    private ExtensionsViewModel? _extensionsPage;
    private IStoragePicker? _extensionsBrowser;

    /// <summary>The picker for color themes, file icon themes and snippets, shown at the top of the window.</summary>
    public QuickPickViewModel QuickPick { get; } = new();

    /// <summary>The installed extensions and the registry, or <see langword="null"/> when the app runs without them.</summary>
    public ExtensionsService? ExtensionsService => _extensions;

    /// <summary>The Extensions window, once it was opened.</summary>
    public ExtensionsViewModel? ExtensionsPage => _extensionsPage;

    /// <summary>Picks .vsix files for Install from VSIX; tests replace it.</summary>
    public IVsixPicker VsixPicker { get; set; } = new TopLevelVsixPicker();

    /// <summary>Whether extensions are available.</summary>
    public bool HasExtensions => _extensions is not null;

    /// <summary>Opens the Extensions window in its tab, or brings it to the front.</summary>
    /// <param name="tab">The list to show.</param>
    /// <returns>The window, or <see langword="null"/> when the app runs without extensions.</returns>
    public ExtensionsViewModel? OpenExtensions(ExtensionsTab tab = ExtensionsTab.Browse)
    {
        if (_extensions is null)
        {
            StatusText = "Extensions are not available.";
            return null;
        }

        _extensionsPage ??= CreateExtensionsPage(_extensions);
        _extensionsPage.VsixPicker = VsixPicker;
        if (Documents.FirstOrDefault(d => d.IsExtensionsPage) is { } open)
        {
            ActiveDocument = open;
        }
        else
        {
            var document = DocumentViewModel.ForExtensionsPage(_extensionsPage, Path.Combine(_paths.State, ExtensionsTabName), _profile);
            Attach(document);
            Documents.Insert(ActiveDocument is null ? Documents.Count : Documents.IndexOf(ActiveDocument) + 1, document);
            ActiveDocument = document;
        }

        _extensionsPage.SelectedTab = tab;
        _ = _extensionsPage.ActivateAsync();
        return _extensionsPage;
    }

    /// <summary>Connects the extensions service; called by the constructor.</summary>
    private void InitializeExtensions(ExtensionsService? extensions, IStoragePicker? browser)
    {
        _extensions = extensions;
        _extensionsBrowser = browser ?? new TopLevelStoragePicker();
        if (extensions is not null)
        {
            EditorExtensions.Changed += OnEditorExtensionsChanged;
        }
    }

    private void DisposeExtensions()
    {
        if (_extensions is not null)
        {
            EditorExtensions.Changed -= OnEditorExtensionsChanged;
        }

        QuickPick.Cancel();
        _extensionsPage?.Dispose();
        _extensionsPage = null;
    }

    private ExtensionsViewModel CreateExtensionsPage(ExtensionsService extensions)
    {
        var page = new ExtensionsViewModel(extensions, VsixPicker, QuickPick, _extensionsBrowser);
        page.Installed += (_, installed) => StatusText = $"Installed {installed.Manifest.DisplayName} {installed.Version}";
        return page;
    }

    /// <summary>Open files take the languages installed extensions now claim, and the Solution Explorer their badges.</summary>
    private void OnEditorExtensionsChanged(object? sender, EventArgs e)
    {
        foreach (var document in Documents)
        {
            document.RefreshFileType();
        }

        if (Workspace is { } workspace)
        {
            Explorer.Load(workspace, [.. Compatibility.Select(c => c.Report)]);
        }
    }

    /// <summary>Extensions &gt; Manage Extensions (Ctrl+Shift+X).</summary>
    [RelayCommand]
    private void ShowExtensions() => OpenExtensions();

    /// <summary>Extensions &gt; Install from VSIX: picks a package file and installs it.</summary>
    [RelayCommand]
    private async Task InstallFromVsixAsync()
    {
        if (OpenExtensions(ExtensionsTab.Installed) is { } page)
        {
            await page.InstallFromFileAsync();
        }
    }

    /// <summary>Extensions &gt; Check for Updates.</summary>
    [RelayCommand]
    private async Task CheckForExtensionUpdatesAsync()
    {
        if (OpenExtensions(ExtensionsTab.Updates) is { } page)
        {
            await page.CheckForUpdatesAsync();
        }
    }

    /// <summary>Extensions &gt; Color Theme: picks the editor's color theme among the installed extensions' themes.</summary>
    [RelayCommand]
    private Task SelectColorThemeAsync() => _extensions is null
        ? Task.CompletedTask
        : ThemePickers.PickColorThemeAsync(_extensions, QuickPick, browseMore: BrowseThemes);

    /// <summary>Extensions &gt; File Icon Theme: picks the icons of the Solution Explorer and tabs.</summary>
    [RelayCommand]
    private Task SelectFileIconThemeAsync() => _extensions is null
        ? Task.CompletedTask
        : ThemePickers.PickIconThemeAsync(_extensions, QuickPick, browseMore: BrowseThemes);

    /// <summary>Edit &gt; IntelliSense &gt; Insert Snippet (Ctrl+K, Ctrl+X): picks a snippet for the file's language and inserts it at the caret.</summary>
    [RelayCommand]
    private async Task InsertSnippetAsync()
    {
        if (ActiveDocument is not { } document || document.IsExtensionsPage || document.IsCiLog || document.IsComparison || document.IsNotebook)
        {
            StatusText = "Open a file to insert a snippet into.";
            return;
        }

        var snippets = EditorExtensions.SnippetsFor(document.FileType, document.FilePath);
        if (snippets.Count == 0)
        {
            StatusText = $"There are no snippets for {document.FileType.DisplayName}. Extensions (Ctrl+Shift+X) can add some.";
            return;
        }

        var items = snippets
            .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(s => new QuickPickItem(s.Name, string.Join(", ", s.Prefixes), s.Description, s))
            .ToList();
        var picked = await QuickPick.ShowAsync("Insert Snippet", $"Select a {document.FileType.DisplayName} snippet", items);
        if (picked?.Value is SnippetDefinition snippet && Documents.Contains(document))
        {
            ActiveDocument = document;
            document.InsertSnippet(snippet);
        }
    }

    private void BrowseThemes()
    {
        if (OpenExtensions() is { } page && page.Categories.FirstOrDefault(c => c.Category == "Themes") is { } themes)
        {
            page.SelectCategory(themes);
        }
    }
}
