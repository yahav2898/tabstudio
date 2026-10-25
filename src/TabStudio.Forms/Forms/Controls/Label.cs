using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>Text that the user cannot edit: a caption for another control, a result, a heading.</summary>
[DefaultProperty(nameof(Text))]
public class Label : Control
{
    private ContentAlignment _textAlign = ContentAlignment.TopLeft;
    private BorderStyle _borderStyle;
    private Image? _image;
    private ContentAlignment _imageAlign = ContentAlignment.MiddleCenter;
    private bool _autoEllipsis;

    /// <summary>Creates a label.</summary>
    public Label()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        TabStop = false;
    }

    /// <summary>Where the text sits in the label.</summary>
    [DefaultValue(ContentAlignment.TopLeft)]
    public virtual ContentAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            _textAlign = value;
            Invalidate();
        }
    }

    /// <summary>The border.</summary>
    [DefaultValue(BorderStyle.None)]
    public virtual BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            UpdateClientSize();
            if (AutoSize)
            {
                Parent?.PerformLayout(this, nameof(BorderStyle));
            }

            Invalidate();
        }
    }

    /// <summary>The look of the label's border. Kept for compatibility.</summary>
    public FlatStyle FlatStyle { get; set; } = FlatStyle.Standard;

    /// <summary>An image drawn in the label.</summary>
    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            Invalidate();
        }
    }

    /// <summary>Where the image sits in the label.</summary>
    public ContentAlignment ImageAlign
    {
        get => _imageAlign;
        set
        {
            _imageAlign = value;
            Invalidate();
        }
    }

    /// <summary>Whether an ampersand marks an access key, shown underlined, rather than showing as is.</summary>
    [DefaultValue(true)]
    public bool UseMnemonic { get; set; } = true;

    /// <summary>Whether text that does not fit ends with an ellipsis.</summary>
    public bool AutoEllipsis
    {
        get => _autoEllipsis;
        set
        {
            _autoEllipsis = value;
            Invalidate();
        }
    }

    /// <summary>Whether text is drawn with GDI+. Kept for compatibility.</summary>
    public bool UseCompatibleTextRendering { get; set; }

    /// <summary>The width the label needs for its text on one line.</summary>
    public virtual int PreferredWidth => GetPreferredSize(Size.Empty).Width;

    /// <summary>The height the label needs for its text.</summary>
    public virtual int PreferredHeight => GetPreferredSize(Size.Empty).Height;

    /// <inheritdoc />
    protected override Size DefaultSize => new(100, 23);

    /// <summary>The label's mnemonic moves focus to the control after it in the tab order, such as its text box.</summary>
    /// <param name="charCode">The character.</param>
    protected internal override bool ProcessMnemonic(char charCode)
    {
        if (UseMnemonic && CanProcessMnemonic() && IsMnemonic(charCode, Text) && Parent is { } parent)
        {
            parent.SelectNextControl(this, forward: true, tabStopOnly: false, nested: true, wrap: false);
            return true;
        }

        return base.ProcessMnemonic(charCode);
    }

    /// <inheritdoc />
    protected override Padding DefaultMargin => new(3, 0, 3, 0);

    /// <inheritdoc />
    internal override Padding NonClientInsets => new(ThemePainter.BorderWidth(_borderStyle));

    /// <summary>The text as drawn: access-key ampersands removed.</summary>
    internal string DisplayText => UseMnemonic ? Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide) : Text;

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        var maxWidth = MaximumSize.Width > 0 ? MaximumSize.Width - Padding.Horizontal - NonClientInsets.Horizontal : double.PositiveInfinity;
        var text = TextMetrics.Measure(DisplayText, Font.Spec, maxWidth);
        var width = (int)Math.Ceiling(text.Width) + 3 + Padding.Horizontal + NonClientInsets.Horizontal;
        var height = (int)Math.Ceiling(text.Height) + Padding.Vertical + NonClientInsets.Vertical;
        if (_image is { } image)
        {
            width = Math.Max(width, image.Width + Padding.Horizontal);
            height = Math.Max(height, image.Height + Padding.Vertical);
        }

        return new Size(width, height);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = new Rectangle(Padding.Left, Padding.Top, ClientSize.Width - Padding.Horizontal, ClientSize.Height - Padding.Vertical);
        if (_image is { } image)
        {
            g.DrawImage(image, ThemePainter.Align(bounds, image.Size, _imageAlign));
        }

        var color = Enabled ? ForeColor : Theme.DisabledText;
        var wrap = !AutoSize || MaximumSize.Width > 0;
        ThemePainter.DrawText(g, Text, Font, color, bounds, _textAlign, wrap, _autoEllipsis, UseMnemonic);
    }

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g) => ThemePainter.DrawBorder(g, new Rectangle(Point.Empty, Size), _borderStyle);
}

