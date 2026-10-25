using System.ComponentModel;
using System.Drawing;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// A window of the app: the main window <see cref="Application.Run(Form)"/> shows, another window shown with
/// <see cref="Control.Show()"/>, or a dialog shown with <see cref="ShowDialog()"/>. In TabStudio, forms are windows inside the
/// app preview, with a title bar to move them, a close button and, when sizable, a grip to resize them.
/// </summary>
[DefaultEvent(nameof(Load))]
public partial class Form : ContainerControl
{
    /// <summary>The width of a window's border on each side, as Windows 10 and 11 report it.</summary>
    internal const int FrameBorder = 8;

    /// <summary>The height of a window's title bar, border included.</summary>
    internal const int FrameTitle = 31;

    private static readonly object EventLoad = new();
    private static readonly object EventShown = new();
    private static readonly object EventActivated = new();
    private static readonly object EventDeactivate = new();
    private static readonly object EventFormClosing = new();
    private static readonly object EventFormClosed = new();
    private static readonly object EventResizeBegin = new();
    private static readonly object EventResizeEnd = new();

    private readonly List<Form> _ownedForms = [];
    private FormBorderStyle _formBorderStyle = FormBorderStyle.Sizable;
    private bool _controlBox = true;
    private bool _maximizeBox = true;
    private bool _minimizeBox = true;
    private FormWindowState _windowState = FormWindowState.Normal;
    private FormWindowState _stateBeforeMinimized = FormWindowState.Normal;
    private Rectangle _restoreBounds;
    private Point? _centeredAt;
    private Form? _owner;
    private DialogResult _dialogResult;
    private bool _modal;
    private bool _loaded;
    private bool _closing;
    private bool _topLevel = true;
    private IButtonControl? _acceptButton;
    private IButtonControl? _cancelButton;

    /// <summary>Creates a form, 300 by 300 pixels.</summary>
    public Form() => SetStyle(ControlStyles.ContainerControl, true);

    /// <summary>Occurs before the form is shown for the first time.</summary>
    public event EventHandler? Load
    {
        add => Events.AddHandler(EventLoad, value);
        remove => Events.RemoveHandler(EventLoad, value);
    }

    /// <summary>Occurs after the form is shown for the first time.</summary>
    public event EventHandler? Shown
    {
        add => Events.AddHandler(EventShown, value);
        remove => Events.RemoveHandler(EventShown, value);
    }

    /// <summary>Occurs when the form becomes the active window.</summary>
    public event EventHandler? Activated
    {
        add => Events.AddHandler(EventActivated, value);
        remove => Events.RemoveHandler(EventActivated, value);
    }

    /// <summary>Occurs when the form stops being the active window.</summary>
    public event EventHandler? Deactivate
    {
        add => Events.AddHandler(EventDeactivate, value);
        remove => Events.RemoveHandler(EventDeactivate, value);
    }

    /// <summary>Occurs before the form closes; a handler can cancel the closing.</summary>
    public event FormClosingEventHandler? FormClosing
    {
        add => Events.AddHandler(EventFormClosing, value);
        remove => Events.RemoveHandler(EventFormClosing, value);
    }

    /// <summary>Occurs after the form closes.</summary>
    public event FormClosedEventHandler? FormClosed
    {
        add => Events.AddHandler(EventFormClosed, value);
        remove => Events.RemoveHandler(EventFormClosed, value);
    }

    /// <summary>Occurs when the user starts resizing the window.</summary>
    public event EventHandler? ResizeBegin
    {
        add => Events.AddHandler(EventResizeBegin, value);
        remove => Events.RemoveHandler(EventResizeBegin, value);
    }

    /// <summary>Occurs when the user stops resizing the window.</summary>
    public event EventHandler? ResizeEnd
    {
        add => Events.AddHandler(EventResizeEnd, value);
        remove => Events.RemoveHandler(EventResizeEnd, value);
    }

    /// <summary>The active form of the app, or <see langword="null"/>.</summary>
    public static Form? ActiveForm => FormsRuntime.Current?.ActiveWindow?.Form;

    /// <summary>The border and title bar.</summary>
    [DefaultValue(FormBorderStyle.Sizable)]
    public FormBorderStyle FormBorderStyle
    {
        get => _formBorderStyle;
        set
        {
            if (_formBorderStyle == value)
            {
                return;
            }

            var client = ClientSize;
            _formBorderStyle = value;
            UpdateClientSize();
            ClientSize = client;
            InvalidateWindow();
        }
    }

    /// <summary>Whether the title bar has its buttons.</summary>
    public bool ControlBox
    {
        get => _controlBox;
        set
        {
            _controlBox = value;
            InvalidateWindow();
        }
    }

