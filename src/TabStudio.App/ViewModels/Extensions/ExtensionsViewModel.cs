using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Platform;
using TabStudio.App.Services.Extensions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>
/// The Extensions window, as Visual Studio's Extension Manager and VS Code's Extensions view: Browse searches Open VSX
/// (popular extensions when nothing is typed), Installed lists what is installed, Updates what has a newer version; the
/// selected extension's details are shown beside the list. Install from VSIX installs a package file.
/// </summary>
/// <remarks>Lives as long as its workspace; the Extensions tab shows it. Call it on the UI thread.</remarks>
public sealed partial class ExtensionsViewModel : ObservableObject, IDisposable
{
    private readonly ExtensionsService _service;
    private readonly QuickPickViewModel _quickPick;
    private readonly IStoragePicker? _browser;
    private readonly Dictionary<ExtensionIdentifier, double> _busy = [];
    private List<InstalledExtension> _installed = [];
    private bool _syncingSelection;
    private bool _disposed;

    /// <summary>Creates the Extensions window.</summary>
    /// <param name="service">The installed extensions and the registry.</param>
    /// <param name="vsixPicker">Picks a .vsix file for Install from VSIX.</param>
    /// <param name="quickPick">The picker for choosing among an extension's themes.</param>
    /// <param name="browser">Opens links, such as an extension's repository, in the browser; <see langword="null"/> hides them.</param>
    public ExtensionsViewModel(ExtensionsService service, IVsixPicker vsixPicker, QuickPickViewModel quickPick, IStoragePicker? browser = null)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(vsixPicker);
        ArgumentNullException.ThrowIfNull(quickPick);
        _service = service;
        VsixPicker = vsixPicker;
        _quickPick = quickPick;
        _browser = browser;
        Categories = [.. CategoryChoices.Select(c => new ExtensionCategoryViewModel(this, c.Label, c.Category))];
        Categories[0].IsSelected = true;
        RegistryText = _service.Client.Registry.AbsoluteUri;
        _service.Changed += OnServiceChanged;
        RefreshInstalled();
    }

    /// <summary>The installed extensions and the registry.</summary>
    public ExtensionsService Service => _service;

    /// <summary>The registry client, for icons and README images.</summary>
    public OpenVsxClient Client => _service.Client;

    /// <summary>Picks a .vsix file; tests replace it.</summary>
    public IVsixPicker VsixPicker { get; set; }

    /// <summary>Whether links can be opened in the browser.</summary>
    public bool CanOpenLinks => _browser is not null;

    /// <summary>The list shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBrowseSelected), nameof(IsInstalledSelected), nameof(IsUpdatesSelected), nameof(SearchWatermark))]
    public partial ExtensionsTab SelectedTab { get; set; }

    /// <summary>Whether Browse is shown.</summary>
    public bool IsBrowseSelected => SelectedTab == ExtensionsTab.Browse;

    /// <summary>Whether Installed is shown.</summary>
    public bool IsInstalledSelected => SelectedTab == ExtensionsTab.Installed;

    /// <summary>Whether Updates is shown.</summary>
    public bool IsUpdatesSelected => SelectedTab == ExtensionsTab.Updates;

    /// <summary>The hint in the search box.</summary>
    public string SearchWatermark => SelectedTab == ExtensionsTab.Browse ? $"Search {Client.Registry.Host}" : "Filter installed extensions";

    /// <summary>The installed extensions, filtered by the search text.</summary>
    public RangeObservableCollection<ExtensionItemViewModel> InstalledItems { get; } = [];

    /// <summary>Installed extensions with a newer version.</summary>
    public RangeObservableCollection<ExtensionItemViewModel> UpdateItems { get; } = [];

    /// <summary>How many extensions are installed, for the tab's count.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstalled))]
    public partial int InstalledCount { get; private set; }

    /// <summary>Whether any extension is installed.</summary>
    public bool HasInstalled => InstalledCount > 0;

    /// <summary>What the Installed list says when it is empty, or <see langword="null"/> when it is not.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstalledMessage))]
    public partial string? InstalledMessage { get; private set; }

    /// <summary>Whether <see cref="InstalledMessage"/> is shown.</summary>
    public bool HasInstalledMessage => InstalledMessage is not null;

    /// <summary>How many updates are available, for the tab's badge.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdates))]
    public partial int UpdateCount { get; private set; }

    /// <summary>Whether updates are available.</summary>
    public bool HasUpdates => UpdateCount > 0;

    /// <summary>The extension whose details are shown.</summary>
    [ObservableProperty]
    public partial ExtensionItemViewModel? SelectedItem { get; set; }

    /// <summary>The selected row of Browse; tapping a row shows its details.</summary>
    [ObservableProperty]
    public partial ExtensionItemViewModel? SelectedBrowseItem { get; set; }

    /// <summary>The selected row of Installed.</summary>
    [ObservableProperty]
    public partial ExtensionItemViewModel? SelectedInstalledItem { get; set; }

    /// <summary>The selected row of Updates.</summary>
    [ObservableProperty]
    public partial ExtensionItemViewModel? SelectedUpdateItem { get; set; }

    /// <summary>The details of <see cref="SelectedItem"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetails))]
    public partial ExtensionDetailsViewModel? Details { get; private set; }

    /// <summary>Whether details are shown.</summary>
    public bool HasDetails => Details is not null;

    /// <summary>A message about the last action, such as "Installed Perl 1.2.0.", shown above the lists.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    /// <summary>Whether the notice reports a failure.</summary>
    [ObservableProperty]
    public partial bool IsNoticeError { get; private set; }

    /// <summary>The label of the notice's button, such as "Set Color Theme", or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoticeAction))]
    public partial string? NoticeActionText { get; private set; }

    /// <summary>Whether a notice is shown.</summary>
    public bool HasNotice => Notice is not null;

    /// <summary>Whether the notice has a button.</summary>
    public bool HasNoticeAction => NoticeActionText is not null;

    private Func<Task>? NoticeAction { get; set; }

    /// <summary>Called when the Extensions tab is shown: loads popular extensions the first time and looks for updates.</summary>
    public async Task ActivateAsync()
    {
        RefreshInstalled();
        var browse = _browseLoaded ? Task.CompletedTask : SearchAsync(append: false);
        var updates = _updatesChecked ? Task.CompletedTask : CheckForUpdatesCoreAsync(quiet: true);
        await Task.WhenAll(browse, updates);
    }

    /// <summary>Shows an extension's details, as tapping it in a list does.</summary>
    /// <param name="id">The extension.</param>
    public void ShowDetails(ExtensionIdentifier id)
    {
        SelectedItem = BrowseResults.FirstOrDefault(i => i.Id == id)
            ?? InstalledItems.FirstOrDefault(i => i.Id == id)
            ?? UpdateItems.FirstOrDefault(i => i.Id == id)
            ?? (_service.Store.Find(id) is { } installed ? WithState(ExtensionItemViewModel.FromInstalled(this, installed)) : null);
    }

    /// <summary>Opens a link of an extension's details in the browser.</summary>
    /// <param name="uri">The link.</param>
    public Task OpenLinkAsync(Uri uri) => _browser?.OpenUriAsync(uri) ?? Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _service.Changed -= OnServiceChanged;
        _search?.Cancel();
        _search?.Dispose();
        Details?.Dispose();
    }

    [RelayCommand]
    private void SelectTab(ExtensionsTab tab)
    {
        SelectedTab = tab;
        if (tab == ExtensionsTab.Updates && !_updatesChecked)
        {
            _ = CheckForUpdatesCoreAsync(quiet: true);
        }
    }

    /// <summary>Closes the details, as the back button does where the window is too narrow to show both.</summary>
    [RelayCommand]
    private void CloseDetails() => SelectedItem = null;

    [RelayCommand]
    private void DismissNotice() => SetNotice(null);

    [RelayCommand]
    private async Task RunNoticeActionAsync()
    {
        var action = NoticeAction;
        SetNotice(null);
        if (action is not null)
        {
            await action();
        }
    }

    partial void OnSelectedTabChanged(ExtensionsTab value) => FilterInstalled();

    partial void OnSelectedItemChanged(ExtensionItemViewModel? value)
    {
        // The same extension from another list, or a list rebuilt around it, keeps its details.
        if (value is null || Details?.Item.Id != value.Id)
        {
            Details?.Dispose();
            Details = value is null ? null : new ExtensionDetailsViewModel(this, value);
            _ = Details?.LoadAsync();
        }

        SyncListSelection();
    }

    partial void OnSelectedBrowseItemChanged(ExtensionItemViewModel? value) => ShowFromList(value);

    partial void OnSelectedInstalledItemChanged(ExtensionItemViewModel? value) => ShowFromList(value);

    partial void OnSelectedUpdateItemChanged(ExtensionItemViewModel? value) => ShowFromList(value);

    /// <summary>A row tapped in a list shows its details; a list that loses its selection because it was rebuilt changes nothing.</summary>
    private void ShowFromList(ExtensionItemViewModel? value)
    {
        if (value is not null && !_syncingSelection)
        {
            SelectedItem = value;
        }
    }

    /// <summary>Selects the shown extension's row in every list that has it, so each list highlights it.</summary>
    private void SyncListSelection()
    {
        _syncingSelection = true;
        try
        {
            var id = SelectedItem?.Id;
            SelectedBrowseItem = id is { } browse ? BrowseResults.FirstOrDefault(i => i.Id == browse) : null;
            SelectedInstalledItem = id is { } installed ? InstalledItems.FirstOrDefault(i => i.Id == installed) : null;
            SelectedUpdateItem = id is { } update ? UpdateItems.FirstOrDefault(i => i.Id == update) : null;
        }
        finally
        {
            _syncingSelection = false;
        }
    }

    /// <summary>Shows a message about the last action.</summary>
    private void SetNotice(string? message, bool isError = false, string? actionText = null, Func<Task>? action = null)
    {
        Notice = message;
        IsNoticeError = isError;
        NoticeActionText = message is null ? null : actionText;
        NoticeAction = message is null ? null : action;
    }

    private void OnServiceChanged(object? sender, EventArgs e)
    {
        if (!_disposed)
        {
            RefreshInstalled();
            OnPropertyChanged(nameof(SearchWatermark));
        }
    }

    /// <summary>Lists the installed extensions again and shows every item's state.</summary>
    private void RefreshInstalled()
    {
        var installed = _service.Store.Installed.OrderBy(e => e.Manifest.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToList();
        _installed = installed;
        InstalledCount = installed.Count;
        foreach (var stale in _updates.Keys.Where(id => installed.All(e => e.Id != id) || !ExtensionVersions.IsNewer(_updates[id].Latest.Version, installed.First(e => e.Id == id).Version)).ToList())
        {
            _updates.Remove(stale);
        }

        UpdateItems.ReplaceAll(_updates.Values.Select(u => WithState(ExtensionItemViewModel.FromInstalled(this, u.Installed))));
        UpdateCount = _updates.Count;
        FilterInstalled();
        foreach (var item in BrowseResults)
        {
            WithState(item);
        }

        if (Details is { } details)
        {
            WithState(details.Item);
            details.Refresh();
        }

        SyncListSelection();
    }

    private void FilterInstalled()
    {
        var filter = SelectedTab == ExtensionsTab.Installed ? SearchText.Trim() : string.Empty;
        InstalledItems.ReplaceAll(_installed
            .Where(e => filter.Length == 0
                || e.Manifest.DisplayName.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || e.Id.ToString().Contains(filter, StringComparison.OrdinalIgnoreCase)
                || e.Manifest.Description.Contains(filter, StringComparison.CurrentCultureIgnoreCase))
            .Select(e => WithState(ExtensionItemViewModel.FromInstalled(this, e))));
        InstalledMessage = InstalledItems.Count > 0 ? null
            : _installed.Count == 0 ? "No extensions are installed yet. Browse Open VSX for languages, themes and snippets, or install a .vsix file."
            : $"No installed extensions match “{filter}”.";
        SyncListSelection();
    }

    /// <summary>Shows whether an item is installed, has an update or is being installed.</summary>
    internal ExtensionItemViewModel WithState(ExtensionItemViewModel item)
    {
        item.SetState(_service.Store.Find(item.Id), _updates.TryGetValue(item.Id, out var update) ? update.Latest.Version : null);
        item.SetBusy(_busy.TryGetValue(item.Id, out var progress), progress);
        return item;
    }

    /// <summary>Shows an extension as being installed, or done, in every list it is in.</summary>
    private void SetBusy(ExtensionIdentifier id, bool busy, double progress = 0)
    {
        if (busy)
        {
            _busy[id] = progress;
        }
        else
        {
            _busy.Remove(id);
        }

        foreach (var item in BrowseResults.Concat(InstalledItems).Concat(UpdateItems).Where(i => i.Id == id))
        {
            item.SetBusy(busy, progress);
        }

        if (Details is { } details && details.Item.Id == id)
        {
            details.Item.SetBusy(busy, progress);
        }
    }
}