/// <summary>A label whose text is a link the user can click or tap.</summary>
[DefaultEvent(nameof(LinkClicked))]
public class LinkLabel : Label
{
    private static readonly object EventLinkClicked = new();
    private bool _pressed;

    /// <summary>Creates a link label.</summary>
    public LinkLabel()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        Cursor = Cursors.Hand;
        Links = new LinkCollection(this);
    }

    /// <summary>Occurs when the link is clicked or tapped.</summary>
    public event LinkLabelLinkClickedEventHandler? LinkClicked
    {
        add => Events.AddHandler(EventLinkClicked, value);
        remove => Events.RemoveHandler(EventLinkClicked, value);
    }

    /// <summary>The color of a link.</summary>
    public Color LinkColor { get; set; } = Color.FromArgb(0, 102, 204);

    /// <summary>The color of a visited link.</summary>
    public Color VisitedLinkColor { get; set; } = Color.FromArgb(128, 0, 128);

    /// <summary>The color of a link being clicked.</summary>
    public Color ActiveLinkColor { get; set; } = Color.Red;

    /// <summary>The color of a disabled link.</summary>
    public Color DisabledLinkColor { get; set; } = Color.FromArgb(133, 133, 133);

    /// <summary>Whether the link was visited, which draws it in <see cref="VisitedLinkColor"/>.</summary>
    public bool LinkVisited { get; set; }

    /// <summary>When the link is underlined.</summary>
    public LinkBehavior LinkBehavior { get; set; } = LinkBehavior.SystemDefault;

    /// <summary>The part of the text that is the link: the whole text by default.</summary>
    public LinkArea LinkArea { get; set; } = new(0, -1);

    /// <summary>The links of the label: one, covering <see cref="LinkArea"/>.</summary>
    public LinkCollection Links { get; }

    /// <summary>Raises the LinkClicked event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnLinkClicked(LinkLabelLinkClickedEventArgs e) => ((LinkLabelLinkClickedEventHandler?)Events[EventLinkClicked])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        _pressed = e.Button == MouseButtons.Left;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        var clicked = _pressed && ClientRectangle.Contains(e.Location);
        _pressed = false;
        Invalidate();
        if (clicked)
        {
            OnLinkClicked(new LinkLabelLinkClickedEventArgs(Links[0], e.Button));
        }
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.KeyCode is Keys.Enter or Keys.Space)
        {
            OnLinkClicked(new LinkLabelLinkClickedEventArgs(Links[0], MouseButtons.Left));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => (keyData & Keys.KeyCode) == Keys.Enter || base.IsInputKey(keyData);

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var color = !Enabled ? DisabledLinkColor : _pressed ? ActiveLinkColor : LinkVisited ? VisitedLinkColor : LinkColor;
        var underline = LinkBehavior != LinkBehavior.NeverUnderline;
        using var font = underline ? new Font(Font, Font.Style | FontStyle.Underline) : null;
        var bounds = new Rectangle(Padding.Left, Padding.Top, ClientSize.Width - Padding.Horizontal, ClientSize.Height - Padding.Vertical);
        ThemePainter.DrawText(g, Text, font ?? Font, color, bounds, TextAlign, !AutoSize, AutoEllipsis, UseMnemonic);
        if (Focused && Window?.ShowFocusCues == true)
        {
            ThemePainter.DrawFocusRectangle(g, new Rectangle(Point.Empty, ClientSize));
        }
    }

    /// <summary>A link of a link label.</summary>
    public class Link
    {
        /// <summary>Creates a link.</summary>
        public Link()
        {
        }

        /// <summary>Creates a link of part of the text.</summary>
        /// <param name="start">The first character.</param>
        /// <param name="length">The number of characters.</param>
        public Link(int start, int length)
        {
            Start = start;
            Length = length;
        }

        /// <summary>Creates a link of part of the text, with data.</summary>
        /// <param name="start">The first character.</param>
        /// <param name="length">The number of characters.</param>
        /// <param name="linkData">Data about the link, such as its address.</param>
        public Link(int start, int length, object? linkData)
            : this(start, length) => LinkData = linkData;

        /// <summary>The first character.</summary>
        public int Start { get; set; }

        /// <summary>The number of characters.</summary>
        public int Length { get; set; } = -1;

        /// <summary>Data about the link, such as its address.</summary>
        public object? LinkData { get; set; }

        /// <summary>The link's name.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether the link was visited.</summary>
        public bool Visited { get; set; }

        /// <summary>Whether the link is enabled.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Data about the link.</summary>
        public object? Tag { get; set; }
    }

    /// <summary>The links of a link label.</summary>
    public class LinkCollection : System.Collections.ObjectModel.Collection<Link>
    {
        /// <summary>Creates the links of a label: one link of its whole text.</summary>
        /// <param name="owner">The label.</param>
        public LinkCollection(LinkLabel owner)
        {
            ArgumentNullException.ThrowIfNull(owner);
            Add(new Link(0, -1));
        }

        /// <summary>Adds a link of part of the text.</summary>
        /// <param name="start">The first character.</param>
        /// <param name="length">The number of characters.</param>
        public Link Add(int start, int length) => Add(start, length, null);

        /// <summary>Adds a link of part of the text, with data.</summary>
        /// <param name="start">The first character.</param>
        /// <param name="length">The number of characters.</param>
        /// <param name="linkData">Data about the link.</param>
        public Link Add(int start, int length, object? linkData)
        {
            var link = new Link(start, length, linkData);
            Add(link);
            return link;
        }
    }
}

