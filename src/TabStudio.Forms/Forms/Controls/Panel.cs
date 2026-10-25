using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A container of other controls, with an optional border, that scrolls them when AutoScroll is on.</summary>
[DefaultProperty(nameof(BorderStyle))]
public class Panel : ScrollableControl
{
    private BorderStyle _borderStyle;

    /// <summary>Creates a panel.</summary>
    public Panel()
    {
        TabStop = false;
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
    }

    /// <summary>The border.</summary>
    [DefaultValue(BorderStyle.None)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value)
            {
                return;
            }

            _borderStyle = value;
            UpdateClientSize();
            PerformLayout(this, nameof(BorderStyle));
            Invalidate();
        }
    }

    /// <summary>How the panel sizes itself when AutoSize is on.</summary>
    [DefaultValue(AutoSizeMode.GrowOnly)]
    public virtual AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    /// <inheritdoc />
    protected override Size DefaultSize => new(200, 100);

    /// <inheritdoc />
    internal override int BorderSize => ThemePainter.BorderWidth(_borderStyle);

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        var content = base.GetPreferredSizeCore(proposedSize);
        return AutoSizeMode == AutoSizeMode.GrowOnly ? new Size(Math.Max(content.Width, Width), Math.Max(content.Height, Height)) : content;
    }

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g)
    {
        ThemePainter.DrawBorder(g, new Rectangle(Point.Empty, Size), _borderStyle);
        base.PaintNonClient(g);
    }
}

/// <summary>A control made of other controls, designed like a form and placed on forms.</summary>
[DefaultEvent(nameof(Load))]
public class UserControl : ContainerControl
{
    private static readonly object EventLoad = new();
    private BorderStyle _borderStyle;

    /// <summary>Creates a user control.</summary>
    public UserControl() => SetStyle(ControlStyles.SupportsTransparentBackColor, true);

    /// <summary>Occurs before the control is shown for the first time.</summary>
    public event EventHandler? Load
    {
        add => Events.AddHandler(EventLoad, value);
        remove => Events.RemoveHandler(EventLoad, value);
    }

    /// <summary>The border.</summary>
    [DefaultValue(BorderStyle.None)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            UpdateClientSize();
            PerformLayout(this, nameof(BorderStyle));
            Invalidate();
        }
    }

    /// <summary>How the control sizes itself when AutoSize is on.</summary>
    public AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    /// <inheritdoc />
    protected override Size DefaultSize => new(150, 150);

    /// <inheritdoc />
    internal override int BorderSize => ThemePainter.BorderWidth(_borderStyle);

    /// <summary>Raises the Load event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnLoad(EventArgs e) => ((EventHandler?)Events[EventLoad])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnCreateControl()
    {
        base.OnCreateControl();
        OnLoad(EventArgs.Empty);
    }

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        var content = base.GetPreferredSizeCore(proposedSize);
        return AutoSizeMode == AutoSizeMode.GrowOnly ? new Size(Math.Max(content.Width, Width), Math.Max(content.Height, Height)) : content;
    }

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g)
    {
        ThemePainter.DrawBorder(g, new Rectangle(Point.Empty, Size), _borderStyle);
        base.PaintNonClient(g);
    }
}
