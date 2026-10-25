using System.Drawing;
using System.Drawing.Drawing2D;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>Drawing: the cells in view, the headers with their sort glyphs and row glyphs, the grid lines and the scroll bars.</summary>
public partial class DataGridView
{
    private int _hotColumnHeader = -1;

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var client = ClientRectangle;
        using (var background = new SolidBrush(_backgroundColor))
        {
            g.FillRectangle(background, client);
        }

        var area = CellsArea;
        var rows = RowsInView(area).ToList();
        var columns = ColumnsInView(area).ToList();
        using var gridPen = new Pen(_gridColor);

        var state = g.Save();
        g.IntersectClip(area);
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                PaintCell(g, row, column, gridPen);
            }
        }

        g.Restore(state);

        if (_columnHeadersVisible)
        {
            state = g.Save();
            g.IntersectClip(new Rectangle(area.Left, 0, area.Width, ColumnHeadersShownHeight));
            foreach (var column in columns)
            {
                PaintColumnHeader(g, column);
            }

            g.Restore(state);
        }

        if (_rowHeadersVisible)
        {
            state = g.Save();
            g.IntersectClip(new Rectangle(0, area.Top, RowHeadersShownWidth, area.Height));
            foreach (var row in rows)
            {
                PaintRowHeader(g, row);
            }

            g.Restore(state);
        }

        if (_rowHeadersVisible && _columnHeadersVisible)
        {
            PaintHeaderBackground(g, new Rectangle(0, 0, RowHeadersShownWidth, ColumnHeadersShownHeight), selected: false, hot: false, _columnHeadersDefaultCellStyle);
        }

        _verticalBar.Paint(g, Enabled);
        _horizontalBar.Paint(g, Enabled);
        if (_verticalBar.Visible && _horizontalBar.Visible)
        {
            using var corner = new SolidBrush(Theme.ScrollTrack);
            g.FillRectangle(corner, _verticalBar.Bounds.X, _horizontalBar.Bounds.Y, Theme.ScrollBarSize, Theme.ScrollBarSize);
        }
    }

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g)
    {
        if (_borderStyle == BorderStyle.FixedSingle)
        {
            using var pen = new Pen(Color.FromArgb(130, 135, 144));
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            return;
        }

        ThemePainter.DrawBorder(g, new Rectangle(Point.Empty, Size), _borderStyle);
    }

    private IEnumerable<DataGridViewRow> RowsInView(Rectangle area)
    {
        var first = RowAt(_verticalBar.Value);
        if (first < 0)
        {
            yield break;
        }

        for (var i = first; i < _rows.Count; i++)
        {
            if (_rowTops[i] - _verticalBar.Value >= area.Height)
            {
                yield break;
            }

            if (_rows[i].Visible)
            {
                yield return _rows[i];
            }
        }
    }

    private IEnumerable<DataGridViewColumn> ColumnsInView(Rectangle area)
    {
        foreach (var column in _columnOrder)
        {
            if (!column.Visible)
            {
                continue;
            }

            var left = _columnLefts[column] - _horizontalBar.Value;
            if (left + column.Width > 0 && left < area.Width)
            {
                yield return column;
            }
        }
    }

    private void PaintCell(Graphics g, DataGridViewRow row, DataGridViewColumn column, Pen gridPen)
    {
        if (column.Index >= row.Cells.Count)
        {
            return;
        }

        var cell = row.Cells[column.Index];
        var bounds = CellBounds(column.Index, row.Index);
        var style = cell.InheritedStyle;
        var editing = cell.IsInEditMode;
        var selected = IsCellSelected(cell) && !editing;
        var value = SafeValue(cell);
        var formatted = editing ? null : cell.FormattedValueFor(value, style);

        void Paint(DataGridViewPaintParts parts)
        {
            if ((parts & DataGridViewPaintParts.Background) != 0)
            {
                var fill = selected && (parts & DataGridViewPaintParts.SelectionBackground) != 0 ? style.SelectionBackColor : style.BackColor;
                using var brush = new SolidBrush(fill.IsEmpty ? SystemColors.Window : fill);
                g.FillRectangle(brush, bounds);
            }

            if ((parts & DataGridViewPaintParts.ContentForeground) != 0 && !editing)
            {
                cell.PaintContent(g, Rectangle.FromLTRB(bounds.Left, bounds.Top, bounds.Right - 1, bounds.Bottom - 1), style, formatted, selected);
            }

            if ((parts & DataGridViewPaintParts.ErrorIcon) != 0 && ShowCellErrors && cell.ErrorText.Length > 0)
            {
                PaintErrorIcon(g, new Rectangle(bounds.Right - 18, bounds.Top + ((bounds.Height - 12) / 2), 12, 12));
            }

            if ((parts & DataGridViewPaintParts.Border) != 0)
            {
                PaintGridLines(g, bounds, gridPen);
            }
        }

        if (Events[EventCellPainting] is not null)
        {
            var e = new DataGridViewCellPaintingEventArgs(g, bounds, bounds, row.Index, column.Index, cell.State, value, formatted, style, Paint);
            OnCellPainting(e);
            if (e.Handled)
            {
                return;
            }
        }

        Paint(DataGridViewPaintParts.All);
        if (cell == _currentCell && Focused && Window?.ShowFocusCues == true && !editing)
        {
            ThemePainter.DrawFocusRectangle(g, Rectangle.FromLTRB(bounds.Left + 1, bounds.Top + 1, bounds.Right - 2, bounds.Bottom - 2));
        }
    }

    private object? SafeValue(DataGridViewCell cell)
    {
        try
        {
            return cell.Value;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or InvalidCastException or IndexOutOfRangeException)
        {
            RaiseDataError(new DataGridViewDataErrorEventArgs(exception, cell.ColumnIndex, cell.RowIndex, DataGridViewDataErrorContexts.Display), showDialog: false);
            return null;
        }
    }

    private void PaintGridLines(Graphics g, Rectangle bounds, Pen pen)
    {
        var horizontalLines = CellBorderStyle is DataGridViewCellBorderStyle.Single or DataGridViewCellBorderStyle.SingleHorizontal
            or DataGridViewCellBorderStyle.Raised or DataGridViewCellBorderStyle.RaisedHorizontal or DataGridViewCellBorderStyle.Sunken or DataGridViewCellBorderStyle.SunkenHorizontal;
        var verticalLines = CellBorderStyle is DataGridViewCellBorderStyle.Single or DataGridViewCellBorderStyle.SingleVertical
            or DataGridViewCellBorderStyle.Raised or DataGridViewCellBorderStyle.RaisedVertical or DataGridViewCellBorderStyle.Sunken or DataGridViewCellBorderStyle.SunkenVertical;
        if (verticalLines)
        {
            g.DrawLine(pen, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        }

        if (horizontalLines)
        {
            g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
        }
    }

    private void PaintColumnHeader(Graphics g, DataGridViewColumn column)
    {
        var bounds = CellBounds(column.Index, -1);
        var style = column.HeaderCell.InheritedStyle;
        var selected = column.Selected;
        PaintHeaderBackground(g, bounds, selected, column.Index == _hotColumnHeader, style);
        var glyph = column.HeaderCell.SortGlyphDirection;
        var textBounds = Rectangle.FromLTRB(bounds.Left + 5 + style.Padding.Left, bounds.Top + 2, bounds.Right - 5 - (glyph == SortOrder.None ? 0 : 14) - style.Padding.Right, bounds.Bottom - 3);
        var color = selected && !EnableHeadersVisualStyles ? style.SelectionForeColor : style.ForeColor;
        var font = style.Font ?? Font;
        ThemePainter.DrawText(g, column.HeaderText, font, Enabled ? color : Theme.DisabledText, textBounds, DataGridViewCell.ToContentAlignment(style.Alignment), wrap: style.WrapMode == DataGridViewTriState.True, ellipsis: true, mnemonics: false);
        if (glyph != SortOrder.None)
        {
            var arrow = new Rectangle(bounds.Right - 16, bounds.Top + ((bounds.Height - 6) / 2), 9, 6);
            ThemePainter.DrawArrow(g, arrow, glyph == SortOrder.Ascending ? ArrowDirection.Up : ArrowDirection.Down, Color.FromArgb(120, 120, 120));
        }
    }

    private void PaintRowHeader(Graphics g, DataGridViewRow row)
    {
        var bounds = CellBounds(-1, row.Index);
        var style = row.HeaderCell.InheritedStyle;
        var selected = row.Selected || (_selectionMode == DataGridViewSelectionMode.FullRowSelect && row.Cells.Items.Exists(IsCellSelected));
        PaintHeaderBackground(g, bounds, selected, hot: false, style);
        var glyphColor = selected && !EnableHeadersVisualStyles ? style.SelectionForeColor : Color.FromArgb(32, 32, 32);
        var center = new Point(bounds.Right - 14, bounds.Top + (bounds.Height / 2));
        if (_currentCell?.OwningRow == row && _editingControl is not null && ShowEditingIcon)
        {
            PaintPencil(g, center, glyphColor);
        }
        else if (_currentCell?.OwningRow == row)
        {
            using var brush = new SolidBrush(glyphColor);
            using var path = new GraphicsPath();
            path.AddPolygon(new Point[] { new(center.X - 2, center.Y - 4), new(center.X + 2, center.Y), new(center.X - 2, center.Y + 4) });
            g.FillPath(brush, path);
        }
        else if (row.IsNewRow)
        {
            using var pen = new Pen(glyphColor, 1.2f);
            g.DrawLine(pen, center.X - 3, center.Y, center.X + 3, center.Y);
            g.DrawLine(pen, center.X - 2, center.Y - 3, center.X + 2, center.Y + 3);
            g.DrawLine(pen, center.X - 2, center.Y + 3, center.X + 2, center.Y - 3);
        }

        if (row.HeaderCell.Value is { } value && Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture) is { Length: > 0 } text)
        {
            ThemePainter.DrawText(g, text, style.Font ?? Font, style.ForeColor, Rectangle.FromLTRB(bounds.Left + 4, bounds.Top, bounds.Right - 22, bounds.Bottom), DataGridViewCell.ToContentAlignment(style.Alignment), ellipsis: true, mnemonics: false);
        }

        if (ShowRowErrors && row.ErrorText.Length > 0)
        {
            PaintErrorIcon(g, new Rectangle(bounds.Left + 4, bounds.Top + ((bounds.Height - 12) / 2), 12, 12));
        }
    }

    private void PaintHeaderBackground(Graphics g, Rectangle bounds, bool selected, bool hot, DataGridViewCellStyle style)
    {
        if (EnableHeadersVisualStyles)
        {
            var fill = selected ? Color.FromArgb(217, 235, 249) : hot ? Color.FromArgb(229, 243, 255) : Theme.HeaderBackground;
            using (var brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, bounds);
            }

            using var separator = new Pen(Theme.HeaderSeparator);
            g.DrawLine(separator, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
            g.DrawLine(separator, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
            return;
        }

        using (var brush = new SolidBrush(selected ? style.SelectionBackColor : style.BackColor))
        {
            g.FillRectangle(brush, bounds);
        }

        using var dark = new Pen(SystemColors.ControlDark);
        using var light = new Pen(SystemColors.ControlLightLight);
        g.DrawLine(light, bounds.Left, bounds.Top, bounds.Right - 2, bounds.Top);
        g.DrawLine(light, bounds.Left, bounds.Top, bounds.Left, bounds.Bottom - 2);
        g.DrawLine(dark, bounds.Right - 1, bounds.Top, bounds.Right - 1, bounds.Bottom - 1);
        g.DrawLine(dark, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
    }

    private static void PaintPencil(Graphics g, Point center, Color color)
    {
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using var pen = new Pen(color, 1.2f);
        var tip = new PointF(center.X - 4, center.Y + 4);
        g.DrawLine(pen, tip.X, tip.Y, center.X + 3, center.Y - 3);
        g.DrawLine(pen, tip.X + 2, tip.Y + 1, center.X + 5, center.Y - 2);
        g.DrawLine(pen, center.X + 3, center.Y - 3, center.X + 5, center.Y - 1);
        g.DrawLine(pen, tip.X, tip.Y, tip.X + 2, tip.Y + 1);
        g.SmoothingMode = smoothing;
    }

    private static void PaintErrorIcon(Graphics g, Rectangle box)
    {
        var smoothing = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        using (var red = new SolidBrush(Color.FromArgb(232, 17, 35)))
        {
            g.FillEllipse(red, box);
        }

        using var white = new Pen(Color.White, 1.6f);
        var cx = box.X + (box.Width / 2f);
        g.DrawLine(white, cx, box.Y + 3, cx, box.Y + 7);
        g.DrawLine(white, cx, box.Y + 8.6f, cx, box.Y + 9.4f);
        g.SmoothingMode = smoothing;
    }
}
