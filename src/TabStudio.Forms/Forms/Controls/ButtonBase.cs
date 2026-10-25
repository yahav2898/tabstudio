using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>
/// The base of buttons, check boxes and radio buttons: pressing with the mouse, a finger, Space or Enter, the
/// hot and pressed looks, text and image alignment.
/// </summary>
public abstract class ButtonBase : Control
{
    private FlatStyle _flatStyle = FlatStyle.Standard;
    private ContentAlignment _textAlign = ContentAlignment.MiddleCenter;
    private Image? _image;
    private ContentAlignment _imageAlign = ContentAlignment.MiddleCenter;
    private bool _hot;
    private bool _pointerPressed;
    private bool _keyPressed;
    private bool _isDefault;

    /// <summary>Creates a button.</summary>
    protected ButtonBase()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.Opaque | ControlStyles.ResizeRedraw, true);
        FlatAppearance = new FlatButtonAppearance(this);
    }

    /// <summary>The look: standard visual style, flat, or popup.</summary>
    [DefaultValue(FlatStyle.Standard)]
    public FlatStyle FlatStyle
    {
        get => _flatStyle;
        set
        {
            _flatStyle = value;
            Invalidate();
        }
    }

    /// <summary>The colors and border of a flat button.</summary>
    public FlatButtonAppearance FlatAppearance { get; }

    /// <summary>Where the text sits in the button.</summary>
    [DefaultValue(ContentAlignment.MiddleCenter)]
    public virtual ContentAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            _textAlign = value;
            Invalidate();
        }
    }

    /// <summary>An image drawn on the button.</summary>
    public Image? Image
    {
        get => _image;
        set
        {
            _image = value;
            Invalidate();
        }
    }

    /// <summary>Where the image sits in the button.</summary>
    [DefaultValue(ContentAlignment.MiddleCenter)]
    public ContentAlignment ImageAlign
    {
        get => _imageAlign;
        set
        {
            _imageAlign = value;
            Invalidate();
        }
    }

    /// <summary>Where the text is relative to the image.</summary>
    public TextImageRelation TextImageRelation { get; set; } = TextImageRelation.Overlay;

    /// <summary>Whether an ampersand marks an access key.</summary>
    [DefaultValue(true)]
    public bool UseMnemonic { get; set; } = true;

    /// <summary>Whether the button is drawn with the visual style's face rather than its BackColor.</summary>
    [DefaultValue(true)]
    public bool UseVisualStyleBackColor { get; set; } = true;

    /// <summary>Whether text is drawn with GDI+. Kept for compatibility.</summary>
    public bool UseCompatibleTextRendering { get; set; }

    /// <summary>Whether text that does not fit ends with an ellipsis.</summary>
    public bool AutoEllipsis { get; set; }

    /// <summary>How the button sizes itself when AutoSize is on.</summary>
    public AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    /// <summary>Whether the button is its form's default button, which Enter presses.</summary>
    protected internal bool IsDefault
    {
        get => _isDefault;
        set
        {
            _isDefault = value;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(75, 23);

    /// <summary>The button's look now.</summary>
    internal PushState PushState => !Enabled
        ? PushState.Disabled
        : (_pointerPressed && _hot) || _keyPressed ? PushState.Pressed : _hot ? PushState.Hot : PushState.Normal;

    /// <summary>Whether the face is drawn with the BackColor the app set.</summary>
    internal bool UsesCustomFace => !UseVisualStyleBackColor || _flatStyle is FlatStyle.Flat or FlatStyle.Popup;

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        _hot = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        _hot = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _pointerPressed = true;
            _hot = true;
            Invalidate();
        }

        base.OnMouseDown(e);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_pointerPressed)
        {
            var inside = ClientRectangle.Contains(e.Location);
            if (inside != _hot)
            {
                _hot = inside;
                Invalidate();
            }
        }

        base.OnMouseMove(e);
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        var clicked = _pointerPressed && e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location);
        _pointerPressed = false;
        Invalidate();
        if (clicked)
        {
            OnClick(e);
            OnMouseClick(e);
        }

        base.OnMouseUp(e);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.KeyCode == Keys.Space && e.Modifiers == Keys.None)
        {
            _keyPressed = true;
            Invalidate();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (_keyPressed && e.KeyCode == Keys.Space)
        {
            _keyPressed = false;
            Invalidate();
            OnClick(EventArgs.Empty);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        _keyPressed = false;
        base.OnLostFocus(e);
    }

    /// <inheritdoc />
    internal override Size GetPreferredSizeCore(Size proposedSize)
    {
        var text = TextMetrics.Measure(Graphics.Prefixed(Text, System.Drawing.Text.HotkeyPrefix.Hide), Font.Spec);
        var width = (int)Math.Ceiling(text.Width) + 16 + Padding.Horizontal;
        var height = (int)Math.Ceiling(text.Height) + 8 + Padding.Vertical;
        if (_image is { } image)
        {
            if (TextImageRelation is TextImageRelation.ImageBeforeText or TextImageRelation.TextBeforeImage)
            {
                width += image.Width + 4;
                height = Math.Max(height, image.Height + 8);
            }
            else if (TextImageRelation is TextImageRelation.ImageAboveText or TextImageRelation.TextAboveImage)
            {
                height += image.Height + 4;
                width = Math.Max(width, image.Width + 16);
            }
            else
            {
                width = Math.Max(width, image.Width + 8);
                height = Math.Max(height, image.Height + 8);
            }
        }

        var preferred = new Size(width, Math.Max(height, 23));
        return AutoSizeMode == AutoSizeMode.GrowOnly ? new Size(Math.Max(preferred.Width, Width), Math.Max(preferred.Height, Height)) : preferred;
    }

    /// <summary>Draws the text and image of the button in an area, as their relation and alignments say.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="area">The area.</param>
    /// <param name="color">The text color.</param>
    internal void PaintTextAndImage(Graphics g, Rectangle area, Color color)
    {
        var text = Text;
        if (_image is { } image)
        {
            var imageBounds = ThemePainter.Align(area, image.Size, _imageAlign);
            switch (TextImageRelation)
            {
                case TextImageRelation.ImageBeforeText:
                    imageBounds = ThemePainter.Align(area, image.Size, ContentAlignment.MiddleLeft);
                    area = Rectangle.FromLTRB(imageBounds.Right + 4, area.Top, area.Right, area.Bottom);
                    break;
                case TextImageRelation.TextBeforeImage:
                    imageBounds = ThemePainter.Align(area, image.Size, ContentAlignment.MiddleRight);
                    area = Rectangle.FromLTRB(area.Left, area.Top, imageBounds.Left - 4, area.Bottom);
                    break;
                case TextImageRelation.ImageAboveText:
                    imageBounds = ThemePainter.Align(area, image.Size, ContentAlignment.TopCenter);
                    area = Rectangle.FromLTRB(area.Left, imageBounds.Bottom + 2, area.Right, area.Bottom);
                    break;
                case TextImageRelation.TextAboveImage:
                    imageBounds = ThemePainter.Align(area, image.Size, ContentAlignment.BottomCenter);
                    area = Rectangle.FromLTRB(area.Left, area.Top, area.Right, imageBounds.Top - 2);
                    break;
            }

            g.DrawImage(image, imageBounds);
        }

        ThemePainter.DrawText(g, text, Font, color, area, _textAlign, wrap: true, AutoEllipsis, UseMnemonic);
    }
}

/// <summary>The colors and border of a flat button.</summary>
public class FlatButtonAppearance
{
    private readonly ButtonBase _owner;
    private Color _borderColor = Color.Empty;
    private int _borderSize = 1;

    internal FlatButtonAppearance(ButtonBase owner) => _owner = owner;

    /// <summary>The border color; the text color when empty.</summary>
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            _borderColor = value;
            _owner.Invalidate();
        }
    }

    /// <summary>The border width.</summary>
    public int BorderSize
    {
        get => _borderSize;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _borderSize = value;
            _owner.Invalidate();
        }
    }

    /// <summary>The face color under the pointer; derived from BackColor when empty.</summary>
    public Color MouseOverBackColor { get; set; } = Color.Empty;

    /// <summary>The face color when pressed; derived from BackColor when empty.</summary>
    public Color MouseDownBackColor { get; set; } = Color.Empty;

    /// <summary>The face color of a checked flat check box or radio button.</summary>
    public Color CheckedBackColor { get; set; } = Color.Empty;
}
