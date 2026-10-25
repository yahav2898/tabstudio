using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// A box for a date or a time. A tap on its button opens a calendar; a tap on a part of the date, such as the month,
/// selects it, for the arrow keys, the digit keys or the up-down buttons to change.
/// </summary>
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
public class DateTimePicker : Control
{
    /// <summary>The least date a picker takes, as in Windows.</summary>
    public static readonly DateTime MinimumDateTime = new(1753, 1, 1);

    /// <summary>The greatest date a picker takes, as in Windows.</summary>
    public static readonly DateTime MaximumDateTime = new(9998, 12, 31);

    private static readonly object EventValueChanged = new();
    private static readonly object EventDropDown = new();
    private static readonly object EventCloseUp = new();
    private static readonly object EventFormatChanged = new();

    /// <summary>The width of the drop-down or up-down buttons.</summary>
    private const int ButtonWidth = 34;

    private DateTime _value = DateTime.Now;
    private DateTime _minDate = MinimumDateTime;
    private DateTime _maxDate = MaximumDateTime;
    private DateTimePickerFormat _format = DateTimePickerFormat.Long;
    private string? _customFormat;
    private bool _checked = true;
    private int _field = -1;
    private string _typed = string.Empty;
    private bool _hot;
    private DatePickerPopup? _popup;

    /// <summary>Creates a date and time picker.</summary>
    public DateTimePicker()
    {
        SetStyle(ControlStyles.StandardDoubleClick, false);
    }

    /// <summary>Occurs when the value changes.</summary>
    public event EventHandler? ValueChanged
    {
        add => Events.AddHandler(EventValueChanged, value);
        remove => Events.RemoveHandler(EventValueChanged, value);
    }

    /// <summary>Occurs when the calendar opens.</summary>
    public event EventHandler? DropDown
    {
        add => Events.AddHandler(EventDropDown, value);
        remove => Events.RemoveHandler(EventDropDown, value);
    }

    /// <summary>Occurs when the calendar closes.</summary>
    public event EventHandler? CloseUp
    {
        add => Events.AddHandler(EventCloseUp, value);
        remove => Events.RemoveHandler(EventCloseUp, value);
    }

    /// <summary>Occurs when <see cref="Format"/> changes.</summary>
    public event EventHandler? FormatChanged
    {
        add => Events.AddHandler(EventFormatChanged, value);
        remove => Events.RemoveHandler(EventFormatChanged, value);
    }

