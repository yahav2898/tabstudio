using Avalonia.Controls;
using TabStudio.App.Platform;

namespace TabStudio.App.Tests.DesktopMode;

/// <summary>
/// An external display for headless tests: connecting it is plugging in a monitor, and what desktop mode presents on
/// it is shown in a second headless window of the display's size, which tests take screenshots of.
/// </summary>
internal sealed class FakeExternalDisplay : IExternalDisplayService
{
    /// <summary>The id the display gets.</summary>
    public const int DisplayId = 2;

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public ExternalDisplay? Display { get; private set; }

    /// <inheritdoc />
    public bool IsPresenting => Window is not null;

    /// <inheritdoc />
    public DesktopCursor Cursor { get; } = new();

    /// <inheritdoc />
    public bool HasKeyboard { get; private set; } = true;

    /// <inheritdoc />
    public event EventHandler? KeyboardChanged;

    /// <summary>Connects or disconnects the tablet's keyboard.</summary>
    public void SetKeyboard(bool connected)
    {
        HasKeyboard = connected;
        KeyboardChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The window standing for the display while it shows something.</summary>
    public Window? Window { get; private set; }

    /// <summary>The scale of the last presentation.</summary>
    public double PresentedScale { get; private set; }

    /// <summary>How many times something was presented.</summary>
    public int Presentations { get; private set; }

    /// <summary>Whether <see cref="Present"/> refuses, as Android does when the display went away meanwhile.</summary>
    public bool Refuses { get; set; }

    /// <inheritdoc />
    public event EventHandler? DisplayChanged;

    /// <inheritdoc />
    public event EventHandler? PresentationEnded;

    /// <summary>Plugs in a monitor.</summary>
    public void Connect(int width = 1920, int height = 1080, double systemScale = 1, string name = "HDMI Screen")
    {
        Display = new ExternalDisplay(DisplayId, name, width, height, systemScale);
        DisplayChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Unplugs the monitor, which ends what it showed, as Android does.</summary>
    public void Disconnect()
    {
        Display = null;
        Close();
        DisplayChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Ends the presentation as the system does when the display's size changes.</summary>
    public void EndPresentationBySystem()
    {
        Close();
        PresentationEnded?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public bool Present(Control content, double scale)
    {
        Dismiss();
        if (Display is not { } display || Refuses)
        {
            return false;
        }

        Window = new Window
        {
            Width = display.PixelWidth / scale,
            Height = display.PixelHeight / scale,
            Content = content,
        };
        Window.Show();
        // A headless window draws one pixel per unit, so its pixels are its units.
        Cursor.Reset((int)Window.Width, (int)Window.Height, Window.RenderScaling);
        PresentedScale = scale;
        Presentations++;
        return true;
    }

    /// <inheritdoc />
    public void Dismiss() => Close();

    private void Close()
    {
        if (Window is { } window)
        {
            Cursor.Hide();
            Window = null;
            window.Content = null;
            window.Close();
        }
    }
}
