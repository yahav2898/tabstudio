using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TabStudio.App.Swarm;

/// <summary>
/// The input of one screen: what a person does on it, captured as <see cref="AppInput"/>s, and inputs replayed on it
/// the way a person makes them. Captured inputs name their element by its <see cref="ElementPaths">path</see>, so
/// they replay on a screen of any size.
/// </summary>
/// <remarks>
/// <para>
/// The screen is two controls: its <em>surface</em>, everything the screen shows, and the app's <em>root</em> view
/// inside it, where element paths start. Handlers on the surface see every input on its way out of the app, handled
/// or not, and never mark a person's input handled, so capturing changes nothing the app receives.
/// </para>
/// <para>
/// A replayed input stops at the surface: it reaches the app as a person's would, but not the IDE around the screen,
/// and it is not captured again, so mirrored input does not echo between screens.
/// </para>
/// </remarks>
internal sealed partial class ScreenInput : IDisposable
{
    private readonly Control _surface;
    private readonly Control _root;
    private WeakReference<IInputElement>? _focused;
    private bool _disposed;

    /// <summary>Starts following a screen's input.</summary>
    /// <param name="surface">Everything the screen shows.</param>
    /// <param name="root">The app's root view, inside the surface.</param>
    public ScreenInput(Control surface, Control root)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(root);
        _surface = surface;
        _root = root;
        _scrollSettle.Tick += OnScrollSettled;
        _surface.AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
        AttachCapture();
    }

    /// <summary>Raised for every input a person makes on the screen. Raised on the UI thread.</summary>
    public event EventHandler<AppInput>? Captured;

    /// <summary>The app's root view, where element paths start.</summary>
    public Control Root => _root;

    /// <summary>
    /// The element of this screen that last had the keyboard focus, where replayed text and keys go. Each screen keeps
    /// its own, so typing mirrored to several screens reaches the same field on each.
    /// </summary>
    public IInputElement? FocusedElement =>
        _focused is not null && _focused.TryGetTarget(out var element) && element is Visual { IsAttachedToVisualTree: true } visual && IsInApp(visual)
            ? element
            : null;

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _scrollSettle.Stop();
        _surface.RemoveHandler(InputElement.GotFocusEvent, OnGotFocus);
        DetachCapture();
    }

    /// <summary>Whether an element is the app's root view or inside it.</summary>
    private bool IsInApp(Visual element) => ReferenceEquals(element, _root) || _root.IsVisualAncestorOf(element);

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (e.Source is IInputElement element and Visual visual && IsInApp(visual))
        {
            _focused = new WeakReference<IInputElement>(element);
        }
    }
}
