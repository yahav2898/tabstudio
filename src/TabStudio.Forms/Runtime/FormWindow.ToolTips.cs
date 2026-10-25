using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Rendering;

namespace TabStudio.Forms.Runtime;

/// <summary>Tooltips: the text a <see cref="ToolTip"/> gave a control, shown when the pointer rests on it.</summary>
internal sealed partial class FormWindow
{
    private IDisposable? _toolTipTimer;
    private (Control Control, string Text, Point At)? _toolTip;

    /// <summary>Starts waiting to show a control's tooltip.</summary>
    private void ScheduleToolTip(Control control)
    {
        _toolTipTimer?.Dispose();
        _toolTipTimer = null;
        if (FindToolTip(control) is not { } entry)
        {
            return;
        }

        _toolTipTimer = Runtime.StartTimer(entry.Delay, () =>
        {
            _toolTipTimer?.Dispose();
            _toolTipTimer = null;
            if (_hovered == control && _popups.Count == 0 && FindToolTip(control) is { Active: true } current)
            {
                _toolTip = (control, current.Text, new Point(_lastPoint.X, _lastPoint.Y + 20));
                Invalidate();
            }
        });
    }

    /// <summary>Waits again to show a control's tooltip, because the part under the pointer changed, such as a strip's item.</summary>
    /// <param name="control">The control.</param>
    public void RestartToolTip(Control control)
    {
        if (_hovered == control)
        {
            HideToolTip();
            ScheduleToolTip(control);
        }
    }

    /// <summary>The tooltip of a control: a ToolTip's text for it, or the tooltip of a strip's item under the pointer.</summary>
    private static (string Text, int Delay, bool Active)? FindToolTip(Control control) =>
        control is ToolStrip strip
            ? strip.HotItemToolTip() is { } text ? (text, 500, true) : null
            : ToolTip.Find(control);

    /// <summary>Hides the tooltip.</summary>
    private void HideToolTip()
    {
        _toolTipTimer?.Dispose();
        _toolTipTimer = null;
        if (_toolTip is not null)
        {
            _toolTip = null;
            Invalidate();
        }
    }

    /// <summary>Hides a tooltip whose control is gone or whose text changed.</summary>
    private void UpdateToolTip()
    {
        if (_toolTip is { } shown && (shown.Control.Window != this || FindToolTip(shown.Control) is not { Active: true } current || current.Text != shown.Text))
        {
            HideToolTip();
        }
    }

    /// <summary>The tooltip as the frame shows it.</summary>
    private ToolTipFrame? RenderToolTip()
    {
        if (_toolTip is not { } shown)
        {
            return null;
        }

        var font = SystemFonts.StatusFont;
        var size = TextMetrics.Measure(shown.Text, font.Spec, 400);
        var bounds = new Rectangle(shown.At.X, shown.At.Y, (int)Math.Ceiling(size.Width) + 12, (int)Math.Ceiling(size.Height) + 8);
        var builder = new DisplayListBuilder();
        var g = new Graphics(builder, bounds.Location, bounds);
        using (var background = new SolidBrush(Theme.ToolTipBackground))
        {
            g.FillRectangle(background, 0, 0, bounds.Width, bounds.Height);
        }

        using (var border = new Pen(Theme.ToolTipBorder))
        {
            g.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
        }

        g.DrawText(shown.Text, font, Color.FromArgb(87, 87, 87), new RectangleF(6, 4, bounds.Width - 12, bounds.Height - 8), new TextOptions(StringAlignment.Near, StringAlignment.Center, Wrap: true, Ellipsis: false, Clip: false));
        return new ToolTipFrame(shown.Text, bounds, builder.Build());
    }
}
