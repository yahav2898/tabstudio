using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Rendering;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// A shown form: what Windows gives a form's window in Windows Forms. It routes input to the form's controls, keeps
/// focus, capture and the pointer's hover, holds the open popups, and draws the form into frames. UI thread only.
/// </summary>
internal sealed partial class FormWindow : IDisposable
{
    private readonly List<Popup> _popups = [];
    private Control? _hovered;
    private bool _suppressNextChar;

    /// <summary>Creates the window of a form.</summary>
    /// <param name="runtime">The app's runtime.</param>
    /// <param name="id">The window's identifier.</param>
    /// <param name="form">The form.</param>
    /// <param name="owner">The form that owns it.</param>
    /// <param name="modal">Whether it is a modal dialog.</param>
    public FormWindow(FormsRuntime runtime, int id, Form form, Form? owner, bool modal)
    {
        Runtime = runtime;
        Id = id;
        Form = form;
        Owner = owner;
        IsModal = modal;
    }

    /// <summary>The app's runtime.</summary>
    public FormsRuntime Runtime { get; }

    /// <summary>The window's identifier.</summary>
    public int Id { get; }

    /// <summary>The form shown in the window.</summary>
    public Form Form { get; }

    /// <summary>The form that owns this one, if any.</summary>
    public Form? Owner { get; }

    /// <summary>Whether the window is a modal dialog.</summary>
    public bool IsModal { get; }

    /// <summary>Whether the window must be drawn again.</summary>
    public bool IsInvalid { get; private set; } = true;

    /// <summary>The control with keyboard focus.</summary>
    public Control? FocusedControl { get; private set; }

    /// <summary>The control that receives the pointer's input until it is released.</summary>
    public Control? CapturedControl { get; private set; }

    /// <summary>Whether focus rectangles are shown, which they are once the keyboard has been used.</summary>
    public bool ShowFocusCues { get; private set; }

    /// <summary>The open popups, the topmost last.</summary>
    public IReadOnlyList<Popup> Popups => _popups;

    /// <summary>Asks for the window to be drawn again.</summary>
    public void Invalidate()
    {
        IsInvalid = true;
        Runtime.RequestPaint();
    }

    /// <summary>Draws the window now.</summary>
    public void PaintNow()
    {
        if (Runtime.IsOnThread)
        {
            Runtime.PaintNow();
        }
    }

    /// <summary>Draws the window as it looks now.</summary>
    public WindowFrame Render()
    {
        IsInvalid = false;
        var builder = new DisplayListBuilder();
        var client = Form.ClientSize;
        Form.PaintClient(builder, new Rectangle(Point.Empty, client), Point.Empty);
        var popups = _popups.Select(popup => new PopupFrame(popup.Bounds, popup.Render())).ToList();
        EditorFrame? editor = null;
        if (FocusedControl is INativeTextEditor native && FocusedControl.Visible)
        {
            editor = native.GetEditorFrame(FocusedControl.ClientOriginInWindow());
        }

        var top = Runtime.TopModal();
        var blocked = top is not null && top != this && Owner != top.Form;
        return new WindowFrame(
            Id,
            Form.Text,
            Form.VisibleFrameOnScreen,
            new Rectangle(Form.ClientOriginOnScreen, client),
            Form.Chrome,
            Form.WindowState == FormWindowState.Maximized,
            IsModal,
            Runtime.ActiveWindow == this,
            blocked,
            builder.Build(),
            popups,
            editor,
            RenderToolTip());
    }

    /// <summary>Gives a control keyboard focus, with the Leave, Validating, Enter, LostFocus and GotFocus events.</summary>
    /// <param name="control">The control, or <see langword="null"/> to clear focus.</param>
    public void SetFocus(Control? control)
    {
        var previous = FocusedControl;
        if (previous == control)
        {
            return;
        }

        if (previous is not null)
        {
            previous.DispatchLeave();
            if (control?.CausesValidation != false && previous.CausesValidation && !previous.DispatchValidation())
            {
                // A Validating handler cancelled: focus stays.
                Invalidate();
                return;
            }
        }

        if (control is not null && control.FindForm() is { } form)
        {
            form.SetActiveControlFromWindow(control);
        }

        control?.DispatchEnter();
        FocusedControl = control;
        previous?.DispatchLostFocus();
        control?.DispatchGotFocus();
        Invalidate();
    }

