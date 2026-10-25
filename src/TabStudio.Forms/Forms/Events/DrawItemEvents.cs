using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>The state of an item an app draws.</summary>
[Flags]
public enum DrawItemState
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>Selected.</summary>
    Selected = 1,

    /// <summary>Grayed.</summary>
    Grayed = 2,

    /// <summary>Disabled.</summary>
    Disabled = 4,

    /// <summary>Checked.</summary>
    Checked = 8,

    /// <summary>Has focus.</summary>
    Focus = 16,

    /// <summary>The default item.</summary>
    Default = 32,

    /// <summary>Under the pointer.</summary>
    HotLight = 64,

    /// <summary>Inactive.</summary>
    Inactive = 128,

    /// <summary>No keyboard cues.</summary>
    NoAccelerator = 256,

    /// <summary>No focus rectangle.</summary>
    NoFocusRect = 512,

    /// <summary>The edit part of a combo box.</summary>
    ComboBoxEdit = 4096,
}

/// <summary>Data for drawing an item of a list box or combo box.</summary>
public class DrawItemEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="graphics">The surface.</param>
    /// <param name="font">The font.</param>
    /// <param name="rect">The item's bounds.</param>
    /// <param name="index">The item.</param>
    /// <param name="state">The item's state.</param>
    public DrawItemEventArgs(Graphics graphics, Font? font, Rectangle rect, int index, DrawItemState state)
        : this(graphics, font, rect, index, state, SystemColors.WindowText, SystemColors.Window)
    {
    }

    /// <summary>Creates the data, with colors.</summary>
    /// <param name="graphics">The surface.</param>
    /// <param name="font">The font.</param>
    /// <param name="rect">The item's bounds.</param>
    /// <param name="index">The item.</param>
    /// <param name="state">The item's state.</param>
    /// <param name="foreColor">The text color.</param>
    /// <param name="backColor">The background color.</param>
    public DrawItemEventArgs(Graphics graphics, Font? font, Rectangle rect, int index, DrawItemState state, Color foreColor, Color backColor)
    {
        Graphics = graphics;
        Font = font;
        Bounds = rect;
        Index = index;
        State = state;
        ForeColor = (state & DrawItemState.Selected) != 0 ? SystemColors.HighlightText : foreColor;
        BackColor = (state & DrawItemState.Selected) != 0 ? SystemColors.Highlight : backColor;
    }

    /// <summary>The surface.</summary>
    public Graphics Graphics { get; }

    /// <summary>The font.</summary>
    public Font? Font { get; }

    /// <summary>The item's bounds.</summary>
    public Rectangle Bounds { get; }

    /// <summary>The item, or -1.</summary>
    public int Index { get; }

    /// <summary>The item's state.</summary>
    public DrawItemState State { get; }

    /// <summary>The text color for the state.</summary>
    public Color ForeColor { get; }

    /// <summary>The background color for the state.</summary>
    public Color BackColor { get; }

    /// <summary>Fills the item's background with <see cref="BackColor"/>.</summary>
    public virtual void DrawBackground()
    {
        using var brush = new SolidBrush(BackColor);
        Graphics.FillRectangle(brush, Bounds);
    }

    /// <summary>Draws the focus rectangle when the item has focus.</summary>
    public virtual void DrawFocusRectangle()
    {
        if ((State & DrawItemState.Focus) != 0 && (State & DrawItemState.NoFocusRect) == 0)
        {
            ThemePainter.DrawFocusRectangle(Graphics, Bounds);
        }
    }
}

/// <summary>Handles drawing an item.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void DrawItemEventHandler(object? sender, DrawItemEventArgs e);

/// <summary>Data for measuring an item an app draws.</summary>
public class MeasureItemEventArgs : EventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="graphics">The surface.</param>
    /// <param name="index">The item.</param>
    /// <param name="itemHeight">The item's height.</param>
    public MeasureItemEventArgs(Graphics graphics, int index, int itemHeight)
    {
        Graphics = graphics;
        Index = index;
        ItemHeight = itemHeight;
    }

    /// <summary>Creates the data.</summary>
    /// <param name="graphics">The surface.</param>
    /// <param name="index">The item.</param>
    public MeasureItemEventArgs(Graphics graphics, int index)
        : this(graphics, index, 0)
    {
    }

    /// <summary>The surface.</summary>
    public Graphics Graphics { get; }

    /// <summary>The item.</summary>
    public int Index { get; }

    /// <summary>The item's height; handlers set it.</summary>
    public int ItemHeight { get; set; }

    /// <summary>The item's width; handlers set it.</summary>
    public int ItemWidth { get; set; }
}

/// <summary>Handles measuring an item.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void MeasureItemEventHandler(object? sender, MeasureItemEventArgs e);
