using System.Drawing;
using System.Windows.Forms;

namespace TabStudio.Forms.Rendering;

/// <summary>
/// A scrolling list of rows of one height, as a list box and a combo box's drop-down draw it: the rows in view, the
/// selection, the vertical scroll bar, and hit testing.
/// </summary>
internal sealed class ItemListView
{
    /// <summary>The vertical scroll bar.</summary>
    public ScrollBarModel Bar { get; } = new(vertical: true);

    /// <summary>The height of a row.</summary>
    public int ItemHeight { get; set; } = 15;

    /// <summary>The number of rows.</summary>
    public int Count { get; set; }

    /// <summary>Where the rows are drawn, without the scroll bar.</summary>
    public Rectangle Area { get; private set; }

    /// <summary>The scroll offset, in pixels.</summary>
    public int Offset
    {
        get => Bar.Value;
        set => Bar.Value = Math.Clamp(value, 0, Bar.MaximumValue);
    }

    /// <summary>The first row in view.</summary>
    public int TopIndex
    {
        get => ItemHeight <= 0 ? 0 : Offset / ItemHeight;
        set => Offset = value * ItemHeight;
    }

    /// <summary>Lays out the rows and the scroll bar in an area.</summary>
    /// <param name="bounds">The area.</param>
    /// <param name="alwaysShowBar">Whether the scroll bar shows even when every row fits.</param>
    public void Layout(Rectangle bounds, bool alwaysShowBar = false)
    {
        var content = Count * ItemHeight;
        Bar.Visible = alwaysShowBar || content > bounds.Height;
        Area = Bar.Visible ? new Rectangle(bounds.X, bounds.Y, Math.Max(0, bounds.Width - Theme.ScrollBarSize), bounds.Height) : bounds;
        Bar.Bounds = new Rectangle(bounds.Right - Theme.ScrollBarSize, bounds.Y, Theme.ScrollBarSize, bounds.Height);
        Bar.ContentLength = content;
        Bar.ViewLength = bounds.Height;
        Bar.SmallChange = ItemHeight;
        ClampOffset();
    }

    /// <summary>Lays out the rows in an area, for a control that places the scroll bar itself.</summary>
    /// <param name="area">The area of the rows.</param>
    public void LayoutRows(Rectangle area)
    {
        Area = area;
        Bar.ContentLength = Count * ItemHeight;
        Bar.ViewLength = area.Height;
        Bar.SmallChange = ItemHeight;
        ClampOffset();
    }

    /// <summary>The row at a point, or -1.</summary>
    /// <param name="point">The point.</param>
    public int IndexAt(Point point)
    {
        if (!Area.Contains(point) || ItemHeight <= 0)
        {
            return -1;
        }

        var index = (point.Y - Area.Y + Offset) / ItemHeight;
        return index < Count ? index : -1;
    }

    /// <summary>A row's bounds.</summary>
    /// <param name="index">The row.</param>
    public Rectangle RowBounds(int index) => new(Area.X, Area.Y + (index * ItemHeight) - Offset, Area.Width, ItemHeight);

    /// <summary>Scrolls so a row is in view.</summary>
    /// <param name="index">The row.</param>
    public void EnsureVisible(int index)
    {
        if (index < 0 || index >= Count)
        {
            return;
        }

        var top = index * ItemHeight;
        if (top < Offset)
        {
            Offset = top;
        }
        else if (top + ItemHeight > Offset + Area.Height)
        {
            Offset = top + ItemHeight - Area.Height;
        }
    }

    /// <summary>The rows in view.</summary>
    public IEnumerable<int> VisibleRows()
    {
        if (ItemHeight <= 0)
        {
            yield break;
        }

        var first = Offset / ItemHeight;
        var last = Math.Min(Count - 1, (Offset + Area.Height - 1) / ItemHeight);
        for (var i = first; i <= last; i++)
        {
            yield return i;
        }
    }

    /// <summary>Keeps the scroll offset within the content, after the content or the view changed size.</summary>
    public void ClampOffset() => Bar.Value = Math.Clamp(Bar.Value, 0, Bar.MaximumValue);

    /// <summary>Draws the rows with their text, the selection and the focus rectangle; the caller draws the scroll bar.</summary>
    /// <param name="g">The surface.</param>
    /// <param name="font">The font.</param>
    /// <param name="foreColor">The text color.</param>
    /// <param name="textOf">The text of a row.</param>
    /// <param name="isSelected">Whether a row is selected.</param>
    /// <param name="hot">A row highlighted under the pointer, or -1.</param>
    /// <param name="focusIndex">The row with the focus rectangle, or -1.</param>
    /// <param name="enabled">Whether the list is enabled.</param>
    /// <param name="textLeft">How far the text starts from the row's left, after a check box for example.</param>
    public void Paint(Graphics g, Font font, Color foreColor, Func<int, string> textOf, Func<int, bool> isSelected, int hot, int focusIndex, bool enabled, int textLeft = 2)
    {
        var state = g.Save();
        g.IntersectClip(Area);
        foreach (var index in VisibleRows())
        {
            var row = RowBounds(index);
            var selected = isSelected(index);
            var color = !enabled ? Theme.DisabledText : selected ? Theme.SelectionText : foreColor;
            if (selected)
            {
                using var fill = new SolidBrush(enabled ? Theme.Selection : Theme.ButtonDisabledFace);
                g.FillRectangle(fill, row);
            }
            else if (index == hot)
            {
                using var fill = new SolidBrush(Theme.ItemHot);
                g.FillRectangle(fill, row);
            }

            var text = new Rectangle(row.X + textLeft, row.Y, Math.Max(0, row.Width - textLeft - 2), row.Height);
            ThemePainter.DrawText(g, textOf(index), font, color, text, ContentAlignment.MiddleLeft, mnemonics: false);
            if (index == focusIndex)
            {
                ThemePainter.DrawFocusRectangle(g, row);
            }
        }

        g.Restore(state);
    }
}
