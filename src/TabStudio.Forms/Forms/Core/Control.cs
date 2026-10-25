using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// The base of every control: a rectangle in its parent with text, colors, a font, child controls, layout, painting,
/// mouse and keyboard input, and focus.
/// </summary>
/// <remarks>
/// <para>
/// Controls are plain objects that the Forms layer lays out and draws itself, the way Windows Forms behaves:
/// <see cref="Dock"/>, <see cref="Anchor"/> and <see cref="AutoSize"/> place them in their parent's coordinates, in
/// pixels, so the coordinates the Windows Forms designer writes look the same here. A shown form is drawn into the
/// app preview by its window; drawing follows each event the app handles.
/// </para>
/// <para>
/// Once a control is shown, it belongs to the app's UI thread, as in Windows Forms: other threads use
/// <see cref="Invoke(Delegate)"/> or <see cref="BeginInvoke(Delegate)"/> to reach it.
/// </para>
/// </remarks>
[DefaultEvent(nameof(Click))]
[DefaultProperty(nameof(Text))]
public partial class Control : Component, ISynchronizeInvoke, IWin32Window
{
    private static int _nextHandle = 0x10000;

    private readonly ControlCollection _controls;
    private Control? _parent;
    private string _text = string.Empty;
    private string _name = string.Empty;
    private Font? _font;
    private Color _foreColor = Color.Empty;
    private Color _backColor = Color.Empty;
    private Image? _backgroundImage;
    private ImageLayout _backgroundImageLayout = ImageLayout.Tile;
    private Cursor? _cursor;
    private ContextMenuStrip? _contextMenuStrip;
    private bool _enabled = true;
    private bool _visible = true;
    private bool _tabStop = true;
    private int _tabIndex = -1;
    private bool _causesValidation = true;
    private RightToLeft _rightToLeft = RightToLeft.Inherit;
    private ControlStyles _styles;
    private IntPtr _handle;
    private bool _created;
    private bool _disposing;
    private bool _isDisposed;

    /// <summary>Creates a control at its default size.</summary>
    public Control()
    {
        _controls = CreateControlsInstance();
        _styles = ControlStyles.UserPaint | ControlStyles.StandardClick | ControlStyles.StandardDoubleClick | ControlStyles.Selectable
            | ControlStyles.AllPaintingInWmPaint | ControlStyles.UseTextForAccessibility;
        var size = DefaultSize;
        _width = size.Width;
        _height = size.Height;
        _padding = DefaultPadding;
        _margin = DefaultMargin;
        _minimumSize = DefaultMinimumSize;
        _maximumSize = DefaultMaximumSize;
        _clientWidth = _width;
        _clientHeight = _height;
    }

    /// <summary>Creates a control with text.</summary>
    /// <param name="text">The text.</param>
    public Control(string? text)
        : this() => _text = text ?? string.Empty;

    /// <summary>Creates a control with text in a parent.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="text">The text.</param>
    public Control(Control? parent, string? text)
        : this(text) => parent?.Controls.Add(this);

    /// <summary>Creates a control with text and bounds.</summary>
    /// <param name="text">The text.</param>
    /// <param name="left">The left.</param>
    /// <param name="top">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public Control(string? text, int left, int top, int width, int height)
        : this(text) => SetBounds(left, top, width, height);

    /// <summary>Creates a control with text and bounds in a parent.</summary>
    /// <param name="parent">The parent.</param>
    /// <param name="text">The text.</param>
    /// <param name="left">The left.</param>
    /// <param name="top">The top.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    public Control(Control? parent, string? text, int left, int top, int width, int height)
        : this(text, left, top, width, height) => parent?.Controls.Add(this);

    /// <summary>The default font of controls: Segoe UI, 9 points, unless <see cref="Application.SetDefaultFont"/> set another.</summary>
    public static Font DefaultFont => Application.DefaultFontInternal;

    /// <summary>The default foreground color: <see cref="SystemColors.ControlText"/>.</summary>
    public static Color DefaultForeColor => SystemColors.ControlText;

