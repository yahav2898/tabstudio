using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>A bar that fills as work progresses, or moves a marquee while the amount is unknown.</summary>
[DefaultProperty(nameof(Value))]
public class ProgressBar : Control
{
    private int _minimum;
    private int _maximum = 100;
    private int _value;
    private ProgressBarStyle _style = ProgressBarStyle.Blocks;
    private int _marqueeSpeed = 100;
    private int _marqueeOffset;
    private IDisposable? _marqueeTimer;

    /// <summary>Creates a progress bar.</summary>
    public ProgressBar()
    {
        SetStyle(ControlStyles.Selectable | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick, false);
        TabStop = false;
    }

    /// <summary>The least value.</summary>
    public int Minimum
    {
        get => _minimum;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _minimum = value;
            _maximum = Math.Max(_maximum, value);
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    /// <summary>The greatest value.</summary>
    public int Maximum
    {
        get => _maximum;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maximum = value;
            _minimum = Math.Min(_minimum, value);
            _value = Math.Clamp(_value, _minimum, _maximum);
            Invalidate();
        }
    }

    /// <summary>The value, from <see cref="Minimum"/> to <see cref="Maximum"/>.</summary>
    public int Value
    {
        get => _value;
        set
        {
            if (value < _minimum || value > _maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Value of '{value}' is not valid for 'Value'. 'Value' should be between 'minimum' and 'maximum'.");
            }

            if (_value != value)
            {
                _value = value;
                Invalidate();
            }
        }
    }

    /// <summary>How much <see cref="PerformStep"/> adds.</summary>
    public int Step { get; set; } = 10;

    /// <summary>Whether the bar shows the value or a marquee.</summary>
    public ProgressBarStyle Style
    {
        get => _style;
        set
        {
            _style = value;
            if (value != ProgressBarStyle.Marquee)
            {
                StopMarquee();
            }

            Invalidate();
        }
    }

    /// <summary>How often the marquee moves, in milliseconds; 0 stops it.</summary>
    public int MarqueeAnimationSpeed
    {
        get => _marqueeSpeed;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _marqueeSpeed = value;
            StopMarquee();
            Invalidate();
        }
    }

    /// <summary>Whether the bar fills from the right in right-to-left layouts.</summary>
    public virtual bool RightToLeftLayout { get; set; }

    /// <inheritdoc />
    protected override Size DefaultSize => new(100, 23);

    /// <inheritdoc />
    protected override Padding DefaultMargin => new(3);

    /// <summary>Adds to the value, within the range.</summary>
    /// <param name="value">The amount.</param>
    public void Increment(int value)
    {
        if (_style == ProgressBarStyle.Marquee)
        {
            throw new InvalidOperationException("Increment cannot be called when the ProgressBar style is Marquee.");
        }

        Value = Math.Clamp(_value + value, _minimum, _maximum);
    }

    /// <summary>Adds <see cref="Step"/> to the value, within the range.</summary>
    public void PerformStep()
    {
        if (_style == ProgressBarStyle.Marquee)
        {
            throw new InvalidOperationException("PerformStep cannot be called when the ProgressBar style is Marquee.");
        }

        Increment(Step);
    }

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()}, Minimum: {_minimum}, Maximum: {_maximum}, Value: {_value}";

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = ClientRectangle;
        using (var track = new SolidBrush(Theme.ProgressTrack))
        {
            g.FillRectangle(track, bounds);
        }

        var inner = Rectangle.Inflate(bounds, -1, -1);
        var color = IsForeColorSet ? ForeColor : Theme.ProgressFill;
        using var fill = new SolidBrush(Enabled ? color : Theme.DisabledText);
        if (_style == ProgressBarStyle.Marquee)
        {
            EnsureMarquee();
            var width = Math.Max(20, inner.Width / 4);
            var x = inner.Left - width + (_marqueeOffset % (inner.Width + width));
            var segment = Rectangle.Intersect(inner, new Rectangle(x, inner.Top, width, inner.Height));
            g.FillRectangle(fill, segment);
        }
        else if (_maximum > _minimum)
        {
            var width = (int)((long)inner.Width * (_value - _minimum) / (_maximum - _minimum));
            var x = RightToLeftLayout && RightToLeft == RightToLeft.Yes ? inner.Right - width : inner.Left;
            g.FillRectangle(fill, x, inner.Top, width, inner.Height);
        }

        using var border = new Pen(Theme.ProgressBorder);
        g.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopMarquee();
        }

        base.Dispose(disposing);
    }

    private void EnsureMarquee()
    {
        if (_marqueeTimer is not null || _marqueeSpeed == 0 || Window is not { } window)
        {
            return;
        }

        _marqueeTimer = window.Runtime.StartTimer(Math.Max(16, _marqueeSpeed / 3), () =>
        {
            if (Window is null || _style != ProgressBarStyle.Marquee)
            {
                StopMarquee();
                return;
            }

            _marqueeOffset += 6;
            Invalidate();
        });
    }

    private void StopMarquee()
    {
        _marqueeTimer?.Dispose();
        _marqueeTimer = null;
    }
}
