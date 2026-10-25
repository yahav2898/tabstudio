using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabStudio.App.ViewModels;

/// <summary>
/// The app preview as one of Swarm's screens: its way to the Device Matrix, which shows the app on every screen at once,
/// and the short notices its screen shows, such as a tap mirrored from a phone that found no element here.
/// </summary>
public sealed partial class AppPreviewViewModel
{
    /// <summary>How long a notice stays on the preview.</summary>
    private static readonly TimeSpan NoticeTime = TimeSpan.FromSeconds(3.5);

    private int _notices;

    /// <summary>Opens the Device Matrix, from the preview's toolbar; the workspace sets it.</summary>
    public ICommand? OpenDeviceMatrixCommand { get; set; }

    /// <summary>A short message shown over the app for a moment.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    public partial string? Notice { get; private set; }

    /// <summary>Whether a notice is shown.</summary>
    public bool HasNotice => Notice is not null;

    /// <summary>Shows a short message over the app for a few seconds. UI thread.</summary>
    /// <param name="text">The message.</param>
    public void ShowNotice(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Notice = text;
        _ = ClearNoticeLaterAsync(++_notices);
    }

    private async Task ClearNoticeLaterAsync(int shown)
    {
        await Task.Delay(NoticeTime);
        if (shown == _notices)
        {
            Notice = null;
        }
    }
}
