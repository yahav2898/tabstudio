using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TabStudio.App.Input;

/// <summary>
/// Whether the user is working by touch or with a hardware keyboard and mouse. On a touch screen, giving the editor
/// the keyboard focus opens the on-screen keyboard over half the screen, so the IDE only does it on its own when a
/// keyboard is in use; a tap in the editor still focuses it.
/// </summary>
public static class InputModality
{
    /// <summary>
    /// Marks a view that is used with a keyboard whatever the last input was, such as the IDE on an external display
    /// in desktop mode: the on-screen keyboard belongs to the tablet, so focusing an editor there never opens it.
    /// Inherited by everything inside the view.
    /// </summary>
    public static readonly AttachedProperty<bool> PrefersKeyboardProperty =
        AvaloniaProperty.RegisterAttached<InputElement, bool>("PrefersKeyboard", typeof(InputModality), inherits: true);

    /// <summary>Whether the last input came from a finger or a pen. Android starts out touch-first.</summary>
    public static bool IsTouch { get; private set; } = OperatingSystem.IsAndroid();

    /// <summary>Gets whether a view is used with a keyboard whatever the last input was.</summary>
    /// <param name="element">The view.</param>
    public static bool GetPrefersKeyboard(InputElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(PrefersKeyboardProperty);
    }

    /// <summary>Sets whether a view is used with a keyboard whatever the last input was.</summary>
    /// <param name="element">The view.</param>
    /// <param name="value">Whether it is.</param>
    public static void SetPrefersKeyboard(InputElement element, bool value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(PrefersKeyboardProperty, value);
    }

    /// <summary>Follows the input of a window or view.</summary>
    /// <param name="root">The top-level element.</param>
    public static void Track(InputElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        root.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>Stops following a window or view.</summary>
    /// <param name="root">The top-level element.</param>
    public static void Untrack(InputElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
        root.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
    }

    /// <summary>Gives an element the keyboard focus, unless the user works by touch and the element is not in a view that prefers the keyboard.</summary>
    /// <param name="element">The element, such as an editor's text area.</param>
    public static void FocusUnlessTouch(InputElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (!IsTouch || element.GetValue(PrefersKeyboardProperty))
        {
            element.Focus();
        }
    }

    // Only Android opens an on-screen keyboard when an editor takes the focus, so elsewhere this stays false.
    private static void OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        IsTouch = OperatingSystem.IsAndroid() && (e.Pointer.Type is PointerType.Touch or PointerType.Pen);

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // On-screen keyboards type text rather than keys, apart from a few such as Enter and Backspace; letters,
        // digits, function keys and shortcuts come from a hardware keyboard.
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift)
            || e.Key is (>= Key.A and <= Key.Z) or (>= Key.D0 and <= Key.D9) or (>= Key.F1 and <= Key.F24)
                or Key.Up or Key.Down or Key.Left or Key.Right or Key.Tab)
        {
            IsTouch = false;
        }
    }
}
