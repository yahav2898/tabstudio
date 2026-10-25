using System.ComponentModel;

namespace TabStudio.App.ViewModels.DesktopMode;

/// <summary>The companion's typing bar: the tablet's on-screen keyboard for the big screen, when no keyboard is connected.</summary>
public sealed partial class DesktopCompanionViewModel
{
    /// <summary>
    /// Whether the companion offers the tablet's on-screen keyboard for the big screen: a workspace is open there and
    /// no keyboard is connected to type on it.
    /// </summary>
    public bool ShowsKeyboard => HasWorkspace && !Desktop.HasKeyboard;

    private void OnDesktopPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesktopModeViewModel.HasKeyboard))
        {
            OnPropertyChanged(nameof(ShowsKeyboard));
        }
    }
}
