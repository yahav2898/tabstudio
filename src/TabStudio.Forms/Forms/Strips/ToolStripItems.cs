using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A button on a tool bar, optionally one that stays checked.</summary>
public class ToolStripButton : ToolStripItem
{
    private static readonly object EventCheckedChanged = new();
    private static readonly object EventCheckStateChanged = new();
    private CheckState _checkState;

    /// <summary>Creates a button.</summary>
    public ToolStripButton()
    {
        AutoToolTip = true;
    }

    /// <summary>Creates a button with text.</summary>
    /// <param name="text">The text.</param>
    public ToolStripButton(string? text)
        : this(text, null, null)
    {
    }

    /// <summary>Creates a button with an image.</summary>
    /// <param name="image">The image.</param>
    public ToolStripButton(Image? image)
        : this(null, image, null)
    {
    }

    /// <summary>Creates a button with text and an image.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    public ToolStripButton(string? text, Image? image)
        : this(text, image, null)
    {
    }

    /// <summary>Creates a button with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    public ToolStripButton(string? text, Image? image, EventHandler? onClick)
        : this(text, image, onClick, null)
    {
    }

    /// <summary>Creates a button with text, an image, a click handler and a name.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    /// <param name="name">The name.</param>
    public ToolStripButton(string? text, Image? image, EventHandler? onClick, string? name)
        : base(text, image, onClick, name)
    {
        AutoToolTip = true;
    }

    /// <summary>Occurs when <see cref="Checked"/> changes.</summary>
    public event EventHandler? CheckedChanged
    {
        add => Events.AddHandler(EventCheckedChanged, value);
        remove => Events.RemoveHandler(EventCheckedChanged, value);
    }

    /// <summary>Occurs when <see cref="CheckState"/> changes.</summary>
    public event EventHandler? CheckStateChanged
    {
        add => Events.AddHandler(EventCheckStateChanged, value);
        remove => Events.RemoveHandler(EventCheckStateChanged, value);
    }

    /// <summary>Whether the button is checked.</summary>
    public bool Checked
    {
        get => _checkState != CheckState.Unchecked;
        set => CheckState = value ? CheckState.Checked : CheckState.Unchecked;
    }