    /// <summary>Sets or releases the control that receives the pointer's input.</summary>
    /// <param name="control">The control, or <see langword="null"/>.</param>
    public void SetCapture(Control? control)
    {
        if (CapturedControl == control)
        {
            return;
        }

        var previous = CapturedControl;
        CapturedControl = control;
        previous?.DispatchMouseCaptureChanged();
    }

    /// <summary>Forgets a control that is leaving the window: focus, capture and hover move off it.</summary>
    /// <param name="control">The control.</param>
    public void ForgetControl(Control control)
    {
        if (Contains(control, FocusedControl))
        {
            FocusedControl = null;
            Form.SelectNextControl(null, true, true, true, false);
        }

        if (Contains(control, CapturedControl))
        {
            CapturedControl = null;
        }

        if (Contains(control, _hovered))
        {
            _hovered = null;
        }

        foreach (var popup in _popups.Where(p => Contains(control, p.Owner)).ToList())
        {
            ClosePopup(popup);
        }
    }

    /// <summary>Moves focus off a control that was disabled or hidden.</summary>
    /// <param name="control">The control.</param>
    public void OnControlDisabledOrHidden(Control control)
    {
        if (FocusedControl is { } focused && Contains(control, focused))
        {
            var form = Form;
            FocusedControl = null;
            focused.DispatchLostFocus();
            form.SelectNextControl(focused, true, true, true, true);
        }

        foreach (var popup in _popups.Where(p => Contains(control, p.Owner)).ToList())
        {
            ClosePopup(popup);
        }
    }

    /// <summary>Opens a popup above the window.</summary>
    /// <param name="popup">The popup.</param>
    public void OpenPopup(Popup popup)
    {
        popup.Window = this;
        _popups.Add(popup);
        Invalidate();
    }

    /// <summary>Closes a popup.</summary>
    /// <param name="popup">The popup.</param>
    public void ClosePopup(Popup popup)
    {
        if (_popups.Remove(popup))
        {
            popup.OnClosed();
            popup.Window = null;
            Invalidate();
        }
    }

    /// <summary>Closes every popup.</summary>
    public void CloseAllPopups()
    {
        foreach (var popup in _popups.ToList())
        {
            ClosePopup(popup);
        }
    }

    /// <summary>Called when the window becomes the active one.</summary>
    public void OnActivated()
    {
        Form.DispatchActivated();
        Invalidate();
    }

    /// <summary>Called when another window becomes the active one.</summary>
    public void OnDeactivated()
    {
        CloseAllPopups();
        Form.DispatchDeactivate();
        Invalidate();
    }

    /// <summary>Called when the app is idle, to show a tooltip for the control under the pointer.</summary>
    public void OnIdle() => UpdateToolTip();

    /// <summary>The visible, enabled control at a point of the client area, and whether the point is on its border or scroll bars.</summary>
    /// <param name="point">The point in the window's client coordinates.</param>
    /// <param name="nonClient">Whether the point is on the control's non-client area.</param>
    public Control HitTest(Point point, out bool nonClient)
    {
        nonClient = false;
        Control current = Form;
        var origin = Point.Empty;
        while (true)
        {
            Control? hit = null;
            foreach (var child in current.Controls.ToArray())
            {
                if (child.VisibleSelf && new Rectangle(origin.X + child.Left, origin.Y + child.Top, child.Width, child.Height).Contains(point))
                {
                    hit = child;
                    break;
                }
            }

            if (hit is null)
            {
                return current;
            }

            var insets = hit.NonClientInsets;
            var clientOrigin = new Point(origin.X + hit.Left + insets.Left, origin.Y + hit.Top + insets.Top);
            if (!new Rectangle(clientOrigin, hit.ClientSize).Contains(point))
            {
                nonClient = true;
                return hit;
            }

            current = hit;
            origin = clientOrigin;
        }
    }

    /// <summary>Stops the window's timers, once it has closed.</summary>
    public void Dispose()
    {
        _toolTipTimer?.Dispose();
        _toolTipTimer = null;
    }

    private static bool Contains(Control container, Control? control) =>
        control is not null && (control == container || container.Contains(control));
}
