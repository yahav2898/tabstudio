using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A slider: a thumb dragged along a track to pick a value, with tick marks.</summary>
[DefaultEvent(nameof(Scroll))]
[DefaultProperty(nameof(Value))]
public class TrackBar : Control, ISupportInitialize
{
    private static readonly object EventScroll = new();
    private static readonly object EventValueChanged = new();

    /// <summary>The thumb's width across the track.</summary>
    private const int ThumbThickness = 11;

    /// <summary>The thumb's length.</summary>
    private const int ThumbLength = 21;

    /// <summary>The space at each end of the track.</summary>
    private const int TrackInset = 13;

    private int _minimum;
    private int _maximum = 10;
    private int _value;
    private Orientation _orientation = Orientation.Horizontal;
    private TickStyle _tickStyle = TickStyle.BottomRight;
    private bool _dragging;
    private bool _hot;
    private int _dragOffset;
    private bool _initializing;

    /// <summary>Creates a track bar.</summary>
    public TrackBar()
    {
        SetStyle(ControlStyles.StandardDoubleClick, false);
        AutoSize = true;
    }

    /// <summary>Occurs when the user moves the thumb.</summary>
    public event EventHandler? Scroll
    {
        add => Events.AddHandler(EventScroll, value);
        remove => Events.RemoveHandler(EventScroll, value);
    }

    /// <summary>Occurs when the value changes.</summary>
    public event EventHandler? ValueChanged
    {
        add => Events.AddHandler(EventValueChanged, value);
        remove => Events.RemoveHandler(EventValueChanged, value);
    }

    /// <summary>The least value.</summary>
    public int Minimum
    {
        get => _minimum;
        set => SetRange(value, Math.Max(value, _maximum));
    }

    /// <summary>The greatest value.</summary>
    public int Maximum
    {
        get => _maximum;
        set => SetRange(Math.Min(_minimum, value), value);
    }

