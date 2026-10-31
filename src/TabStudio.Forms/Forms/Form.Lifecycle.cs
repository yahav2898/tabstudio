using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>Showing, activating and closing the form, and its window's events.</summary>
public partial class Form
{
    /// <summary>The space kept around windows that open centered.</summary>
    private const int DesktopMargin = 12;

    /// <summary>How far each new window opens from the last.</summary>
    private const int Cascade = 32;

    /// <summary>Shows the form as a window owned by another form, which it stays above.</summary>
    /// <param name="owner">The owner.</param>
    public void Show(IWin32Window? owner)
    {
        if (owner is Control control && control.FindForm() is { } form && form != this)
        {
            Owner = form;
        }

        Show();
    }

    /// <summary>Shows the form as a modal dialog and waits until it closes.</summary>
    /// <returns>The dialog's result: the button that closed it, or Cancel when its close button did.</returns>
    public DialogResult ShowDialog() => ShowDialog(null);

    /// <summary>Shows the form as a modal dialog over an owner and waits until it closes.</summary>
    /// <param name="owner">The owner, or <see langword="null"/> for the active form.</param>
    /// <returns>The dialog's result: the button that closed it, or Cancel when its close button did.</returns>
    public DialogResult ShowDialog(IWin32Window? owner)
    {
        var runtime = RequireRuntime(nameof(ShowDialog));
        if (WindowInternal is not null)
        {
            throw new InvalidOperationException("Form that is already visible cannot be displayed as a modal dialog box. Set the form's visible property to false before calling showDialog.");
        }

        if (!_topLevel)
        {
            throw new InvalidOperationException("Form that is not a top-level form cannot be displayed as a modal dialog box. Remove the form from any parent form before calling showDialog.");
        }

        var ownerForm = (owner as Control)?.FindForm() ?? ActiveForm;
        _dialogResult = DialogResult.None;
        _modal = true;
        try
        {
            ShowWindow(modal: true, ownerForm == this ? null : ownerForm);
            runtime.RunLoop(() => WindowInternal is null);
            runtime.ThrowIfCrashed();
        }
        finally
        {
            _modal = false;
            if (WindowInternal is not null)
            {
                CloseWindowCore();
            }
        }

        return _dialogResult == DialogResult.None ? DialogResult.Cancel : _dialogResult;
    }

    /// <summary>Closes the form, raising FormClosing, which can cancel it, and FormClosed.</summary>
    public void Close()
    {
        if (WindowInternal is not null)
        {
            CloseFromWindow(CloseReason.UserClosing);
        }
        else if (IsMdiChild && !IsDisposed)
        {
            CloseMdiChild(CloseReason.UserClosing);
        }
        else if (!_topLevel && Parent is not null && !IsDisposed && !_closing)
        {
            // A form shown inside a panel closes as a window does: FormClosing, FormClosed, then it is disposed.
            _closing = true;
            try
            {
                var closing = new FormClosingEventArgs(CloseReason.UserClosing, cancel: false);
                OnFormClosing(closing);
                if (closing.Cancel)
                {
                    return;
                }

                OnFormClosed(new FormClosedEventArgs(CloseReason.UserClosing));
            }
            finally
            {
                _closing = false;
            }

            Dispose();
        }
    }

    /// <summary>Brings the form to the front and makes it the active window.</summary>
    public void Activate()
    {
        if (WindowInternal is { } window)
        {
            window.Runtime.Activate(window);
        }
        else if (_mdiParent is { } parent)
        {
            parent.Activate();
            parent.MdiClient?.Activate(this);
        }
    }

    /// <summary>Moves the form to a point of the desktop.</summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    public void SetDesktopLocation(int x, int y) => Location = new Point(x, y);

    /// <summary>Moves and sizes the form on the desktop.</summary>
    /// <param name="x">The left.</param>
    /// <param name="y">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public void SetDesktopBounds(int x, int y, int width, int height) => Bounds = new Rectangle(x, y, width, height);

    /// <summary>Adds a form this form owns.</summary>
    /// <param name="ownedForm">The form.</param>
    public void AddOwnedForm(Form? ownedForm)
    {
        if (ownedForm is not null)
        {
            ownedForm.Owner = this;
        }
    }

    /// <summary>Removes a form this form owns.</summary>
    /// <param name="ownedForm">The form.</param>
    public void RemoveOwnedForm(Form? ownedForm)
    {
        if (ownedForm?.Owner == this)
        {
            ownedForm.Owner = null;
        }
    }

