using System.Drawing;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>A display: in TabStudio, the app preview's desktop.</summary>
public sealed class Screen
{
    private Screen()
    {
    }

    /// <summary>The display: the app preview's desktop.</summary>
    public static Screen PrimaryScreen { get; } = new();

    /// <summary>Every display: the app preview's desktop.</summary>
    public static Screen[] AllScreens => [PrimaryScreen];

    /// <summary>The display's bounds.</summary>
    public Rectangle Bounds => new(Point.Empty, SystemInformation.PrimaryMonitorSize);

    /// <summary>The display's area for windows.</summary>
    public Rectangle WorkingArea => Bounds;

    /// <summary>Whether this is the primary display. It is.</summary>
    public bool Primary => true;

    /// <summary>The display's name.</summary>
    public string DeviceName => @"\\.\DISPLAY1";

    /// <summary>The bits of a pixel's color.</summary>
    public int BitsPerPixel => 32;

    /// <summary>The display a control is on.</summary>
    /// <param name="control">The control.</param>
    public static Screen FromControl(Control control) => PrimaryScreen;

    /// <summary>The display a window is on.</summary>
    /// <param name="hwnd">The window's handle.</param>
    public static Screen FromHandle(IntPtr hwnd) => PrimaryScreen;

    /// <summary>The display a point is on.</summary>
    /// <param name="point">The point.</param>
    public static Screen FromPoint(Point point) => PrimaryScreen;

    /// <summary>The display a rectangle is on.</summary>
    /// <param name="rect">The rectangle.</param>
    public static Screen FromRectangle(Rectangle rect) => PrimaryScreen;

    /// <summary>The working area of the display a control is on.</summary>
    /// <param name="ctl">The control.</param>
    public static Rectangle GetWorkingArea(Control ctl) => PrimaryScreen.WorkingArea;

    /// <summary>The working area of the display a point is on.</summary>
    /// <param name="pt">The point.</param>
    public static Rectangle GetWorkingArea(Point pt) => PrimaryScreen.WorkingArea;

    /// <summary>The bounds of the display a control is on.</summary>
    /// <param name="ctl">The control.</param>
    public static Rectangle GetBounds(Control ctl) => PrimaryScreen.Bounds;

    /// <inheritdoc />
    public override string ToString() => $"Screen[Bounds={Bounds} WorkingArea={WorkingArea} Primary=True DeviceName={DeviceName}]";
}

/// <summary>Settings of the system, as Windows reports them for the app preview's desktop.</summary>
public static class SystemInformation
{
    /// <summary>The desktop's size.</summary>
    public static Size PrimaryMonitorSize => FormsRuntime.Current?.DesktopSize is { Width: > 0 } size ? size : new Size(1280, 800);

    /// <summary>The desktop's size, for maximized windows.</summary>
    public static Size PrimaryMonitorMaximizedWindowSize => PrimaryMonitorSize;

    /// <summary>The desktop's area for windows.</summary>
    public static Rectangle WorkingArea => new(Point.Empty, PrimaryMonitorSize);

    /// <summary>The bounds of every display together.</summary>
    public static Rectangle VirtualScreen => WorkingArea;

    /// <summary>The number of displays.</summary>
    public static int MonitorCount => 1;

    /// <summary>The width of a vertical scroll bar.</summary>
    public static int VerticalScrollBarWidth => 17;

    /// <summary>The height of a horizontal scroll bar.</summary>
    public static int HorizontalScrollBarHeight => 17;

    /// <summary>The height of a vertical scroll bar's arrow.</summary>
    public static int VerticalScrollBarArrowHeight => 17;

    /// <summary>The width of a horizontal scroll bar's arrow.</summary>
    public static int HorizontalScrollBarArrowWidth => 17;

    /// <summary>The height of a title bar.</summary>
    public static int CaptionHeight => 23;

    /// <summary>The height of a tool window's title bar.</summary>
    public static int ToolWindowCaptionHeight => 22;

    /// <summary>The size of a title bar button.</summary>
    public static Size CaptionButtonSize => new(36, 22);

    /// <summary>The height of a menu bar.</summary>
    public static int MenuHeight => 20;

    /// <summary>The size of a flat border.</summary>
    public static Size BorderSize => new(1, 1);

