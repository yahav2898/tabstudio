using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabStudio.App.ViewModels;

/// <summary>How big the app preview draws the running app.</summary>
public enum AppPreviewFrame
{
    /// <summary>As big as the pane.</summary>
    Fill,

    /// <summary>A phone's screen in portrait, scaled to fit.</summary>
    Phone,

    /// <summary>A tablet's screen in landscape, scaled to fit.</summary>
    Tablet,
}

/// <summary>
/// The app preview in the side bar: the running Avalonia app's main view, rendered natively beside the code, in a
/// frame the size of a phone, a tablet or the pane.
/// </summary>
public sealed partial class AppPreviewViewModel : ObservableObject
{
    /// <summary>The app being previewed, such as <c>HelloAvalonia</c>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasApp), nameof(Title))]
    public partial string? AppName { get; private set; }

    /// <summary>The app's main view while it runs.</summary>
    [ObservableProperty]
    public partial Control? View { get; private set; }

    /// <summary>How the preview differs from the app on a device, when it does.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNote))]
    public partial string? Note { get; private set; }

    /// <summary>Whether the app is running.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    public partial bool IsRunning { get; private set; }

    /// <summary>The frame the app is drawn in.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FrameWidth), nameof(FrameHeight), nameof(IsFill))]
    public partial AppPreviewFrame Frame { get; set; }

    /// <summary>Whether an app has run in this workspace, which shows the preview's tab.</summary>
    public bool HasApp => AppName is not null;

    /// <summary>Whether there is a note to show above the app.</summary>
    public bool HasNote => Note is not null;

    /// <summary>Whether the app fills the pane rather than a device's frame.</summary>
    public bool IsFill => Frame == AppPreviewFrame.Fill;

    /// <summary>The frame's width in device-independent pixels, before it is scaled to fit.</summary>
    public double FrameWidth => Frame switch
    {
        AppPreviewFrame.Phone => 390,
        AppPreviewFrame.Tablet => 1280,
        _ => double.NaN,
    };

    /// <summary>The frame's height in device-independent pixels, before it is scaled to fit.</summary>
    public double FrameHeight => Frame switch
    {
        AppPreviewFrame.Phone => 844,
        AppPreviewFrame.Tablet => 800,
        _ => double.NaN,
    };

    /// <summary>The preview's header.</summary>
    public string Title => AppName ?? "App Preview";

    /// <summary>What the preview says when the app is not running.</summary>
    public string StatusText => IsRunning ? $"{AppName} is running." : $"{AppName} is not running. Press F5 to start it.";

    /// <summary>The frames to choose from.</summary>
    public IReadOnlyList<AppPreviewFrame> Frames { get; } = Enum.GetValues<AppPreviewFrame>();

    /// <summary>Shows a running app.</summary>
    /// <param name="appName">The project's name.</param>
    /// <param name="view">Its main view.</param>
    /// <param name="note">How the preview differs from the app on a device, if it does.</param>
    public void Show(string appName, Control view, string? note)
    {
        ArgumentNullException.ThrowIfNull(appName);
        ArgumentNullException.ThrowIfNull(view);
        AppName = appName;
        View = view;
        Note = note;
        IsRunning = true;
    }

    /// <summary>Takes the app's view away once it stops.</summary>
    public void ShowStopped()
    {
        View = null;
        IsRunning = false;
    }
}