    /// <summary>The value, from <see cref="Minimum"/> to <see cref="Maximum"/>.</summary>
    public int Value
    {
        get => _value;
        set
        {
            if (!_initializing && (value < _minimum || value > _maximum))
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Value of '{value}' is not valid for 'Value'. 'Value' should be between 'Minimum' and 'Maximum'.");
            }

            if (_value != value)
            {
                _value = value;
                Invalidate();
                OnValueChanged(EventArgs.Empty);
            }
        }
    }

    /// <summary>How far the arrow keys move the thumb.</summary>
    public int SmallChange { get; set; } = 1;

    /// <summary>How far Page Up, Page Down and a tap on the track move the thumb.</summary>
    public int LargeChange { get; set; } = 5;

    /// <summary>The value between tick marks.</summary>
    public int TickFrequency { get; set; } = 1;

    /// <summary>Where the tick marks are.</summary>
    public TickStyle TickStyle
    {
        get => _tickStyle;
        set
        {
            _tickStyle = value;
            Invalidate();
        }
    }

    /// <summary>Whether the track runs across or down.</summary>
    public Orientation Orientation
    {
        get => _orientation;
        set
        {
            if (_orientation == value)
            {
                return;
            }

            _orientation = value;
            SetBoundsCore(Left, Top, Height, Width, BoundsSpecified.Size);
            Invalidate();
        }
    }

    /// <summary>Whether the bar fills from the right in right-to-left layouts. Kept for compatibility.</summary>
    public virtual bool RightToLeftLayout { get; set; }

    /// <inheritdoc />
    protected override Size DefaultSize => new(104, 45);

    /// <summary>Sets the least and greatest values together.</summary>
    /// <param name="minValue">The least value.</param>
    /// <param name="maxValue">The greatest value.</param>
    public void SetRange(int minValue, int maxValue)
    {
        _minimum = minValue;
        _maximum = Math.Max(minValue, maxValue);
        if (!_initializing)
        {
            Value = Math.Clamp(_value, _minimum, _maximum);
        }

        Invalidate();
    }

    /// <inheritdoc />
    public void BeginInit() => _initializing = true;

    /// <inheritdoc />
    public void EndInit()
    {
        _initializing = false;
        Value = Math.Clamp(_value, _minimum, _maximum);
    }

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()}, Minimum: {_minimum}, Maximum: {_maximum}, Value: {_value}";

    /// <summary>Raises Scroll.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnScroll(EventArgs e) => ((EventHandler?)Events[EventScroll])?.Invoke(this, e);

    /// <summary>Raises ValueChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnValueChanged(EventArgs e) => ((EventHandler?)Events[EventValueChanged])?.Invoke(this, e);

    /// <summary>Keeps the thickness at the standard size when sized automatically.</summary>
    /// <inheritdoc />
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (AutoSize)
        {
            if (_orientation == Orientation.Horizontal)
            {
                height = DefaultSize.Height;
            }
            else
            {
                width = DefaultSize.Height;
            }
        }

        base.SetBoundsCore(x, y, width, height, specified);
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        int? target = e.KeyCode switch
        {
            Keys.Left or Keys.Down => _value - SmallChange,
            Keys.Right or Keys.Up => _value + SmallChange,
            Keys.PageUp => _orientation == Orientation.Horizontal ? _value + LargeChange : _value - LargeChange,
            Keys.PageDown => _orientation == Orientation.Horizontal ? _value - LargeChange : _value + LargeChange,
            Keys.Home => _minimum,
            Keys.End => _maximum,
            _ => null,
        };

        if (target is { } value)
        {
            MoveTo(value);
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        Focus();
        var thumb = ThumbBounds();
        if (Rectangle.Inflate(thumb, 6, 6).Contains(e.Location))
        {
            _dragging = true;
            _dragOffset = _orientation == Orientation.Horizontal ? e.X - (thumb.X + (thumb.Width / 2)) : e.Y - (thumb.Y + (thumb.Height / 2));
            Capture = true;
            Invalidate();
            return;
        }

        var along = _orientation == Orientation.Horizontal ? e.X : e.Y;
        var center = _orientation == Orientation.Horizontal ? thumb.X + (thumb.Width / 2) : thumb.Y + (thumb.Height / 2);
        var increasing = _orientation == Orientation.Horizontal ? along > center : along < center;
        MoveTo(_value + (increasing ? LargeChange : -LargeChange));
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_dragging)
        {
            MoveTo(ValueAt(_orientation == Orientation.Horizontal ? e.X - _dragOffset : e.Y - _dragOffset));
            return;
        }

        var hot = ThumbBounds().Contains(e.Location);
        if (hot != _hot)
        {
            _hot = hot;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (_dragging)
        {
            _dragging = false;
            Capture = false;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = false;
        Invalidate();
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var horizontal = _orientation == Orientation.Horizontal;
        var length = (horizontal ? Width : Height) - (2 * TrackInset);
        var thumb = ThumbBounds();
        var track = horizontal
            ? new Rectangle(TrackInset - 5, thumb.Y + (thumb.Height / 2) - 2 - (_tickStyle == TickStyle.Both ? 0 : 3), length + 10, 4)
            : new Rectangle(thumb.X + (thumb.Width / 2) - 2 - (_tickStyle == TickStyle.Both ? 0 : 3), TrackInset - 5, 4, length + 10);
        using (var trackFill = new SolidBrush(Theme.TrackBarTrack))
        using (var trackBorder = new Pen(Theme.TrackBarTrackBorder))
        {
            g.FillRectangle(trackFill, track);
            g.DrawRectangle(trackBorder, track.X, track.Y, track.Width - 1, track.Height - 1);
        }

        PaintTicks(g, thumb, horizontal, length);
        var color = !Enabled ? Theme.ButtonDisabledFace : _dragging ? Color.FromArgb(204, 204, 204) : _hot ? Color.FromArgb(23, 23, 23) : Theme.Accent;
        using (var thumbFill = new SolidBrush(color))
        {
            g.FillRectangle(thumbFill, thumb);
        }

        if (Focused && Window?.ShowFocusCues == true)
        {
            ThemePainter.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -1, -1));
        }
    }

    private void PaintTicks(Graphics g, Rectangle thumb, bool horizontal, int length)
    {
        if (_tickStyle == TickStyle.None || _maximum <= _minimum || TickFrequency <= 0)
        {
            return;
        }

        using var pen = new Pen(Theme.TrackBarTick);
        for (var value = _minimum; value <= _maximum; value += TickFrequency)
        {
            var at = TrackInset + (int)((long)length * (value - _minimum) / (_maximum - _minimum));
            DrawTick(g, pen, at, thumb, horizontal);
        }

        // The last value always has a tick, as in Windows.
        DrawTick(g, pen, TrackInset + length, thumb, horizontal);
    }

    private void DrawTick(Graphics g, Pen pen, int at, Rectangle thumb, bool horizontal)
    {
        if ((_tickStyle & TickStyle.BottomRight) != 0)
        {
            if (horizontal)
            {
                g.DrawLine(pen, at, thumb.Bottom + 3, at, thumb.Bottom + 6);
            }
            else
            {
                g.DrawLine(pen, thumb.Right + 3, at, thumb.Right + 6, at);
            }
        }

        if ((_tickStyle & TickStyle.TopLeft) != 0)
        {
            if (horizontal)
            {
                g.DrawLine(pen, at, thumb.Top - 6, at, thumb.Top - 3);
            }
            else
            {
                g.DrawLine(pen, thumb.Left - 6, at, thumb.Left - 3, at);
            }
        }
    }

    private Rectangle ThumbBounds()
    {
        var horizontal = _orientation == Orientation.Horizontal;
        var length = (horizontal ? Width : Height) - (2 * TrackInset);
        var fraction = _maximum > _minimum ? (double)(_value - _minimum) / (_maximum - _minimum) : 0;
        var center = TrackInset + (int)Math.Round(length * (horizontal ? fraction : 1 - fraction));
        var cross = _tickStyle == TickStyle.Both ? 12 : _tickStyle == TickStyle.TopLeft ? 12 : 4;
        return horizontal
            ? new Rectangle(center - (ThumbThickness / 2), cross, ThumbThickness, ThumbLength)
            : new Rectangle(cross, center - (ThumbThickness / 2), ThumbLength, ThumbThickness);
    }

    private int ValueAt(int along)
    {
        var horizontal = _orientation == Orientation.Horizontal;
        var length = (horizontal ? Width : Height) - (2 * TrackInset);
        if (length <= 0)
        {
            return _value;
        }

        var fraction = Math.Clamp((double)(along - TrackInset) / length, 0, 1);
        if (!horizontal)
        {
            fraction = 1 - fraction;
        }

        return _minimum + (int)Math.Round(fraction * (_maximum - _minimum));
    }

    private void MoveTo(int value)
    {
        value = Math.Clamp(value, _minimum, _maximum);
        if (value == _value)
        {
            return;
        }

        Value = value;
        OnScroll(EventArgs.Empty);
    }
}
