using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace TabStudio.App.Converters;

/// <summary>Value converters for the debugger's views. Each looks up its brushes in the app's theme.</summary>
public static class DebugConverters
{
    /// <summary>The status bar's background: orange while debugging, as in Visual Studio, otherwise the accent color.</summary>
    public static IValueConverter StatusBarBackground { get; } =
        new FuncValueConverter<bool, IBrush?>(debugging => Find<IBrush>(debugging ? "Brush.Debugging" : "Brush.Accent"));

    private static T? Find<T>(string key)
        where T : class =>
        Application.Current?.TryGetResource(key, Application.Current.ActualThemeVariant, out var value) == true ? value as T : null;
}
