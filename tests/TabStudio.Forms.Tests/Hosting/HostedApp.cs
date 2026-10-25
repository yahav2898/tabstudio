using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using TabStudio.Forms.Hosting;
using TabStudio.Forms.Runtime;
using TabStudio.Forms.Tests.Infrastructure;
using AvaloniaPoint = Avalonia.Point;
using DrawingPoint = System.Drawing.Point;

namespace TabStudio.Forms.Tests.Hosting;

/// <summary>
/// An app running on a <see cref="FormsDesktop"/> in a headless Avalonia window, as the app preview hosts it: the
/// tests press, drag and type on the desktop as a user does, and read the app's state on its own thread.
/// </summary>
internal sealed class HostedApp : IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private HostedApp(FormsHost host, Window window)
    {
        Host = host;
        Window = window;
    }

    /// <summary>The run.</summary>
    public FormsHost Host { get; }

    /// <summary>The window the desktop fills.</summary>
    public Window Window { get; }

    /// <summary>The desktop.</summary>
    public FormsDesktop Desktop => Host.View;

    /// <summary>The app's runtime.</summary>
    public FormsRuntime Runtime => Desktop.Runtime ?? throw new InvalidOperationException("The run ended.");

    /// <summary>Starts an app in a window of a size, and waits for its first window.</summary>
    /// <param name="main">The app's Main.</param>
    /// <param name="width">The width of the pane it runs in.</param>
    /// <param name="height">The height of the pane.</param>
    public static HostedApp Start(Func<int> main, double width = 900, double height = 600)
    {
        var host = FormsHost.Start(main);
        var window = new Window { Width = width, Height = height, Content = host.View };
        window.Show();
        var app = new HostedApp(host, window);
        app.WaitFor(() => app.Desktop.Windows.Count > 0, "The app's first window never showed.");
        return app;
    }

    /// <summary>Runs the UI thread's work until a condition holds.</summary>
    /// <param name="condition">The condition, checked on the UI thread.</param>
    /// <param name="message">What failed, if it never holds.</param>
    public void WaitFor(Func<bool> condition, string message)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < Timeout, message);
            Settle();
            Thread.Sleep(5);
        }
    }

    /// <summary>Lets the app finish what it is doing and the desktop show it.</summary>
    public void Settle()
    {
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            if (Desktop.Runtime is { } runtime)
            {
                runtime.Send(() => runtime.PaintNow(), TimeSpan.FromSeconds(5));
            }

            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>Reads the app's state on its thread.</summary>
    /// <param name="read">Reads it.</param>
    public T Read<T>(Func<T> read)
    {
        T value = default!;
        Assert.True(Runtime.Send(() => value = read(), Timeout), "The app is busy.");
        return value;
    }

    /// <summary>A point on the desktop in the window's coordinates.</summary>
    /// <param name="point">The point on the desktop.</param>
    public AvaloniaPoint ToWindow(DrawingPoint point) => new((point.X + 0.5) * Desktop.Scale, (point.Y + 0.5) * Desktop.Scale);

    /// <summary>Clicks a point of the desktop with the mouse.</summary>
    /// <param name="point">The point.</param>
    public void Click(DrawingPoint point)
    {
        Window.MouseMove(ToWindow(point));
        Window.MouseDown(ToWindow(point), MouseButton.Left);
        Window.MouseUp(ToWindow(point), MouseButton.Left);
        Settle();
    }

    /// <summary>Drags the mouse from one point of the desktop to another.</summary>
    /// <param name="from">Where the drag starts.</param>
    /// <param name="to">Where it ends.</param>
    public void Drag(DrawingPoint from, DrawingPoint to)
    {
        Window.MouseMove(ToWindow(from));
        Window.MouseDown(ToWindow(from), MouseButton.Left);
        Settle();
        for (var step = 1; step <= 4; step++)
        {
            var x = from.X + ((to.X - from.X) * step / 4);
            var y = from.Y + ((to.Y - from.Y) * step / 4);
            Window.MouseMove(ToWindow(new DrawingPoint(x, y)), RawInputModifiers.LeftMouseButton);
            Settle();
        }

        Window.MouseUp(ToWindow(to), MouseButton.Left);
        Settle();
    }

    /// <summary>Types text into what has the keyboard.</summary>
    /// <param name="text">The text.</param>
    public void Type(string text)
    {
        foreach (var character in text)
        {
            Window.KeyTextInput(character.ToString());
            Settle();
        }
    }

    /// <summary>Presses and releases a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers held.</param>
    public void Key(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Settle();
    }

    /// <summary>Saves a picture of the pane, for the screenshots CI publishes, and returns it.</summary>
    /// <param name="name">The file name, without extension.</param>
    public WriteableBitmapPixels Screenshot(string name)
    {
        Settle();
        var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("Nothing was rendered.");
        if (Screenshots.Folder is { } folder)
        {
            Directory.CreateDirectory(folder);
            frame.Save(Path.Combine(folder, name + ".png"));
        }

        return WriteableBitmapPixels.From(frame);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Host.Stop();
        Host.Completion.Wait(Timeout);
        Host.Dispose();
        Window.Close();
    }
}
