using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using TabStudio.App.Swarm;

namespace TabStudio.App.Tests.Swarm;

/// <summary>Helpers for the Swarm tests: windows of a device's size, and awaiting work while frames are drawn.</summary>
internal static class SwarmTestHelpers
{
    /// <summary>Shows content in a headless window of a screen's size and lays it out.</summary>
    public static Window Show(Control content, double width, double height)
    {
        var window = new Window { Width = width, Height = height, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// Awaits work while the headless platform draws frames, as a device's screen does on its own: replayed input
    /// waits for a frame, and Avalonia's controls check what was last drawn.
    /// </summary>
    public static async Task<T> WithFramesAsync<T>(Task<T> work, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (!work.IsCompleted)
        {
            Assert.True(DateTime.UtcNow < deadline, "Timed out waiting for the work while drawing frames.");
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            await Task.Delay(10);
        }

        Dispatcher.UIThread.RunJobs();
        return await work;
    }

    /// <summary>Draws a frame and runs what it scheduled.</summary>
    public static void Frame()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The element with a name under a root, by its logical tree.</summary>
    public static T Named<T>(Visual root, string name)
        where T : Control =>
        root.GetLogicalDescendants().OfType<T>().Single(c => c.Name == name);

    /// <summary>A screen profile for tests.</summary>
    public static ScreenProfile Profile(double width, double height, FormFactor formFactor = FormFactor.Phone, bool isDark = false) =>
        new("Test", formFactor, width, height, IsDark: isDark);
}
