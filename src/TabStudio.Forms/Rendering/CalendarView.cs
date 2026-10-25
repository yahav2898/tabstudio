using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// A month of days, as a month calendar and a date picker's drop-down show it: the month's title between arrows, the
/// days of the week, six weeks of days, and today's date at the bottom.
/// </summary>
internal sealed class CalendarView
{
    /// <summary>The size of the view at 96 DPI, as Windows draws it.</summary>
    public static readonly Size DefaultSize = new(227, 162);

    private const int TitleHeight = 26;
    private const int WeekdaysHeight = 18;
    private const int FooterHeight = 20;

    /// <summary>The first day of the month shown.</summary>
    public DateTime Month { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    /// <summary>The first selected day.</summary>
    public DateTime SelectionStart { get; set; } = DateTime.Today;

    /// <summary>The last selected day.</summary>
    public DateTime SelectionEnd { get; set; } = DateTime.Today;

    /// <summary>Today, as the calendar shows it.</summary>
    public DateTime Today { get; set; } = DateTime.Today;

    /// <summary>The least date.</summary>
    public DateTime MinDate { get; set; } = DateTimePicker.MinimumDateTime;

    /// <summary>The greatest date.</summary>
    public DateTime MaxDate { get; set; } = DateTimePicker.MaximumDateTime;

    /// <summary>The first day of the week.</summary>
    public DayOfWeek FirstDayOfWeek { get; set; } = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>Whether today's date is shown at the bottom.</summary>
    public bool ShowToday { get; set; } = true;

    /// <summary>Whether today is circled.</summary>
    public bool ShowTodayCircle { get; set; } = true;

    /// <summary>The days drawn in bold.</summary>
    public HashSet<DateTime> BoldedDates { get; } = [];

    /// <summary>The day under the pointer, if any.</summary>
    public DateTime? HotDay { get; set; }

    /// <summary>The first day drawn: the start of the week the month starts in.</summary>
    public DateTime FirstShown
    {
        get
        {
            var offset = ((int)Month.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            return Month.AddDays(-offset);
        }
    }

    /// <summary>What is at a point of the view.</summary>
    public enum Part
    {
        /// <summary>Nothing.</summary>
        None,

        /// <summary>The arrow to the previous month.</summary>
        Previous,

        /// <summary>The arrow to the next month.</summary>
        Next,

        /// <summary>A day.</summary>
        Day,

        /// <summary>Today's date at the bottom.</summary>
        Today,
    }

    /// <summary>What is at a point, and the day there.</summary>
    /// <param name="bounds">The view's bounds.</param>
    /// <param name="point">The point.</param>
    public (Part Part, DateTime Day) HitTest(Rectangle bounds, Point point)
    {
        if (!bounds.Contains(point))
        {
            return (Part.None, default);
        }

        if (point.Y < bounds.Top + TitleHeight)
        {
            if (point.X < bounds.Left + 30)
            {
                return (Part.Previous, default);
            }

            if (point.X >= bounds.Right - 30)
            {
                return (Part.Next, default);
            }

            return (Part.None, default);
        }

        var days = DaysArea(bounds);
        if (days.Contains(point))
        {
            var column = (point.X - days.X) * 7 / Math.Max(1, days.Width);
            var row = (point.Y - days.Y) * 6 / Math.Max(1, days.Height);
            var day = FirstShown.AddDays((row * 7) + column);
            return day < MinDate.Date || day > MaxDate.Date ? (Part.None, default) : (Part.Day, day);
        }

        if (ShowToday && point.Y >= bounds.Bottom - FooterHeight)
        {
            return (Part.Today, Today);
        }

        return (Part.None, default);
    }

    /// <summary>Draws the view.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="bounds">The view's bounds.</param>
    /// <param name="font">The font.</param>
    /// <param name="enabled">Whether the view is enabled.</param>
    public void Paint(Graphics g, Rectangle bounds, Font font, bool enabled)
    {
        using (var background = new SolidBrush(Theme.Window))
        {
            g.FillRectangle(background, bounds);
        }

        var text = enabled ? Color.Black : Theme.DisabledText;
        var dim = Color.FromArgb(160, 160, 160);
        var title = new Rectangle(bounds.X, bounds.Y + 2, bounds.Width, TitleHeight - 4);
        var titleText = Month.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        using (var bold = new Font(font, FontStyle.Bold))
        {
            ThemePainter.DrawText(g, titleText, bold, text, title, ContentAlignment.MiddleCenter, mnemonics: false);
        }

        ThemePainter.DrawArrow(g, new Rectangle(title.X + 8, title.Y, 16, title.Height), ArrowDirection.Left, Month > MinDate ? text : dim);
        ThemePainter.DrawArrow(g, new Rectangle(title.Right - 24, title.Y, 16, title.Height), ArrowDirection.Right, Month.AddMonths(1) <= MaxDate ? text : dim);

        var days = DaysArea(bounds);
        var cellWidth = days.Width / 7f;
        var cellHeight = days.Height / 6f;
        var names = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        for (var column = 0; column < 7; column++)
        {
            var name = names[((int)FirstDayOfWeek + column) % 7];
            var cell = new Rectangle((int)(days.X + (column * cellWidth)), days.Y - WeekdaysHeight, (int)cellWidth, WeekdaysHeight);
            ThemePainter.DrawText(g, name, font, text, cell, ContentAlignment.MiddleCenter, mnemonics: false);
        }

        using (var line = new Pen(Theme.Separator))
        {
            g.DrawLine(line, days.X + 4, days.Y - 1, days.Right - 4, days.Y - 1);
        }

        var first = FirstShown;
        for (var index = 0; index < 42; index++)
        {
            var day = first.AddDays(index);
            var cell = new Rectangle((int)(days.X + (index % 7 * cellWidth)), (int)(days.Y + (index / 7 * cellHeight)), (int)Math.Ceiling(cellWidth), (int)Math.Ceiling(cellHeight));
            var selected = day >= SelectionStart.Date && day <= SelectionEnd.Date;
            var inMonth = day.Month == Month.Month;
            var inRange = day >= MinDate.Date && day <= MaxDate.Date;
            if (selected)
            {
                using var fill = new SolidBrush(enabled ? Color.FromArgb(204, 232, 255) : Theme.ButtonDisabledFace);
                g.FillRectangle(fill, Rectangle.Inflate(cell, -1, 0));
            }
            else if (HotDay == day && inRange)
            {
                using var fill = new SolidBrush(Color.FromArgb(229, 243, 255));
                g.FillRectangle(fill, Rectangle.Inflate(cell, -1, 0));
            }

            if (ShowTodayCircle && day == Today.Date)
            {
                using var circle = new Pen(Theme.Accent);
                g.DrawRectangle(circle, cell.X + 1, cell.Y, cell.Width - 3, cell.Height - 1);
            }

            var color = !inRange || !inMonth ? dim : text;
            var dayFont = BoldedDates.Contains(day) ? new Font(font, FontStyle.Bold) : font;
            ThemePainter.DrawText(g, day.Day.ToString(CultureInfo.CurrentCulture), dayFont, color, cell, ContentAlignment.MiddleCenter, mnemonics: false);
            if (!ReferenceEquals(dayFont, font))
            {
                dayFont.Dispose();
            }
        }

        if (ShowToday)
        {
            var footer = new Rectangle(bounds.X + 8, bounds.Bottom - FooterHeight, bounds.Width - 16, FooterHeight - 2);
            ThemePainter.DrawText(g, "Today: " + Today.ToString("d", CultureInfo.CurrentCulture), font, text, footer, ContentAlignment.MiddleLeft, mnemonics: false);
        }
    }

    private Rectangle DaysArea(Rectangle bounds)
    {
        var top = bounds.Top + TitleHeight + WeekdaysHeight;
        var bottom = bounds.Bottom - (ShowToday ? FooterHeight : 4);
        return new Rectangle(bounds.X + 6, top, bounds.Width - 12, Math.Max(6, bottom - top));
    }
}
