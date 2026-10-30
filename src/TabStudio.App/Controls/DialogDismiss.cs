using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace TabStudio.App.Controls;

/// <summary>
/// What closes a dialog drawn over the IDE (docs/design/README.md, "Dialogs"): Cancel, Escape, or a tap on the scrim
/// beside it. Escape is taken from the window, before the control with the focus, so it works wherever the focus
/// was left, behind the scrim.
/// </summary>
public static class DialogDismiss
{
    /// <summary>Makes Escape and a tap on the scrim close a dialog while it is open.</summary>
    /// <param name="view">The view that draws the dialog.</param>
    /// <param name="scrim">The scrim: the dialog's full-size background, whose own taps close it.</param>
    /// <param name="close">Closes the dialog. Called only while the scrim is visible.</param>
    public static void Attach(Control view, Panel scrim, Action close)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(scrim);
        ArgumentNullException.ThrowIfNull(close);

        TopLevel? window = null;
        void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape && !e.Handled && scrim.IsEffectivelyVisible)
            {
                close();
                e.Handled = true;
            }
        }

        view.AttachedToVisualTree += (_, _) =>
        {
            window = TopLevel.GetTopLevel(view);
            window?.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        };
        view.DetachedFromVisualTree += (_, _) =>
        {
            window?.RemoveHandler(InputElement.KeyDownEvent, OnKeyDown);
            window = null;
        };

        // A tap on the scrim itself, not on the dialog: its controls handle their own presses.
        scrim.PointerPressed += (_, e) =>
        {
            if (ReferenceEquals(e.Source, scrim) && scrim.IsEffectivelyVisible)
            {
                close();
                e.Handled = true;
            }
        };
    }
}
