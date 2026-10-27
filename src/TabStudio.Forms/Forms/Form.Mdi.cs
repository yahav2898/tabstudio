using System.Drawing;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// MDI children: forms inside an MDI parent's workspace, each drawn with a title bar of its own, as Windows draws
/// them, that moves the child, and buttons that minimize, maximize and close it.
/// </summary>
public partial class Form
{
    /// <summary>The height of an MDI child's title bar, tall enough for a finger.</summary>
    internal const int MdiCaptionHeight = 28;

    /// <summary>The width of an MDI child's border.</summary>
    internal const int MdiBorder = 1;

    private const int MdiButtonWidth = 36;

    private MdiButton _mdiPressed;
    private Point? _mdiGrab;

    private enum MdiButton
    {
        None,
        Close,
        Maximize,
        Minimize,
    }

    /// <summary>Paints an MDI child's border and title bar.</summary>
    /// <param name="g">The graphics, at the form's bounds.</param>
    internal override void PaintNonClient(Graphics g)
    {
        base.PaintNonClient(g);
        if (_mdiParent is null || _formBorderStyle == FormBorderStyle.None)
        {
            return;
        }

        var active = _mdiParent.MdiClient?.ActiveChild == this;
        using (var border = new Pen(active ? Color.FromArgb(0x70, 0x70, 0x70) : Color.FromArgb(0xA8, 0xA8, 0xA8)))
        {
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        var caption = new Rectangle(MdiBorder, MdiBorder, Math.Max(0, Width - (2 * MdiBorder)), MdiCaptionHeight);
        g.FillRectangle(Brushes.White, caption);
        var buttons = MdiButtons();
        var textColor = active ? Color.FromArgb(0x1A, 0x1A, 0x1A) : Color.FromArgb(0x8C, 0x8C, 0x8C);
        var textBounds = Rectangle.FromLTRB(caption.Left + 8, caption.Top, Math.Max(caption.Left + 8, caption.Right - (buttons.Count * MdiButtonWidth) - 4), caption.Bottom);
        TextRenderer.DrawText(g, Text, SystemFonts.CaptionFont, textBounds, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

        for (var i = 0; i < buttons.Count; i++)
        {
            var bounds = MdiButtonBounds(i);
            var kind = buttons[i];
            if (_mdiPressed == kind)
            {
                using var pressed = new SolidBrush(kind == MdiButton.Close ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Color.FromArgb(0x33, 0, 0, 0));
                g.FillRectangle(pressed, bounds);
            }

            using var glyph = new Pen(_mdiPressed == kind && kind == MdiButton.Close ? Color.White : textColor);
            var cx = bounds.Left + (bounds.Width / 2);
            var cy = bounds.Top + (bounds.Height / 2);
            switch (kind)
            {
                case MdiButton.Close:
                    g.DrawLine(glyph, cx - 5, cy - 5, cx + 5, cy + 5);
                    g.DrawLine(glyph, cx - 5, cy + 5, cx + 5, cy - 5);
                    break;
                case MdiButton.Maximize when _windowState == FormWindowState.Maximized:
                case MdiButton.Minimize when _windowState == FormWindowState.Minimized:
                    g.DrawRectangle(glyph, cx - 5, cy - 3, 8, 8);
                    g.DrawLine(glyph, cx - 3, cy - 5, cx + 5, cy - 5);
                    g.DrawLine(glyph, cx + 5, cy - 5, cx + 5, cy + 3);
                    break;
                case MdiButton.Maximize:
                    g.DrawRectangle(glyph, cx - 5, cy - 5, 10, 10);
                    break;
                case MdiButton.Minimize:
                    g.DrawLine(glyph, cx - 5, cy, cx + 5, cy);
                    break;
            }
        }
    }

    /// <summary>A press on an MDI child's title bar: on a button, or to move the child.</summary>
    /// <param name="point">The point, in the form's bounds coordinates.</param>
    internal override bool NonClientMouseDown(Point point)
    {
        if (_mdiParent is null || _formBorderStyle == FormBorderStyle.None || point.Y >= MdiBorder + MdiCaptionHeight)
        {
            return base.NonClientMouseDown(point);
        }

        _mdiParent.MdiClient?.Activate(this);
        _mdiPressed = MdiButtonAt(point);
        _mdiGrab = _mdiPressed == MdiButton.None && _windowState == FormWindowState.Normal ? point : null;
        Invalidate();
        return true;
    }

    /// <inheritdoc />
    internal override void NonClientMouseMove(Point point)
    {
        if (_mdiGrab is not { } grab || _mdiParent?.MdiClient is not { } client)
        {
            base.NonClientMouseMove(point);
            return;
        }

        // The title bar stays in the workspace, to drag the child back by.
        const int Reachable = 60;
        var area = client.ClientSize;
        var left = Math.Clamp(Left + point.X - grab.X, Reachable - Width, Math.Max(0, area.Width - Reachable));
        var top = Math.Clamp(Top + point.Y - grab.Y, 0, Math.Max(0, area.Height - MdiCaptionHeight));
        Location = new Point(left, top);
    }

    /// <inheritdoc />
    internal override void NonClientMouseUp(Point point)
    {
        var pressed = _mdiPressed;
        var wasMdi = _mdiGrab is not null || pressed != MdiButton.None;
        _mdiPressed = MdiButton.None;
        _mdiGrab = null;
        if (!wasMdi)
        {
            base.NonClientMouseUp(point);
            return;
        }

        Invalidate();
        if (pressed == MdiButton.None || MdiButtonAt(point) != pressed)
        {
            return;
        }

        switch (pressed)
        {
            case MdiButton.Close:
                Close();
                break;
            case MdiButton.Maximize:
                WindowState = _windowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                break;
            case MdiButton.Minimize:
                WindowState = _windowState == FormWindowState.Minimized ? FormWindowState.Normal : FormWindowState.Minimized;
                break;
        }
    }

    /// <summary>Raises TextChanged, and redraws an MDI child's title and its parent's window list.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        if (_mdiParent?.MdiClient is { } client)
        {
            Invalidate();
            client.UpdateWindowList();
        }
    }

    /// <summary>Arranges the MDI children: cascaded or tiled.</summary>
    /// <param name="value">The arrangement.</param>
    public void LayoutMdi(MdiLayout value) => _mdiClient?.LayoutMdi(value);

    /// <summary>Shows or hides a form that is not a window: an MDI child, or a form put in a panel.</summary>
    private void SetChildVisibleCore(bool value)
    {
        if (value && !_loaded && !IsDisposed)
        {
            // Such a form loads when it is first shown, as a window does.
            _loaded = true;
            CreateControl();
            PerformAutoScale();
            PerformLayout();
            OnLoad(EventArgs.Empty);
            if (IsDisposed)
            {
                return;
            }

            if (_mdiParent?.MdiClient is { } placing && _windowState != FormWindowState.Normal)
            {
                Bounds = _windowState == FormWindowState.Maximized ? placing.MaximizedBounds : placing.MinimizedBounds(this);
            }

            base.SetVisibleCore(true);
            if (FormsRuntime.Current is { } runtime)
            {
                runtime.Post(() =>
                {
                    if (!IsDisposed && Visible)
                    {
                        OnShown(EventArgs.Empty);
                    }
                });
            }
        }
        else
        {
            base.SetVisibleCore(value);
        }

        if (_mdiParent?.MdiClient is { } client)
        {
            if (value)
            {
                client.Activate(this);
            }
            else
            {
                client.ChildHidden(this);
            }
        }
    }

    /// <summary>Closes an MDI child: FormClosing, which can cancel, FormClosed, then it leaves the workspace.</summary>
    private void CloseMdiChild(CloseReason reason)
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        try
        {
            var closing = new FormClosingEventArgs(reason, cancel: false);
            OnFormClosing(closing);
            if (closing.Cancel)
            {
                return;
            }

            OnFormClosed(new FormClosedEventArgs(reason));
        }
        finally
        {
            _closing = false;
        }

        _mdiParent?.MdiClient?.RemoveChild(this);
        Dispose();
    }

