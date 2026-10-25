using System.ComponentModel;

namespace System.Windows.Forms;

/// <summary>The events of every control, kept in <see cref="Component.Events"/> as Windows Forms keeps them.</summary>
public partial class Control
{
    private static readonly object EventAutoSizeChanged = new();
    private static readonly object EventBackColorChanged = new();
    private static readonly object EventBackgroundImageChanged = new();
    private static readonly object EventBackgroundImageLayoutChanged = new();
    private static readonly object EventCausesValidationChanged = new();
    private static readonly object EventClick = new();
    private static readonly object EventClientSizeChanged = new();
    private static readonly object EventContextMenuStripChanged = new();
    private static readonly object EventControlAdded = new();
    private static readonly object EventControlRemoved = new();
    private static readonly object EventCursorChanged = new();
    private static readonly object EventDockChanged = new();
    private static readonly object EventDoubleClick = new();
    private static readonly object EventEnabledChanged = new();
    private static readonly object EventEnter = new();
    private static readonly object EventFontChanged = new();
    private static readonly object EventForeColorChanged = new();
    private static readonly object EventGotFocus = new();
    private static readonly object EventHandleCreated = new();
    private static readonly object EventHandleDestroyed = new();
    private static readonly object EventInvalidated = new();
    private static readonly object EventKeyDown = new();
    private static readonly object EventKeyPress = new();
    private static readonly object EventKeyUp = new();
    private static readonly object EventLayout = new();
    private static readonly object EventLeave = new();
    private static readonly object EventLocationChanged = new();
    private static readonly object EventLostFocus = new();
    private static readonly object EventMarginChanged = new();
    private static readonly object EventMouseCaptureChanged = new();
    private static readonly object EventMouseClick = new();
    private static readonly object EventMouseDoubleClick = new();
    private static readonly object EventMouseDown = new();
    private static readonly object EventMouseEnter = new();
    private static readonly object EventMouseHover = new();
    private static readonly object EventMouseLeave = new();
    private static readonly object EventMouseMove = new();
    private static readonly object EventMouseUp = new();
    private static readonly object EventMouseWheel = new();
    private static readonly object EventMove = new();
    private static readonly object EventPaddingChanged = new();
    private static readonly object EventPaint = new();
    private static readonly object EventParentChanged = new();
    private static readonly object EventPreviewKeyDown = new();
    private static readonly object EventResize = new();
    private static readonly object EventRightToLeftChanged = new();
    private static readonly object EventSizeChanged = new();
    private static readonly object EventStyleChanged = new();
    private static readonly object EventTabIndexChanged = new();
    private static readonly object EventTabStopChanged = new();
    private static readonly object EventTextChanged = new();
    private static readonly object EventValidated = new();
    private static readonly object EventValidating = new();
    private static readonly object EventVisibleChanged = new();

    /// <summary>Occurs when the value of AutoSize changes.</summary>
    public event EventHandler? AutoSizeChanged
    {
        add => Events.AddHandler(EventAutoSizeChanged, value);
        remove => Events.RemoveHandler(EventAutoSizeChanged, value);
    }

    /// <summary>Occurs when the background color changes.</summary>
    public event EventHandler? BackColorChanged
    {
        add => Events.AddHandler(EventBackColorChanged, value);
        remove => Events.RemoveHandler(EventBackColorChanged, value);
    }

    /// <summary>Occurs when the background image changes.</summary>
    public event EventHandler? BackgroundImageChanged
    {
        add => Events.AddHandler(EventBackgroundImageChanged, value);
        remove => Events.RemoveHandler(EventBackgroundImageChanged, value);
    }

    /// <summary>Occurs when the layout of the background image changes.</summary>
    public event EventHandler? BackgroundImageLayoutChanged
    {
        add => Events.AddHandler(EventBackgroundImageLayoutChanged, value);
        remove => Events.RemoveHandler(EventBackgroundImageLayoutChanged, value);
    }

    /// <summary>Occurs when the value of CausesValidation changes.</summary>
    public event EventHandler? CausesValidationChanged
    {
        add => Events.AddHandler(EventCausesValidationChanged, value);
        remove => Events.RemoveHandler(EventCausesValidationChanged, value);
    }