    /// <summary>The date and time.</summary>
    public DateTime Value
    {
        get => _value;
        set
        {
            if (value < _minDate || value > _maxDate)
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Value of '{value}' is not valid for 'Value'. 'Value' should be between 'MinDate' and 'MaxDate'.");
            }

            if (_value == value)
            {
                return;
            }

            _value = value;
            Invalidate();
            OnValueChanged(EventArgs.Empty);
        }
    }

    /// <summary>The least date.</summary>
    public DateTime MinDate
    {
        get => _minDate;
        set
        {
            _minDate = value < MinimumDateTime ? MinimumDateTime : value;
            if (_value < _minDate)
            {
                Value = _minDate;
            }
        }
    }

    /// <summary>The greatest date.</summary>
    public DateTime MaxDate
    {
        get => _maxDate;
        set
        {
            _maxDate = value > MaximumDateTime ? MaximumDateTime : value;
            if (_value > _maxDate)
            {
                Value = _maxDate;
            }
        }
    }

    /// <summary>How the value is shown.</summary>
    public DateTimePickerFormat Format
    {
        get => _format;
        set
        {
            if (_format == value)
            {
                return;
            }

            _format = value;
            _field = -1;
            Invalidate();
            ((EventHandler?)Events[EventFormatChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The format of <see cref="DateTimePickerFormat.Custom"/>, such as "dd/MM/yyyy HH:mm".</summary>
    public string? CustomFormat
    {
        get => _customFormat;
        set
        {
            _customFormat = value;
            _field = -1;
            Invalidate();
        }
    }

    /// <summary>Whether a check box shows, which turns the value on and off.</summary>
    public bool ShowCheckBox { get; set; }

    /// <summary>Whether the value is on, when <see cref="ShowCheckBox"/> is.</summary>
    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked != value)
            {
                _checked = value;
                Invalidate();
                OnValueChanged(EventArgs.Empty);
            }
        }
    }

    /// <summary>Whether up-down buttons show instead of the calendar's button.</summary>
    public bool ShowUpDown { get; set; }

    /// <summary>The text: the value as shown.</summary>
    [AllowNull]
    public override string Text
    {
        get => FormatValue();
        set
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            Value = DateTime.Parse(value, CultureInfo.CurrentCulture);
        }
    }

    /// <summary>The background of the calendar. Kept for compatibility.</summary>
    public Color CalendarMonthBackground { get; set; } = SystemColors.Window;

    /// <summary>The text color of the calendar. Kept for compatibility.</summary>
    public Color CalendarForeColor { get; set; } = SystemColors.WindowText;

    /// <summary>The font of the calendar. Kept for compatibility.</summary>
    public Font? CalendarFont { get; set; }

    /// <summary>The height of the box in its font.</summary>
    public int PreferredHeight => Font.Height + 8;

    /// <summary>Whether the calendar is open.</summary>
    internal bool DroppedDown => _popup is not null;

    /// <inheritdoc />
    protected override Size DefaultSize => new(200, 23);

    private Rectangle ButtonArea => new(Width - ButtonWidth / 2 - 2, 1, (ButtonWidth / 2) + 1, Height - 2);

    private Rectangle CheckArea => ShowCheckBox ? new Rectangle(4, (Height - ThemePainter.GlyphSize) / 2, ThemePainter.GlyphSize, ThemePainter.GlyphSize) : Rectangle.Empty;

    private int TextLeft => ShowCheckBox ? ThemePainter.GlyphSize + 8 : 4;

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()}, Value: {FormatValue()}";

    /// <summary>Raises ValueChanged.</summary>
    /// <param name="eventargs">The event data.</param>
    protected virtual void OnValueChanged(EventArgs eventargs) => ((EventHandler?)Events[EventValueChanged])?.Invoke(this, eventargs);

    /// <summary>Raises DropDown.</summary>
    /// <param name="eventargs">The event data.</param>
    protected virtual void OnDropDown(EventArgs eventargs) => ((EventHandler?)Events[EventDropDown])?.Invoke(this, eventargs);

    /// <summary>Raises CloseUp.</summary>
    /// <param name="eventargs">The event data.</param>
    protected virtual void OnCloseUp(EventArgs eventargs) => ((EventHandler?)Events[EventCloseUp])?.Invoke(this, eventargs);

    /// <summary>Keeps the height at the font's.</summary>
    /// <inheritdoc />
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified) =>
        base.SetBoundsCore(x, y, width, PreferredHeight, specified);

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Height = PreferredHeight;
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End or Keys.F4 || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        var segments = Segments();
        var fields = segments.Where(s => s.Kind != FieldKind.Literal).ToList();
        if (e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
        {
            OpenCalendar();
            e.Handled = true;
            return;
        }

        if (fields.Count == 0)
        {
            return;
        }

        var current = Math.Max(0, fields.FindIndex(f => f.Index == _field));
        switch (e.KeyCode)
        {
            case Keys.Left:
                SelectField(fields[Math.Max(0, current - 1)].Index);
                break;
            case Keys.Right:
                SelectField(fields[Math.Min(fields.Count - 1, current + 1)].Index);
                break;
            case Keys.Up:
                Step(fields[current], +1);
                break;
            case Keys.Down:
                Step(fields[current], -1);
                break;
            case Keys.Space when ShowCheckBox:
                Checked = !Checked;
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || !char.IsDigit(e.KeyChar))
        {
            return;
        }

        var fields = Segments().Where(s => s.Kind != FieldKind.Literal).ToList();
        if (fields.Count == 0)
        {
            return;
        }

        var field = fields.Find(f => f.Index == _field) ?? fields[0];
        _field = field.Index;
        _typed += e.KeyChar;
        var number = int.Parse(_typed, CultureInfo.InvariantCulture);
        var (max, digits) = field.Kind switch
        {
            FieldKind.Year => (9998, 4),
            FieldKind.Month => (12, 2),
            FieldKind.Day => (DateTime.DaysInMonth(_value.Year, _value.Month), 2),
            FieldKind.Hour => (field.Token[0] == 'H' ? 23 : 12, 2),
            FieldKind.Minute or FieldKind.Second => (59, 2),
            _ => (0, 0),
        };

        if (digits > 0 && (field.Kind != FieldKind.Year || _typed.Length == 4))
        {
            SetField(field.Kind, Math.Clamp(number, field.Kind is FieldKind.Hour && field.Token[0] == 'H' || field.Kind is FieldKind.Minute or FieldKind.Second ? 0 : 1, max), field.Token);
        }

        if (_typed.Length >= digits || number * 10 > max)
        {
            _typed = string.Empty;
            var next = fields.IndexOf(field) + 1;
            if (next < fields.Count)
            {
                _field = fields[next].Index;
            }
        }

        Invalidate();
        e.Handled = true;
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
        if (ShowCheckBox && Rectangle.Inflate(CheckArea, 3, 3).Contains(e.Location))
        {
            Checked = !Checked;
            return;
        }

        if (ShowUpDown && e.X >= Width - (ButtonWidth / 2) - 2)
        {
            var fields = Segments().Where(s => s.Kind != FieldKind.Literal).ToList();
            if (fields.Count > 0)
            {
                var field = fields.Find(f => f.Index == _field) ?? fields[0];
                _field = field.Index;
                Step(field, e.Y < Height / 2 ? +1 : -1);
            }

            return;
        }

        if (!ShowUpDown && e.X >= Width - (ButtonWidth / 2) - 2)
        {
            if (DroppedDown)
            {
                _popup?.Close();
            }
            else
            {
                OpenCalendar();
            }

            return;
        }

        if (FieldAt(e.X) is { } index)
        {
            SelectField(index);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hot = true;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hot = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        _typed = string.Empty;
        _popup?.Close();
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        if (_field < 0 && Segments().FirstOrDefault(s => s.Kind != FieldKind.Literal) is { } first)
        {
            _field = first.Index;
        }

        Invalidate();
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = ClientRectangle;
        using (var background = new SolidBrush(Enabled ? SystemColors.Window : SystemColors.Control))
        {
            g.FillRectangle(background, bounds);
        }

        ThemePainter.DrawEditBorder(g, bounds, BorderStyle.Fixed3D, Focused || _hot || DroppedDown, Enabled);
        if (ShowCheckBox)
        {
            ThemePainter.DrawCheckBox(g, CheckArea, _checked ? CheckState.Checked : CheckState.Unchecked, Enabled ? PushState.Normal : PushState.Disabled);
        }

        var color = Enabled && (_checked || !ShowCheckBox) ? ForeColor : Theme.DisabledText;
        var x = (float)TextLeft;
        var textArea = new Rectangle(TextLeft, 0, Math.Max(0, Width - TextLeft - ButtonWidth), Height);
        var state = g.Save();
        g.IntersectClip(textArea);
        foreach (var segment in Segments())
        {
            var width = (float)TextMetrics.Measure(segment.Text, Font.Spec).Width;
            var selected = Focused && segment.Index == _field && segment.Kind != FieldKind.Literal && (_checked || !ShowCheckBox);
            if (selected)
            {
                using var highlight = new SolidBrush(Theme.Selection);
                g.FillRectangle(highlight, x, 3, width, Height - 6);
            }

            g.DrawText(segment.Text, Font, selected ? Theme.SelectionText : color, new RectangleF(x, 0, width + 2, Height), new TextOptions(StringAlignment.Near, StringAlignment.Center, Wrap: false, Ellipsis: false, Clip: false));
            x += width;
        }

        g.Restore(state);
        var button = ButtonArea;
        if (ShowUpDown)
        {
            var up = new Rectangle(button.X, button.Y, button.Width, button.Height / 2);
            var down = Rectangle.FromLTRB(button.X, up.Bottom, button.Right, button.Bottom);
            ThemePainter.DrawArrow(g, new Rectangle(up.X + ((up.Width - 7) / 2), up.Y + ((up.Height - 4) / 2), 7, 4), ArrowDirection.Up, Enabled ? Theme.Glyph : Theme.DisabledText);
            ThemePainter.DrawArrow(g, new Rectangle(down.X + ((down.Width - 7) / 2), down.Y + ((down.Height - 4) / 2), 7, 4), ArrowDirection.Down, Enabled ? Theme.Glyph : Theme.DisabledText);
            return;
        }

        if (DroppedDown || _hot)
        {
            using var face = new SolidBrush(DroppedDown ? Theme.ButtonPressedFace : Theme.ButtonHotFace);
            g.FillRectangle(face, button);
        }

        ThemePainter.DrawArrow(g, button, ArrowDirection.Down, Enabled ? Theme.Glyph : Theme.DisabledText);
    }

    /// <summary>Called when the calendar closes.</summary>
    internal void OnCalendarClosed()
    {
        _popup = null;
        Invalidate();
        OnCloseUp(EventArgs.Empty);
    }

    /// <summary>Sets the date from the calendar, keeping the time of day.</summary>
    internal void SetDateFromCalendar(DateTime day)
    {
        var value = day.Date + _value.TimeOfDay;
        Value = value < _minDate ? _minDate : value > _maxDate ? _maxDate : value;
        if (ShowCheckBox)
        {
            Checked = true;
        }
    }

    /// <summary>The pattern the value is shown with.</summary>
    private string Pattern()
    {
        var format = CultureInfo.CurrentCulture.DateTimeFormat;
        return _format switch
        {
            DateTimePickerFormat.Short => format.ShortDatePattern,
            DateTimePickerFormat.Time => format.LongTimePattern,
            DateTimePickerFormat.Custom when !string.IsNullOrEmpty(_customFormat) => _customFormat,
            _ => format.LongDatePattern,
        };
    }

    private string FormatValue() => string.Concat(Segments().Select(s => s.Text));

    /// <summary>The shown text in parts: the fields, such as the day and the month, and the literal text between them.</summary>
    private List<Segment> Segments()
    {
        var pattern = Pattern();
        var segments = new List<Segment>();
        var i = 0;
        while (i < pattern.Length)
        {
            var c = pattern[i];
            if (c is '\'' or '"')
            {
                var end = pattern.IndexOf(c, i + 1);
                end = end < 0 ? pattern.Length : end;
                segments.Add(new Segment(segments.Count, FieldKind.Literal, string.Empty, pattern[(i + 1)..end]));
                i = end + 1;
                continue;
            }

            if (c == '\\' && i + 1 < pattern.Length)
            {
                segments.Add(new Segment(segments.Count, FieldKind.Literal, string.Empty, pattern[i + 1].ToString()));
                i += 2;
                continue;
            }

            var kind = c switch
            {
                'd' => FieldKind.Day,
                'M' => FieldKind.Month,
                'y' => FieldKind.Year,
                'h' or 'H' => FieldKind.Hour,
                'm' => FieldKind.Minute,
                's' => FieldKind.Second,
                't' => FieldKind.AmPm,
                _ => FieldKind.Literal,
            };

            var length = 1;
            while (i + length < pattern.Length && pattern[i + length] == c)
            {
                length++;
            }

            if (kind == FieldKind.Literal)
            {
                segments.Add(new Segment(segments.Count, kind, string.Empty, pattern.Substring(i, length)));
            }
            else
            {
                var token = pattern.Substring(i, length);
                if (kind == FieldKind.Day && length >= 3)
                {
                    kind = FieldKind.DayOfWeek;
                }

                var text = _value.ToString(length == 1 ? "%" + token : token, CultureInfo.CurrentCulture);
                segments.Add(new Segment(segments.Count, kind, token, text));
            }

            i += length;
        }

        return segments;
    }

    private int? FieldAt(int x)
    {
        var left = (float)TextLeft;
        foreach (var segment in Segments())
        {
            var width = (float)TextMetrics.Measure(segment.Text, Font.Spec).Width;
            if (x >= left && x < left + width)
            {
                return segment.Kind == FieldKind.Literal ? null : segment.Index;
            }

            left += width;
        }

        return null;
    }

    private void SelectField(int index)
    {
        _field = index;
        _typed = string.Empty;
        Invalidate();
    }

    private void Step(Segment field, int step)
    {
        _typed = string.Empty;
        var value = field.Kind switch
        {
            FieldKind.Year => SafeAdd(() => _value.AddYears(step)),
            FieldKind.Month => SafeAdd(() => _value.AddMonths(step)),
            FieldKind.Day or FieldKind.DayOfWeek => SafeAdd(() => _value.AddDays(step)),
            FieldKind.Hour => SafeAdd(() => _value.AddHours(step)),
            FieldKind.Minute => SafeAdd(() => _value.AddMinutes(step)),
            FieldKind.Second => SafeAdd(() => _value.AddSeconds(step)),
            FieldKind.AmPm => SafeAdd(() => _value.AddHours(_value.Hour < 12 ? 12 : -12)),
            _ => _value,
        };

        Value = value < _minDate ? _minDate : value > _maxDate ? _maxDate : value;
    }

    private DateTime SafeAdd(Func<DateTime> add)
    {
        try
        {
            return add();
        }
        catch (ArgumentOutOfRangeException)
        {
            return _value;
        }
    }

    private void SetField(FieldKind kind, int number, string token)
    {
        var v = _value;
        DateTime value;
        try
        {
            value = kind switch
            {
                FieldKind.Year => new DateTime(number, v.Month, Math.Min(v.Day, DateTime.DaysInMonth(number, v.Month)), v.Hour, v.Minute, v.Second),
                FieldKind.Month => new DateTime(v.Year, number, Math.Min(v.Day, DateTime.DaysInMonth(v.Year, number)), v.Hour, v.Minute, v.Second),
                FieldKind.Day => new DateTime(v.Year, v.Month, number, v.Hour, v.Minute, v.Second),
                FieldKind.Hour when token[0] == 'H' => new DateTime(v.Year, v.Month, v.Day, number, v.Minute, v.Second),
                FieldKind.Hour => new DateTime(v.Year, v.Month, v.Day, (number % 12) + (v.Hour >= 12 ? 12 : 0), v.Minute, v.Second),
                FieldKind.Minute => new DateTime(v.Year, v.Month, v.Day, v.Hour, number, v.Second),
                FieldKind.Second => new DateTime(v.Year, v.Month, v.Day, v.Hour, v.Minute, number),
                _ => v,
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return;
        }

        if (value >= _minDate && value <= _maxDate)
        {
            Value = value;
        }
    }

    private void OpenCalendar()
    {
        if (_popup is not null || Window is not { } window || !Enabled || ShowUpDown)
        {
            return;
        }

        OnDropDown(EventArgs.Empty);
        var origin = ClientOriginInWindow();
        var size = new Size(CalendarView.DefaultSize.Width + 2, CalendarView.DefaultSize.Height + 2);
        var screen = window.Form.ClientOriginOnScreen;
        var desktop = window.Runtime.DesktopSize;
        var x = Math.Max(origin.X, origin.X + Width - size.Width);
        var y = origin.Y + Height;
        if (screen.Y + y + size.Height > desktop.Height && screen.Y + origin.Y - size.Height >= 0)
        {
            y = origin.Y - size.Height;
        }

        _popup = new DatePickerPopup(this) { Bounds = new Rectangle(x, y, size.Width, size.Height) };
        window.OpenPopup(_popup);
        Invalidate();
    }

    /// <summary>A part of the shown text.</summary>
    private sealed record Segment(int Index, FieldKind Kind, string Token, string Text);

    /// <summary>What a part of the shown text is.</summary>
    private enum FieldKind
    {
        Literal,
        Day,
        DayOfWeek,
        Month,
        Year,
        Hour,
        Minute,
        Second,
        AmPm,
    }
}

/// <summary>A calendar of a month, to pick a day or a range of days.</summary>
[DefaultEvent(nameof(DateChanged))]
[DefaultProperty(nameof(SelectionRange))]
public class MonthCalendar : Control
{
    private static readonly object EventDateChanged = new();
    private static readonly object EventDateSelected = new();
    private readonly CalendarView _view = new();
    private int _maxSelectionCount = 7;

    /// <summary>Creates a month calendar.</summary>
    public MonthCalendar()
    {
        SetStyle(ControlStyles.StandardDoubleClick, false);
    }

    /// <summary>Occurs when the selection changes.</summary>
    public event DateRangeEventHandler? DateChanged
    {
        add => Events.AddHandler(EventDateChanged, value);
        remove => Events.RemoveHandler(EventDateChanged, value);
    }

    /// <summary>Occurs when the user picks a day.</summary>
    public event DateRangeEventHandler? DateSelected
    {
        add => Events.AddHandler(EventDateSelected, value);
        remove => Events.RemoveHandler(EventDateSelected, value);
    }

    /// <summary>The first selected day.</summary>
    public DateTime SelectionStart
    {
        get => _view.SelectionStart;
        set => SetSelectionRange(value, value > _view.SelectionEnd ? value : _view.SelectionEnd);
    }

    /// <summary>The last selected day.</summary>
    public DateTime SelectionEnd
    {
        get => _view.SelectionEnd;
        set => SetSelectionRange(value < _view.SelectionStart ? value : _view.SelectionStart, value);
    }

    /// <summary>The selected days.</summary>
    public SelectionRange SelectionRange
    {
        get => new(_view.SelectionStart, _view.SelectionEnd);
        set => SetSelectionRange(value.Start, value.End);
    }

    /// <summary>The most days that can be selected.</summary>
    public int MaxSelectionCount
    {
        get => _maxSelectionCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            _maxSelectionCount = value;
        }
    }

    /// <summary>The least date.</summary>
    public DateTime MinDate
    {
        get => _view.MinDate;
        set
        {
            _view.MinDate = value;
            Invalidate();
        }
    }

    /// <summary>The greatest date.</summary>
    public DateTime MaxDate
    {
        get => _view.MaxDate;
        set
        {
            _view.MaxDate = value;
            Invalidate();
        }
    }

    /// <summary>Today, as the calendar shows it.</summary>
    public DateTime TodayDate
    {
        get => _view.Today;
        set
        {
            _view.Today = value.Date;
            Invalidate();
        }
    }

    /// <summary>Whether today's date shows at the bottom.</summary>
    public bool ShowToday
    {
        get => _view.ShowToday;
        set
        {
            _view.ShowToday = value;
            Invalidate();
        }
    }

    /// <summary>Whether today is circled.</summary>
    public bool ShowTodayCircle
    {
        get => _view.ShowTodayCircle;
        set
        {
            _view.ShowTodayCircle = value;
            Invalidate();
        }
    }

    /// <summary>Whether week numbers show. Kept for compatibility.</summary>
    public bool ShowWeekNumbers { get; set; }

    /// <summary>The first day of the week.</summary>
    public Day FirstDayOfWeek
    {
        get => field;
        set
        {
            field = value;
            _view.FirstDayOfWeek = value == Day.Default ? CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek : (DayOfWeek)(((int)value + 1) % 7);
            Invalidate();
        }
    } = Day.Default;

    /// <summary>The days drawn in bold.</summary>
    public DateTime[] BoldedDates
    {
        get => [.. _view.BoldedDates];
        set
        {
            _view.BoldedDates.Clear();
            foreach (var date in value ?? [])
            {
                _view.BoldedDates.Add(date.Date);
            }

            Invalidate();
        }
    }

    /// <summary>The number of months shown across and down. One month is shown.</summary>
    public Size CalendarDimensions { get; set; } = new(1, 1);

    /// <summary>How far the arrows scroll, in months.</summary>
    public int ScrollChange { get; set; }

    /// <inheritdoc />
    protected override Size DefaultSize => CalendarView.DefaultSize;

    /// <summary>Selects a range of days.</summary>
    /// <param name="date1">The first day.</param>
    /// <param name="date2">The last day.</param>
    public void SetSelectionRange(DateTime date1, DateTime date2)
    {
        var start = (date1 < date2 ? date1 : date2).Date;
        var end = (date1 < date2 ? date2 : date1).Date;
        if ((end - start).TotalDays >= _maxSelectionCount)
        {
            end = start.AddDays(_maxSelectionCount - 1);
        }

        if (start == _view.SelectionStart.Date && end == _view.SelectionEnd.Date)
        {
            return;
        }

        _view.SelectionStart = start;
        _view.SelectionEnd = end;
        _view.Month = new DateTime(start.Year, start.Month, 1);
        Invalidate();
        OnDateChanged(new DateRangeEventArgs(start, end));
    }

    /// <summary>Selects a day.</summary>
    /// <param name="date">The day.</param>
    public void SetDate(DateTime date) => SetSelectionRange(date, date);

    /// <summary>Adds a day drawn in bold.</summary>
    /// <param name="date">The day.</param>
    public void AddBoldedDate(DateTime date)
    {
        _view.BoldedDates.Add(date.Date);
        Invalidate();
    }

    /// <summary>Removes every day drawn in bold.</summary>
    public void RemoveAllBoldedDates()
    {
        _view.BoldedDates.Clear();
        Invalidate();
    }

    /// <summary>Draws the bolded days again. They are drawn as they change.</summary>
    public void UpdateBoldedDates() => Invalidate();

    /// <summary>Raises DateChanged.</summary>
    /// <param name="drevent">The event data.</param>
    protected virtual void OnDateChanged(DateRangeEventArgs drevent) => ((DateRangeEventHandler?)Events[EventDateChanged])?.Invoke(this, drevent);

    /// <summary>Raises DateSelected.</summary>
    /// <param name="drevent">The event data.</param>
    protected virtual void OnDateSelected(DateRangeEventArgs drevent) => ((DateRangeEventHandler?)Events[EventDateSelected])?.Invoke(this, drevent);

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int? days = e.KeyCode switch
        {
            Keys.Left => -1,
            Keys.Right => 1,
            Keys.Up => -7,
            Keys.Down => 7,
            _ => null,
        };

        if (days is { } step)
        {
            var day = _view.SelectionStart.AddDays(step);
            if (e.Shift)
            {
                SetSelectionRange(_view.SelectionStart, _view.SelectionEnd.AddDays(step));
            }
            else
            {
                SetDate(day);
            }

            e.Handled = true;
        }
        else if (e.KeyCode is Keys.PageUp or Keys.PageDown)
        {
            SetDate(_view.SelectionStart.AddMonths(e.KeyCode == Keys.PageUp ? -1 : 1));
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var (part, day) = _view.HitTest(ClientRectangle, e.Location);
        switch (part)
        {
            case CalendarView.Part.Previous:
                _view.Month = _view.Month.AddMonths(-Math.Max(1, ScrollChange));
                Invalidate();
                break;
            case CalendarView.Part.Next:
                _view.Month = _view.Month.AddMonths(Math.Max(1, ScrollChange));
                Invalidate();
                break;
            case CalendarView.Part.Day or CalendarView.Part.Today:
                if (ModifierKeys.HasFlag(Keys.Shift))
                {
                    SetSelectionRange(_view.SelectionStart, day);
                }
                else
                {
                    SetDate(day);
                }

                OnDateSelected(new DateRangeEventArgs(_view.SelectionStart, _view.SelectionEnd));
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var (part, day) = _view.HitTest(ClientRectangle, e.Location);
        DateTime? hot = part == CalendarView.Part.Day ? day : null;
        if (hot != _view.HotDay)
        {
            _view.HotDay = hot;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _view.HotDay = null;
        Invalidate();
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        _view.Paint(g, ClientRectangle, Font, Enabled);
        using var border = new Pen(Theme.MenuBorder);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
    }
}

/// <summary>A range of days.</summary>
public sealed class SelectionRange
{
    /// <summary>Creates a range of today.</summary>
    public SelectionRange()
    {
        Start = End = DateTime.Today;
    }

    /// <summary>Creates a range.</summary>
    /// <param name="lower">The first day.</param>
    /// <param name="upper">The last day.</param>
    public SelectionRange(DateTime lower, DateTime upper)
    {
        Start = lower < upper ? lower.Date : upper.Date;
        End = lower < upper ? upper.Date : lower.Date;
    }

    /// <summary>The first day.</summary>
    public DateTime Start { get; set; }

    /// <summary>The last day.</summary>
    public DateTime End { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"SelectionRange: Start: {Start}, End: {End}";
}

/// <summary>Data about a range of days.</summary>
/// <param name="start">The first day.</param>
/// <param name="end">The last day.</param>
public class DateRangeEventArgs(DateTime start, DateTime end) : EventArgs
{
    /// <summary>The first day.</summary>
    public DateTime Start { get; } = start;

    /// <summary>The last day.</summary>
    public DateTime End { get; } = end;
}

/// <summary>Handles a change of a range of days.</summary>
/// <param name="sender">The calendar.</param>
/// <param name="e">The event data.</param>
public delegate void DateRangeEventHandler(object? sender, DateRangeEventArgs e);

/// <summary>A day of the week, as a month calendar takes it.</summary>
public enum Day
{
    /// <summary>Monday.</summary>
    Monday = 0,

    /// <summary>Tuesday.</summary>
    Tuesday = 1,

    /// <summary>Wednesday.</summary>
    Wednesday = 2,

    /// <summary>Thursday.</summary>
    Thursday = 3,

    /// <summary>Friday.</summary>
    Friday = 4,

    /// <summary>Saturday.</summary>
    Saturday = 5,

    /// <summary>Sunday.</summary>
    Sunday = 6,

    /// <summary>The culture's first day.</summary>
    Default = 7,
}
