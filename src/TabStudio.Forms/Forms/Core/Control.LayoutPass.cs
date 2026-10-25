using System.Drawing;
using System.Windows.Forms.Layout;

namespace System.Windows.Forms;

/// <summary>Suspending, resuming and performing layout, as Windows Forms batches it.</summary>
public partial class Control
{
    private int _layoutSuspendCount;
    private bool _layoutPending;
    private bool _inLayout;

    /// <summary>The engine that lays out the child controls: docking and anchoring.</summary>
    public virtual LayoutEngine LayoutEngine => DefaultLayout.Instance;

    /// <summary>The distances to the parent's edges that anchoring keeps, measured when the control was placed.</summary>
    internal AnchorInfo? AnchorInfo { get; set; }

    /// <summary>Whether the control is laying out its children now.</summary>
    internal bool IsLayingOut => _inLayout;

    /// <summary>Whether layout is suspended.</summary>
    internal bool IsLayoutSuspended => _layoutSuspendCount > 0;

    /// <summary>Stops laying out the children until <see cref="ResumeLayout()"/>, while many changes are made.</summary>
    public void SuspendLayout() => _layoutSuspendCount++;

    /// <summary>Resumes layout and lays out the children if a change asked for it meanwhile.</summary>
    public void ResumeLayout() => ResumeLayout(true);

    /// <summary>Resumes layout.</summary>
    /// <param name="performLayout">
    /// Whether to lay out the children now if a change asked for it. With <see langword="false"/>, the children's
    /// anchors are measured again against the current size instead, as the designer's code relies on.
    /// </param>
    public void ResumeLayout(bool performLayout)
    {
        if (_layoutSuspendCount > 0)
        {
            _layoutSuspendCount--;
            if (_layoutSuspendCount == 0 && _layoutPending && performLayout)
            {
                PerformLayout();
            }
        }

        if (!performLayout)
        {
            foreach (var child in _controls.ToArray())
            {
                InitChildLayout(child);
            }
        }
    }

    /// <summary>Lays out the children now.</summary>
    public void PerformLayout() => PerformLayout(null, null);

    /// <summary>Lays out the children now, because a property of a control changed.</summary>
    /// <param name="affectedControl">The control that changed.</param>
    /// <param name="affectedProperty">The property that changed.</param>
    public void PerformLayout(Control? affectedControl, string? affectedProperty)
    {
        if (_layoutSuspendCount > 0)
        {
            _layoutPending = true;
            return;
        }

        if (_inLayout)
        {
            return;
        }

        _layoutPending = false;
        var preferred = AutoSize && _parent is not null ? GetPreferredSize(Size.Empty) : default;
        _inLayout = true;
        try
        {
            OnLayout(new LayoutEventArgs(affectedControl, affectedProperty));
        }
        finally
        {
            _inLayout = false;
        }

        // A change inside an automatically sized container can change the container's own size.
        if (AutoSize && _parent is { } parent && GetPreferredSize(Size.Empty) != preferred)
        {
            parent.PerformLayout(this, nameof(PreferredSize));
        }
    }

    /// <summary>Raises the Layout event and lays out the children with the layout engine.</summary>
    /// <param name="levent">The event data.</param>
    protected virtual void OnLayout(LayoutEventArgs levent)
    {
        ((LayoutEventHandler?)Events[EventLayout])?.Invoke(this, levent);
        LayoutEngine.Layout(this, levent);
    }

    /// <summary>Measures a child's anchors against the current client area, as adding or moving it does.</summary>
    /// <param name="child">The child.</param>
    internal void InitChildLayout(Control child) => LayoutEngine.InitLayout(child, BoundsSpecified.All);

    /// <summary>Called when the control is added to a container, to measure its anchors.</summary>
    protected virtual void InitLayout() => _parent?.InitChildLayout(this);
}