    /// <summary>Centers the form on the desktop.</summary>
    protected void CenterToScreen()
    {
        var desktop = WindowInternal?.Runtime.DesktopSize ?? FormsRuntime.Current?.DesktopSize ?? Size.Empty;
        Location = new Point(Math.Max(DesktopMargin, (desktop.Width - Width) / 2), Math.Max(DesktopMargin, (desktop.Height - Height) / 2));
    }

    /// <summary>Centers the form over its owner.</summary>
    protected void CenterToParent()
    {
        if (_owner is { } owner)
        {
            Location = new Point(owner.Left + ((owner.Width - Width) / 2), Math.Max(DesktopMargin, owner.Top + ((owner.Height - Height) / 2)));
        }
        else
        {
            CenterToScreen();
        }
    }

    /// <summary>Raises the Load event, before the form is shown for the first time.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnLoad(EventArgs e) => ((EventHandler?)Events[EventLoad])?.Invoke(this, e);

    /// <summary>Raises the Shown event, after the form is shown for the first time.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnShown(EventArgs e) => ((EventHandler?)Events[EventShown])?.Invoke(this, e);

    /// <summary>Raises the Activated event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnActivated(EventArgs e) => ((EventHandler?)Events[EventActivated])?.Invoke(this, e);

    /// <summary>Raises the Deactivate event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDeactivate(EventArgs e) => ((EventHandler?)Events[EventDeactivate])?.Invoke(this, e);

    /// <summary>Raises the FormClosing event.</summary>
    /// <param name="e">The event data; setting Cancel keeps the form open.</param>
    protected virtual void OnFormClosing(FormClosingEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var closing = new CancelEventArgs(e.Cancel);
        OnClosing(closing);
        e.Cancel = closing.Cancel;
        ((FormClosingEventHandler?)Events[EventFormClosing])?.Invoke(this, e);
    }

    /// <summary>Raises the Closing event, which comes before FormClosing.</summary>
    /// <param name="e">The event data; setting Cancel keeps the form open.</param>
    protected virtual void OnClosing(CancelEventArgs e) => ((CancelEventHandler?)Events[EventClosing])?.Invoke(this, e);

    /// <summary>Raises the Closed event, which comes after FormClosed.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnClosed(EventArgs e) => ((EventHandler?)Events[EventClosed])?.Invoke(this, e);

    /// <summary>Raises the FormClosed event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnFormClosed(FormClosedEventArgs e)
    {
        ((FormClosedEventHandler?)Events[EventFormClosed])?.Invoke(this, e);
        OnClosed(e);
    }

    /// <summary>Raises the ResizeBegin event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnResizeBegin(EventArgs e) => ((EventHandler?)Events[EventResizeBegin])?.Invoke(this, e);

    /// <summary>Raises the ResizeEnd event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnResizeEnd(EventArgs e) => ((EventHandler?)Events[EventResizeEnd])?.Invoke(this, e);

    /// <summary>Shows or hides the form's window.</summary>
    /// <param name="value">Whether it is shown.</param>
    protected override void SetVisibleCore(bool value)
    {
        if (!_topLevel)
        {
            SetChildVisibleCore(value);
            return;
        }

        if (value)
        {
            ShowWindow(modal: false, _owner);
        }
        else if (WindowInternal is not null)
        {
            CloseWindowCore();
        }
    }

    /// <summary>Presses the accept button on Enter and the cancel button on Esc.</summary>
    /// <param name="keyData">The key.</param>
    protected override bool ProcessDialogKey(Keys keyData)
    {
        if ((keyData & (Keys.Alt | Keys.Control)) == 0)
        {
            var code = keyData & Keys.KeyCode;
            if (code == Keys.Enter && _acceptButton is { } accept && Window?.FocusedControl is not IButtonControl)
            {
                accept.PerformClick();
                return true;
            }

            if (code == Keys.Escape && _cancelButton is { } cancel)
            {
                cancel.PerformClick();
                return true;
            }
        }

        return base.ProcessDialogKey(keyData);
    }

    /// <summary>Looks through the main menu's shortcuts.</summary>
    /// <param name="msg">The key's message.</param>
    /// <param name="keyData">The key.</param>
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (MainMenuStrip?.ProcessShortcut(keyData) == true)
        {
            return true;
        }