    /// <summary>The check state.</summary>
    public CheckState CheckState
    {
        get => _checkState;
        set
        {
            if (_checkState == value)
            {
                return;
            }

            var wasChecked = Checked;
            _checkState = value;
            Invalidate();
            if (wasChecked != Checked)
            {
                OnCheckedChanged(EventArgs.Empty);
            }

            OnCheckStateChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether a click toggles <see cref="Checked"/>.</summary>
    public bool CheckOnClick { get; set; }

    /// <inheritdoc />
    public override bool CanSelect => Enabled;

    /// <summary>Raises CheckedChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCheckedChanged(EventArgs e) => ((EventHandler?)Events[EventCheckedChanged])?.Invoke(this, e);

    /// <summary>Raises CheckStateChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCheckStateChanged(EventArgs e) => ((EventHandler?)Events[EventCheckStateChanged])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnClick(EventArgs e)
    {
        if (CheckOnClick)
        {
            Checked = !Checked;
        }

        base.OnClick(e);
    }

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        if (Checked && !Selected && !Pressed)
        {
            using var fill = new SolidBrush(Theme.ItemHot);
            using var border = new Pen(Theme.Accent);
            g.FillRectangle(fill, bounds);
            g.DrawRectangle(border, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        base.PaintOnStrip(g, bounds);
    }
}

/// <summary>Text, an image or a link on a strip, which does not respond to taps unless it is a link.</summary>
public class ToolStripLabel : ToolStripItem
{
    /// <summary>Creates a label.</summary>
    public ToolStripLabel()
    {
    }

    /// <summary>Creates a label with text.</summary>
    /// <param name="text">The text.</param>
    public ToolStripLabel(string? text)
        : base(text, null, null)
    {
    }

    /// <summary>Creates a label with an image.</summary>
    /// <param name="image">The image.</param>
    public ToolStripLabel(Image? image)
        : base(null, image, null)
    {
    }

    /// <summary>Creates a label with text and an image.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    public ToolStripLabel(string? text, Image? image)
        : base(text, image, null)
    {
    }

    /// <summary>Creates a label that may be a link.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="isLink">Whether it is a link.</param>
    public ToolStripLabel(string? text, Image? image, bool isLink)
        : this(text, image, isLink, null)
    {
    }

    /// <summary>Creates a label that may be a link, with a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="isLink">Whether it is a link.</param>
    /// <param name="onClick">The Click handler.</param>
    public ToolStripLabel(string? text, Image? image, bool isLink, EventHandler? onClick)
        : this(text, image, isLink, onClick, null)
    {
    }

    /// <summary>Creates a label that may be a link, with a click handler and a name.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="isLink">Whether it is a link.</param>
    /// <param name="onClick">The Click handler.</param>
    /// <param name="name">The name.</param>
    public ToolStripLabel(string? text, Image? image, bool isLink, EventHandler? onClick, string? name)
        : base(text, image, onClick, name)
    {
        IsLink = isLink;
    }

    /// <summary>Whether the label is drawn and behaves as a link.</summary>
    public bool IsLink { get; set; }

    /// <summary>The color of the link.</summary>
    public Color LinkColor { get; set; } = Color.FromArgb(0, 102, 204);

    /// <summary>The color of a link being pressed.</summary>
    public Color ActiveLinkColor { get; set; } = Color.Red;

    /// <summary>The color of a visited link.</summary>
    public Color VisitedLinkColor { get; set; } = Color.FromArgb(128, 0, 128);

    /// <summary>Whether the link was visited.</summary>
    public bool LinkVisited { get; set; }

    /// <summary>When the link is underlined.</summary>
    public LinkBehavior LinkBehavior { get; set; }

    /// <inheritdoc />
    public override bool CanSelect => IsLink && Enabled;

    /// <inheritdoc />
    private protected override int HorizontalSpace => 4;

    /// <inheritdoc />
    private protected override int TextVerticalSpace => 2;

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        if (!BackColor.IsEmpty)
        {
            using var brush = new SolidBrush(BackColor);
            g.FillRectangle(brush, bounds);
        }

        if (IsLink && ShowsText)
        {
            var color = !Enabled ? Theme.DisabledText : Pressed ? ActiveLinkColor : LinkVisited ? VisitedLinkColor : LinkColor;
            using var font = LinkBehavior == LinkBehavior.NeverUnderline || (LinkBehavior == LinkBehavior.HoverUnderline && !Selected)
                ? new Font(Font, Font.Style)
                : new Font(Font, Font.Style | FontStyle.Underline);
            ThemePainter.DrawText(g, Text, font, color, Inner(bounds), TextAlign);
            return;
        }

        PaintContent(g, Inner(bounds));
    }

    private Rectangle Inner(Rectangle bounds) =>
        Rectangle.FromLTRB(bounds.Left + 2 + Padding.Left, bounds.Top + Padding.Top, bounds.Right - 2 - Padding.Right, bounds.Bottom - Padding.Bottom);
}

/// <summary>A pane of a status bar: text, an image, or both, which can take the space left with <see cref="Spring"/>.</summary>
public class ToolStripStatusLabel : ToolStripLabel
{
    private bool _spring;

    /// <summary>Creates a status label.</summary>
    public ToolStripStatusLabel()
    {
    }

    /// <summary>Creates a status label with text.</summary>
    /// <param name="text">The text.</param>
    public ToolStripStatusLabel(string? text)
        : base(text)
    {
    }

    /// <summary>Creates a status label with an image.</summary>
    /// <param name="image">The image.</param>
    public ToolStripStatusLabel(Image? image)
        : base(image)
    {
    }

    /// <summary>Creates a status label with text and an image.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    public ToolStripStatusLabel(string? text, Image? image)
        : base(text, image)
    {
    }

    /// <summary>Creates a status label with text, an image and a click handler.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    public ToolStripStatusLabel(string? text, Image? image, EventHandler? onClick)
        : base(text, image, false, onClick)
    {
    }

    /// <summary>Creates a status label with text, an image, a click handler and a name.</summary>
    /// <param name="text">The text.</param>
    /// <param name="image">The image.</param>
    /// <param name="onClick">The Click handler.</param>
    /// <param name="name">The name.</param>
    public ToolStripStatusLabel(string? text, Image? image, EventHandler? onClick, string? name)
        : base(text, image, false, onClick, name)
    {
    }

    /// <summary>Whether the label takes the space the status bar's other items leave.</summary>
    public bool Spring
    {
        get => _spring;
        set
        {
            _spring = value;
            Relayout();
        }
    }

    /// <summary>The sides with a border.</summary>
    public ToolStripStatusLabelBorderSides BorderSides { get; set; }

    /// <summary>The look of the border.</summary>
    public Border3DStyle BorderStyle { get; set; } = Border3DStyle.Flat;

    /// <inheritdoc />
    protected override Padding DefaultMargin => new(0, 3, 0, 2);

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        base.PaintOnStrip(g, bounds);
        if (BorderSides == ToolStripStatusLabelBorderSides.None)
        {
            return;
        }

        using var pen = new Pen(Theme.ButtonBorder);
        if ((BorderSides & ToolStripStatusLabelBorderSides.Left) != 0)
        {
            g.DrawLine(pen, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 1);
        }

        if ((BorderSides & ToolStripStatusLabelBorderSides.Top) != 0)
        {
            g.DrawLine(pen, bounds.Left, bounds.Top, bounds.Right - 1, bounds.Top);
        }

        if ((BorderSides & ToolStripStatusLabelBorderSides.Right) != 0)
        {
            g.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        }

        if ((BorderSides & ToolStripStatusLabelBorderSides.Bottom) != 0)
        {
            g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
        }
    }
}

/// <summary>A line between groups of items.</summary>
public class ToolStripSeparator : ToolStripItem
{
    /// <summary>Creates a separator.</summary>
    public ToolStripSeparator()
    {
    }

    /// <inheritdoc />
    public override bool CanSelect => false;

    /// <inheritdoc />
    protected override Size DefaultSize => new(6, 6);

    /// <inheritdoc />
    protected override Padding DefaultMargin => Padding.Empty;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size constrainingSize) =>
        Owner is { Orientation: Orientation.Vertical } ? new Size(6, 6) : new Size(6, 25);

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds)
    {
        using var pen = new Pen(Theme.Separator);
        if (Owner is { Orientation: Orientation.Vertical })
        {
            var y = bounds.Top + (bounds.Height / 2);
            g.DrawLine(pen, bounds.Left + 2, y, bounds.Right - 3, y);
        }
        else
        {
            var x = bounds.Left + (bounds.Width / 2);
            g.DrawLine(pen, x, bounds.Top + 3, x, bounds.Bottom - 4);
        }
    }
}
