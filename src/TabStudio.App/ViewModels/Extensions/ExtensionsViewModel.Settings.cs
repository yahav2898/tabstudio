using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Extensions.OpenVsx;

namespace TabStudio.App.ViewModels.Extensions;

/// <summary>The registry setting: open-vsx.org, or a company's own Open VSX server.</summary>
public sealed partial class ExtensionsViewModel
{
    /// <summary>Whether the registry setting is shown.</summary>
    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    /// <summary>The registry's address as typed.</summary>
    [ObservableProperty]
    public partial string RegistryText { get; set; } = string.Empty;

    /// <summary>Why the typed address cannot be used, or <see langword="null"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRegistryError))]
    public partial string? RegistryError { get; private set; }

    /// <summary>Whether <see cref="RegistryError"/> is shown.</summary>
    public bool HasRegistryError => RegistryError is not null;

    /// <summary>The registry in use, for the settings.</summary>
    public string RegistryHost => Client.Registry.Host;

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
        RegistryText = Client.Registry.AbsoluteUri;
        RegistryError = null;
    }

    /// <summary>Uses the typed registry address and searches it.</summary>
    [RelayCommand]
    private async Task ApplyRegistryAsync()
    {
        var text = RegistryText.Trim();
        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var address))
        {
            RegistryError = "Type the address of an Open VSX server, such as https://open-vsx.org.";
            return;
        }

        try
        {
            _service.SetRegistry(address);
        }
        catch (ArgumentException)
        {
            RegistryError = "The address must start with https:// or http://.";
            return;
        }

        RegistryError = null;
        IsSettingsOpen = false;
        await OnRegistryChangedAsync();
    }

    /// <summary>Goes back to open-vsx.org.</summary>
    [RelayCommand]
    private async Task ResetRegistryAsync()
    {
        _service.SetRegistry(null);
        RegistryText = OpenVsxClient.DefaultRegistry.AbsoluteUri;
        RegistryError = null;
        IsSettingsOpen = false;
        await OnRegistryChangedAsync();
    }

    private async Task OnRegistryChangedAsync()
    {
        OnPropertyChanged(nameof(RegistryHost));
        OnPropertyChanged(nameof(SearchWatermark));
        _updatesChecked = false;
        SetNotice($"Extensions now come from {Client.Registry.Host}.");
        SelectedTab = ExtensionsTab.Browse;
        await SearchAsync(append: false);
    }
}
