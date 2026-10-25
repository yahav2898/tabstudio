using System.Drawing;
using System.Globalization;

namespace System.Windows.Forms;

/// <summary>
/// Mouse and keyboard input. The window routes input to controls through the internal Dispatch methods, which
/// call the protected On* methods that controls override, as Windows Forms' window procedure does.
/// </summary>
public partial class Control
{
    /// <summary>Whether the control receives all mouse input until the button or finger is released.</summary>
    public bool Capture
    {
        get => Window?.CapturedControl == this;
        set
        {
            if (Window is { } window)
            {
                window.SetCapture(value ? this : null);
            }
        }
    }

    /// <summary>The visible child at a point of the client area, or <see langword="null"/>.</summary>
    /// <param name="pt">The point.</param>
    public Control? GetChildAtPoint(Point pt)
    {
        foreach (var child in _controls.ToArray())
        {
            if (child.Visible && child.Bounds.Contains(pt))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>The visible child at a point of the client area, or <see langword="null"/>.</summary>
    /// <param name="pt">The point.</param>
    /// <param name="skipValue">Which children to skip; invisible ones are always skipped.</param>
    public Control? GetChildAtPoint(Point pt, GetChildAtPointSkip skipValue)
    {
        foreach (var child in _controls.ToArray())
        {
            if ((skipValue & GetChildAtPointSkip.Disabled) != 0 && !child.Enabled)
            {
                continue;
            }

            if ((skipValue & GetChildAtPointSkip.Transparent) != 0 && child.BackColor.A == 0)
            {
                continue;
            }

            if (child.Visible && child.Bounds.Contains(pt))
            {
                return child;
            }
        }

        return null;
    }

    /// <summary>Whether a key is one the control handles itself rather than one the form uses to move focus.</summary>
    /// <param name="keyData">The key with modifiers.</param>
    protected virtual bool IsInputKey(Keys keyData) => false;

    /// <summary>Whether a character is one the control handles itself.</summary>
    /// <param name="charCode">The character.</param>
    protected virtual bool IsInputChar(char charCode) => true;

    /// <summary>
    /// Processes a command key, such as a menu shortcut, before the focused control sees it. The parent gets it next;
    /// a form looks through its main menu's shortcuts.
    /// </summary>
    /// <param name="msg">The key's message.</param>
    /// <param name="keyData">The key with modifiers.</param>
    /// <returns>Whether the key was handled.</returns>
    protected virtual bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_contextMenuStrip is { } menu && menu.ProcessShortcut(keyData))
        {
            return true;
        }

        return _parent?.ProcessCmdKey(ref msg, keyData) ?? false;
    }

    /// <summary>
    /// Processes a dialog key, such as Tab, the arrows, Enter or Esc, which the focused control does not handle itself.
    /// The parent gets it next; a form moves focus or presses its accept or cancel button.
    /// </summary>
    /// <param name="keyData">The key with modifiers.</param>
    /// <returns>Whether the key was handled.</returns>
    protected virtual bool ProcessDialogKey(Keys keyData) => _parent?.ProcessDialogKey(keyData) ?? false;

    /// <summary>Processes a character the control does not handle itself; the parent gets it next.</summary>
    /// <param name="charCode">The character.</param>
    /// <returns>Whether it was handled.</returns>
    protected virtual bool ProcessDialogChar(char charCode) => _parent?.ProcessDialogChar(charCode) ?? false;

    /// <summary>
    /// Whether a character is the mnemonic of a text: the character after a single ampersand, as <c>N</c> is in
    /// <c>&amp;No</c>, matched without regard to case.
    /// </summary>
    /// <param name="charCode">The character.</param>
    /// <param name="text">The text.</param>
    public static bool IsMnemonic(char charCode, string? text)
    {
        if (charCode == '&' || string.IsNullOrEmpty(text))
        {
            return false;
        }

        for (var i = 0; i < text.Length - 1; i++)
        {
            if (text[i] != '&')
            {
                continue;
            }

            if (text[i + 1] == '&')
            {
                i++;
                continue;
            }

            return char.ToUpper(text[i + 1], CultureInfo.CurrentCulture) == char.ToUpper(charCode, CultureInfo.CurrentCulture);
        }

        return false;
    }

    /// <summary>
    /// Processes a mnemonic: a control whose text has it acts, as a button clicks or a label moves focus to the
    /// control after it. Typed with Alt, or alone where the focused control does not take characters.
    /// </summary>
    /// <param name="charCode">The character.</param>
    /// <returns>Whether a control took it.</returns>
    protected internal virtual bool ProcessMnemonic(char charCode) => false;

    /// <summary>Lets a container see a key before the focused control, as a form with KeyPreview does.</summary>
    /// <param name="m">The key's message.</param>
    /// <returns>Whether it was handled.</returns>
    protected virtual bool ProcessKeyPreview(ref Message m) => _parent?.ProcessKeyPreview(ref m) ?? false;

    internal void DispatchMouseDown(MouseEventArgs e) => OnMouseDown(e);

    internal void DispatchMouseMove(MouseEventArgs e) => OnMouseMove(e);