/// <summary>The part of a link label's text that is a link.</summary>
/// <param name="start">The first character.</param>
/// <param name="length">The number of characters; -1 to the end.</param>
public readonly struct LinkArea(int start, int length) : IEquatable<LinkArea>
{
    /// <summary>The first character.</summary>
    public int Start { get; } = start;

    /// <summary>The number of characters.</summary>
    public int Length { get; } = length;

    /// <summary>Whether the area is empty.</summary>
    public bool IsEmpty => Length == 0 && Start == 0;

    /// <summary>Whether two areas are the same.</summary>
    /// <param name="left">The first area.</param>
    /// <param name="right">The second area.</param>
    public static bool operator ==(LinkArea left, LinkArea right) => left.Equals(right);

    /// <summary>Whether two areas differ.</summary>
    /// <param name="left">The first area.</param>
    /// <param name="right">The second area.</param>
    public static bool operator !=(LinkArea left, LinkArea right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(LinkArea other) => Start == other.Start && Length == other.Length;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is LinkArea other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Start, Length);
}

/// <summary>Data of the LinkClicked event.</summary>
public class LinkLabelLinkClickedEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="link">The link clicked.</param>
    public LinkLabelLinkClickedEventArgs(LinkLabel.Link? link)
        : this(link, MouseButtons.Left)
    {
    }

    /// <summary>Creates the data.</summary>
    /// <param name="link">The link clicked.</param>
    /// <param name="button">The button that clicked it.</param>
    public LinkLabelLinkClickedEventArgs(LinkLabel.Link? link, MouseButtons button)
    {
        Link = link;
        Button = button;
    }

    /// <summary>The link clicked.</summary>
    public LinkLabel.Link? Link { get; }

    /// <summary>The button that clicked it.</summary>
    public MouseButtons Button { get; }
}

/// <summary>Handles a LinkClicked event.</summary>
/// <param name="sender">The link label.</param>
/// <param name="e">The event data.</param>
public delegate void LinkLabelLinkClickedEventHandler(object? sender, LinkLabelLinkClickedEventArgs e);