    /// <summary>Whether the title bar has a maximize button.</summary>
    public bool MaximizeBox
    {
        get => _maximizeBox;
        set
        {
            _maximizeBox = value;
            InvalidateWindow();
        }
    }

    /// <summary>Whether the title bar has a minimize button. The app preview keeps windows open, so it shows none.</summary>
    public bool MinimizeBox
    {
        get => _minimizeBox;
        set => _minimizeBox = value;
    }

    /// <summary>Whether the title bar has a help button. Kept for compatibility.</summary>
    public bool HelpButton { get; set; }

    /// <summary>Whether the title bar shows the icon. Kept for compatibility.</summary>
    public bool ShowIcon { get; set; } = true;

    /// <summary>Whether the form shows in the taskbar. Kept for compatibility.</summary>
    public bool ShowInTaskbar { get; set; } = true;

    /// <summary>Whether the form stays above other forms. Kept for compatibility.</summary>
    public bool TopMost { get; set; }

    /// <summary>The icon of the title bar. Kept for compatibility.</summary>
    public Icon? Icon { get; set; }

    /// <summary>The opacity of the window. Kept for compatibility: windows are opaque in the app preview.</summary>
    public double Opacity { get; set; } = 1.0;

    /// <summary>A color drawn transparent. Kept for compatibility.</summary>
    public Color TransparencyKey { get; set; }

    /// <summary>Whether a size grip shows in the corner. Kept for compatibility.</summary>
    public SizeGripStyle SizeGripStyle { get; set; }

    /// <summary>Where the form appears when first shown.</summary>
    [DefaultValue(FormStartPosition.WindowsDefaultLocation)]
    public FormStartPosition StartPosition { get; set; } = FormStartPosition.WindowsDefaultLocation;

    /// <summary>Whether the form is normal, maximized, or minimized to a button at the bottom of the app preview.</summary>
    [DefaultValue(FormWindowState.Normal)]
    public FormWindowState WindowState
    {
        get => _windowState;
        set
        {
            if (_windowState == value)
            {
                return;
            }

            if (value == FormWindowState.Minimized)
            {
                _stateBeforeMinimized = _windowState;
                _windowState = value;
                if (WindowInternal is { } window)
                {
                    window.OnMinimized();
                }

                InvalidateWindow();
                OnResize(EventArgs.Empty);
                return;
            }

            if (_windowState == FormWindowState.Minimized)
            {
                _windowState = _stateBeforeMinimized;
                if (_windowState == value)
                {
                    InvalidateWindow();
                    OnResize(EventArgs.Empty);
                    return;
                }
            }

            if (value == FormWindowState.Maximized)
            {
                _restoreBounds = Bounds;
                _windowState = value;
                if (WindowInternal is not null)
                {
                    ApplyMaximized();
                }
            }
            else
            {
                _windowState = value;
                if (WindowInternal is not null && !_restoreBounds.IsEmpty)
                {
                    Bounds = _restoreBounds;
                }
            }

            InvalidateWindow();
        }
    }

    /// <summary>The bounds the form has when it is not maximized.</summary>
    public Rectangle RestoreBounds => _windowState == FormWindowState.Maximized ? _restoreBounds : Bounds;

    /// <summary>The location on the desktop.</summary>
    public Point DesktopLocation
    {
        get => Location;
        set => Location = value;
    }

    /// <summary>The bounds on the desktop.</summary>
    public Rectangle DesktopBounds
    {
        get => Bounds;
        set => Bounds = value;
    }

    /// <summary>The button Enter presses.</summary>
    public IButtonControl? AcceptButton
    {
        get => _acceptButton;
        set
        {
            _acceptButton?.NotifyDefault(false);
            _acceptButton = value;
            value?.NotifyDefault(true);
        }
    }

    /// <summary>The button Esc presses; its DialogResult becomes Cancel when it has none.</summary>
    public IButtonControl? CancelButton
    {
        get => _cancelButton;
        set
        {
            _cancelButton = value;
            if (value is { DialogResult: DialogResult.None })
            {
                value.DialogResult = DialogResult.Cancel;
            }
        }
    }

    /// <summary>The result of the dialog; setting it closes a dialog shown with <see cref="ShowDialog()"/>.</summary>
    [DefaultValue(DialogResult.None)]
    public DialogResult DialogResult
    {
        get => _dialogResult;
        set
        {
            _dialogResult = value;
            if (_modal && value != DialogResult.None && WindowInternal is { } window)
            {
                // The dialog closes once the handler that set the result returns, unless it sets None again.
                window.Runtime.Post(() =>
                {
                    if (_dialogResult != DialogResult.None && WindowInternal == window)
                    {
                        CloseFromWindow(CloseReason.None);
                    }
                });
            }
        }
    }

