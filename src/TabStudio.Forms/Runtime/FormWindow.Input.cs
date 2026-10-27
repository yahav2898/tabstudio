using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// Input routing, as Windows routes mouse and keyboard messages to a form's controls. A finger is the left button;
/// on content that scrolls by dragging, such as a list, a finger's press is held back until it is clear whether it
/// taps or drags.
/// </summary>
internal sealed partial class FormWindow
{
    /// <summary>How far a finger moves before a press becomes a drag that scrolls.</summary>
    private const int TouchSlop = 8;

    private Control? _pressed;
    private bool _pressedNonClient;
    private int _pressClicks;
    private MouseButtons _pressButton;
    private Popup? _pressedPopup;
    private PendingPress? _pendingTouch;
    private Control? _touchScroller;
    private Point _lastPoint;

    /// <summary>A button or finger went down.</summary>
    /// <param name="point">Where, in the client area.</param>
    /// <param name="button">The button; a finger is the left button.</param>
    /// <param name="clicks">1, or 2 for the second press of a double click.</param>
    /// <param name="touch">Whether it is a finger.</param>
    public void PointerPressed(Point point, MouseButtons button, int clicks, bool touch)
    {
        if (FilterPointer(point, button switch { MouseButtons.Right => clicks > 1 ? 0x0206 : 0x0204, MouseButtons.Middle => clicks > 1 ? 0x0209 : 0x0207, _ => clicks > 1 ? 0x0203 : 0x0201 }, 0))
        {
            return;
        }

        Runtime.MouseButtons |= button;
        _lastPoint = point;
        UpdateCursorPosition(point);
        if (touch)
        {
            ShowFocusCues = false;
        }

        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            var popup = _popups[i];
            if (popup.Bounds.Contains(point))
            {
                _pressedPopup = popup;
                popup.PointerDown(Local(popup, point));
                return;
            }
        }

        if (_popups.Count > 0)
        {
            // A press outside the drop-downs closes them, and does nothing else, as in Windows.
            if (!_popups[0].PointerDownOutside(point))
            {
                CloseAllPopups();
            }

            return;
        }

        var target = HitTest(point, out var nonClient);
        ActivateMdiChildren(target);
        if (!target.Enabled)
        {
            return;
        }

        if (!touch)
        {
            UpdateHover(target);
        }

        if (nonClient && target.NonClientMouseDown(ToBounds(target, point)))
        {
            _pressed = target;
            _pressedNonClient = true;
            SetCapture(target);
            return;
        }

        if (touch && FindTouchScroller(target) is { } scroller)
        {
            _pendingTouch = new PendingPress(target, point, button, clicks);
            _touchScroller = scroller;
            return;
        }