    /// <summary>Occurs when the control is clicked or tapped.</summary>
    public event EventHandler? Click
    {
        add => Events.AddHandler(EventClick, value);
        remove => Events.RemoveHandler(EventClick, value);
    }

    /// <summary>Occurs when the size of the client area changes.</summary>
    public event EventHandler? ClientSizeChanged
    {
        add => Events.AddHandler(EventClientSizeChanged, value);
        remove => Events.RemoveHandler(EventClientSizeChanged, value);
    }

    /// <summary>Occurs when the context menu changes.</summary>
    public event EventHandler? ContextMenuStripChanged
    {
        add => Events.AddHandler(EventContextMenuStripChanged, value);
        remove => Events.RemoveHandler(EventContextMenuStripChanged, value);
    }

    /// <summary>Occurs when a control is added to Controls.</summary>
    public event ControlEventHandler? ControlAdded
    {
        add => Events.AddHandler(EventControlAdded, value);
        remove => Events.RemoveHandler(EventControlAdded, value);
    }

    /// <summary>Occurs when a control is removed from Controls.</summary>
    public event ControlEventHandler? ControlRemoved
    {
        add => Events.AddHandler(EventControlRemoved, value);
        remove => Events.RemoveHandler(EventControlRemoved, value);
    }

    /// <summary>Occurs when the cursor changes.</summary>
    public event EventHandler? CursorChanged
    {
        add => Events.AddHandler(EventCursorChanged, value);
        remove => Events.RemoveHandler(EventCursorChanged, value);
    }

    /// <summary>Occurs when the value of Dock changes.</summary>
    public event EventHandler? DockChanged
    {
        add => Events.AddHandler(EventDockChanged, value);
        remove => Events.RemoveHandler(EventDockChanged, value);
    }

    /// <summary>Occurs when the control is double-clicked or double-tapped.</summary>
    public event EventHandler? DoubleClick
    {
        add => Events.AddHandler(EventDoubleClick, value);
        remove => Events.RemoveHandler(EventDoubleClick, value);
    }

    /// <summary>Occurs when the value of Enabled changes.</summary>
    public event EventHandler? EnabledChanged
    {
        add => Events.AddHandler(EventEnabledChanged, value);
        remove => Events.RemoveHandler(EventEnabledChanged, value);
    }

    /// <summary>Occurs when the control becomes the active control of its form.</summary>
    public event EventHandler? Enter
    {
        add => Events.AddHandler(EventEnter, value);
        remove => Events.RemoveHandler(EventEnter, value);
    }

    /// <summary>Occurs when the font changes.</summary>
    public event EventHandler? FontChanged
    {
        add => Events.AddHandler(EventFontChanged, value);
        remove => Events.RemoveHandler(EventFontChanged, value);
    }

    /// <summary>Occurs when the foreground color changes.</summary>
    public event EventHandler? ForeColorChanged
    {
        add => Events.AddHandler(EventForeColorChanged, value);
        remove => Events.RemoveHandler(EventForeColorChanged, value);
    }

    /// <summary>Occurs when the control receives focus.</summary>
    public event EventHandler? GotFocus
    {
        add => Events.AddHandler(EventGotFocus, value);
        remove => Events.RemoveHandler(EventGotFocus, value);
    }

    /// <summary>Occurs when the control is created on screen.</summary>
    public event EventHandler? HandleCreated
    {
        add => Events.AddHandler(EventHandleCreated, value);
        remove => Events.RemoveHandler(EventHandleCreated, value);
    }

    /// <summary>Occurs when the control is removed from the screen for good.</summary>
    public event EventHandler? HandleDestroyed
    {
        add => Events.AddHandler(EventHandleDestroyed, value);
        remove => Events.RemoveHandler(EventHandleDestroyed, value);
    }

    /// <summary>Occurs when the control must be drawn again.</summary>
    public event InvalidateEventHandler? Invalidated
    {
        add => Events.AddHandler(EventInvalidated, value);
        remove => Events.RemoveHandler(EventInvalidated, value);
    }

    /// <summary>Occurs when a key is pressed while the control has focus.</summary>
    public event KeyEventHandler? KeyDown
    {
        add => Events.AddHandler(EventKeyDown, value);
        remove => Events.RemoveHandler(EventKeyDown, value);
    }