    /// <summary>Whether the form is shown as a modal dialog.</summary>
    public bool Modal => _modal;

    /// <summary>Whether the form sees key events before the control with focus.</summary>
    public bool KeyPreview { get; set; }

    /// <summary>The form's main menu, whose shortcuts work anywhere on the form.</summary>
    public MenuStrip? MainMenuStrip { get; set; }

    /// <summary>Whether the form is a top-level window rather than a child control.</summary>
    public bool TopLevel
    {
        get => _topLevel;
        set
        {
            if (!value && WindowInternal is not null)
            {
                throw new InvalidOperationException("A shown form cannot stop being a top-level window.");
            }

            _topLevel = value;
        }
    }

    /// <summary>The form that owns this one: an owned form stays above its owner and closes with it.</summary>
    public Form? Owner
    {
        get => _owner;
        set
        {
            _owner?._ownedForms.Remove(this);
            _owner = value;
            value?._ownedForms.Add(this);
        }
    }

    /// <summary>The forms this form owns.</summary>
    public Form[] OwnedForms => [.. _ownedForms];

    /// <summary>Whether the form contains MDI child forms. MDI is not supported in the app preview.</summary>
    /// <exception cref="NotSupportedException">Set to <see langword="true"/>.</exception>
    public bool IsMdiContainer
    {
        get => false;
        set
        {
            if (value)
            {
                throw new NotSupportedException("Form.IsMdiContainer is not supported by TabStudio's Forms layer: show child forms as separate windows with Show(owner), or put a UserControl in a panel.");
            }
        }
    }

    /// <summary>The MDI parent. MDI is not supported in the app preview.</summary>
    /// <exception cref="NotSupportedException">Set to a form.</exception>
    public Form? MdiParent
    {
        get => null;
        set
        {
            if (value is not null)
            {
                throw new NotSupportedException("Form.MdiParent is not supported by TabStudio's Forms layer: show child forms as separate windows with Show(owner).");
            }
        }
    }

    /// <summary>The MDI children: none.</summary>
    public Form[] MdiChildren => [];

    /// <summary>The active MDI child: none.</summary>
    public Form? ActiveMdiChild => null;

    /// <summary>How the form sizes itself when AutoSize is on.</summary>
    public AutoSizeMode AutoSizeMode { get; set; } = AutoSizeMode.GrowOnly;

    /// <inheritdoc />
    protected override Size DefaultSize => new(300, 300);

    /// <summary>The window the form is shown in, while it is shown.</summary>
    internal FormWindow? WindowInternal { get; private set; }

    /// <summary>Whether the form is shown.</summary>
    internal bool IsShown => WindowInternal is not null;

    /// <summary>The frame of the window, as the app preview draws it.</summary>
    internal WindowChrome Chrome => new(_formBorderStyle, _controlBox, _maximizeBox, _minimizeBox, ShowIcon);

    /// <summary>The client area's top-left corner on the desktop.</summary>
    internal Point ClientOriginOnScreen
    {
        get
        {
            if (Parent is not null)
            {
                return PointToScreen(Point.Empty);
            }

            var insets = FrameInsets;
            return new Point(Left + insets.Left, Top + insets.Top);
        }
    }

    /// <summary>The visible window on the desktop: the client area with the title bar and a one-pixel border.</summary>
    internal Rectangle VisibleFrameOnScreen
    {
        get
        {
            var client = new Rectangle(ClientOriginOnScreen, ClientSize);
            var insets = FrameInsets;
            return insets.Top == 0
                ? client
                : Rectangle.FromLTRB(client.Left - 1, client.Top - insets.Top + (_windowState == FormWindowState.Maximized ? FrameBorder : 0), client.Right + 1, client.Bottom + 1);
        }
    }

    /// <inheritdoc />
    internal override Padding NonClientInsets
    {
        get
        {
            var frame = FrameInsets;
            var scroll = base.NonClientInsets;
            return new Padding(frame.Left + scroll.Left, frame.Top + scroll.Top, frame.Right + scroll.Right, frame.Bottom + scroll.Bottom);
        }
    }

    /// <summary>The window frame around the client area: the border and the title bar.</summary>
    private Padding FrameInsets => !_topLevel || _formBorderStyle == FormBorderStyle.None
        ? Padding.Empty
        : new Padding(FrameBorder, _formBorderStyle is FormBorderStyle.FixedToolWindow or FormBorderStyle.SizableToolWindow ? FrameTitle - 6 : FrameTitle, FrameBorder, FrameBorder);
}