        foreach (var strip in Controls.ToArray().OfType<ToolStrip>())
        {
            if (strip != MainMenuStrip && strip.ProcessShortcut(keyData))
            {
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (WindowInternal is not null)
            {
                CloseWindowCore();
            }

            Owner = null;
        }

        base.Dispose(disposing);
    }

    internal void DispatchActivated() => OnActivated(EventArgs.Empty);

    internal void DispatchDeactivate() => OnDeactivate(EventArgs.Empty);

    /// <summary>Moves the window by a distance, as dragging its title bar does.</summary>
    /// <param name="dx">The horizontal distance.</param>
    /// <param name="dy">The vertical distance.</param>
    internal void MoveFromWindow(int dx, int dy)
    {
        if (_windowState != FormWindowState.Maximized)
        {
            Location = new Point(Left + dx, Math.Max(0, Top + dy));
        }
    }

    /// <summary>Resizes the client area, as dragging the window's size grip does.</summary>
    /// <param name="clientSize">The new client size.</param>
    internal void SetClientSizeFromWindow(Size clientSize)
    {
        if (_windowState != FormWindowState.Maximized && Chrome.IsSizable)
        {
            OnResizeBegin(EventArgs.Empty);
            ClientSize = new Size(Math.Max(clientSize.Width, 40), Math.Max(clientSize.Height, 20));
            OnResizeEnd(EventArgs.Empty);
        }
    }

    /// <summary>Closes the window for a reason: FormClosing, which can cancel, then FormClosed.</summary>
    /// <param name="reason">Why it closes.</param>
    internal void CloseFromWindow(CloseReason reason)
    {
        if (WindowInternal is null || _closing)
        {
            return;
        }

        _closing = true;
        try
        {
            foreach (var owned in _ownedForms.ToList())
            {
                owned.CloseFromWindow(CloseReason.FormOwnerClosing);
            }

            // MDI children are asked first; one that cancels cancels the parent's closing, which the parent sees.
            var children = MdiChildren.Where(child => child.VisibleSelf).ToList();
            var childCancelled = false;
            foreach (var child in children)
            {
                var childClosing = new FormClosingEventArgs(CloseReason.MdiFormClosing, cancel: false);
                child.OnFormClosing(childClosing);
                childCancelled |= childClosing.Cancel;
            }

            var closing = new FormClosingEventArgs(reason, childCancelled);
            OnFormClosing(closing);
            if (closing.Cancel)
            {
                if (_modal)
                {
                    _dialogResult = DialogResult.None;
                }

                return;
            }

            if (_modal && _dialogResult == DialogResult.None)
            {
                _dialogResult = DialogResult.Cancel;
            }

            foreach (var child in children)
            {
                child.OnFormClosed(new FormClosedEventArgs(CloseReason.MdiFormClosing));
            }

            CloseWindowCore();
            OnFormClosed(new FormClosedEventArgs(reason));
        }
        finally
        {
            _closing = false;
        }

        if (!_modal)
        {
            Dispose();
        }
    }

    /// <summary>Closes the window without events, as the end of a run does.</summary>
    internal void CloseForRuntimeEnd()
    {
        if (WindowInternal is not null)
        {
            CloseWindowCore();
        }
    }

    private static FormsRuntime RequireRuntime(string member) =>
        FormsRuntime.Current ?? throw new InvalidOperationException(
            $"Form.{member} needs the app's UI thread, which Application.Run starts. Run the project with F5 in TabStudio, and show forms from its UI thread.");

    /// <summary>Loads the form the first time, places it and opens its window.</summary>
    private void ShowWindow(bool modal, Form? owner)
    {
        var runtime = RequireRuntime("Show");
        if (WindowInternal is { } existing)
        {
            runtime.Activate(existing);
            return;
        }

        if (owner is not null && owner != this)
        {
            Owner = owner;
        }

        if (!_loaded)
        {
            _loaded = true;
            CreateControl();
            PerformAutoScale();
            PerformLayout();
            Place(runtime);
            var placed = Bounds;
            OnLoad(EventArgs.Empty);
            if (IsDisposed)
            {
                return;
            }

            // A Load handler that resizes a centered form without moving it gets it centered at its new size.
            if (Location == placed.Location && Size != placed.Size && _centeredAt == placed.Location)
            {
                Place(runtime);
            }
        }

        WindowInternal = runtime.OpenWindow(this, _owner, modal);
        if (_windowState == FormWindowState.Maximized)
        {
            ApplyMaximized();
        }

        PerformLayout();
        OnVisibleChanged(EventArgs.Empty);
        if (ActiveControl is { CanSelect: true } active)
        {
            WindowInternal.SetFocus(active);
        }
        else
        {
            SelectNextControl(null, true, true, true, false);
        }

        var shownWindow = WindowInternal;
        runtime.Post(() =>
        {
            if (WindowInternal == shownWindow)
            {
                OnShown(EventArgs.Empty);
            }
        });
    }

