using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Rendering;

namespace System.Windows.Forms;

/// <summary>An item that hosts a control on a strip, such as a text box or a combo box on a tool bar.</summary>
/// <remarks>
/// On a bar, the control is a child of the strip and works as on a form. On a drop-down menu, it is drawn as its text.
/// </remarks>
public class ToolStripControlHost : ToolStripItem
{
    /// <summary>Creates a host for a control.</summary>
    /// <param name="c">The control.</param>
    public ToolStripControlHost(Control c)
    {
        ArgumentNullException.ThrowIfNull(c);
        Control = c;
        c.TextChanged += (_, e) => OnTextChanged(e);
    }

    /// <summary>Creates a host for a control, with a name.</summary>
    /// <param name="c">The control.</param>
    /// <param name="name">The name.</param>
    public ToolStripControlHost(Control c, string name)
        : this(c)
    {
        Name = name;
    }

    /// <summary>Occurs when the control gets focus.</summary>
    public event EventHandler? GotFocus
    {
        add => Control.GotFocus += value;
        remove => Control.GotFocus -= value;
    }

    /// <summary>Occurs when the control loses focus.</summary>
    public event EventHandler? LostFocus
    {
        add => Control.LostFocus += value;
        remove => Control.LostFocus -= value;
    }

    /// <summary>Occurs when focus enters the control.</summary>
    public event EventHandler? Enter
    {
        add => Control.Enter += value;
        remove => Control.Enter -= value;
    }

    /// <summary>Occurs when focus leaves the control.</summary>
    public event EventHandler? Leave
    {
        add => Control.Leave += value;
        remove => Control.Leave -= value;
    }

    /// <summary>Occurs when a key goes down in the control.</summary>
    public event KeyEventHandler? KeyDown
    {
        add => Control.KeyDown += value;
        remove => Control.KeyDown -= value;
    }

    /// <summary>Occurs when a character is typed in the control.</summary>
    public event KeyPressEventHandler? KeyPress
    {
        add => Control.KeyPress += value;
        remove => Control.KeyPress -= value;
    }

    /// <summary>Occurs when a key goes up in the control.</summary>
    public event KeyEventHandler? KeyUp
    {
        add => Control.KeyUp += value;
        remove => Control.KeyUp -= value;
    }

    /// <summary>Occurs when the control is validated.</summary>
    public event CancelEventHandler? Validating
    {
        add => Control.Validating += value;
        remove => Control.Validating -= value;
    }

    /// <summary>Occurs when the control has been validated.</summary>
    public event EventHandler? Validated
    {
        add => Control.Validated += value;
        remove => Control.Validated -= value;
    }

    /// <summary>The hosted control.</summary>
    public Control Control { get; }

    /// <summary>Where the control sits in the item.</summary>
    public ContentAlignment ControlAlign { get; set; } = ContentAlignment.MiddleCenter;

    /// <summary>The control's text.</summary>
    [AllowNull]
    public override string Text
    {
        get => Control.Text;
        set => Control.Text = value;
    }

    /// <summary>The control's font.</summary>
    [AllowNull]
    public override Font Font
    {
        get => Control.Font;
        set => Control.Font = value;
    }

    /// <summary>The control's text color.</summary>
    public override Color ForeColor
    {
        get => Control.ForeColor;
        set => Control.ForeColor = value;
    }

    /// <summary>The control's background color.</summary>
    public override Color BackColor
    {
        get => Control.BackColor;
        set => Control.BackColor = value;
    }

    /// <summary>Whether the control responds to input.</summary>
    public override bool Enabled
    {
        get => base.Enabled && Control.Enabled;
        set
        {
            base.Enabled = value;
            Control.Enabled = value;
        }
    }

    /// <summary>The control's size.</summary>
    public override Size Size
    {
        get => Control.Size;
        set
        {
            Control.Size = value;
            base.Size = Control.Size;
        }
    }

    /// <summary>Whether the control has focus.</summary>
    public bool Focused => Control.Focused;

    /// <summary>Whether the control can take focus.</summary>
    public override bool CanSelect => Control.CanSelect;

    /// <summary>Gives the control focus.</summary>
    public void Focus() => Control.Focus();

    /// <inheritdoc />
    public override Size GetPreferredSize(Size constrainingSize) => Control.Size;

    /// <summary>Where the control goes in the item's bounds: its own height, at <see cref="ControlAlign"/>.</summary>
    internal Rectangle ControlBounds()
    {
        var size = new Size(Bounds.Width, Math.Min(Control.Height, Bounds.Height));
        return ThemePainter.Align(Bounds, size, ControlAlign);
    }

