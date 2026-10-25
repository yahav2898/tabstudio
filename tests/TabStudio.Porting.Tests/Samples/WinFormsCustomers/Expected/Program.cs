using Avalonia;

namespace CustomerEntry;

internal static class Program
{
    // TabStudio shows this app beside the code. To run it on a PC, add the Avalonia.Desktop package
    // and call UsePlatformDetect() before StartWithClassicDesktopLifetime.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().LogToTrace();
}