    /// <summary>Places the form where its start position says, the app's first form centered in the preview.</summary>
    private void Place(FormsRuntime runtime)
    {
        var desktop = runtime.DesktopSize;
        var position = StartPosition;
        if (runtime.Windows.Count == 0 && position is FormStartPosition.WindowsDefaultLocation or FormStartPosition.WindowsDefaultBounds)
        {
            position = FormStartPosition.CenterScreen;
        }

        _centeredAt = null;
        switch (position)
        {
            case FormStartPosition.CenterScreen:
                CenterToScreen();
                _centeredAt = Location;
                break;
            case FormStartPosition.CenterParent:
                CenterToParent();
                _centeredAt = Location;
                break;
            case FormStartPosition.WindowsDefaultLocation or FormStartPosition.WindowsDefaultBounds:
                var top = runtime.Windows[^1].Form;
                Location = new Point(top.Left + Cascade, top.Top + Cascade);
                break;
        }

        if (desktop.Width > 0 && Right > desktop.Width && Width < desktop.Width)
        {
            Left = Math.Max(0, desktop.Width - Width);
        }

        if (Top < 0)
        {
            Top = 0;
        }
    }

    /// <summary>
    /// Keeps the window where it belongs after the desktop changed size, as the app preview's pane does when it is
    /// resized: a maximized window fills the desktop again, a window the app opened centered and nobody moved is
    /// centered again, and any other window keeps its title bar on the desktop.
    /// </summary>
    internal void OnDesktopResized()
    {
        if (WindowInternal is null || _windowState == FormWindowState.Minimized)
        {
            return;
        }

        if (_windowState == FormWindowState.Maximized)
        {
            ApplyMaximized();
            return;
        }

        var desktop = WindowInternal.Runtime.DesktopSize;
        if (_centeredAt == Location)
        {
            if (StartPosition == FormStartPosition.CenterParent && _owner is not null)
            {
                CenterToParent();
            }
            else
            {
                CenterToScreen();
            }

            _centeredAt = Location;
            return;
        }

        // Some of the title bar stays on the desktop, to drag the window back by.
        const int Reachable = 80;
        var left = Math.Clamp(Left, Reachable - Width, Math.Max(0, desktop.Width - Reachable));
        var top = Math.Clamp(Top, 0, Math.Max(0, desktop.Height - FrameTitle));
        if (left != Left || top != Top)
        {
            Location = new Point(left, top);
        }
    }

    /// <summary>Makes the window fill the desktop.</summary>
    private void ApplyMaximized()
    {
        var desktop = WindowInternal?.Runtime.DesktopSize ?? Size.Empty;
        if (desktop.Width <= 0 || desktop.Height <= 0)
        {
            return;
        }

        var frame = FrameInsets;
        var titleWhenMaximized = frame.Top - FrameBorder;
        Bounds = new Rectangle(-frame.Left, -FrameBorder, desktop.Width + frame.Horizontal, desktop.Height - titleWhenMaximized + frame.Top + frame.Bottom);
    }

    /// <summary>Closes the window: it leaves the desktop, and the form stays as it is for code that reads it.</summary>
    private void CloseWindowCore()
    {
        if (WindowInternal is not { } window)
        {
            return;
        }

        WindowInternal = null;
        window.CloseAllPopups();
        window.Dispose();
        window.Runtime.RemoveWindow(window);
        OnVisibleChanged(EventArgs.Empty);
    }
}

/// <summary>How MDI child forms are arranged.</summary>
public enum MdiLayout
{
    /// <summary>Cascaded.</summary>
    Cascade = 0,

    /// <summary>Tiled side by side.</summary>
    TileHorizontal = 1,

    /// <summary>Tiled one above the other.</summary>
    TileVertical = 2,

    /// <summary>Their icons arranged.</summary>
    ArrangeIcons = 3,
}
