using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services.Extensions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>What the Extensions window does to extensions: install, update, uninstall, enable, disable and use their themes.</summary>
public sealed partial class ExtensionsViewModel
{
    private readonly Dictionary<ExtensionIdentifier, ExtensionUpdate> _updates = [];
    private bool _updatesChecked;

    /// <summary>Whether updates are being looked for.</summary>
    [ObservableProperty]
    public partial bool IsCheckingUpdates { get; private set; }

    /// <summary>What the Updates list says when it is empty, or after a failed check.</summary>
    [ObservableProperty]
    public partial string UpdatesMessage { get; private set; } = "Your extensions are up to date.";

    /// <summary>Raised after an extension was installed, with the extension, so the IDE can report it.</summary>
    public event EventHandler<InstalledExtension>? Installed;

    /// <summary>Downloads an extension's latest version from the registry, verifies it and installs it.</summary>
    /// <param name="item">The extension.</param>
    public async Task InstallAsync(ExtensionItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_busy.ContainsKey(item.Id))
        {
            return;
        }

        SetBusy(item.Id, true);
        try
        {
            var extension = await Client.GetExtensionAsync(item.Id);
            var progress = new Progress<double>(done => SetBusy(item.Id, true, done));
            var installed = await _service.InstallAsync(extension, progress);
            SetBusy(item.Id, false);
            await InstallDependenciesAsync(installed);
            ReportInstalled(installed, updated: false);
        }
        catch (Exception ex) when (ex is OpenVsxException or ExtensionPackageException or IOException or UnauthorizedAccessException)
        {
            SetNotice($"{item.DisplayName} was not installed. {ex.Message}", isError: true);
        }
        finally
        {
            SetBusy(item.Id, false);
        }
    }

    /// <summary>Updates an installed extension to the registry's latest version.</summary>
    /// <param name="item">The extension.</param>
    public async Task UpdateAsync(ExtensionItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_busy.ContainsKey(item.Id))
        {
            return;
        }

        SetBusy(item.Id, true);
        try
        {
            var extension = _updates.TryGetValue(item.Id, out var update) ? update.Latest : await Client.GetExtensionAsync(item.Id);
            var progress = new Progress<double>(done => SetBusy(item.Id, true, done));
            var installed = await _service.InstallAsync(extension, progress);
            _updates.Remove(item.Id);
            SetBusy(item.Id, false);
            RefreshInstalled();
            ReportInstalled(installed, updated: true);
        }
        catch (Exception ex) when (ex is OpenVsxException or ExtensionPackageException or IOException or UnauthorizedAccessException)
        {
            SetNotice($"{item.DisplayName} was not updated. {ex.Message}", isError: true);
        }
        finally
        {
            SetBusy(item.Id, false);
        }
    }

    /// <summary>Uninstalls an extension.</summary>
    /// <param name="id">The extension.</param>
    public async Task UninstallAsync(ExtensionIdentifier id)
    {
        var name = _service.Store.Find(id)?.Manifest.DisplayName ?? id.ToString();
        try
        {
            await _service.UninstallAsync(id);
            SetNotice($"Uninstalled {name}.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetNotice($"{name} was not uninstalled. {ex.Message}", isError: true);
        }
    }

    /// <summary>Enables or disables an extension: a disabled one keeps its files but adds nothing.</summary>
    /// <param name="id">The extension.</param>
    /// <param name="enabled">Whether it is enabled.</param>
    public async Task SetEnabledAsync(ExtensionIdentifier id, bool enabled)
    {
        var name = _service.Store.Find(id)?.Manifest.DisplayName ?? id.ToString();
        try
        {
            await _service.SetEnabledAsync(id, enabled);
            SetNotice(enabled ? $"Enabled {name}." : $"Disabled {name}. Its languages, snippets and themes are not used until it is enabled again.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetNotice($"{name} was not {(enabled ? "enabled" : "disabled")}. {ex.Message}", isError: true);
        }
    }

    /// <summary>Uses one of an extension's color themes, asking which when it has several.</summary>
    /// <param name="id">The extension.</param>
    public Task UseColorThemeAsync(ExtensionIdentifier id) => ThemePickers.PickColorThemeAsync(_service, _quickPick, id);

    /// <summary>Uses one of an extension's file icon themes, asking which when it has several.</summary>
    /// <param name="id">The extension.</param>
    public Task UseIconThemeAsync(ExtensionIdentifier id) => ThemePickers.PickIconThemeAsync(_service, _quickPick, id);

    /// <summary>Installs an extension from a .vsix file the user picks.</summary>
    [RelayCommand]
    public async Task InstallFromFileAsync()
    {
        PickedVsix? file;
        try
        {
            file = await VsixPicker.PickAsync();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetNotice($"The file cannot be read. {ex.Message}", isError: true);
            return;
        }

        if (file is null)
        {
            return;
        }

        using (file)
        {
            try
            {
                var installed = await _service.InstallFileAsync(file.Content, file.Name);
                await InstallDependenciesAsync(installed);
                SelectedTab = ExtensionsTab.Installed;
                ShowDetails(installed.Id);
                ReportInstalled(installed, updated: false);
            }
            catch (Exception ex) when (ex is ExtensionPackageException or IOException or UnauthorizedAccessException)
            {
                SetNotice($"{file.Name} was not installed. {ex.Message}", isError: true);
            }
        }
    }

    /// <summary>Looks for newer versions of the installed extensions.</summary>
    [RelayCommand]
    public Task CheckForUpdatesAsync() => CheckForUpdatesCoreAsync(quiet: false);

    /// <summary>Updates every extension that has a newer version.</summary>
    [RelayCommand]
    private async Task UpdateAllAsync()
    {
        foreach (var item in UpdateItems.ToList())
        {
            await UpdateAsync(item);
        }
    }

    private async Task CheckForUpdatesCoreAsync(bool quiet)
    {
        if (IsCheckingUpdates)
        {
            return;
        }

        IsCheckingUpdates = true;
        try
        {
            var updates = await _service.CheckForUpdatesAsync();
            _updatesChecked = true;
            _updates.Clear();
            foreach (var update in updates)
            {
                _updates[update.Installed.Id] = update;
            }

            UpdatesMessage = "Your extensions are up to date.";
            RefreshInstalled();
            if (!quiet)
            {
                SetNotice(updates.Count switch
                {
                    0 => "Your extensions are up to date.",
                    1 => $"An update is available for {updates[0].Installed.Manifest.DisplayName}.",
                    _ => $"Updates are available for {updates.Count} extensions.",
                });
            }
        }
        catch (OpenVsxException ex)
        {
            UpdatesMessage = $"Updates cannot be checked now. {ex.Message}";
            if (!quiet)
            {
                SetNotice(UpdatesMessage, isError: true);
            }
        }
        finally
        {
            IsCheckingUpdates = false;
        }
    }

    /// <summary>
    /// Installs the extensions an extension depends on and, for an extension pack, its members, from the registry, as
    /// VS Code does. One that cannot be installed is reported and the rest go on.
    /// </summary>
    private async Task InstallDependenciesAsync(InstalledExtension installed)
    {
        var pending = new Queue<ExtensionIdentifier>(Requirements(installed.Manifest));
        var seen = new HashSet<ExtensionIdentifier> { installed.Id };
        var failed = new List<string>();
        while (pending.Count > 0)
        {
            var id = pending.Dequeue();
            if (!seen.Add(id) || _service.Store.Find(id) is not null)
            {
                continue;
            }

            SetBusy(id, true);
            try
            {
                var extension = await Client.GetExtensionAsync(id);
                var dependency = await _service.InstallAsync(extension, new Progress<double>(done => SetBusy(id, true, done)));
                foreach (var next in Requirements(dependency.Manifest))
                {
                    pending.Enqueue(next);
                }
            }
            catch (Exception ex) when (ex is OpenVsxException or ExtensionPackageException or IOException or UnauthorizedAccessException)
            {
                failed.Add(id.ToString());
            }
            finally
            {
                SetBusy(id, false);
            }
        }

        if (failed.Count > 0)
        {
            SetNotice($"{installed.Manifest.DisplayName} is installed, but {string.Join(", ", failed)} it needs could not be installed.", isError: true);
        }
    }

    private static IEnumerable<ExtensionIdentifier> Requirements(ExtensionManifest manifest)
    {
        foreach (var text in manifest.ExtensionDependencies.Concat(manifest.ExtensionPack))
        {
            if (ExtensionIdentifier.TryParse(text, out var id))
            {
                yield return id;
            }
        }
    }

    /// <summary>Reports an installed extension, offering its color theme when it has one, as VS Code does.</summary>
    private void ReportInstalled(InstalledExtension installed, bool updated)
    {
        if (Notice is not null && IsNoticeError)
        {
            // A dependency failed: that message matters more.
            Installed?.Invoke(this, installed);
            return;
        }

        var name = $"{installed.Manifest.DisplayName} {installed.Version}";
        var themes = installed.Manifest.Contributes.Themes;
        var iconThemes = installed.Manifest.Contributes.IconThemes;
        if (!updated && themes.Count > 0)
        {
            SetNotice($"Installed {name}.", actionText: "Set Color Theme", action: () => UseColorThemeAsync(installed.Id));
        }
        else if (!updated && iconThemes.Count > 0)
        {
            SetNotice($"Installed {name}.", actionText: "Set File Icon Theme", action: () => UseIconThemeAsync(installed.Id));
        }
        else
        {
            SetNotice(updated ? $"Updated {name}." : $"Installed {name}.");
        }

        Installed?.Invoke(this, installed);
    }
}
