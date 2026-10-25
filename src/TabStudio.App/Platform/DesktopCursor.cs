using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TabStudio.App.Platform;

/// <summary>
/// The mouse pointer TabStudio draws on the external display in desktop mode while it has the tablet's mouse to
/// itself, on Android versions that keep the system's pointer on the tablet. The platform moves it by the mouse's
/// movements and gives the IDE the mouse's events where it is; the display's view draws it.
/// </summary>
/// <remarks>
/// Positions are kept in the display's pixels, where the platform's events are, with fractions, so slow movements
/// add up. A mouse gives raw movements to an app that captures it, so the pointer speeds up with the mouse, as a PC's
/// pointer does: one pixel per count for slow, precise movements, up to <see cref="MaxGain"/> for fast ones.
/// </remarks>
public sealed partial class DesktopCursor : ObservableObject
{
    /// <summary>The fastest the pointer goes, in pixels per count of the mouse.</summary>
    public const double MaxGain = 2.5;

    /// <summary>The longest movement of one event, in counts, that moves the pointer one pixel per count.</summary>
    public const double PreciseDistance = 6;

    /// <summary>How many counts longer than <see cref="PreciseDistance"/> a movement is when the pointer is fastest.</summary>
    private const double AccelerationDistance = 20;

    private double _x;
    private double _y;
    private int _width;
    private int _height;
    private double _scale = 1;

    /// <summary>Where the pointer's tip is, in device-independent units of the display's view.</summary>
    [ObservableProperty]
    public partial Point Position { get; private set; }

    /// <summary>Whether it is drawn: while TabStudio has the mouse for the display.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; private set; }

    /// <summary>Where the pointer's tip is, in the display's pixels: where the IDE gets the mouse's events.</summary>
    public Point PixelPosition => new(_x, _y);

    /// <summary>
    /// The display the pointer moves on. It starts in the middle of the first one, and stays where it was, inside the
    /// display, when the display or its scale changes.
    /// </summary>
    /// <param name="pixelWidth">The display's width, in pixels.</param>
    /// <param name="pixelHeight">The display's height, in pixels.</param>
    /// <param name="scale">How many pixels one device-independent unit of the display's view takes.</param>
    public void Reset(int pixelWidth, int pixelHeight, double scale)
    {
        var first = _width == 0;
        _width = Math.Max(1, pixelWidth);
        _height = Math.Max(1, pixelHeight);
        _scale = scale > 0 ? scale : 1;
        if (first)
        {
            _x = _width / 2.0;
            _y = _height / 2.0;
        }

        Place(_x, _y);
    }

    /// <summary>Moves the pointer by a mouse's movement, faster the faster the mouse moves, and keeps it on the display.</summary>
    /// <param name="dx">The movement to the right, in counts of the mouse.</param>
    /// <param name="dy">The movement down, in counts of the mouse.</param>
    /// <returns>The new position, in the display's pixels.</returns>
    public Point MoveBy(double dx, double dy)
    {
        var gain = Gain(Math.Sqrt((dx * dx) + (dy * dy)));
        Place(_x + (dx * gain), _y + (dy * gain));
        return PixelPosition;
    }

    /// <summary>Puts the pointer at a point of the display, such as where the system's own pointer was on it.</summary>
    /// <param name="x">The distance from the display's left edge, in its pixels.</param>
    /// <param name="y">The distance from the display's top edge, in its pixels.</param>
    public void MoveTo(double x, double y) => Place(x, y);

    /// <summary>Draws the pointer: TabStudio has the mouse for the display.</summary>
    public void Show() => IsVisible = true;

    /// <summary>Stops drawing the pointer: the mouse is with the tablet, or the system draws its own pointer.</summary>
    public void Hide() => IsVisible = false;

    /// <summary>How many pixels the pointer moves per count of a movement of the given length.</summary>
    /// <param name="distance">The length of one event's movement, in counts.</param>
    /// <returns>1 up to <see cref="PreciseDistance"/>, rising to <see cref="MaxGain"/>.</returns>
    public static double Gain(double distance) =>
        Math.Clamp(1 + ((distance - PreciseDistance) * (MaxGain - 1) / AccelerationDistance), 1, MaxGain);

    private void Place(double x, double y)
    {
        _x = _width == 0 ? x : Math.Clamp(x, 0, _width - 1);
        _y = _height == 0 ? y : Math.Clamp(y, 0, _height - 1);
        Position = new Point(_x / _scale, _y / _scale);
    }
}
