using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Converters;

/// <summary>
/// The Test Explorer's outcome icons, as in Visual Studio: a green check for passed, a red cross for failed, a
/// yellow warning for skipped and a blue mark for tests that have not run.
/// </summary>
public static class TestConverters
{
    /// <summary>The icon geometry for a test state.</summary>
    public static IValueConverter StateIcon { get; } = new FuncValueConverter<TestState, Geometry?>(state => Find<Geometry>(state switch
    {
        TestState.Passed => "Icon.TestPassed",
        TestState.Failed => "Icon.Error",
        TestState.Skipped => "Icon.Warning",
        TestState.Running => "Icon.TestRunning",
        TestState.Queued => "Icon.TestQueued",
        _ => "Icon.TestNotRun",
    }));

    /// <summary>The icon color for a test state.</summary>
    public static IValueConverter StateBrush { get; } = new FuncValueConverter<TestState, IBrush?>(state => Find<IBrush>(state switch
    {
        TestState.Passed => "Brush.Success",
        TestState.Failed => "Brush.Error",
        TestState.Skipped => "Brush.Warning",
        TestState.Running => "Brush.Running",
        TestState.Queued => "Brush.TextMuted",
        _ => "Brush.Info",
    }));

    private static T? Find<T>(string key)
        where T : class =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true ? value as T : null;
}
