using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using TabStudio.WindowsBridge.Frames;

namespace TabStudio.App.Tests.WindowsBridge;

/// <summary>Pictures of windows for the fake desktop, drawn with Avalonia as Windows 11 draws a small app's window.</summary>
internal static class BridgePictures
{
    /// <summary>The client area's color after the button was clicked.</summary>
    public const uint Green = 0xFF20A040;

    /// <summary>The client area's color before.</summary>
    public const uint Light = 0xFFF3F3F3;

    /// <summary>A small app's main window: its title bar, a greeting and a button; green once the button was clicked.</summary>
    /// <param name="title">The window's title.</param>
    /// <param name="greeting">The text above the button.</param>
    /// <param name="clicked">Whether the button was clicked.</param>
    public static FrameBitmap AppWindow(string title, string greeting, bool clicked) =>
        Render(Window(title, greeting, clicked), 520, 360);

    /// <summary>Renders a control at a size, as the PC captures a window.</summary>
    public static FrameBitmap Render(Control content, int width, int height)
    {
        content.Measure(new Size(width, height));
        content.Arrange(new Rect(0, 0, width, height));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(content);
        var frame = new FrameBitmap(width, height);
        var buffer = Marshal.AllocHGlobal(frame.Pixels.Length);
        try
        {
            bitmap.CopyPixels(new Avalonia.PixelRect(0, 0, width, height), buffer, frame.Pixels.Length, frame.Stride);
            Marshal.Copy(buffer, frame.Pixels, 0, frame.Pixels.Length);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return frame;
    }

    private static Border Window(string title, string greeting, bool clicked)
    {
        var text = new SolidColorBrush(clicked ? Colors.White : Color.FromRgb(0x1B, 0x1B, 0x1B));
        var titleBar = new DockPanel
        {
            Height = 32,
            Background = Brushes.White,
            Children =
            {
                Caption("M0,0 L10,10 M10,0 L0,10"),
                Caption("M0,0 L10,0 L10,10 L0,10 Z"),
                Caption("M0,5 L10,5"),
                new TextBlock { Text = title, FontSize = 12, Margin = new Thickness(14, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B)) },
            },
        };
        DockPanel.SetDock(titleBar, Dock.Top);
        var button = new Border
        {
            Width = 200,
            Height = 64,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xFD, 0xFD)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock { Text = clicked ? "Clicked" : "Click me", FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(0x1B, 0x1B, 0x1B)) },
        };
        var client = new StackPanel
        {
            Spacing = 22,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = greeting, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = text, HorizontalAlignment = HorizontalAlignment.Center },
                button,
            },
        };
        return new Border
        {
            Background = new SolidColorBrush(Color.FromUInt32(clicked ? Green : Light)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0)),
            BorderThickness = new Thickness(1),
            Child = new DockPanel { Children = { titleBar, client } },
        };
    }

    private static Border Caption(string geometry)
    {
        var caption = new Border
        {
            Width = 46,
            Child = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(geometry),
                Stroke = new SolidColorBrush(Color.FromRgb(0x40, 0x40, 0x40)),
                StrokeThickness = 1,
                Width = 10,
                Height = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        DockPanel.SetDock(caption, Dock.Right);
        return caption;
    }
}
