using System.Collections.ObjectModel;
using Avalonia;

namespace TabStudio.App.Swarm;

/// <summary>Where an <see cref="IAppScreen"/> shows the app being built.</summary>
public enum AppScreenKind
{
    /// <summary>The tablet's own app preview, beside the code.</summary>
    Preview,

    /// <summary>A simulated device the tablet draws itself: a frame of a given size, theme, direction and font scale.</summary>
    Virtual,

    /// <summary>Another phone or tablet in the swarm, running TabStudio and paired through TabStudio Link.</summary>
    Device,
}

/// <summary>The kind of device a screen stands for, which decides its frame and the layout checks that apply.</summary>
public enum FormFactor
{
    /// <summary>A phone, up to about 400 dips wide.</summary>
    Phone,

    /// <summary>A large phone or a foldable, about 400 to 600 dips wide.</summary>
    Phablet,

    /// <summary>A tablet, 600 dips wide or more.</summary>
    Tablet,

    /// <summary>A desktop window or an external display.</summary>
    Desktop,
}

/// <summary>Whether a screen lays text out from the left or from the right.</summary>
public enum TextDirection
{
    /// <summary>Left to right, as English.</summary>
    LeftToRight,

    /// <summary>Right to left, as Hebrew and Arabic.</summary>
    RightToLeft,
}

/// <summary>How a screen looks to the app: its size, density, theme, text direction and font scale.</summary>
/// <param name="Name">What people call it, such as "Small phone" or "Pixel 9".</param>
/// <param name="FormFactor">The kind of device.</param>
/// <param name="Width">The app's width, in device-independent pixels.</param>
/// <param name="Height">The app's height, in device-independent pixels.</param>
/// <param name="Scale">Physical pixels per device-independent pixel.</param>
/// <param name="IsDark">Whether the app is shown in its dark theme.</param>
/// <param name="Direction">The text direction.</param>
/// <param name="FontScale">The system font scale, 1 for the default size.</param>
public sealed record ScreenProfile(
    string Name,
    FormFactor FormFactor,
    double Width,
    double Height,
    double Scale = 1,
    bool IsDark = false,
    TextDirection Direction = TextDirection.LeftToRight,
    double FontScale = 1);

/// <summary>What a person did on a screen, in terms that replay on a screen of another size.</summary>
public enum AppInputKind
{
    /// <summary>A tap or click on an element.</summary>
    Tap,

    /// <summary>A long press, or a right click.</summary>
    LongPress,

    /// <summary>A scroll of the element under the pointer.</summary>
    Scroll,

    /// <summary>Text typed into the focused element.</summary>
    Text,

    /// <summary>A key with modifiers, such as Enter or Ctrl+A, on the focused element.</summary>
    Key,
}

/// <summary>
/// One input on a screen, aimed at an element rather than at pixels, so the same input lands on the same control on
/// every screen: a tap on a phone replays on a tablet, and a recorded test replays anywhere.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="ElementPath">
/// The element it was aimed at, from the app's root: names where elements have them, otherwise the type and the index
/// among siblings, such as <c>MainView/StackPanel[0]/Button#CountButton</c>. Empty for keys and text, which go to the
/// focused element.
/// </param>
/// <param name="Position">Where inside the element, as fractions of its width and height, from 0 to 1.</param>
/// <param name="Text">The text typed, for <see cref="AppInputKind.Text"/>.</param>
/// <param name="Key">The key, for <see cref="AppInputKind.Key"/>, as Avalonia names it, such as <c>Enter</c>.</param>
/// <param name="Modifiers">The modifiers held, as Avalonia names them, such as <c>Control, Shift</c>.</param>
/// <param name="ScrollDelta">How far it scrolled, in device-independent pixels, for <see cref="AppInputKind.Scroll"/>.</param>
public sealed record AppInput(
    AppInputKind Kind,
    string ElementPath,
    Point Position = default,
    string? Text = null,
    string? Key = null,
    string? Modifiers = null,
    Vector ScrollDelta = default);

/// <summary>The kinds of layout problem the layout checks find.</summary>
public enum LayoutIssueKind
{
    /// <summary>Text that is cut off or trimmed because it does not fit.</summary>
    ClippedText,

