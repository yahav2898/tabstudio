using TabStudio.App.ViewModels.DesktopMode;

namespace TabStudio.App.ViewModels;

/// <summary>Desktop mode in the IDE: View > Desktop Mode and the status bar button that turn it on and off.</summary>
public sealed partial class WorkspaceViewModel
{
    /// <summary>
    /// Desktop mode, for the menu item and the status bar button; <see langword="null"/> when the workspace was
    /// created outside the app, as in tests of the workspace alone.
    /// </summary>
    public DesktopModeViewModel? DesktopMode { get; internal set; }
}