    /// <summary>The default background color: <see cref="SystemColors.Control"/>.</summary>
    public static Color DefaultBackColor => SystemColors.Control;

    /// <summary>Whether a control used from a thread other than the one that created it throws, as when debugging in Visual Studio.</summary>
    public static bool CheckForIllegalCrossThreadCalls { get; set; } = true;

    /// <summary>The modifier keys held now.</summary>
    public static Keys ModifierKeys => FormsRuntime.Current?.ModifierKeys ?? Keys.None;

    /// <summary>The mouse buttons held now; a finger on the screen is the left button.</summary>
    public static MouseButtons MouseButtons => FormsRuntime.Current?.MouseButtons ?? MouseButtons.None;

    /// <summary>The pointer's last position, in screen coordinates.</summary>
    public static Point MousePosition => Cursor.Position;

    /// <summary>The name, as the designer sets it to the field's name.</summary>
    public string Name
    {
        get => _name.Length > 0 ? _name : Site?.Name ?? string.Empty;
        set => _name = value ?? string.Empty;
    }

    /// <summary>The text of the control: a button's caption, a text box's contents, a form's title.</summary>
    [AllowNull]
    public virtual string Text
    {
        get => _text;
        set
        {
            value ??= string.Empty;
            if (string.Equals(_text, value, StringComparison.Ordinal))
            {
                return;
            }

            VerifyThread();
            _text = value;
            OnTextChanged(EventArgs.Empty);
        }
    }

    /// <summary>Data about the control.</summary>
    public object? Tag { get; set; }

    /// <summary>The parent control, or <see langword="null"/>.</summary>
    public Control? Parent
    {
        get => _parent;
        set
        {
            if (_parent == value)
            {
                return;
            }

            if (value is null)
            {
                _parent!.Controls.Remove(this);
            }
            else
            {
                value.Controls.Add(this);
            }
        }
    }

    /// <summary>The child controls.</summary>
    public ControlCollection Controls => _controls;

    /// <summary>Whether the control has child controls.</summary>
    public bool HasChildren => _controls.Count > 0;