    /// <inheritdoc />
    internal override void PaintOnStrip(Graphics g, Rectangle bounds) =>
        PaintContent(g, Rectangle.FromLTRB(bounds.Left + 4, bounds.Top, bounds.Right - 4, bounds.Bottom));

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Control.Dispose();
        }

        base.Dispose(disposing);
    }
}

/// <summary>A text box on a strip.</summary>
public class ToolStripTextBox : ToolStripControlHost
{
    /// <summary>Creates a text box item.</summary>
    public ToolStripTextBox()
        : base(new TextBox { Size = new Size(100, 23) })
    {
    }

    /// <summary>Creates a text box item with a name.</summary>
    /// <param name="name">The name.</param>
    public ToolStripTextBox(string name)
        : this()
    {
        Name = name;
    }

    /// <summary>The text box.</summary>
    public TextBox TextBox => (TextBox)Control;

    /// <summary>The most characters the text box takes.</summary>
    public int MaxLength
    {
        get => TextBox.MaxLength;
        set => TextBox.MaxLength = value;
    }

    /// <summary>Whether the text is read-only.</summary>
    public bool ReadOnly
    {
        get => TextBox.ReadOnly;
        set => TextBox.ReadOnly = value;
    }

    /// <summary>Whether the text box shows several lines.</summary>
    public bool Multiline
    {
        get => TextBox.Multiline;
        set => TextBox.Multiline = value;
    }

    /// <summary>Whether the text is drawn at the left, the center or the right.</summary>
    public HorizontalAlignment TextBoxTextAlign
    {
        get => TextBox.TextAlign;
        set => TextBox.TextAlign = value;
    }

    /// <summary>The border.</summary>
    public BorderStyle BorderStyle
    {
        get => TextBox.BorderStyle;
        set => TextBox.BorderStyle = value;
    }

    /// <summary>Whether letters are changed to upper or lower case as they are typed.</summary>
    public CharacterCasing CharacterCasing
    {
        get => TextBox.CharacterCasing;
        set => TextBox.CharacterCasing = value;
    }

    /// <summary>The text shown when the box is empty.</summary>
    [AllowNull]
    public string PlaceholderText
    {
        get => TextBox.PlaceholderText;
        set => TextBox.PlaceholderText = value;
    }

    /// <summary>Whether the user changed the text.</summary>
    public bool Modified
    {
        get => TextBox.Modified;
        set => TextBox.Modified = value;
    }

    /// <summary>The selected text.</summary>
    [AllowNull]
    public string SelectedText
    {
        get => TextBox.SelectedText;
        set => TextBox.SelectedText = value;
    }

    /// <summary>The start of the selection.</summary>
    public int SelectionStart
    {
        get => TextBox.SelectionStart;
        set => TextBox.SelectionStart = value;
    }

    /// <summary>The length of the selection.</summary>
    public int SelectionLength
    {
        get => TextBox.SelectionLength;
        set => TextBox.SelectionLength = value;
    }

    /// <summary>The length of the text.</summary>
    public int TextLength => TextBox.TextLength;

    /// <summary>How the text box completes what is typed.</summary>
    public AutoCompleteMode AutoCompleteMode
    {
        get => TextBox.AutoCompleteMode;
        set => TextBox.AutoCompleteMode = value;
    }

    /// <summary>Where completions come from.</summary>
    public AutoCompleteSource AutoCompleteSource
    {
        get => TextBox.AutoCompleteSource;
        set => TextBox.AutoCompleteSource = value;
    }

    /// <summary>The completions of a custom source.</summary>
    public AutoCompleteStringCollection AutoCompleteCustomSource
    {
        get => TextBox.AutoCompleteCustomSource;
        set => TextBox.AutoCompleteCustomSource = value;
    }

    /// <summary>Selects all the text.</summary>
    public void SelectAll() => TextBox.SelectAll();

    /// <summary>Clears the text.</summary>
    public void Clear() => TextBox.Clear();

    /// <summary>Selects part of the text.</summary>
    /// <param name="start">The start.</param>
    /// <param name="length">The length.</param>
    public void Select(int start, int length) => TextBox.Select(start, length);
}

/// <summary>A combo box on a strip.</summary>
public class ToolStripComboBox : ToolStripControlHost
{
    private static readonly object EventSelectedIndexChanged = new();

    /// <summary>Creates a combo box item.</summary>
    public ToolStripComboBox()
        : base(new ComboBox { Size = new Size(121, 23) })
    {
        ComboBox.SelectedIndexChanged += (_, e) => OnSelectedIndexChanged(e);
    }

    /// <summary>Creates a combo box item with a name.</summary>
    /// <param name="name">The name.</param>
    public ToolStripComboBox(string name)
        : this()
    {
        Name = name;
    }