    /// <summary>Sets an MDI child's state: maximized, it fills the workspace; minimized, only its title bar shows.</summary>
    private void SetMdiWindowState(FormWindowState value)
    {
        if (_windowState == FormWindowState.Normal)
        {
            _restoreBounds = Bounds;
        }

        _windowState = value;
        if (_mdiParent?.MdiClient is { } client)
        {
            Bounds = value switch
            {
                FormWindowState.Maximized => client.MaximizedBounds,
                FormWindowState.Minimized => client.MinimizedBounds(this),
                _ => _restoreBounds.IsEmpty ? Bounds : _restoreBounds,
            };
        }

        Invalidate();
        OnResize(EventArgs.Empty);
    }

    private List<MdiButton> MdiButtons()
    {
        var buttons = new List<MdiButton>(3);
        if (!_controlBox)
        {
            return buttons;
        }

        buttons.Add(MdiButton.Close);
        if (_maximizeBox)
        {
            buttons.Add(MdiButton.Maximize);
        }

        if (_minimizeBox)
        {
            buttons.Add(MdiButton.Minimize);
        }

        return buttons;
    }

    /// <summary>The bounds of a title bar button, counted from the right.</summary>
    private Rectangle MdiButtonBounds(int index) =>
        new(Width - MdiBorder - ((index + 1) * MdiButtonWidth), MdiBorder, MdiButtonWidth, MdiCaptionHeight);

    private MdiButton MdiButtonAt(Point point)
    {
        var buttons = MdiButtons();
        for (var i = 0; i < buttons.Count; i++)
        {
            if (MdiButtonBounds(i).Contains(point))
            {
                return buttons[i];
            }
        }

        return MdiButton.None;
    }
}
