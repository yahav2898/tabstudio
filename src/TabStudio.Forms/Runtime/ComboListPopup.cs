using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Rendering;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// The open list of a combo box. The highlight follows the pointer; a tap picks an item and closes the list; a finger
/// drags the list to scroll it; the arrow keys, Enter and Escape work as in Windows.
/// </summary>
internal sealed class ComboListPopup : Popup
{
    private readonly ComboBox _combo;
    private readonly ItemListView _view = new();
    private int _highlight = -1;
    private Point _pressPoint;
    private bool _dragging;
    private bool _pressedOnBar;

    /// <summary>Creates the list of a combo box.</summary>
    /// <param name="combo">The combo box.</param>
    public ComboListPopup(ComboBox combo) => _combo = combo;

    /// <inheritdoc />
    public override Control Owner => _combo;

    /// <summary>Lays the list out again after its items changed.</summary>
    public void SyncItems()
    {
        _view.ItemHeight = _combo.ItemHeight;
        _view.Count = _combo.Items.Count;
        _view.Layout(new Rectangle(1, 1, Math.Max(0, Bounds.Width - 2), Math.Max(0, Bounds.Height - 2)));
        SyncSelection();
    }

    /// <summary>Highlights the selected item, and scrolls to it.</summary>
    public void SyncSelection()
    {
        _highlight = _combo.SelectedIndex;
        _view.EnsureVisible(_highlight);
        Invalidate();
    }

    /// <inheritdoc />
    public override void Paint(Graphics g)
    {
        var bounds = new Rectangle(Point.Empty, Bounds.Size);
        using (var background = new SolidBrush(_combo.BackColor))
        {
            g.FillRectangle(background, bounds);
        }

        var state = g.Save();
        g.IntersectClip(_view.Area);
        foreach (var index in _view.VisibleRows())
        {
            var row = _view.RowBounds(index);
            var selected = index == _highlight;
            if (_combo.DrawListItem(g, index, row, selected))
            {
                continue;
            }

            if (selected)
            {
                using var fill = new SolidBrush(Theme.Selection);
                g.FillRectangle(fill, row);
            }

            var color = selected ? Theme.SelectionText : _combo.ForeColor;
            ThemePainter.DrawText(g, _combo.GetItemText(_combo.Items[index]), _combo.Font, color, Rectangle.FromLTRB(row.Left + 2, row.Top, row.Right - 2, row.Bottom), ContentAlignment.MiddleLeft, mnemonics: false);
        }

        g.Restore(state);
        _view.Bar.Paint(g, enabled: true);
        using var border = new Pen(Theme.EditBorder);
        g.DrawRectangle(border, 0, 0, bounds.Width - 1, bounds.Height - 1);
    }

    /// <inheritdoc />
    public override void PointerDown(Point point)
    {
        _pressPoint = point;
        _dragging = false;
        _pressedOnBar = _view.Bar.PointerDown(point);
        if (_pressedOnBar)
        {
            Invalidate();
            return;
        }

        Highlight(_view.IndexAt(point));
    }

    /// <inheritdoc />
    public override void PointerMove(Point point, bool pressed)
    {
        if (_pressedOnBar)
        {
            _view.Bar.PointerMove(point);
            Invalidate();
            return;
        }

        if (pressed && (_dragging || Math.Abs(point.Y - _pressPoint.Y) > 8) && _view.Bar.MaximumValue > 0)
        {
            // A finger dragging the list scrolls it.
            _view.Offset += _pressPoint.Y - point.Y;
            _pressPoint = point;
            _dragging = true;
            Invalidate();
            return;
        }

        if (!pressed || !_dragging)
        {
            Highlight(_view.IndexAt(point));
        }
    }

    /// <inheritdoc />
    public override void PointerUp(Point point)
    {
        if (_pressedOnBar)
        {
            _view.Bar.PointerUp();
            _pressedOnBar = false;
            Invalidate();
            return;
        }

        if (_dragging)
        {
            _dragging = false;
            return;
        }

        var index = _view.IndexAt(point);
        if (index >= 0)
        {
            Pick(index);
        }
    }

    /// <inheritdoc />
    public override void Wheel(int delta)
    {
        _view.Offset -= delta / 120 * 3 * _view.ItemHeight;
        Invalidate();
    }

    /// <inheritdoc />
    public override bool PointerDownOutside(Point windowPoint)
    {
        // A tap on the combo box itself closes the list without opening it again.
        var bounds = _combo.BoundsInWindow();
        if (bounds.Contains(windowPoint))
        {
            Close();
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public override bool KeyDown(Keys keyData)
    {
        var count = _combo.Items.Count;
        var page = Math.Max(1, _view.Area.Height / Math.Max(1, _view.ItemHeight));
        switch (keyData & Keys.KeyCode)
        {
            case Keys.Down when (keyData & Keys.Alt) == 0:
                Move(Math.Min(count - 1, _highlight + 1));
                return true;
            case Keys.Up when (keyData & Keys.Alt) == 0:
                Move(Math.Max(0, _highlight - 1));
                return true;
            case Keys.PageDown:
                Move(Math.Min(count - 1, Math.Max(0, _highlight) + page));
                return true;
            case Keys.PageUp:
                Move(Math.Max(0, _highlight - page));
                return true;
            case Keys.Enter:
                if (_highlight >= 0)
                {
                    Pick(_highlight);
                }
                else
                {
                    Close();
                }

                return true;
            case Keys.Escape or Keys.F4:
            case Keys.Up or Keys.Down:
                Close();
                return true;
            case Keys.Tab:
                if (_highlight >= 0)
                {
                    _combo.SelectIndex(_highlight, byUser: true);
                }

                Close();
                return false;
        }

        return false;
    }

    /// <inheritdoc />
    public override void OnClosed() => _combo.OnListClosed();

    private void Highlight(int index)
    {
        if (index >= 0 && index != _highlight)
        {
            _highlight = index;
            Invalidate();
        }
    }

    private void Move(int index)
    {
        if (index < 0)
        {
            return;
        }

        // As in Windows, moving with the keys in an open list selects as it goes.
        _combo.SelectIndex(index, byUser: true);
        _highlight = index;
        _view.EnsureVisible(index);
        Invalidate();
    }

    private void Pick(int index)
    {
        Close();
        _combo.SelectIndex(index, byUser: true);
    }
}