    /// <summary>Occurs when the selected item changes.</summary>
    public event EventHandler? SelectedIndexChanged
    {
        add => Events.AddHandler(EventSelectedIndexChanged, value);
        remove => Events.RemoveHandler(EventSelectedIndexChanged, value);
    }

    /// <summary>The combo box.</summary>
    public ComboBox ComboBox => (ComboBox)Control;

    /// <summary>The items.</summary>
    public ComboBox.ObjectCollection Items => ComboBox.Items;

    /// <summary>The index of the selected item.</summary>
    public int SelectedIndex
    {
        get => ComboBox.SelectedIndex;
        set => ComboBox.SelectedIndex = value;
    }

    /// <summary>The selected item.</summary>
    public object? SelectedItem
    {
        get => ComboBox.SelectedItem;
        set => ComboBox.SelectedItem = value;
    }

    /// <summary>Whether the text can be typed or only picked from the list.</summary>
    public ComboBoxStyle DropDownStyle
    {
        get => ComboBox.DropDownStyle;
        set => ComboBox.DropDownStyle = value;
    }

    /// <summary>The most items the list shows at once.</summary>
    public int MaxDropDownItems
    {
        get => ComboBox.MaxDropDownItems;
        set => ComboBox.MaxDropDownItems = value;
    }

    /// <summary>The width of the list.</summary>
    public int DropDownWidth
    {
        get => ComboBox.DropDownWidth;
        set => ComboBox.DropDownWidth = value;
    }

    /// <summary>The height of the list.</summary>
    public int DropDownHeight
    {
        get => ComboBox.DropDownHeight;
        set => ComboBox.DropDownHeight = value;
    }

    /// <summary>Whether the list is sorted.</summary>
    public bool Sorted
    {
        get => ComboBox.Sorted;
        set => ComboBox.Sorted = value;
    }

    /// <summary>Whether the list is open.</summary>
    public bool DroppedDown
    {
        get => ComboBox.DroppedDown;
        set => ComboBox.DroppedDown = value;
    }

    /// <summary>The look. Kept for compatibility.</summary>
    public FlatStyle FlatStyle
    {
        get => ComboBox.FlatStyle;
        set => ComboBox.FlatStyle = value;
    }

    /// <summary>Raises SelectedIndexChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnSelectedIndexChanged(EventArgs e) => ((EventHandler?)Events[EventSelectedIndexChanged])?.Invoke(this, e);

    /// <summary>Starts updating the list without drawing it after every change.</summary>
    public void BeginUpdate() => ComboBox.BeginUpdate();

    /// <summary>Ends updating the list.</summary>
    public void EndUpdate() => ComboBox.EndUpdate();
}

/// <summary>A progress bar on a strip.</summary>
public class ToolStripProgressBar : ToolStripControlHost
{
    /// <summary>Creates a progress bar item.</summary>
    public ToolStripProgressBar()
        : base(new ProgressBar { Size = new Size(100, 15) })
    {
    }

    /// <summary>Creates a progress bar item with a name.</summary>
    /// <param name="name">The name.</param>
    public ToolStripProgressBar(string name)
        : this()
    {
        Name = name;
    }

    /// <summary>The progress bar.</summary>
    public ProgressBar ProgressBar => (ProgressBar)Control;

    /// <summary>The value.</summary>
    public int Value
    {
        get => ProgressBar.Value;
        set => ProgressBar.Value = value;
    }

    /// <summary>The least value.</summary>
    public int Minimum
    {
        get => ProgressBar.Minimum;
        set => ProgressBar.Minimum = value;
    }

    /// <summary>The greatest value.</summary>
    public int Maximum
    {
        get => ProgressBar.Maximum;
        set => ProgressBar.Maximum = value;
    }

    /// <summary>How much <see cref="PerformStep"/> adds.</summary>
    public int Step
    {
        get => ProgressBar.Step;
        set => ProgressBar.Step = value;
    }

    /// <summary>Whether the bar shows a value or a marquee.</summary>
    public ProgressBarStyle Style
    {
        get => ProgressBar.Style;
        set => ProgressBar.Style = value;
    }

    /// <summary>How fast the marquee moves, in milliseconds.</summary>
    public int MarqueeAnimationSpeed
    {
        get => ProgressBar.MarqueeAnimationSpeed;
        set => ProgressBar.MarqueeAnimationSpeed = value;
    }

    /// <inheritdoc />
    protected override Padding DefaultMargin => new(1, 2, 1, 1);

    /// <summary>Adds to the value.</summary>
    /// <param name="value">The amount.</param>
    public void Increment(int value) => ProgressBar.Increment(value);

    /// <summary>Adds <see cref="Step"/> to the value.</summary>
    public void PerformStep() => ProgressBar.PerformStep();
}
