using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TabStudio.App.Controls;

/// <summary>
/// A long press on a button names it (docs/design/README.md: "A long press does what a right click or a hover does").
/// Avalonia opens tooltips only for a mouse that moves, so by touch an icon-only button's name never showed, and
/// lifting the finger after the long press ran the button: a long press on a notebook's trash icon, to learn what it
/// was, deleted the cell. Here a long press on any button with a tooltip and no menu of its own shows the tooltip,
/// and the release that follows does not click. <see cref="AdaptiveBarItem"/> and the activity bar do this themselves.
/// </summary>
public static class TouchToolTips
{
    private static readonly AttachedProperty<bool> IsHeldOpenProperty =
        AvaloniaProperty.RegisterAttached<Button, bool>("IsHeldOpen", typeof(TouchToolTips));

    private static bool _started;

    /// <summary>Starts naming buttons on a long press. Call once, on the UI thread, when the app starts.</summary>
    public static void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        Gestures.HoldingEvent.AddClassHandler<Button>(OnHolding);
        InputElement.PointerReleasedEvent.AddClassHandler<Button>(OnReleased, RoutingStrategies.Tunnel);
        InputElement.PointerCaptureLostEvent.AddClassHandler<Button>((button, _) => Close(button));
    }

    /// <summary>Whether a long press on a button shows its name: a tooltip, and no menu that the long press opens instead.</summary>
    /// <param name="button">The button.</param>
    internal static bool Names(Button button) =>
        button is not AdaptiveBarItem
        && ToolTip.GetTip(button) is not null
        && button.ContextMenu is null
        && button.ContextFlyout is null;

    private static void OnHolding(Button button, HoldingRoutedEventArgs e)
    {
        if (e.HoldingState == HoldingState.Started && e.PointerType is PointerType.Touch or PointerType.Pen && !e.Handled && Names(button))
        {
            button.SetValue(IsHeldOpenProperty, true);
            ToolTip.SetIsOpen(button, true);
            e.Handled = true;
        }
    }

    private static void OnReleased(Button button, PointerReleasedEventArgs e)
    {
        if (!button.GetValue(IsHeldOpenProperty))
        {
            return;
        }

        // Releasing the capture ends the press without a click; the tooltip closes with it.
        e.Handled = true;
        e.Pointer.Capture(null);
        Close(button);
    }

    private static void Close(Button button)
    {
        if (button.GetValue(IsHeldOpenProperty))
        {
            button.SetValue(IsHeldOpenProperty, false);
            ToolTip.SetIsOpen(button, false);
        }
    }
}
