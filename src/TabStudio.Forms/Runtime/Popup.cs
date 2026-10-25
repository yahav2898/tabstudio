using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Rendering;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// A drop-down drawn above its window: a combo box's list, a menu, a date picker's calendar. Popups take input before
/// the window's controls; a press outside every popup closes them.
/// </summary>
internal abstract class Popup
{
    /// <summary>The control that opened the popup.</summary>
    public abstract Control Owner { get; }

    /// <summary>Where the popup is, in the window's client coordinates.</summary>
    public Rectangle Bounds { get; set; }

    /// <summary>The window the popup is open in.</summary>
    public FormWindow? Window { get; set; }

    /// <summary>Draws the popup, in its own coordinates.</summary>
    /// <param name="g">The surface.</param>
    public abstract void Paint(Graphics g);

    /// <summary>A press on the popup.</summary>
    /// <param name="point">The point in the popup's coordinates.</param>
    public virtual void PointerDown(Point point)
    {
    }

    /// <summary>A move over the popup, pressed or not.</summary>
    /// <param name="point">The point in the popup's coordinates.</param>
    /// <param name="pressed">Whether a button or finger is down.</param>
    public virtual void PointerMove(Point point, bool pressed)
    {
    }

    /// <summary>A release over the popup.</summary>
    /// <param name="point">The point in the popup's coordinates.</param>
    public virtual void PointerUp(Point point)
    {
    }

    /// <summary>A move outside every popup, with no button or finger down.</summary>
    /// <param name="windowPoint">The point in the window's client coordinates.</param>
    public virtual void PointerMoveOutside(Point windowPoint)
    {
    }

    /// <summary>
    /// A press outside every popup, offered to the first popup open. Unless it takes the press, every popup closes
    /// and the press does nothing else, as in Windows.
    /// </summary>
    /// <param name="windowPoint">The point in the window's client coordinates.</param>
    /// <returns>Whether the popup took the press.</returns>
    public virtual bool PointerDownOutside(Point windowPoint) => false;

    /// <summary>A turn of the mouse wheel over the popup.</summary>
    /// <param name="delta">The wheel delta.</param>
    public virtual void Wheel(int delta)
    {
    }

    /// <summary>A key pressed while the popup is open.</summary>
    /// <param name="keyData">The key.</param>
    /// <returns>Whether the popup handled it.</returns>
    public virtual bool KeyDown(Keys keyData) => false;

    /// <summary>Called when the popup closes.</summary>
    public virtual void OnClosed()
    {
    }

    /// <summary>Asks for the popup to be drawn again.</summary>
    public void Invalidate() => Window?.Invalidate();

    /// <summary>Closes the popup.</summary>
    public void Close() => Window?.ClosePopup(this);

    /// <summary>Draws the popup into a display list, in the window's client coordinates.</summary>
    public DisplayList Render()
    {
        var builder = new DisplayListBuilder();
        Paint(new Graphics(builder, Bounds.Location, Bounds));
        return builder.Build();
    }
}

/// <summary>
/// A control whose text is edited in a native text box while it has focus: text boxes, editable combo boxes, numeric
/// up-downs and grid cells.
/// </summary>
internal interface INativeTextEditor
{
    /// <summary>The editor's frame, or <see langword="null"/> when the control is not editing now.</summary>
    /// <param name="clientOrigin">Where the control's client area is in the window.</param>
    EditorFrame? GetEditorFrame(Point clientOrigin);

    /// <summary>Takes text the user typed in the native box.</summary>
    /// <param name="text">The text.</param>
    /// <param name="selectionStart">The selection's start.</param>
    /// <param name="selectionLength">The selection's length.</param>
    /// <param name="revision">The text revision the native box had, to ignore edits the app has since replaced.</param>
    void ApplyEditorText(string text, int selectionStart, int selectionLength, int revision);

    /// <summary>Takes the selection the user made in the native box.</summary>
    /// <param name="selectionStart">The selection's start.</param>
    /// <param name="selectionLength">The selection's length.</param>
    void ApplyEditorSelection(int selectionStart, int selectionLength);
}
