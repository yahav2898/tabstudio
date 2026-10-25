namespace System.Windows.Forms;

/// <summary>
/// The On* methods of property changes, which do what Windows Forms does when a property changes before raising the
/// event: ambient properties reach the children that use their parent's value, and layout and painting follow.
/// </summary>
public partial class Control
{
    /// <summary>Raises the TextChanged event; an automatically sized control is sized to the new text.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnTextChanged(EventArgs e)
    {
        if (AutoSize)
        {
            _parent?.PerformLayout(this, nameof(Text));
        }

        Invalidate();
        ((EventHandler?)Events[EventTextChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the FontChanged event, and tells the children that use this font.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnFontChanged(EventArgs e)
    {
        Invalidate();
        if (AutoSize)
        {
            _parent?.PerformLayout(this, nameof(Font));
        }

        ((EventHandler?)Events[EventFontChanged])?.Invoke(this, e);
        foreach (var child in _controls.ToArray())
        {
            child.OnParentFontChanged(e);
        }

        PerformLayout(this, nameof(Font));
    }

    /// <summary>Called when the parent's font changes; raises FontChanged when this control uses it.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnParentFontChanged(EventArgs e)
    {
        if (_font is null)
        {
            OnFontChanged(e);
        }
    }

    /// <summary>Raises the ForeColorChanged event, and tells the children that use this color.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnForeColorChanged(EventArgs e)
    {
        Invalidate();
        ((EventHandler?)Events[EventForeColorChanged])?.Invoke(this, e);
        foreach (var child in _controls.ToArray())
        {
            child.OnParentForeColorChanged(e);
        }
    }

    /// <summary>Called when the parent's text color changes; raises ForeColorChanged when this control uses it.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnParentForeColorChanged(EventArgs e)
    {
        if (_foreColor.IsEmpty)
        {
            OnForeColorChanged(e);
        }
    }

    /// <summary>Raises the BackColorChanged event, and tells the children that use this color.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnBackColorChanged(EventArgs e)
    {
        Invalidate();
        ((EventHandler?)Events[EventBackColorChanged])?.Invoke(this, e);
        foreach (var child in _controls.ToArray())
        {
            child.OnParentBackColorChanged(e);
        }
    }

    /// <summary>Called when the parent's background color changes; raises BackColorChanged when this control uses it.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnParentBackColorChanged(EventArgs e)
    {
        if (_backColor.IsEmpty)
        {
            OnBackColorChanged(e);
        }
    }

    /// <summary>Raises the EnabledChanged event, and tells the children.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnEnabledChanged(EventArgs e)
    {
        if (!Enabled)
        {
            Window?.OnControlDisabledOrHidden(this);
        }

        Invalidate();
        ((EventHandler?)Events[EventEnabledChanged])?.Invoke(this, e);
        foreach (var child in _controls.ToArray())
        {
            child.OnParentEnabledChanged(e);
        }
    }

    /// <summary>Called when the parent is enabled or disabled; raises EnabledChanged when this control follows it.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnParentEnabledChanged(EventArgs e)
    {
        if (_enabled)
        {
            OnEnabledChanged(e);
        }
    }

    /// <summary>Raises the VisibleChanged event, and tells the children.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnVisibleChanged(EventArgs e)
    {
        if (!Visible)
        {
            Window?.OnControlDisabledOrHidden(this);
        }

        InvalidateWindow();
        ((EventHandler?)Events[EventVisibleChanged])?.Invoke(this, e);
        foreach (var child in _controls.ToArray())
        {
            child.OnParentVisibleChanged(e);
        }
    }

    /// <summary>Called when the parent is shown or hidden; raises VisibleChanged when this control follows it.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnParentVisibleChanged(EventArgs e)
    {
        if (_visible)
        {
            OnVisibleChanged(e);
        }
    }

    /// <summary>Raises the SizeChanged event, after Resize.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSizeChanged(EventArgs e)
    {
        OnResize(EventArgs.Empty);
        ((EventHandler?)Events[EventSizeChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the Resize event, after laying out the children for the new size.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnResize(EventArgs e)
    {
        _immediate?.Clear();
        PerformLayout(this, nameof(Bounds));
        Invalidate();
        ((EventHandler?)Events[EventResize])?.Invoke(this, e);
    }

    /// <summary>Raises the ClientSizeChanged event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnClientSizeChanged(EventArgs e) => ((EventHandler?)Events[EventClientSizeChanged])?.Invoke(this, e);

    /// <summary>Raises the LocationChanged event, after Move.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnLocationChanged(EventArgs e)
    {
        OnMove(EventArgs.Empty);
        ((EventHandler?)Events[EventLocationChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the Move event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMove(EventArgs e)
    {
        _immediate?.Clear();
        InvalidateWindow();
        ((EventHandler?)Events[EventMove])?.Invoke(this, e);
    }

    /// <summary>Raises the DockChanged event, after the parent lays out its children again.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDockChanged(EventArgs e)
    {
        _parent?.PerformLayout(this, nameof(Dock));
        ((EventHandler?)Events[EventDockChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the AutoSizeChanged event, after the parent lays out its children again.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnAutoSizeChanged(EventArgs e)
    {
        _parent?.PerformLayout(this, nameof(AutoSize));
        ((EventHandler?)Events[EventAutoSizeChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the PaddingChanged event, after laying out the children again.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnPaddingChanged(EventArgs e)
    {
        PerformLayout(this, nameof(Padding));
        Invalidate();
        ((EventHandler?)Events[EventPaddingChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the MarginChanged event, after the parent lays out its children again.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMarginChanged(EventArgs e)
    {
        _parent?.PerformLayout(this, nameof(Margin));
        ((EventHandler?)Events[EventMarginChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the ParentChanged event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnParentChanged(EventArgs e) => ((EventHandler?)Events[EventParentChanged])?.Invoke(this, e);

    /// <summary>Raises the CursorChanged event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCursorChanged(EventArgs e) => ((EventHandler?)Events[EventCursorChanged])?.Invoke(this, e);

    /// <summary>Raises the RightToLeftChanged event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnRightToLeftChanged(EventArgs e) => ((EventHandler?)Events[EventRightToLeftChanged])?.Invoke(this, e);

    /// <summary>Raises the BackgroundImageChanged event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnBackgroundImageChanged(EventArgs e)
    {
        Invalidate();
        ((EventHandler?)Events[EventBackgroundImageChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the BackgroundImageLayoutChanged event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnBackgroundImageLayoutChanged(EventArgs e)
    {
        Invalidate();
        ((EventHandler?)Events[EventBackgroundImageLayoutChanged])?.Invoke(this, e);
    }

    /// <summary>Raises the GotFocus event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnGotFocus(EventArgs e)
    {
        Invalidate();
        ((EventHandler?)Events[EventGotFocus])?.Invoke(this, e);
    }

    /// <summary>Raises the LostFocus event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnLostFocus(EventArgs e)
    {
        Invalidate();
        ((EventHandler?)Events[EventLostFocus])?.Invoke(this, e);
    }
}