    /// <summary>The font: the parent's unless set.</summary>
    [AllowNull]
    public virtual Font Font
    {
        get => _font ?? _parent?.Font ?? DefaultFont;
        set
        {
            if (Equals(_font, value))
            {
                return;
            }

            VerifyThread();
            _font = value;
            OnFontChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether the font was set on the control rather than taken from its parent.</summary>
    internal bool IsFontSet => _font is not null;

    /// <summary>Whether the text color was set on the control rather than taken from its parent.</summary>
    internal bool IsForeColorSet => !_foreColor.IsEmpty;

    /// <summary>Whether the background color was set on the control rather than taken from its parent.</summary>
    internal bool IsBackColorSet => !_backColor.IsEmpty;

    /// <summary>The text color: the parent's unless set.</summary>
    public virtual Color ForeColor
    {
        get => !_foreColor.IsEmpty ? _foreColor : _parent?.ForeColor ?? DefaultForeColor;
        set
        {
            if (_foreColor == value)
            {
                return;
            }

            VerifyThread();
            _foreColor = value;
            OnForeColorChanged(EventArgs.Empty);
        }
    }

    /// <summary>The background color: the parent's unless set.</summary>
    public virtual Color BackColor
    {
        get => !_backColor.IsEmpty ? _backColor : _parent?.BackColor ?? DefaultBackColor;
        set
        {
            if (!value.IsEmpty && value.A < 255 && !GetStyle(ControlStyles.SupportsTransparentBackColor))
            {
                throw new ArgumentException("Control does not support transparent background colors.");
            }

            if (_backColor == value)
            {
                return;
            }

            VerifyThread();
            _backColor = value;
            OnBackColorChanged(EventArgs.Empty);
        }
    }

    /// <summary>An image drawn behind the control's content.</summary>
    public virtual Image? BackgroundImage
    {
        get => _backgroundImage;
        set
        {
            if (_backgroundImage == value)
            {
                return;
            }

            _backgroundImage = value;
            OnBackgroundImageChanged(EventArgs.Empty);
        }
    }

    /// <summary>How <see cref="BackgroundImage"/> is laid out.</summary>
    public virtual ImageLayout BackgroundImageLayout
    {
        get => _backgroundImageLayout;
        set
        {
            if (_backgroundImageLayout == value)
            {
                return;
            }

            _backgroundImageLayout = value;
            OnBackgroundImageLayoutChanged(EventArgs.Empty);
        }
    }

    /// <summary>The cursor shown over the control: the parent's unless set.</summary>
    [AllowNull]
    public virtual Cursor Cursor
    {
        get => _cursor ?? _parent?.Cursor ?? Cursors.Default;
        set
        {
            if (_cursor == value)
            {
                return;
            }

            _cursor = value;
            OnCursorChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether the wait cursor is shown over the control.</summary>
    public bool UseWaitCursor { get; set; }

    /// <summary>The shortcut menu shown when the control is right-clicked or long-pressed.</summary>
    public virtual ContextMenuStrip? ContextMenuStrip
    {
        get => _contextMenuStrip;
        set
        {
            if (_contextMenuStrip == value)
            {
                return;
            }

            _contextMenuStrip = value;
            OnContextMenuStripChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether the control responds to input; a disabled parent disables its children.</summary>
    public bool Enabled
    {
        get => _enabled && (_parent?.Enabled ?? true);
        set
        {
            var was = Enabled;
            _enabled = value;
            if (was != Enabled)
            {
                VerifyThread();
                OnEnabledChanged(EventArgs.Empty);
            }
        }
    }

    /// <summary>Whether the control is shown; a hidden parent hides its children.</summary>
    public bool Visible
    {
        get => GetVisibleCore();
        set => SetVisibleCore(value);
    }

    /// <summary>The position in the tab order of the parent.</summary>
    public int TabIndex
    {
        get => _tabIndex < 0 ? 0 : _tabIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_tabIndex == value)
            {
                return;
            }

            _tabIndex = value;
            OnTabIndexChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether Tab moves focus to the control.</summary>
    public bool TabStop
    {
        get => _tabStop;
        set
        {
            if (_tabStop == value)
            {
                return;
            }

            _tabStop = value;
            OnTabStopChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether moving focus away validates the control first.</summary>
    public bool CausesValidation
    {
        get => _causesValidation;
        set
        {
            if (_causesValidation == value)
            {
                return;
            }

            _causesValidation = value;
            OnCausesValidationChanged(EventArgs.Empty);
        }
    }

    /// <summary>Whether text is laid out right to left. Kept for compatibility: text is laid out left to right.</summary>
    public virtual RightToLeft RightToLeft
    {
        get => _rightToLeft == RightToLeft.Inherit ? _parent?.RightToLeft ?? RightToLeft.No : _rightToLeft;
        set
        {
            if (_rightToLeft == value)
            {
                return;
            }

            _rightToLeft = value;
            OnRightToLeftChanged(EventArgs.Empty);
        }
    }

    /// <summary>The input method mode. Kept for compatibility: the device's keyboard decides.</summary>
    public ImeMode ImeMode { get; set; } = ImeMode.Inherit;

    /// <summary>Whether the control accepts dragged data. Drag and drop between controls is not supported on this device.</summary>
    public virtual bool AllowDrop { get; set; }

    /// <summary>The name accessibility tools read.</summary>
    public string? AccessibleName { get; set; }

    /// <summary>The description accessibility tools read.</summary>
    public string? AccessibleDescription { get; set; }

    /// <summary>The window handle: an identifier of the control, created when first read.</summary>
    public IntPtr Handle
    {
        get
        {
            if (_handle == IntPtr.Zero)
            {
                _handle = Interlocked.Increment(ref _nextHandle);
                OnHandleCreated(EventArgs.Empty);
            }

            return _handle;
        }
    }

    /// <summary>Whether the control has a handle.</summary>
    public bool IsHandleCreated => _handle != IntPtr.Zero;

    /// <summary>Whether the control has been created, which it is once its form is shown.</summary>
    public bool Created => _created;

    /// <summary>Whether the control has been disposed.</summary>
    public bool IsDisposed => _isDisposed;

    /// <summary>Whether the control is being disposed.</summary>
    public bool Disposing => _disposing;

    /// <summary>Whether the control is being designed in TabStudio's designer.</summary>
    protected internal bool IsInDesigner => Site?.DesignMode == true || (_parent?.IsInDesigner ?? false);

    /// <summary>The control's form, or the outermost control when it is not on a form.</summary>
    public Control? TopLevelControl
    {
        get
        {
            var control = this;
            while (control._parent is { } parent)
            {
                control = parent;
            }

            return control;
        }
    }

    /// <summary>The window the control is shown in, or <see langword="null"/> when it is not shown.</summary>
    internal FormWindow? Window => (TopLevelControl as Form)?.WindowInternal;

    /// <summary>
    /// Called when the outermost control of a tree that is not shown in a window must be drawn again, as the
    /// designer shows a form.
    /// </summary>
    internal Action? RepaintRequested { get; set; }

    /// <summary>Whether the control's thread may use it: always, until it is shown.</summary>
    public bool InvokeRequired => Window?.Runtime is { } runtime && !runtime.IsOnThread;

    /// <summary>The form the control is on, or <see langword="null"/>.</summary>
    public Form? FindForm()
    {
        var control = this;
        while (control is not null and not Form)
        {
            control = control._parent;
        }

        return control as Form;
    }

    /// <summary>The container control the control is in, such as its form.</summary>
    public IContainerControl? GetContainerControl()
    {
        var control = _parent;
        while (control is not null and not IContainerControl)
        {
            control = control._parent;
        }

        return control as IContainerControl;
    }

    /// <summary>Whether a control is this control's child or descendant.</summary>
    /// <param name="ctl">The control.</param>
    public bool Contains(Control? ctl)
    {
        for (var control = ctl?._parent; control is not null; control = control._parent)
        {
            if (control == this)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Creates the control and its children, as a shown form does.</summary>
    public void CreateControl()
    {
        if (_created)
        {
            return;
        }

        _created = true;
        _ = Handle;
        foreach (var child in _controls.ToArray())
        {
            child.CreateControl();
        }

        OnCreateControl();
    }

    /// <summary>Shows the control.</summary>
    public void Show() => Visible = true;

    /// <summary>Hides the control.</summary>
    public void Hide() => Visible = false;

    /// <summary>Brings the control to the front of its parent's z-order.</summary>
    public void BringToFront()
    {
        if (_parent is { } parent)
        {
            parent.Controls.SetChildIndex(this, 0);
        }
        else if (this is Form form)
        {
            form.Activate();
        }
    }

    /// <summary>Sends the control to the back of its parent's z-order.</summary>
    public void SendToBack() => _parent?.Controls.SetChildIndex(this, _parent.Controls.Count - 1);

    /// <summary>Resets the background color to the parent's.</summary>
    public virtual void ResetBackColor() => BackColor = Color.Empty;

    /// <summary>Resets the text color to the parent's.</summary>
    public virtual void ResetForeColor() => ForeColor = Color.Empty;

    /// <summary>Resets the font to the parent's.</summary>
    public virtual void ResetFont() => Font = null;

    /// <summary>Resets the text to empty.</summary>
    public virtual void ResetText() => Text = string.Empty;

    /// <summary>Resets the cursor to the parent's.</summary>
    public void ResetCursor() => Cursor = null;

    /// <summary>Whether a style is set.</summary>
    /// <param name="flag">The style.</param>
    protected bool GetStyle(ControlStyles flag) => (_styles & flag) == flag;

    /// <summary>Sets or clears styles.</summary>
    /// <param name="flag">The styles.</param>
    /// <param name="value">Whether they are set.</param>
    protected void SetStyle(ControlStyles flag, bool value) => _styles = value ? _styles | flag : _styles & ~flag;

    /// <summary>Raises the HandleCreated event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnHandleCreated(EventArgs e) => ((EventHandler?)Events[EventHandleCreated])?.Invoke(this, e);

    /// <summary>Raises the HandleDestroyed event.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnHandleDestroyed(EventArgs e) => ((EventHandler?)Events[EventHandleDestroyed])?.Invoke(this, e);

    /// <summary>Called when the control is created, before it is first shown.</summary>
    protected virtual void OnCreateControl()
    {
    }

    /// <summary>Whether the control is visible itself and in a visible parent.</summary>
    protected virtual bool GetVisibleCore() => _visible && (_parent?.Visible ?? (this is not Form form || form.IsShown || IsInDesigner || RepaintRequested is not null));

    /// <summary>Shows or hides the control.</summary>
    /// <param name="value">Whether it is shown.</param>
    protected virtual void SetVisibleCore(bool value)
    {
        if (_visible == value)
        {
            return;
        }

        var was = Visible;
        _visible = value;
        VerifyThread();
        if (was != Visible || _parent is null)
        {
            OnVisibleChanged(EventArgs.Empty);
        }

        _parent?.PerformLayout(this, nameof(Visible));
    }

    /// <summary>Whether the control itself is set visible, regardless of its parents.</summary>
    internal bool VisibleSelf => _visible;

    /// <summary>
    /// Throws when another thread uses a control that is shown, as Windows Forms does when debugging: such code must
    /// use <see cref="Invoke(Delegate)"/>.
    /// </summary>
    private protected void VerifyThread()
    {
        if (CheckForIllegalCrossThreadCalls && Window?.Runtime is { IsOnThread: false })
        {
            throw new InvalidOperationException($"Cross-thread operation not valid: Control '{Name}' accessed from a thread other than the thread it was created on. Use Invoke or BeginInvoke.");
        }
    }

    /// <summary>Marks the control as created and gives its tree a handle, as showing its form does.</summary>
    internal void MarkCreated() => CreateControl();

    /// <summary>Creates the collection of child controls.</summary>
    protected virtual ControlCollection CreateControlsInstance() => new(this);

    /// <summary>Sets the parent without going through a collection; called by <see cref="ControlCollection"/>.</summary>
    /// <param name="parent">The new parent.</param>
    internal void AssignParent(Control? parent)
    {
        var oldEnabled = Enabled;
        var oldVisible = Visible;
        var oldFont = Font;
        var oldForeColor = ForeColor;
        var oldBackColor = BackColor;
        _parent = parent;
        OnParentChanged(EventArgs.Empty);
        OnParentChangedForBinding();
        if (oldEnabled != Enabled)
        {
            OnEnabledChanged(EventArgs.Empty);
        }

        if (oldVisible != Visible)
        {
            OnVisibleChanged(EventArgs.Empty);
        }

        if (!Equals(oldFont, Font))
        {
            OnFontChanged(EventArgs.Empty);
        }

        if (oldForeColor != ForeColor)
        {
            OnForeColorChanged(EventArgs.Empty);
        }

        if (oldBackColor != BackColor)
        {
            OnBackColorChanged(EventArgs.Empty);
        }

        if (parent?._created == true)
        {
            CreateControl();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (_isDisposed)
        {
            base.Dispose(disposing);
            return;
        }

        _disposing = true;
        try
        {
            if (disposing)
            {
                foreach (var child in _controls.ToArray())
                {
                    child.Dispose();
                }

                _parent?.Controls.Remove(this);
                if (_handle != IntPtr.Zero)
                {
                    OnHandleDestroyed(EventArgs.Empty);
                }
            }

            _isDisposed = true;
        }
        finally
        {
            _disposing = false;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        var type = GetType().FullName;
        return _text.Length > 0 ? $"{type}, Text: {_text}" : type ?? base.ToString();
    }
}