        DeliverPress(target, point, button, clicks);
    }

    /// <summary>A press in an MDI child brings it, and any MDI child it is in, to the front.</summary>
    private static void ActivateMdiChildren(Control target)
    {
        for (Control? control = target; control is not null; control = control.Parent)
        {
            if (control is Form { MdiParent.MdiClient: { } client } child)
            {
                client.Activate(child);
            }
        }
    }

    /// <summary>The pointer or a finger moved.</summary>
    /// <param name="point">Where, in the client area.</param>
    /// <param name="touch">Whether it is a finger.</param>
    public void PointerMoved(Point point, bool touch)
    {
        if (FilterPointer(point, 0x0200, 0))
        {
            return;
        }

        var previous = _lastPoint;
        _lastPoint = point;
        UpdateCursorPosition(point);
        if (_pressedPopup is { } pressedPopup)
        {
            pressedPopup.PointerMove(Local(pressedPopup, point), pressed: true);
            return;
        }

        if (_pendingTouch is { } pending && _touchScroller is not null)
        {
            if (Math.Abs(point.X - pending.Point.X) <= TouchSlop && Math.Abs(point.Y - pending.Point.Y) <= TouchSlop)
            {
                return;
            }

            // The finger drags: the content scrolls, and the press never reaches the control.
            _pendingTouch = null;
            previous = pending.Point;
        }

        if (_touchScroller is { } scroller && _pendingTouch is null && (Runtime.MouseButtons & MouseButtons.Left) != 0 && _pressed is null)
        {
            scroller.ScrollByTouch(previous.X - point.X, previous.Y - point.Y);
            return;
        }

        if (_pressed is { } pressed)
        {
            if (_pressedNonClient)
            {
                pressed.NonClientMouseMove(ToBounds(pressed, point));
            }
            else
            {
                var local = ToClient(pressed, point);
                pressed.DispatchMouseMove(new MouseEventArgs(Runtime.MouseButtons, 0, local.X, local.Y, 0));
            }

            return;
        }

        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            if (_popups[i].Bounds.Contains(point))
            {
                _popups[i].PointerMove(Local(_popups[i], point), pressed: false);
                return;
            }
        }

        if (_popups.Count > 0)
        {
            if (!touch)
            {
                _popups[0].PointerMoveOutside(point);
            }

            return;
        }

        if (touch)
        {
            return;
        }

        var target = HitTest(point, out _);
        UpdateHover(target);
        var at = ToClient(target, point);
        if (target.Enabled)
        {
            target.DispatchMouseMove(new MouseEventArgs(MouseButtons.None, 0, at.X, at.Y, 0));
        }
    }

    /// <summary>A button or finger was released.</summary>
    /// <param name="point">Where, in the client area.</param>
    /// <param name="button">The button; a finger is the left button.</param>
    /// <param name="touch">Whether it is a finger.</param>
    public void PointerReleased(Point point, MouseButtons button, bool touch)
    {
        if (FilterPointer(point, button switch { MouseButtons.Right => 0x0205, MouseButtons.Middle => 0x0208, _ => 0x0202 }, 0))
        {
            Runtime.MouseButtons &= ~button;
            return;
        }

        Runtime.MouseButtons &= ~button;
        _lastPoint = point;
        if (_pressedPopup is { } popup)
        {
            _pressedPopup = null;
            popup.PointerUp(Local(popup, point));
            return;
        }

        if (_pendingTouch is { } pending)
        {
            // A tap: the held-back press happens now, then the release.
            _pendingTouch = null;
            _touchScroller = null;
            DeliverPress(pending.Target, pending.Point, pending.Button, pending.Clicks);
        }
        else if (_touchScroller is not null)
        {
            _touchScroller = null;
            return;
        }

        if (_pressed is { } pressed)
        {
            _pressed = null;
            if (_pressedNonClient)
            {
                _pressedNonClient = false;
                pressed.NonClientMouseUp(ToBounds(pressed, point));
                SetCapture(null);
                return;
            }

            var local = ToClient(pressed, point);
            var args = new MouseEventArgs(_pressButton, _pressClicks, local.X, local.Y, 0);
            pressed.DispatchMouseUp(args);
            SetCapture(null);
            var inside = pressed.BoundsInWindow().Contains(point);
            if (inside && pressed.Enabled && pressed.Window == this)
            {
                pressed.DispatchClick(args);
                if (_pressButton == MouseButtons.Right)
                {
                    ShowContextMenu(pressed, point);
                }
            }
        }

        if (touch)
        {
            UpdateHover(null);
        }
    }

    /// <summary>The mouse wheel turned.</summary>
    /// <param name="point">Where the pointer is, in the client area.</param>
    /// <param name="delta">How far, in multiples of 120 per notch; positive away from the user.</param>
    public void PointerWheel(Point point, int delta)
    {
        if (FilterPointer(point, 0x020A, delta << 16))
        {
            return;
        }

        for (var i = _popups.Count - 1; i >= 0; i--)
        {
            if (_popups[i].Bounds.Contains(point))
            {
                _popups[i].Wheel(delta);
                return;
            }
        }

        for (Control? control = HitTest(point, out _); control is not null; control = control.Parent)
        {
            if (!control.Enabled)
            {
                continue;
            }

            var local = ToClient(control, point);
            var args = new HandledMouseEventArgs(MouseButtons.None, 0, local.X, local.Y, delta);
            control.DispatchMouseWheel(args);
            if (args.Handled)
            {
                return;
            }
        }
    }

    /// <summary>A long press asks for the context menu of the control under the finger.</summary>
    /// <param name="point">Where, in the client area.</param>
    public void ContextRequested(Point point)
    {
        _pendingTouch = null;
        _touchScroller = null;
        _pressedPopup = null;
        if (_pressed is { } pressed)
        {
            // The finger that asked for the menu does not click what it pressed when it lifts.
            _pressed = null;
            _pressedNonClient = false;
            Runtime.MouseButtons = MouseButtons.None;
            var local = ToClient(pressed, point);
            pressed.DispatchMouseUp(new MouseEventArgs(_pressButton, 0, local.X, local.Y, 0));
            SetCapture(null);
        }

        if (_popups.Count > 0)
        {
            return;
        }

        var target = HitTest(point, out _);
        if (target.Enabled)
        {
            ShowContextMenu(target, point);
        }
    }

    /// <summary>A key went down.</summary>
    /// <param name="keyData">The key with modifiers.</param>
    /// <returns>Whether the app handled it, which keeps it from a native text box.</returns>
    public bool KeyDown(Keys keyData)
    {
        if (FilterKey((keyData & Keys.Alt) != 0 ? 0x0104 : 0x0100, (int)(keyData & Keys.KeyCode)))
        {
            return true;
        }

        Runtime.ModifierKeys = keyData & Keys.Modifiers;
        var code = keyData & Keys.KeyCode;
        if (code is Keys.Tab or Keys.Left or Keys.Right or Keys.Up or Keys.Down)
        {
            ShowFocusCues = true;
            Invalidate();
        }

        if (_popups.Count > 0 && _popups[^1].KeyDown(keyData))
        {
            return true;
        }

        var target = FocusedControl ?? Form;
        var preview = new PreviewKeyDownEventArgs(keyData);
        target.DispatchPreviewKeyDown(preview);
        if (target.DispatchProcessCmdKey(keyData))
        {
            return true;
        }

        var isInput = preview.IsInputKey || target.DispatchIsInputKey(keyData);
        if (!isInput && target.DispatchProcessDialogKey(keyData))
        {
            return true;
        }

        // Alt with a letter or digit is a mnemonic, as Windows sends it with WM_SYSCHAR.
        if ((keyData & (Keys.Alt | Keys.Control)) == Keys.Alt && code is >= Keys.A and <= Keys.Z or >= Keys.D0 and <= Keys.D9
            && target.DispatchProcessDialogChar((char)code))
        {
            return true;
        }

        var e = new KeyEventArgs(keyData);
        if (target != Form && Form.KeyPreview)
        {
            Form.DispatchKeyDown(e);
        }

        if (!e.Handled)
        {
            (FocusedControl ?? Form).DispatchKeyDown(e);
        }

        _suppressNextChar = e.SuppressKeyPress;
        if (e.Handled)
        {
            return true;
        }

        // Windows types a character for these keys, which KeyPress handlers often look for.
        var character = code switch
        {
            Keys.Enter => '\r',
            Keys.Back => '\b',
            Keys.Escape => (char)27,
            Keys.Tab when isInput => '\t',
            _ => '\0',
        };
        return character != '\0' && (keyData & (Keys.Control | Keys.Alt)) == 0 && TextInput(new string(character, 1)) is null;
    }

    /// <summary>A key went up.</summary>
    /// <param name="keyData">The key with modifiers.</param>
    public void KeyUp(Keys keyData)
    {
        if (FilterKey((keyData & Keys.Alt) != 0 ? 0x0105 : 0x0101, (int)(keyData & Keys.KeyCode)))
        {
            return;
        }

        Runtime.ModifierKeys = keyData & Keys.Modifiers;
        var e = new KeyEventArgs(keyData);
        if (FocusedControl is { } focused && focused != Form && Form.KeyPreview)
        {
            Form.DispatchKeyUp(e);
        }

        if (!e.Handled)
        {
            (FocusedControl ?? Form).DispatchKeyUp(e);
        }
    }

    /// <summary>Characters were typed: KeyPress for each.</summary>
    /// <param name="text">The characters.</param>
    /// <returns>The characters the app let through, changed as its handlers changed them, or <see langword="null"/> for none.</returns>
    public string? TextInput(string text)
    {
        if (_suppressNextChar)
        {
            _suppressNextChar = false;
            return null;
        }

        var target = FocusedControl ?? Form;
        var accepted = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (FilterKey(0x0102, character))
            {
                continue;
            }

            // A character the focused control does not take is a mnemonic first, as Windows' dialog manager has it.
            if (!target.DispatchIsInputChar(character) && target.DispatchProcessDialogChar(character))
            {
                continue;
            }

            var e = new KeyPressEventArgs(character);
            if (target != Form && Form.KeyPreview)
            {
                Form.DispatchKeyPress(e);
            }

            if (!e.Handled)
            {
                (FocusedControl ?? Form).DispatchKeyPress(e);
            }

            if (!e.Handled)
            {
                accepted.Append(e.KeyChar);
            }
        }

        return accepted.Length == 0 ? null : accepted.ToString();
    }

    /// <summary>The native text box's text changed as the user typed.</summary>
    /// <param name="controlId">The edited control's identifier.</param>
    /// <param name="text">The text.</param>
    /// <param name="selectionStart">The selection's start.</param>
    /// <param name="selectionLength">The selection's length.</param>
    /// <param name="revision">The text revision the native box had.</param>
    public void EditorTextChanged(int controlId, string text, int selectionStart, int selectionLength, int revision)
    {
        if (FocusedControl is INativeTextEditor editor && FocusedControl.Handle == controlId)
        {
            editor.ApplyEditorText(text, selectionStart, selectionLength, revision);
        }
    }

    /// <summary>The native text box's selection changed.</summary>
    /// <param name="controlId">The edited control's identifier.</param>
    /// <param name="selectionStart">The selection's start.</param>
    /// <param name="selectionLength">The selection's length.</param>
    public void EditorSelectionChanged(int controlId, int selectionStart, int selectionLength)
    {
        if (FocusedControl is INativeTextEditor editor && FocusedControl.Handle == controlId)
        {
            editor.ApplyEditorSelection(selectionStart, selectionLength);
        }
    }

    /// <summary>The pointer left the window: nothing is hovered any more.</summary>
    public void PointerLeft()
    {
        if (_pressed is null && _pressedPopup is null)
        {
            UpdateHover(null);
        }
    }

    /// <summary>The user dragged an edge or corner of the window.</summary>
    /// <param name="client">The client area the drag asks for, on the desktop.</param>
    /// <param name="keepRight">Whether the right edge stays put, as when the left edge is dragged.</param>
    /// <param name="keepBottom">Whether the bottom edge stays put, as when the top edge is dragged.</param>
    public void ResizedTo(Rectangle client, bool keepRight, bool keepBottom)
    {
        Form.SetClientSizeFromWindow(client.Size);
        var size = Form.ClientSize;
        var origin = Form.ClientOriginOnScreen;
        var x = keepRight ? client.Right - size.Width : client.X;
        var y = keepBottom ? client.Bottom - size.Height : client.Y;
        if (x != origin.X || y != origin.Y)
        {
            Form.MoveFromWindow(x - origin.X, y - origin.Y);
        }
    }

    /// <summary>The user dragged the window by its title bar.</summary>
    /// <param name="dx">How far it moved horizontally.</param>
    /// <param name="dy">How far it moved vertically.</param>
    public void MovedBy(int dx, int dy) => Form.MoveFromWindow(dx, dy);

    /// <summary>The user resized the window.</summary>
    /// <param name="clientSize">The new size of the client area.</param>
    public void Resized(Size clientSize) => Form.SetClientSizeFromWindow(clientSize);

    /// <summary>The user asked to close the window.</summary>
    public void CloseRequested() => Form.CloseFromWindow(CloseReason.UserClosing);

    /// <summary>The user minimized the window.</summary>
    public void Minimize() => Form.WindowState = FormWindowState.Minimized;

    /// <summary>The user restored the minimized window, from its button at the bottom of the desktop.</summary>
    public void Restore()
    {
        if (Form.WindowState == FormWindowState.Minimized)
        {
            Form.WindowState = FormWindowState.Normal;
        }

        Runtime.Activate(this);
    }

    /// <summary>The user maximized or restored the window.</summary>
    public void ToggleMaximize() =>
        Form.WindowState = Form.WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    /// <summary>A point in a control's client coordinates.</summary>
    private static Point ToClient(Control control, Point point)
    {
        var origin = control.ClientOriginInWindow();
        return new Point(point.X - origin.X, point.Y - origin.Y);
    }

    /// <summary>A point in a control's bounds coordinates.</summary>
    private static Point ToBounds(Control control, Point point)
    {
        var bounds = control.BoundsInWindow();
        return new Point(point.X - bounds.X, point.Y - bounds.Y);
    }

    private static Point Local(Popup popup, Point point) => new(point.X - popup.Bounds.X, point.Y - popup.Bounds.Y);

    private static Control? FindTouchScroller(Control target)
    {
        for (Control? control = target; control is not null; control = control.Parent)
        {
            if (control.ScrollsByTouch)
            {
                return control;
            }
        }

        return null;
    }

    private void DeliverPress(Control target, Point point, MouseButtons button, int clicks)
    {
        _pressed = target;
        _pressedNonClient = false;
        _pressClicks = clicks;
        _pressButton = button;
        if (target.CanSelect)
        {
            SetFocus(target);
        }

        SetCapture(target);
        var local = ToClient(target, point);
        target.DispatchMouseDown(new MouseEventArgs(button, clicks, local.X, local.Y, 0));
    }

    private void UpdateHover(Control? target)
    {
        if (_hovered == target)
        {
            return;
        }

        var previous = _hovered;
        _hovered = target;
        HideToolTip();
        if (previous?.Window == this)
        {
            previous.DispatchMouseLeave();
        }

        if (target is not null)
        {
            target.DispatchMouseEnter();
            ScheduleToolTip(target);
        }
    }

    private void UpdateCursorPosition(Point point)
    {
        var origin = Form.ClientOriginOnScreen;
        Cursor.Position = new Point(origin.X + point.X, origin.Y + point.Y);
    }

    private void ShowContextMenu(Control control, Point point)
    {
        for (Control? owner = control; owner is not null; owner = owner.Parent)
        {
            if (owner.ContextMenuStrip is { } menu)
            {
                menu.ShowAt(owner, this, point);
                return;
            }
        }

        // A text box without a menu of its own has Windows' editing menu.
        if (control is TextBoxBase { ShortcutsEnabled: true } box)
        {
            TextEditMenu.Show(box, this, point);
        }
    }

    /// <summary>A finger's press held back until it is clear whether it taps or scrolls.</summary>
    private sealed record PendingPress(Control Target, Point Point, MouseButtons Button, int Clicks);

    /// <summary>
    /// Shows a mouse message to the app's message filters, as Windows' message loop does before dispatching it.
    /// </summary>
    /// <returns>Whether a filter stopped it.</returns>
    private bool FilterPointer(Point point, int message, int wParam)
    {
        if (Runtime.MessageFilters.Count == 0)
        {
            return false;
        }

        var target = HitTest(point, out _) ?? Form;
        var origin = target.ClientOriginInWindow();
        var (x, y) = (point.X - origin.X, point.Y - origin.Y);
        var buttons = Runtime.MouseButtons;
        var flags = ((buttons & MouseButtons.Left) != 0 ? 0x1 : 0) | ((buttons & MouseButtons.Right) != 0 ? 0x2 : 0) | ((buttons & MouseButtons.Middle) != 0 ? 0x10 : 0);
        return Filter(target, message, wParam | flags, (y << 16) | (x & 0xFFFF));
    }

    /// <summary>Shows a keyboard message to the app's message filters.</summary>
    /// <returns>Whether a filter stopped it.</returns>
    private bool FilterKey(int message, int wParam) =>
        Runtime.MessageFilters.Count > 0 && Filter(FocusedControl ?? Form, message, wParam, 1);

    private bool Filter(Control target, int message, int wParam, int lParam)
    {
        var m = Message.Create(target.Handle, message, wParam, lParam);
        foreach (var filter in Runtime.MessageFilters.ToArray())
        {
            if (filter.PreFilterMessage(ref m))
            {
                return true;
            }
        }

        return false;
    }
}
