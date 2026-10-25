namespace System.Windows.Forms;

/// <summary>Focus and the tab order.</summary>
public partial class Control
{
    /// <summary>Whether the control has keyboard focus.</summary>
    public virtual bool Focused => Window?.FocusedControl == this;

    /// <summary>Whether the control or one of its descendants has focus.</summary>
    public bool ContainsFocus => Window?.FocusedControl is { } focused && (focused == this || Contains(focused));

    /// <summary>Whether the control can receive focus: it is shown, visible and enabled.</summary>
    public bool CanFocus => Window is not null && Visible && Enabled;

    /// <summary>Whether the control can be selected: it is selectable, visible and enabled.</summary>
    public bool CanSelect => GetStyle(ControlStyles.Selectable) && Visible && Enabled;

    /// <summary>Gives the control keyboard focus.</summary>
    /// <returns>Whether it has focus.</returns>
    public bool Focus()
    {
        if (CanFocus && Window is { } window)
        {
            window.SetFocus(this);
        }

        return Focused;
    }

    /// <summary>Activates the control: it gets focus, or, for a container, its first child in the tab order does.</summary>
    public void Select() => Select(directed: false, forward: true);

    /// <summary>Activates a child control.</summary>
    /// <param name="directed">Whether the direction is given.</param>
    /// <param name="forward">Whether to go forward in the tab order.</param>
    protected virtual void Select(bool directed, bool forward)
    {
        if (GetStyle(ControlStyles.ContainerControl) && _controls.Count > 0 && SelectNextControl(null, forward, tabStopOnly: true, nested: true, wrap: false))
        {
            return;
        }

        if (CanSelect)
        {
            Focus();
        }
    }

    /// <summary>Activates the next control in the tab order.</summary>
    /// <param name="ctl">Where to start, or <see langword="null"/> to start at the beginning.</param>
    /// <param name="forward">Whether to go forward.</param>
    /// <param name="tabStopOnly">Whether to skip controls with TabStop off.</param>
    /// <param name="nested">Whether to look into child containers.</param>
    /// <param name="wrap">Whether to wrap around at the end.</param>
    /// <returns>Whether a control was activated.</returns>
    public bool SelectNextControl(Control? ctl, bool forward, bool tabStopOnly, bool nested, bool wrap)
    {
        if (ctl is not null && (!Contains(ctl) || (!nested && ctl._parent != this)))
        {
            ctl = null;
        }

        var start = ctl;
        var wrapped = false;
        do
        {
            ctl = GetNextControl(ctl, forward);
            if (ctl is null)
            {
                if (!wrap || wrapped)
                {
                    return false;
                }

                wrapped = true;
                continue;
            }

            if (ctl.CanSelect && (!tabStopOnly || ctl.TabStop) && (nested || ctl._parent == this))
            {
                ctl.Select(directed: true, forward);
                return true;
            }
        }
        while (ctl != start);

        return false;
    }

    /// <summary>The next or previous control in the tab order of this control's descendants.</summary>
    /// <param name="ctl">Where to start, or <see langword="null"/> for the beginning.</param>
    /// <param name="forward">Whether to go forward.</param>
    public Control? GetNextControl(Control? ctl, bool forward)
    {
        if (ctl is null || !Contains(ctl))
        {
            ctl = this;
        }

        if (forward)
        {
            if (ctl._controls.Count > 0 && (ctl == this || !IsFocusManagingContainer(ctl)) && ctl.FirstInTabOrder(forward: true) is { } first)
            {
                return first;
            }

            while (ctl != this)
            {
                var parent = ctl._parent!;
                Control? found = null;
                var passed = false;
                foreach (var sibling in parent._controls.ToArray())
                {
                    if (sibling == ctl)
                    {
                        passed = true;
                        continue;
                    }

                    if (sibling.TabIndex >= ctl.TabIndex && (found is null || found.TabIndex > sibling.TabIndex) && (sibling.TabIndex != ctl.TabIndex || passed))
                    {
                        found = sibling;
                    }
                }

                if (found is not null)
                {
                    return found;
                }

                ctl = parent;
            }

            return null;
        }

        if (ctl != this)
        {
            var parent = ctl._parent!;
            Control? found = null;
            var passed = false;
            var siblings = parent._controls.ToArray();
            for (var i = siblings.Length - 1; i >= 0; i--)
            {
                var sibling = siblings[i];
                if (sibling == ctl)
                {
                    passed = true;
                    continue;
                }

                if (sibling.TabIndex <= ctl.TabIndex && (found is null || found.TabIndex < sibling.TabIndex) && (sibling.TabIndex != ctl.TabIndex || passed))
                {
                    found = sibling;
                }
            }

            if (found is null)
            {
                return parent == this ? null : parent;
            }

            ctl = found;
        }

        while (ctl._controls.Count > 0 && (ctl == this || !IsFocusManagingContainer(ctl)) && ctl.FirstInTabOrder(forward: false) is { } last)
        {
            ctl = last;
        }

        return ctl == this ? null : ctl;
    }

    private static bool IsFocusManagingContainer(Control control) => control.GetStyle(ControlStyles.ContainerControl) && control is not Form;

    /// <summary>The child with the lowest tab index going forward, or the highest going backward.</summary>
    private Control? FirstInTabOrder(bool forward)
    {
        var children = _controls.ToArray();
        Control? found = null;
        if (forward)
        {
            foreach (var child in children)
            {
                if (found is null || found.TabIndex > child.TabIndex)
                {
                    found = child;
                }
            }
        }
        else
        {
            for (var i = children.Length - 1; i >= 0; i--)
            {
                if (found is null || found.TabIndex < children[i].TabIndex)
                {
                    found = children[i];
                }
            }
        }

        return found;
    }

    internal void DispatchEnter() => OnEnter(EventArgs.Empty);

    internal void DispatchLeave() => OnLeave(EventArgs.Empty);

    internal void DispatchGotFocus() => OnGotFocus(EventArgs.Empty);

    internal void DispatchLostFocus() => OnLostFocus(EventArgs.Empty);

    /// <summary>Validates the control as focus leaves it: Validating, then Validated unless a handler cancelled.</summary>
    /// <returns>Whether the control is valid, so focus may move on.</returns>
    internal bool DispatchValidation()
    {
        var e = new System.ComponentModel.CancelEventArgs();
        OnValidating(e);
        if (e.Cancel)
        {
            return false;
        }

        OnValidated(EventArgs.Empty);
        return true;
    }
}