    /// <summary>An element that lies partly or wholly outside the screen or its scrollable area.</summary>
    OffScreen,

    /// <summary>Interactive elements that overlap, so one hides or steals the taps of the other.</summary>
    Overlap,

    /// <summary>An interactive element smaller than a finger can hit reliably.</summary>
    SmallTouchTarget,

    /// <summary>Text whose contrast with its background is too low to read comfortably.</summary>
    LowContrast,
}

/// <summary>How serious a layout issue is.</summary>
public enum LayoutIssueSeverity
{
    /// <summary>Worth fixing; shown as a warning.</summary>
    Warning,

    /// <summary>Breaks the screen for someone; shown as an error.</summary>
    Error,
}

/// <summary>One layout problem found on one screen.</summary>
/// <param name="Kind">The kind of problem.</param>
/// <param name="Severity">How serious it is.</param>
/// <param name="Message">What is wrong and why it matters, in plain words.</param>
/// <param name="ElementPath">The element, in <see cref="AppInput.ElementPath"/>'s form.</param>
/// <param name="Bounds">Where the element is on the screen, in device-independent pixels.</param>
public sealed record LayoutIssue(
    LayoutIssueKind Kind,
    LayoutIssueSeverity Severity,
    string Message,
    string ElementPath,
    Rect Bounds);

/// <summary>Finds layout problems in an app's visual tree. Runs wherever the app runs, on the tablet or on a device.</summary>
public interface ILayoutInspector
{
    /// <summary>Inspects the app shown under a root element.</summary>
    /// <param name="root">The app's root element on its screen.</param>
    /// <param name="profile">The screen it is shown on.</param>
    /// <returns>The problems found, most serious first.</returns>
    IReadOnlyList<LayoutIssue> Inspect(Visual root, ScreenProfile profile);
}

/// <summary>
/// A place the app being built runs: the tablet's app preview, a virtual device the tablet draws, or a phone in the
/// swarm. Every Swarm feature works through this interface, so mirroring input, layout checks, screenshots, recorded
/// tests and the device matrix treat every screen the same way.
/// </summary>
public interface IAppScreen
{
    /// <summary>A stable id, unique among the screens.</summary>
    string Id { get; }

    /// <summary>Where it is.</summary>
    AppScreenKind Kind { get; }

    /// <summary>How it looks to the app.</summary>
    ScreenProfile Profile { get; }

    /// <summary>Whether the app is running on it now.</summary>
    bool IsRunning { get; }

    /// <summary>Raised when <see cref="IsRunning"/>, <see cref="Profile"/> or what the app shows changed.</summary>
    event EventHandler? Changed;

    /// <summary>Raised for every input a person makes on this screen, for mirroring and recording.</summary>
    event EventHandler<AppInput>? InputCaptured;

    /// <summary>Replays an input on this screen, as if a person made it.</summary>
    /// <param name="input">The input.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the element was found and the input delivered.</returns>
    Task<bool> SendInputAsync(AppInput input, CancellationToken cancellationToken);

    /// <summary>Takes a picture of what the app shows.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A PNG image, or <see langword="null"/> while the app is not running.</returns>
    Task<byte[]?> CaptureAsync(CancellationToken cancellationToken);

    /// <summary>Runs the layout checks on what the app shows.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The problems found, or none while the app is not running.</returns>
    Task<IReadOnlyList<LayoutIssue>> InspectLayoutAsync(CancellationToken cancellationToken);
}

/// <summary>Every screen the app being built runs on, as one list the Swarm features watch. One per app.</summary>
public sealed class AppScreenRegistry
{
    /// <summary>The screens, in the order they were added.</summary>
    public ObservableCollection<IAppScreen> Screens { get; } = [];

    /// <summary>Adds a screen, unless one with its id is already there.</summary>
    /// <param name="screen">The screen.</param>
    public void Add(IAppScreen screen)
    {
        ArgumentNullException.ThrowIfNull(screen);
        if (Screens.All(s => s.Id != screen.Id))
        {
            Screens.Add(screen);
        }
    }

    /// <summary>Removes a screen.</summary>
    /// <param name="screen">The screen.</param>
    public void Remove(IAppScreen screen) => Screens.Remove(screen);
}
