using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using TabStudio.Forms.Runtime;
using DrawingPoint = System.Drawing.Point;
using DrawingRectangle = System.Drawing.Rectangle;

namespace TabStudio.Forms.Hosting;

/// <summary>The native text box over the text field that has focus in the active window.</summary>
internal sealed partial class FormsDesktop
{
    private void InitializeEditor()
    {
        _editor.Edited += (_, edit) =>
            PostTo(edit.WindowId, window => window.EditorTextChanged(edit.ControlId, edit.Text, edit.SelectionStart, edit.SelectionLength, edit.Revision));
        _editor.SelectionMoved += (_, edit) =>
            PostTo(edit.WindowId, window => window.EditorSelectionChanged(edit.ControlId, edit.SelectionStart, edit.SelectionLength));

        // A right-click or a long press in the field opens its Windows Forms menu: its own, or Windows' editing menu.
        _editor.ContextRequested += (_, e) =>
        {
            e.Handled = true;
            if (_editor.WindowId == 0 || !_views.TryGetValue(_editor.WindowId, out var view))
            {
                return;
            }

            var point = e.TryGetPosition(_canvas, out var position)
                ? new DrawingPoint((int)position.X, (int)position.Y)
                : new DrawingPoint(_editor.DesktopBounds.X + 4, _editor.DesktopBounds.Bottom);
            var client = ToClient(view.Frame, point);
            PostTo(view.Frame.Id, window => window.ContextRequested(client));
        };
    }

    /// <summary>
    /// Shows the native text box over the active window's field with focus, or hides it. While the desktop has the
    /// keyboard, the box has it whenever it shows, so typing goes to the field.
    /// </summary>
    private void UpdateEditor()
    {
        var active = ViewsFromTop().Select(v => v.Frame).FirstOrDefault(f => f.IsActive && !f.IsMinimized && !f.IsBlocked);
        var hadKeyboard = IsKeyboardFocusWithin;
        if (active?.Editor is { } field)
        {
            var bounds = new DrawingRectangle(active.Client.X + field.Bounds.X, active.Client.Y + field.Bounds.Y, field.Bounds.Width, field.Bounds.Height);
            var moved = bounds != _editor.DesktopBounds;
            _editor.Show(active.Id, field, bounds);

            // A menu or drop-down open above the field takes the pointer, not the box under it.
            _editor.IsHitTestVisible = active.Popups.Count == 0;
            if (moved)
            {
                _canvas.InvalidateArrange();
            }

            if (hadKeyboard && !_editor.IsFocused)
            {
                _editor.Focus(NavigationMethod.Pointer);
            }
        }
        else if (_editor.IsVisible || _editor.Field is not null)
        {
            var hadEditorFocus = _editor.IsKeyboardFocusWithin;
            _editor.Hide();
            if (hadEditorFocus)
            {
                Focus();
            }
        }
    }

    /// <summary>Whether a routed event came from the native text box, which handles it itself.</summary>
    private bool IsFromEditor(object? source)
    {
        for (var element = source as Avalonia.Visual; element is not null; element = element.GetVisualParent())
        {
            if (element == _editor)
            {
                return true;
            }

            if (element == this)
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>Queues work for one of the app's windows on its UI thread; nothing happens if it closed meanwhile.</summary>
    private void PostTo(int windowId, Action<FormWindow> action)
    {
        if (Runtime is { } runtime)
        {
            runtime.Post(() =>
            {
                if (runtime.FindWindow(windowId) is { } window)
                {
                    action(window);
                }
            });
        }
    }

    /// <summary>A point on the desktop in a window's client coordinates.</summary>
    private static DrawingPoint ToClient(WindowFrame frame, DrawingPoint point) => new(point.X - frame.Client.X, point.Y - frame.Client.Y);
}