    /// <summary>Occurs when a character is typed while the control has focus.</summary>
    public event KeyPressEventHandler? KeyPress
    {
        add => Events.AddHandler(EventKeyPress, value);
        remove => Events.RemoveHandler(EventKeyPress, value);
    }

    /// <summary>Occurs when a key is released while the control has focus.</summary>
    public event KeyEventHandler? KeyUp
    {
        add => Events.AddHandler(EventKeyUp, value);
        remove => Events.RemoveHandler(EventKeyUp, value);
    }

    /// <summary>Occurs when the control lays out its child controls.</summary>
    public event LayoutEventHandler? Layout
    {
        add => Events.AddHandler(EventLayout, value);
        remove => Events.RemoveHandler(EventLayout, value);
    }

    /// <summary>Occurs when the control stops being the active control of its form.</summary>
    public event EventHandler? Leave
    {
        add => Events.AddHandler(EventLeave, value);
        remove => Events.RemoveHandler(EventLeave, value);
    }

    /// <summary>Occurs when the location changes.</summary>
    public event EventHandler? LocationChanged
    {
        add => Events.AddHandler(EventLocationChanged, value);
        remove => Events.RemoveHandler(EventLocationChanged, value);
    }

    /// <summary>Occurs when the control loses focus.</summary>
    public event EventHandler? LostFocus
    {
        add => Events.AddHandler(EventLostFocus, value);
        remove => Events.RemoveHandler(EventLostFocus, value);
    }

    /// <summary>Occurs when the margin changes.</summary>
    public event EventHandler? MarginChanged
    {
        add => Events.AddHandler(EventMarginChanged, value);
        remove => Events.RemoveHandler(EventMarginChanged, value);
    }

    /// <summary>Occurs when the control loses the mouse capture.</summary>
    public event EventHandler? MouseCaptureChanged
    {
        add => Events.AddHandler(EventMouseCaptureChanged, value);
        remove => Events.RemoveHandler(EventMouseCaptureChanged, value);
    }

    /// <summary>Occurs when the control is clicked or tapped, with the pointer's position.</summary>
    public event MouseEventHandler? MouseClick
    {
        add => Events.AddHandler(EventMouseClick, value);
        remove => Events.RemoveHandler(EventMouseClick, value);
    }

    /// <summary>Occurs when the control is double-clicked, with the pointer's position.</summary>
    public event MouseEventHandler? MouseDoubleClick
    {
        add => Events.AddHandler(EventMouseDoubleClick, value);
        remove => Events.RemoveHandler(EventMouseDoubleClick, value);
    }

    /// <summary>Occurs when a mouse button or a finger goes down on the control.</summary>
    public event MouseEventHandler? MouseDown
    {
        add => Events.AddHandler(EventMouseDown, value);
        remove => Events.RemoveHandler(EventMouseDown, value);
    }

    /// <summary>Occurs when the pointer enters the control.</summary>
    public event EventHandler? MouseEnter
    {
        add => Events.AddHandler(EventMouseEnter, value);
        remove => Events.RemoveHandler(EventMouseEnter, value);
    }

    /// <summary>Occurs when the pointer rests on the control.</summary>
    public event EventHandler? MouseHover
    {
        add => Events.AddHandler(EventMouseHover, value);
        remove => Events.RemoveHandler(EventMouseHover, value);
    }

    /// <summary>Occurs when the pointer leaves the control.</summary>
    public event EventHandler? MouseLeave
    {
        add => Events.AddHandler(EventMouseLeave, value);
        remove => Events.RemoveHandler(EventMouseLeave, value);
    }

    /// <summary>Occurs when the pointer moves over the control, or a finger moves after touching it.</summary>
    public event MouseEventHandler? MouseMove
    {
        add => Events.AddHandler(EventMouseMove, value);
        remove => Events.RemoveHandler(EventMouseMove, value);
    }

    /// <summary>Occurs when a mouse button or a finger is released.</summary>
    public event MouseEventHandler? MouseUp
    {
        add => Events.AddHandler(EventMouseUp, value);
        remove => Events.RemoveHandler(EventMouseUp, value);
    }