    internal void DispatchMouseUp(MouseEventArgs e) => OnMouseUp(e);

    internal void DispatchMouseWheel(MouseEventArgs e) => OnMouseWheel(e);

    internal void DispatchMouseEnter() => OnMouseEnter(EventArgs.Empty);

    internal void DispatchMouseLeave() => OnMouseLeave(EventArgs.Empty);

    internal void DispatchMouseHover() => OnMouseHover(EventArgs.Empty);

    internal void DispatchMouseCaptureChanged() => OnMouseCaptureChanged(EventArgs.Empty);

    internal void DispatchKeyDown(KeyEventArgs e) => OnKeyDown(e);

    internal void DispatchKeyUp(KeyEventArgs e) => OnKeyUp(e);

    internal void DispatchKeyPress(KeyPressEventArgs e) => OnKeyPress(e);

    internal void DispatchPreviewKeyDown(PreviewKeyDownEventArgs e) => OnPreviewKeyDown(e);

    internal bool DispatchIsInputKey(Keys keyData) => IsInputKey(keyData);

    internal bool DispatchProcessCmdKey(Keys keyData)
    {
        var message = Message.Create(Handle, Message.KeyDownMessage, (IntPtr)(int)(keyData & Keys.KeyCode), IntPtr.Zero);
        return ProcessCmdKey(ref message, keyData);
    }

    internal bool DispatchProcessDialogKey(Keys keyData) => ProcessDialogKey(keyData);

    internal bool DispatchIsInputChar(char charCode) => IsInputChar(charCode);

    internal bool DispatchProcessDialogChar(char charCode) => ProcessDialogChar(charCode);

    /// <summary>Whether the control can take a mnemonic: it and its parents are enabled and visible.</summary>
    internal bool CanProcessMnemonic()
    {
        for (Control? control = this; control is not null; control = control._parent)
        {
            if (!control.Enabled || !control.VisibleSelf)
            {
                return false;
            }
        }

        return true;
    }

    internal bool DispatchProcessKeyPreview(Keys keyData)
    {
        var message = Message.Create(Handle, Message.KeyDownMessage, (IntPtr)(int)(keyData & Keys.KeyCode), IntPtr.Zero);
        return ProcessKeyPreview(ref message);
    }

    /// <summary>
    /// Called after a button or finger is released over the control it went down on: raises Click and MouseClick, or
    /// DoubleClick and MouseDoubleClick for the second click of a double click, for controls with the standard click
    /// styles.
    /// </summary>
    /// <param name="e">The release's data, with the click count.</param>
    internal virtual void DispatchClick(MouseEventArgs e)
    {
        if (e.Clicks >= 2 && GetStyle(ControlStyles.StandardDoubleClick))
        {
            OnDoubleClick(e);
            OnMouseDoubleClick(e);
        }
        else if (GetStyle(ControlStyles.StandardClick))
        {
            OnClick(e);
            OnMouseClick(e);
        }
    }

    /// <summary>
    /// Called when a finger goes down on the client area of a control that scrolls its content by dragging: lists,
    /// grids, scrolling panels. Such controls return whether a drag should scroll rather than select.
    /// </summary>
    internal virtual bool ScrollsByTouch => false;

    /// <summary>Scrolls the content by a drag of a finger; controls that scroll by touch override it.</summary>
    /// <param name="dx">The horizontal distance moved since the last call.</param>
    /// <param name="dy">The vertical distance moved since the last call.</param>
    internal virtual void ScrollByTouch(int dx, int dy)
    {
    }

    /// <summary>
    /// Whether a point of the client area belongs to the control rather than to the child under it, such as the area
    /// around a split container's splitter, which is wider than the splitter for a finger.
    /// </summary>
    /// <param name="point">The point, in client coordinates.</param>
    internal virtual bool KeepsPointFromChildren(Point point) => false;

    /// <summary>Handles a press on the non-client area: a border or a scroll bar.</summary>
    /// <param name="point">The point in the control's bounds coordinates.</param>
    /// <returns>Whether the press was handled.</returns>
    internal virtual bool NonClientMouseDown(Point point) => false;

    /// <summary>Handles a move during a press that began on the non-client area.</summary>
    /// <param name="point">The point in the control's bounds coordinates.</param>
    internal virtual void NonClientMouseMove(Point point)
    {
    }

    /// <summary>Handles the release of a press that began on the non-client area.</summary>
    /// <param name="point">The point in the control's bounds coordinates.</param>
    internal virtual void NonClientMouseUp(Point point)
    {
    }
}

/// <summary>Which children <see cref="Control.GetChildAtPoint(Point, GetChildAtPointSkip)"/> skips.</summary>
[Flags]
public enum GetChildAtPointSkip
{
    /// <summary>No children are skipped.</summary>
    None = 0,

    /// <summary>Invisible children are skipped.</summary>
    Invisible = 1,

    /// <summary>Disabled children are skipped.</summary>
    Disabled = 2,

    /// <summary>Transparent children are skipped.</summary>
    Transparent = 4,
}
