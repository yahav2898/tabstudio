using Avalonia;
using TabStudio.App;

namespace TabStudio.Desktop;

/// <summary>The desktop entry point.</summary>
internal static class Program
{
    /// <summary>Starts TabStudio in a desktop window.</summary>
    /// <param name="args">Command-line arguments.</param>
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Configures Avalonia. Also used by the XAML previewer.</summary>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TabStudioApplication>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
