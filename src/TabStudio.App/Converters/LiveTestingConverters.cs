using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.ViewModels;
using TabStudio.LiveTesting.Results;

namespace TabStudio.App.Converters;

/// <summary>
/// Live Unit Testing's icons, as in Visual Studio: a green check for code only passing tests run, a red cross for
/// code a failing test runs, a blue dash for code no test runs, and the flask of Live Unit Testing itself.
/// </summary>
public static class LiveTestingConverters
{
    /// <summary>The icon of a line's coverage.</summary>
    public static IValueConverter CoverageIcon { get; } = new FuncValueConverter<LineCoverageState, Geometry?>(state => Find<Geometry>(state switch
    {
        LineCoverageState.Covered => "Icon.TestPassed",
        LineCoverageState.Failing => "Icon.Error",
        _ => "Icon.NotCovered",
    }));

    /// <summary>The color of a line's coverage.</summary>
    public static IValueConverter CoverageBrush { get; } = new FuncValueConverter<LineCoverageState, IBrush?>(state => Find<IBrush>(state switch
    {
        LineCoverageState.Covered => "Brush.Success",
        LineCoverageState.Failing => "Brush.Error",
        _ => "Brush.Info",
    }));

    /// <summary>The status bar's icon for what Live Unit Testing is doing.</summary>
    public static IValueConverter StateIcon { get; } = new FuncValueConverter<LiveTestingState, Geometry?>(state => Find<Geometry>(state switch
    {
        LiveTestingState.Paused => "Icon.Pause",
        LiveTestingState.BuildFailed => "Icon.Error",
        LiveTestingState.WaitingForDevice => "Icon.Thermometer",
        _ => "Icon.LiveTesting",
    }));

    private static T? Find<T>(string key)
        where T : class =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true ? value as T : null;
}
