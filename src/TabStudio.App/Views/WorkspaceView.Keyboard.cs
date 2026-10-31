using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace TabStudio.App.Views;

/// <summary>
/// Moving between the shell's panes with the keyboard (docs/design/README.md, "Keyboard"). Tab moves between the
/// controls of a pane, but the editor keeps Tab for indenting, so F6 moves the focus to the next pane (the menu bar,
/// the toolbar, the side bar, the editor, the bottom panel, the status bar) and Shift+F6 to the previous one, as in
/// Visual Studio's and the browsers' F6. Nothing traps the keyboard.
/// </summary>
public sealed partial class WorkspaceView
{
    /// <summary>The panes in reading order.</summary>
    private Control[] Panes => [MenuBar, Toolbar, SideBarFrame, EditorPanel, BottomDock, StatusBar];

    /// <summary>Starts handling F6. Called by the constructor.</summary>
    private void InitializeKeyboard() => AddHandler(KeyDownEvent, OnPaneKeyDown, RoutingStrategies.Tunnel);

    /// <summary>Moves the keyboard focus to the next or the previous pane that shows; for tests and the key gesture.</summary>
    /// <param name="forward">Whether to go to the next pane.</param>
    /// <returns>Whether the focus moved.</returns>
    internal bool MoveToPane(bool forward)
    {
        var panes = Panes;
        var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Visual;
        var current = focused is null ? -1 : Array.FindIndex(panes, pane => ReferenceEquals(pane, focused) || pane.IsVisualAncestorOf(focused));
        for (var step = 1; step <= panes.Length; step++)
        {
            var index = ((current < 0 ? (forward ? -1 : 0) : current) + (forward ? step : -step) + (panes.Length * 2)) % panes.Length;
            if (FirstStop(panes[index]) is { } target)
            {
                return target.Focus(NavigationMethod.Tab);
            }
        }

        return false;
    }

    private void OnPaneKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F6 && (e.KeyModifiers & ~KeyModifiers.Shift) == KeyModifiers.None && MoveToPane(forward: (e.KeyModifiers & KeyModifiers.Shift) == 0))
        {
            e.Handled = true;
        }
    }

    /// <summary>The first control in a pane that the keyboard can reach, the editor's text in the editor's pane.</summary>
    private static InputElement? FirstStop(Control pane)
    {
        if (!pane.IsEffectivelyVisible || pane.Bounds.Width < 1 || pane.Bounds.Height < 1)
        {
            return null;
        }

        return pane.GetVisualDescendants().OfType<InputElement>().FirstOrDefault(element =>
            element.Focusable && element.IsEffectivelyVisible && element.IsEffectivelyEnabled && KeyboardNavigation.GetIsTabStop(element)
            && element is not Avalonia.Controls.Primitives.ScrollBar && element is not Avalonia.Controls.Primitives.Thumb
            && element is not ScrollViewer);
    }
}
