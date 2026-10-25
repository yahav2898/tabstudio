using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Rendering;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// The calendar a date picker opens: a tap on a day picks it and closes the calendar, the arrows change the month,
/// and the arrow keys, Enter and Escape work as in Windows.
/// </summary>
internal sealed class DatePickerPopup : Popup
{
    private readonly DateTimePicker _picker;
    private readonly CalendarView _view = new();
    private readonly DateTime _original;

    /// <summary>Creates the calendar of a date picker.</summary>
    /// <param name="picker">The picker.</param>
    public DatePickerPopup(DateTimePicker picker)
    {
        _picker = picker;
        _original = picker.Value;
        _view.MinDate = picker.MinDate;
        _view.MaxDate = picker.MaxDate;
        Select(picker.Value.Date);
    }

    /// <inheritdoc />
    public override Control Owner => _picker;

    /// <inheritdoc />
    public override void Paint(Graphics g)
    {
        var bounds = new Rectangle(Point.Empty, Bounds.Size);
        _view.Paint(g, Rectangle.Inflate(bounds, -1, -1), _picker.CalendarFont ?? _picker.Font, enabled: true);
        using var border = new Pen(Theme.MenuBorder);
        g.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    /// <inheritdoc />
    public override void PointerMove(Point point, bool pressed)
    {
        var (part, day) = _view.HitTest(Inner, point);
        DateTime? hot = part == CalendarView.Part.Day ? day : null;
        if (hot != _view.HotDay)
        {
            _view.HotDay = hot;
            Invalidate();
        }
    }

    /// <inheritdoc />
    public override void PointerUp(Point point)
    {
        var (part, day) = _view.HitTest(Inner, point);
        switch (part)
        {
            case CalendarView.Part.Previous:
                _view.Month = _view.Month.AddMonths(-1);
                Invalidate();
                break;
            case CalendarView.Part.Next:
                _view.Month = _view.Month.AddMonths(1);
                Invalidate();
                break;
            case CalendarView.Part.Day or CalendarView.Part.Today:
                Close();
                _picker.SetDateFromCalendar(day);
                break;
        }
    }

    /// <inheritdoc />
    public override bool PointerDownOutside(Point windowPoint)
    {
        if (_picker.BoundsInWindow().Contains(windowPoint))
        {
            Close();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override bool KeyDown(Keys keyData)
    {
        int? days = (keyData & Keys.KeyCode) switch
        {
            Keys.Left => -1,
            Keys.Right => 1,
            Keys.Up when (keyData & Keys.Alt) == 0 => -7,
            Keys.Down when (keyData & Keys.Alt) == 0 => 7,
            _ => null,
        };

        if (days is { } step)
        {
            var day = _view.SelectionStart.AddDays(step);
            if (day >= _view.MinDate.Date && day <= _view.MaxDate.Date)
            {
                Select(day);
                _picker.SetDateFromCalendar(day);
            }

            return true;
        }

        switch (keyData & Keys.KeyCode)
        {
            case Keys.PageUp or Keys.PageDown:
                _view.Month = _view.Month.AddMonths((keyData & Keys.KeyCode) == Keys.PageUp ? -1 : 1);
                Invalidate();
                return true;
            case Keys.Enter or Keys.F4 or Keys.Up or Keys.Down:
                Close();
                return true;
            case Keys.Escape:
                Close();
                if (_picker.Value != _original)
                {
                    _picker.Value = _original;
                }

                return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override void OnClosed() => _picker.OnCalendarClosed();

    private Rectangle Inner => Rectangle.Inflate(new Rectangle(Point.Empty, Bounds.Size), -1, -1);

    private void Select(DateTime day)
    {
        _view.SelectionStart = day;
        _view.SelectionEnd = day;
        _view.Month = new DateTime(day.Year, day.Month, 1);
        Invalidate();
    }
}