    /// <summary>Occurs when the mouse wheel turns while the control has focus.</summary>
    public event MouseEventHandler? MouseWheel
    {
        add => Events.AddHandler(EventMouseWheel, value);
        remove => Events.RemoveHandler(EventMouseWheel, value);
    }

    /// <summary>Occurs when the control moves.</summary>
    public event EventHandler? Move
    {
        add => Events.AddHandler(EventMove, value);
        remove => Events.RemoveHandler(EventMove, value);
    }

    /// <summary>Occurs when the padding changes.</summary>
    public event EventHandler? PaddingChanged
    {
        add => Events.AddHandler(EventPaddingChanged, value);
        remove => Events.RemoveHandler(EventPaddingChanged, value);
    }

    /// <summary>Occurs when the control is drawn.</summary>
    public event PaintEventHandler? Paint
    {
        add => Events.AddHandler(EventPaint, value);
        remove => Events.RemoveHandler(EventPaint, value);
    }

    /// <summary>Occurs when the parent changes.</summary>
    public event EventHandler? ParentChanged
    {
        add => Events.AddHandler(EventParentChanged, value);
        remove => Events.RemoveHandler(EventParentChanged, value);
    }

    /// <summary>Occurs before KeyDown, when a key is pressed while the control has focus.</summary>
    public event PreviewKeyDownEventHandler? PreviewKeyDown
    {
        add => Events.AddHandler(EventPreviewKeyDown, value);
        remove => Events.RemoveHandler(EventPreviewKeyDown, value);
    }

    /// <summary>Occurs when the control is resized.</summary>
    public event EventHandler? Resize
    {
        add => Events.AddHandler(EventResize, value);
        remove => Events.RemoveHandler(EventResize, value);
    }

    /// <summary>Occurs when the value of RightToLeft changes.</summary>
    public event EventHandler? RightToLeftChanged
    {
        add => Events.AddHandler(EventRightToLeftChanged, value);
        remove => Events.RemoveHandler(EventRightToLeftChanged, value);
    }

    /// <summary>Occurs when the size changes.</summary>
    public event EventHandler? SizeChanged
    {
        add => Events.AddHandler(EventSizeChanged, value);
        remove => Events.RemoveHandler(EventSizeChanged, value);
    }

    /// <summary>Occurs when the control's style changes.</summary>
    public event EventHandler? StyleChanged
    {
        add => Events.AddHandler(EventStyleChanged, value);
        remove => Events.RemoveHandler(EventStyleChanged, value);
    }

    /// <summary>Occurs when the tab index changes.</summary>
    public event EventHandler? TabIndexChanged
    {
        add => Events.AddHandler(EventTabIndexChanged, value);
        remove => Events.RemoveHandler(EventTabIndexChanged, value);
    }

    /// <summary>Occurs when the value of TabStop changes.</summary>
    public event EventHandler? TabStopChanged
    {
        add => Events.AddHandler(EventTabStopChanged, value);
        remove => Events.RemoveHandler(EventTabStopChanged, value);
    }

    /// <summary>Occurs when the text changes.</summary>
    public event EventHandler? TextChanged
    {
        add => Events.AddHandler(EventTextChanged, value);
        remove => Events.RemoveHandler(EventTextChanged, value);
    }

    /// <summary>Occurs when the control has been validated.</summary>
    public event EventHandler? Validated
    {
        add => Events.AddHandler(EventValidated, value);
        remove => Events.RemoveHandler(EventValidated, value);
    }

    /// <summary>Occurs when the control is validating, before focus leaves it; setting Cancel keeps focus in it.</summary>
    public event CancelEventHandler? Validating
    {
        add => Events.AddHandler(EventValidating, value);
        remove => Events.RemoveHandler(EventValidating, value);
    }

    /// <summary>Occurs when the value of Visible changes.</summary>
    public event EventHandler? VisibleChanged
    {
        add => Events.AddHandler(EventVisibleChanged, value);
        remove => Events.RemoveHandler(EventVisibleChanged, value);
    }

    /// <summary>Raises the <see cref="CausesValidationChanged"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnCausesValidationChanged(EventArgs e) => ((EventHandler?)Events[EventCausesValidationChanged])?.Invoke(this, e);

