using TabStudio.App.ViewModels.DesktopMode;

namespace TabStudio.App.ViewModels;

/// <summary>Desktop mode: the IDE on an external display, and the tablet as its touch companion.</summary>
public sealed partial class MainViewModel
{
    /// <summary>Desktop mode, or <see langword="null"/> where the app was built without it.</summary>
    public DesktopModeViewModel? DesktopMode { get; }

    /// <summary>Gives desktop mode, and the workspace's status bar button and menu, the workspace that is open now.</summary>
    partial void OnCurrentPageChanged(object value)
    {
        var workspace = value as WorkspaceViewModel;
        if (workspace is not null)
        {
            workspace.DesktopMode = DesktopMode;
        }

        if (DesktopMode is not null)
        {
            DesktopMode.Workspace = workspace;
        }
    }
}
