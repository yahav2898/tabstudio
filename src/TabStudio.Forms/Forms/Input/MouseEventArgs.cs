using System.Drawing;

namespace System.Windows.Forms;

/// <summary>Data of the mouse events: MouseDown, MouseUp, MouseMove, MouseClick and MouseWheel.</summary>
public class MouseEventArgs : EventArgs
{
    /// <summary>Creates the data of a mouse event.</summary>
    /// <param name="button">The button pressed.</param>
    /// <param name="clicks">How many times it was pressed: 2 for a double click.</param>
    /// <param name="x">The x in the control's client coordinates.</param>
    /// <param name="y">The y in the control's client coordinates.</param>
    /// <param name="delta">How far the wheel turned, in multiples of 120 per notch.</param>
    public MouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
    {
        Button = button;
        Clicks = clicks;
        X = x;
        Y = y;
        Delta = delta;
    }

    /// <summary>The button pressed. A finger is the left button.</summary>
    public MouseButtons Button { get; }

    /// <summary>How many times the button was pressed and released: 2 for a double click.</summary>
    public int Clicks { get; }

    /// <summary>The x in the control's client coordinates.</summary>
    public int X { get; }

    /// <summary>The y in the control's client coordinates.</summary>
    public int Y { get; }

    /// <summary>How far the wheel turned, in multiples of 120 per notch; positive away from the user.</summary>
    public int Delta { get; }

    /// <summary>The location in the control's client coordinates.</summary>
    public Point Location => new(X, Y);
}

/// <summary>Handles a mouse event.</summary>
/// <param name="sender">The control.</param>
/// <param name="e">The event data.</param>
public delegate void MouseEventHandler(object? sender, MouseEventArgs e);

/// <summary>The data of a mouse wheel event that the handler can mark as handled.</summary>
public class HandledMouseEventArgs : MouseEventArgs
{
    /// <summary>Creates the data.</summary>
    /// <param name="button">The button.</param>
    /// <param name="clicks">The clicks.</param>
    /// <param name="x">The x.</param>
    /// <param name="y">The y.</param>
    /// <param name="delta">The wheel delta.</param>
    public HandledMouseEventArgs(MouseButtons button, int clicks, int x, int y, int delta)
        : base(button, clicks, x, y, delta)
    {
    }

    /// <summary>Whether the event was handled, which stops the control's own scrolling.</summary>
    public bool Handled { get; set; }
}