    /// <summary>The size of a three-dimensional border.</summary>
    public static Size Border3DSize => new(2, 2);

    /// <summary>The size of a window's sizing border.</summary>
    public static Size FrameBorderSize => new(4, 4);

    /// <summary>The size of a fixed window's border.</summary>
    public static Size FixedFrameBorderSize => new(3, 3);

    /// <summary>The width of a window's border.</summary>
    public static int BorderMultiplierFactor => 1;

    /// <summary>The smallest size of a window.</summary>
    public static Size MinimumWindowSize => new(136, 39);

    /// <summary>The largest size of a window.</summary>
    public static Size MaxWindowTrackSize => new(PrimaryMonitorSize.Width + 12, PrimaryMonitorSize.Height + 12);

    /// <summary>The size of an icon.</summary>
    public static Size IconSize => new(32, 32);

    /// <summary>The size of a small icon.</summary>
    public static Size SmallIconSize => new(16, 16);

    /// <summary>The size of a cursor.</summary>
    public static Size CursorSize => new(32, 32);

    /// <summary>The longest time between the clicks of a double click, in milliseconds.</summary>
    public static int DoubleClickTime => 500;

    /// <summary>How far the pointer may move between the clicks of a double click.</summary>
    public static Size DoubleClickSize => new(4, 4);

    /// <summary>How far the pointer moves before a drag starts.</summary>
    public static Size DragSize => new(4, 4);

    /// <summary>How long the pointer rests before a hover.</summary>
    public static int MouseHoverTime => 400;

    /// <summary>How far the pointer may move during a hover.</summary>
    public static Size MouseHoverSize => new(4, 4);

    /// <summary>The lines a wheel notch scrolls.</summary>
    public static int MouseWheelScrollLines => 3;

    /// <summary>The wheel delta of a notch.</summary>
    public static int MouseWheelScrollDelta => 120;

    /// <summary>Whether a mouse is present. A finger counts as one.</summary>
    public static bool MousePresent => true;

    /// <summary>Whether a mouse wheel is present.</summary>
    public static bool MouseWheelPresent => true;

    /// <summary>The number of mouse buttons.</summary>
    public static int MouseButtons => 3;

    /// <summary>Whether the mouse buttons are swapped.</summary>
    public static bool MouseButtonsSwapped => false;

    /// <summary>The delay before a held key repeats.</summary>
    public static int KeyboardDelay => 1;

    /// <summary>How fast a held key repeats.</summary>
    public static int KeyboardSpeed => 31;

    /// <summary>The width of the caret.</summary>
    public static int CaretWidth => 1;

    /// <summary>How fast the caret blinks, in milliseconds.</summary>
    public static int CaretBlinkTime => 530;

    /// <summary>The name of the device.</summary>
    public static string ComputerName => Environment.MachineName;

    /// <summary>The name of the user.</summary>
    public static string UserName => Environment.UserName;

    /// <summary>The domain of the user.</summary>
    public static string UserDomainName => Environment.UserDomainName;

    /// <summary>Whether the app runs in a remote session.</summary>
    public static bool TerminalServerSession => false;

    /// <summary>Whether high contrast is on.</summary>
    public static bool HighContrast => false;

    /// <summary>Whether windows have drop shadows.</summary>
    public static bool IsDropShadowEnabled => true;

    /// <summary>Whether menus fade.</summary>
    public static bool IsMenuFadeEnabled => false;

    /// <summary>Whether the user interface uses right-to-left layout.</summary>
    public static bool MidEastEnabled => false;

    /// <summary>Whether a network is present.</summary>
    public static bool Network => true;

    /// <summary>Whether Windows started normally.</summary>
    public static BootMode BootMode => BootMode.Normal;

    /// <summary>Whether the screen is a tablet's.</summary>
    public static bool PenWindows => true;

    /// <summary>Whether the visual styles are on.</summary>
    public static bool UIEffectsEnabled => true;
}

/// <summary>How Windows started.</summary>
public enum BootMode
{
    /// <summary>Normally.</summary>
    Normal = 0,

    /// <summary>In safe mode.</summary>
    FailSafe = 1,

    /// <summary>In safe mode with networking.</summary>
    FailSafeWithNetwork = 2,
}
