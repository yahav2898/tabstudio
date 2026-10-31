using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels.Actions;

namespace TabStudio.App.Converters;

/// <summary>The GitHub Actions window's state icons and colors, from the app's theme.</summary>
public static class ActionsConverters
{
    /// <summary>The icon of a state.</summary>
    public static IValueConverter StateIcon { get; } = new FuncValueConverter<RunVisual, Geometry?>(visual => Find<Geometry>(visual switch
    {
        RunVisual.Passed => "Icon.RunPassed",
        RunVisual.Failed => "Icon.RunFailed",
        RunVisual.Running => "Icon.RunRunning",
        RunVisual.Queued => "Icon.RunQueued",
        RunVisual.Cancelled or RunVisual.Skipped => "Icon.RunCancelled",
        RunVisual.Neutral => "Icon.RunNeutral",
        _ => "Icon.RunNone",
    }));

    /// <summary>The color of a state.</summary>
    public static IValueConverter StateBrush { get; } = new FuncValueConverter<RunVisual, IBrush?>(visual => Find<IBrush>(visual switch
    {
        RunVisual.Passed => "Brush.Run.Passed",
        RunVisual.Failed => "Brush.Run.Failed",
        RunVisual.Running => "Brush.Run.Running",
        RunVisual.Queued => "Brush.Run.Queued",
        RunVisual.Skipped => "Brush.TextMuted",
        _ => "Brush.Run.Muted",
    }));

    /// <summary>
    /// The color of a state on the status bar's dark well: the same in both themes, as the status bar is teal in both.
    /// </summary>
    public static IValueConverter StatusBarStateBrush { get; } = new FuncValueConverter<RunVisual, IBrush?>(visual => Find<IBrush>(visual switch
    {
        RunVisual.Passed => "Brush.StatusBar.Passed",
        RunVisual.Failed => "Brush.StatusBar.Failed",
        RunVisual.Running => "Brush.StatusBar.Running",
        RunVisual.Queued => "Brush.StatusBar.Queued",
        _ => "Brush.StatusBar.Muted",
    }));

    /// <summary>The soft background of a state, for pills and banners.</summary>
    public static IValueConverter StateSoftBrush { get; } = new FuncValueConverter<RunVisual, IBrush?>(visual => Find<IBrush>(visual switch
    {
        RunVisual.Passed => "Brush.Run.PassedSoft",
        RunVisual.Failed => "Brush.Run.FailedSoft",
        RunVisual.Running or RunVisual.Queued => "Brush.Run.RunningSoft",
        _ => "Brush.Pill",
    }));

    /// <summary>Whether a state's icon spins: only while running.</summary>
    public static IValueConverter IsSpinning { get; } = new FuncValueConverter<RunVisual, bool>(visual => visual == RunVisual.Running);

    private static T? Find<T>(string key)
        where T : class =>
        ThemeBrushes.Find<T>(key);
}