    /// <summary>Raises the <see cref="Click"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnClick(EventArgs e) => ((EventHandler?)Events[EventClick])?.Invoke(this, e);

    /// <summary>Raises the <see cref="ContextMenuStripChanged"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnContextMenuStripChanged(EventArgs e) => ((EventHandler?)Events[EventContextMenuStripChanged])?.Invoke(this, e);

    /// <summary>Raises the <see cref="ControlAdded"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnControlAdded(ControlEventArgs e) => ((ControlEventHandler?)Events[EventControlAdded])?.Invoke(this, e);

    /// <summary>Raises the <see cref="ControlRemoved"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnControlRemoved(ControlEventArgs e) => ((ControlEventHandler?)Events[EventControlRemoved])?.Invoke(this, e);

    /// <summary>Raises the <see cref="DoubleClick"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnDoubleClick(EventArgs e) => ((EventHandler?)Events[EventDoubleClick])?.Invoke(this, e);

    /// <summary>Raises the <see cref="Enter"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnEnter(EventArgs e) => ((EventHandler?)Events[EventEnter])?.Invoke(this, e);

    /// <summary>Raises the <see cref="HandleCreated"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnHandleCreated(EventArgs e) => ((EventHandler?)Events[EventHandleCreated])?.Invoke(this, e);

    /// <summary>Raises the <see cref="HandleDestroyed"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnHandleDestroyed(EventArgs e) => ((EventHandler?)Events[EventHandleDestroyed])?.Invoke(this, e);

    /// <summary>Raises the <see cref="KeyDown"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnKeyDown(KeyEventArgs e) => ((KeyEventHandler?)Events[EventKeyDown])?.Invoke(this, e);

    /// <summary>Raises the <see cref="KeyPress"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnKeyPress(KeyPressEventArgs e) => ((KeyPressEventHandler?)Events[EventKeyPress])?.Invoke(this, e);

    /// <summary>Raises the <see cref="KeyUp"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnKeyUp(KeyEventArgs e) => ((KeyEventHandler?)Events[EventKeyUp])?.Invoke(this, e);

    /// <summary>Raises the <see cref="Leave"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnLeave(EventArgs e) => ((EventHandler?)Events[EventLeave])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseCaptureChanged"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseCaptureChanged(EventArgs e) => ((EventHandler?)Events[EventMouseCaptureChanged])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseClick"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseClick(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseClick])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseDoubleClick"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseDoubleClick(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseDoubleClick])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseDown"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseDown(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseDown])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseEnter"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseEnter(EventArgs e) => ((EventHandler?)Events[EventMouseEnter])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseHover"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseHover(EventArgs e) => ((EventHandler?)Events[EventMouseHover])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseLeave"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseLeave(EventArgs e) => ((EventHandler?)Events[EventMouseLeave])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseMove"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseMove(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseMove])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseUp"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseUp(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseUp])?.Invoke(this, e);

    /// <summary>Raises the <see cref="MouseWheel"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnMouseWheel(MouseEventArgs e) => ((MouseEventHandler?)Events[EventMouseWheel])?.Invoke(this, e);

    /// <summary>Raises the <see cref="PreviewKeyDown"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnPreviewKeyDown(PreviewKeyDownEventArgs e) => ((PreviewKeyDownEventHandler?)Events[EventPreviewKeyDown])?.Invoke(this, e);

    /// <summary>Raises the <see cref="StyleChanged"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnStyleChanged(EventArgs e) => ((EventHandler?)Events[EventStyleChanged])?.Invoke(this, e);

    /// <summary>Raises the <see cref="TabIndexChanged"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnTabIndexChanged(EventArgs e) => ((EventHandler?)Events[EventTabIndexChanged])?.Invoke(this, e);

    /// <summary>Raises the <see cref="TabStopChanged"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnTabStopChanged(EventArgs e) => ((EventHandler?)Events[EventTabStopChanged])?.Invoke(this, e);

    /// <summary>Raises the <see cref="Validated"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnValidated(EventArgs e) => ((EventHandler?)Events[EventValidated])?.Invoke(this, e);

    /// <summary>Raises the <see cref="Validating"/> event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnValidating(CancelEventArgs e) => ((CancelEventHandler?)Events[EventValidating])?.Invoke(this, e);
}
