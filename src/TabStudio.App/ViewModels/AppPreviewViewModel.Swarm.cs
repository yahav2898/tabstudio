using System.Windows.Input;

namespace TabStudio.App.ViewModels;

/// <summary>The app preview's way to the Device Matrix, which shows the app on every screen at once.</summary>
public sealed partial class AppPreviewViewModel
{
    /// <summary>Opens the Device Matrix, from the preview's toolbar; the workspace sets it.</summary>
    public ICommand? OpenDeviceMatrixCommand { get; set; }
}
