using Avalonia.Controls;

namespace TabStudio.App.Platform;

/// <summary>
/// The displays connected to the device besides its own screen, and a view shown full screen on one of them. Android
/// implements this with DisplayManager's presentation displays and a Presentation; the desktop host has none.
/// </summary>
/// <remarks>Members are used and events raised on the UI thread.</remarks>
public interface IExternalDisplayService
{
    /// <summary>Whether the platform can show a view on another display at all. False on the desktop host.</summary>
    bool IsSupported { get; }

    /// <summary>The connected display a view can be shown on, or <see langword="null"/> when there is none.</summary>
    ExternalDisplay? Display { get; }

    /// <summary>Whether a view is shown on <see cref="Display"/>.</summary>
    bool IsPresenting { get; }

    /// <summary>
    /// The pointer the platform moves on the display while it has the tablet's mouse to itself, for the shown view
    /// to draw. Hidden while the system's own pointer is used, or the mouse is with the tablet.
    /// </summary>
    DesktopCursor Cursor { get; }

    /// <summary>
    /// Whether a keyboard is connected to the device: its keys type on the display. Without one, the tablet's
    /// on-screen keyboard types there through desktop mode's companion.
    /// </summary>
    bool HasKeyboard { get; }

    /// <summary>Raised when a keyboard is connected or disconnected.</summary>
    event EventHandler? KeyboardChanged;

    /// <summary>Raised when a display is connected or disconnected, or its size changes.</summary>
    event EventHandler? DisplayChanged;

    /// <summary>
    /// Raised when the system ends the presentation on its own: the display was disconnected, or its size changed,
    /// which Android does not resize a presentation for. Not raised by <see cref="Dismiss"/>.
    /// </summary>
    event EventHandler? PresentationEnded;

    /// <summary>Shows a view full screen on <see cref="Display"/>, replacing the one shown there.</summary>
    /// <param name="content">The view: the root of what the display shows.</param>
    /// <param name="scale">How many display pixels one device-independent unit takes, such as 1.25.</param>
    /// <returns>Whether it is shown; <see langword="false"/> when there is no display or the system refused.</returns>
    bool Present(Control content, double scale);

    /// <summary>Stops showing the view on the display, and lets go of it so it can be put somewhere else.</summary>
    void Dismiss();
}
