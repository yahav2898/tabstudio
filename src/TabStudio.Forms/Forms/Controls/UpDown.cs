using System.Collections;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// A text box with up and down buttons beside it: the base of numeric and domain up-downs. The text is typed in the
/// device's own text box while the control has focus; holding a button repeats it.
/// </summary>
public abstract class UpDownBase : ContainerControl, INativeTextEditor
{
    /// <summary>The width of the up and down buttons.</summary>
    internal const int ButtonWidth = 17;

    private readonly EditableText _edit = new();
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private int _pressedButton;
    private int _hotButton;
    private IDisposable? _repeat;

    /// <summary>Creates an up-down.</summary>
    protected UpDownBase()
    {
        SetStyle(ControlStyles.Selectable, true);
        SetStyle(ControlStyles.StandardDoubleClick, false);
    }

    /// <summary>The text shown.</summary>
    [AllowNull]
    public override string Text
    {
        get => _edit.Text;
        set
        {
            value ??= string.Empty;
            if (_edit.SetFromApp(value))
            {
                UserEdit = false;
                Invalidate();
                OnTextChanged(EventArgs.Empty);
            }
        }
    }

    /// <summary>The border.</summary>
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            _borderStyle = value;
            Invalidate();
        }
    }

    /// <summary>Whether the text can only be changed with the buttons.</summary>
    public bool ReadOnly { get; set; }

    /// <summary>Whether the arrow keys press the buttons.</summary>
    public bool InterceptArrowKeys { get; set; } = true;

    /// <summary>Where the text is drawn.</summary>
    public HorizontalAlignment TextAlign
    {
        get => field;
        set
        {
            field = value;
            Invalidate();
        }
    }

    /// <summary>Which side the buttons are on.</summary>
    public LeftRightAlignment UpDownAlign
    {
        get => field;
        set
        {
            field = value;
            Invalidate();
        }
    } = LeftRightAlignment.Right;

    /// <summary>The height of the control in its font.</summary>
    public int PreferredHeight => Font.Height + 7;

    /// <summary>Whether the user typed text that has not been validated yet.</summary>
    protected bool UserEdit { get; set; }

    /// <summary>Whether the user typed text that has not been validated yet.</summary>
    protected bool ChangingText { get; set; }

    /// <inheritdoc />
    protected override Size DefaultSize => new(120, 23);

    /// <summary>The area of the text.</summary>
    internal Rectangle TextArea => UpDownAlign == LeftRightAlignment.Right
        ? new Rectangle(3, 2, Math.Max(0, Width - ButtonWidth - 6), Math.Max(0, Height - 4))
        : new Rectangle(ButtonWidth + 3, 2, Math.Max(0, Width - ButtonWidth - 6), Math.Max(0, Height - 4));

    private Rectangle ButtonsArea => UpDownAlign == LeftRightAlignment.Right
        ? new Rectangle(Width - ButtonWidth - 1, 1, ButtonWidth, Height - 2)
        : new Rectangle(1, 1, ButtonWidth, Height - 2);

    /// <summary>Presses the down button: decreases the value.</summary>
    public abstract void DownButton();

    /// <summary>Presses the up button: increases the value.</summary>
    public abstract void UpButton();

    /// <summary>Selects part of the text.</summary>
    /// <param name="start">The start.</param>
    /// <param name="length">The length.</param>
    public void Select(int start, int length) => _edit.Select(start, length);

    /// <summary>Shows the value as text.</summary>
    protected abstract void UpdateEditText();

    /// <summary>Takes the text the user typed as the value.</summary>
    protected virtual void ValidateEditText()
    {
    }

    /// <summary>Called when the user changes the text.</summary>
    /// <param name="source">The source of the change.</param>
    /// <param name="e">The event data.</param>
    protected virtual void OnChanged(object? source, EventArgs e)
    {
    }

    /// <summary>Takes typed text on Enter, and presses the buttons with the arrow keys.</summary>
    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Up or Keys.Down or Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Delete or Keys.Back || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled)
        {
            return;
        }

        if (InterceptArrowKeys && e.KeyCode == Keys.Up)
        {
            Commit();
            UpButton();
            e.Handled = true;
        }
        else if (InterceptArrowKeys && e.KeyCode == Keys.Down)
        {
            Commit();
            DownButton();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            Commit();
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        Commit();
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        Focus();
        var button = ButtonAt(e.Location);
        if (button == 0 || e.Button != MouseButtons.Left)
        {
            return;
        }

        _pressedButton = button;
        Capture = true;
        Step();
        _repeat?.Dispose();
        if (Window is { } window)
        {
            // Holding a button repeats it, after a pause, as in Windows.
            _repeat = window.Runtime.RunAfter(400, () =>
            {
                _repeat = window.Runtime.StartTimer(60, () =>
                {
                    if (_pressedButton != 0)
                    {
                        Step();
                    }
                });
            });
        }

        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hot = ButtonAt(e.Location);
        if (hot != _hotButton)
        {
            _hotButton = hot;
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        StopRepeat();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hotButton = 0;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (!Focused)
        {
            return;
        }

        Commit();
        if (e.Delta > 0)
        {
            UpButton();
        }
        else if (e.Delta < 0)
        {
            DownButton();
        }
    }

    /// <summary>Keeps the height at the font's.</summary>
    /// <inheritdoc />
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified) =>
        base.SetBoundsCore(x, y, width, PreferredHeight, specified);

    /// <inheritdoc />
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Height = PreferredHeight;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopRepeat();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        var bounds = ClientRectangle;
        using (var background = new SolidBrush(Enabled && !ReadOnly ? BackColor : SystemColors.Control))
        {
            g.FillRectangle(background, bounds);
        }

        ThemePainter.DrawEditBorder(g, bounds, _borderStyle, Focused, Enabled);
        var buttons = ButtonsArea;
        var up = new Rectangle(buttons.X, buttons.Y, buttons.Width, buttons.Height / 2);
        var down = Rectangle.FromLTRB(buttons.X, up.Bottom, buttons.Right, buttons.Bottom);
        PaintButton(g, up, 1, ArrowDirection.Up);
        PaintButton(g, down, 2, ArrowDirection.Down);
        if (!(Focused && Window is not null))
        {
            var alignment = TextAlign switch
            {
                HorizontalAlignment.Center => ContentAlignment.MiddleCenter,
                HorizontalAlignment.Right => ContentAlignment.MiddleRight,
                _ => ContentAlignment.MiddleLeft,
            };
            ThemePainter.DrawText(g, _edit.Text, Font, Enabled ? ForeColor : Theme.DisabledText, TextArea, alignment, mnemonics: false);
        }
    }

    /// <summary>Shows the value as text, from the app rather than the user.</summary>
    private protected void SetTextFromValue(string text)
    {
        ChangingText = true;
        try
        {
            Text = text;
        }
        finally
        {
            ChangingText = false;
        }
    }

    /// <summary>Takes the typed text as the value, if the user typed.</summary>
    private protected void Commit()
    {
        if (UserEdit)
        {
            ValidateEditText();
        }
    }

    /// <inheritdoc />
    EditorFrame? INativeTextEditor.GetEditorFrame(Point clientOrigin) =>
        Enabled ? _edit.Frame(this, clientOrigin, TextArea, TextAlign, ReadOnly, 32767, BackColor) : null;

    /// <inheritdoc />
    void INativeTextEditor.ApplyEditorText(string text, int selectionStart, int selectionLength, int revision)
    {
        if (_edit.ApplyFromEditor(text, selectionStart, selectionLength, revision))
        {
            UserEdit = true;
            OnChanged(this, EventArgs.Empty);
            OnTextChanged(EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    void INativeTextEditor.ApplyEditorSelection(int selectionStart, int selectionLength) => _edit.ApplySelection(selectionStart, selectionLength);

    private void PaintButton(Graphics g, Rectangle bounds, int button, ArrowDirection direction)
    {
        var state = !Enabled ? PushState.Disabled : _pressedButton == button ? PushState.Pressed : _hotButton == button ? PushState.Hot : PushState.Normal;
        if (state is PushState.Hot or PushState.Pressed)
        {
            using var face = new SolidBrush(state == PushState.Pressed ? Theme.ButtonPressedFace : Theme.ButtonHotFace);
            g.FillRectangle(face, bounds);
        }

        var arrow = new Rectangle(bounds.X + ((bounds.Width - 7) / 2), bounds.Y + ((bounds.Height - 4) / 2), 7, 4);
        ThemePainter.DrawArrow(g, arrow, direction, state == PushState.Disabled ? Theme.DisabledText : Theme.Glyph);
    }

    private int ButtonAt(Point point)
    {
        var buttons = ButtonsArea;
        if (!buttons.Contains(point))
        {
            return 0;
        }

        return point.Y < buttons.Y + (buttons.Height / 2) ? 1 : 2;
    }

    private void Step()
    {
        Commit();
        if (_pressedButton == 1)
        {
            UpButton();
        }
        else if (_pressedButton == 2)
        {
            DownButton();
        }
    }

    private void StopRepeat()
    {
        _repeat?.Dispose();
        _repeat = null;
        if (_pressedButton != 0)
        {
            _pressedButton = 0;
            Capture = false;
            Invalidate();
        }
    }
}

/// <summary>A box for a number, with up and down buttons, between a minimum and a maximum.</summary>
[DefaultEvent(nameof(ValueChanged))]
[DefaultProperty(nameof(Value))]
public class NumericUpDown : UpDownBase, ISupportInitialize
{
    private static readonly object EventValueChanged = new();

    private decimal _value;
    private decimal _minimum;
    private decimal _maximum = 100m;
    private decimal _increment = 1m;
    private int _decimalPlaces;
    private bool _thousandsSeparator;
    private bool _hexadecimal;
    private bool _initializing;

    /// <summary>Creates a numeric up-down.</summary>
    public NumericUpDown()
    {
        TextAlign = HorizontalAlignment.Left;
        UpdateEditText();
    }

    /// <summary>Occurs when the value changes.</summary>
    public event EventHandler? ValueChanged
    {
        add => Events.AddHandler(EventValueChanged, value);
        remove => Events.RemoveHandler(EventValueChanged, value);
    }

    /// <summary>The value. Reading it takes the text the user typed first.</summary>
    public decimal Value
    {
        get
        {
            if (UserEdit)
            {
                ValidateEditText();
            }

            return _value;
        }

        set
        {
            if (!_initializing && (value < _minimum || value > _maximum))
            {
                throw new ArgumentOutOfRangeException(nameof(value), $"Value of '{value}' is not valid for 'Value'. 'Value' should be between 'Minimum' and 'Maximum'.");
            }

            var changed = _value != value;
            _value = value;
            if (changed)
            {
                OnValueChanged(EventArgs.Empty);
            }

            UpdateEditText();
        }
    }

    /// <summary>The least value.</summary>
    public decimal Minimum
    {
        get => _minimum;
        set
        {
            _minimum = value;
            _maximum = Math.Max(_maximum, value);
            if (!_initializing)
            {
                Value = Math.Clamp(_value, _minimum, _maximum);
            }
        }
    }

    /// <summary>The greatest value.</summary>
    public decimal Maximum
    {
        get => _maximum;
        set
        {
            _maximum = value;
            _minimum = Math.Min(_minimum, value);
            if (!_initializing)
            {
                Value = Math.Clamp(_value, _minimum, _maximum);
            }
        }
    }

    /// <summary>How much the buttons add or take away.</summary>
    public decimal Increment
    {
        get => _increment;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _increment = value;
        }
    }

    /// <summary>The number of decimal places shown.</summary>
    public int DecimalPlaces
    {
        get => _decimalPlaces;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, 99);
            _decimalPlaces = value;
            UpdateEditText();
        }
    }

    /// <summary>Whether thousands are separated.</summary>
    public bool ThousandsSeparator
    {
        get => _thousandsSeparator;
        set
        {
            _thousandsSeparator = value;
            UpdateEditText();
        }
    }

    /// <summary>Whether the value is shown in hexadecimal.</summary>
    public bool Hexadecimal
    {
        get => _hexadecimal;
        set
        {
            _hexadecimal = value;
            UpdateEditText();
        }
    }

    /// <summary>Faster steps while a button is held. Kept for compatibility.</summary>
    public NumericUpDownAccelerationCollection Accelerations { get; } = [];

    /// <inheritdoc />
    public void BeginInit() => _initializing = true;

    /// <inheritdoc />
    public void EndInit()
    {
        _initializing = false;
        Value = Math.Clamp(_value, _minimum, _maximum);
    }

    /// <inheritdoc />
    public override void DownButton()
    {
        if (UserEdit)
        {
            ValidateEditText();
        }

        Value = Math.Max(_minimum, _value - _increment);
    }

    /// <inheritdoc />
    public override void UpButton()
    {
        if (UserEdit)
        {
            ValidateEditText();
        }

        Value = Math.Min(_maximum, _value + _increment);
    }

    /// <inheritdoc />
    public override string ToString() => $"{base.ToString()}, Minimum = {_minimum}, Maximum = {_maximum}";

    /// <summary>Raises ValueChanged.</summary>
    /// <param name="e">The event data.</param>
    protected virtual void OnValueChanged(EventArgs e) => ((EventHandler?)Events[EventValueChanged])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void UpdateEditText()
    {
        if (_initializing)
        {
            return;
        }

        SetTextFromValue(Format(_value));
    }

    /// <inheritdoc />
    protected override void ValidateEditText()
    {
        UserEdit = false;
        var text = Text.Trim();
        var parsed = 0m;
        var valid = false;
        if (_hexadecimal)
        {
            if (long.TryParse(text, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var hex))
            {
                parsed = hex;
                valid = true;
            }
        }
        else
        {
            valid = decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed);
        }

        if (valid)
        {
            parsed = Math.Clamp(Math.Round(parsed, _decimalPlaces), _minimum, _maximum);
            if (parsed != _value)
            {
                _value = parsed;
                OnValueChanged(EventArgs.Empty);
            }
        }

        UpdateEditText();
    }

    /// <inheritdoc />
    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (e.Handled || char.IsControl(e.KeyChar))
        {
            return;
        }

        // As in Windows, only digits, signs, separators and hexadecimal letters can be typed.
        var format = CultureInfo.CurrentCulture.NumberFormat;
        var text = new string(e.KeyChar, 1);
        var allowed = char.IsDigit(e.KeyChar)
            || text == format.NegativeSign
            || text == format.NumberDecimalSeparator
            || text == format.NumberGroupSeparator
            || (_hexadecimal && Uri.IsHexDigit(e.KeyChar));
        e.Handled = !allowed;
    }

    private string Format(decimal value)
    {
        if (_hexadecimal)
        {
            return ((long)value).ToString("X", CultureInfo.CurrentCulture);
        }

        return value.ToString((_thousandsSeparator ? "N" : "F") + _decimalPlaces.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);
    }
}

/// <summary>A faster step after holding a numeric up-down's button for a while. Kept for compatibility.</summary>
/// <param name="seconds">After how many seconds.</param>
/// <param name="increment">The step.</param>
public class NumericUpDownAcceleration(int seconds, decimal increment)
{
    /// <summary>After how many seconds.</summary>
    public int Seconds { get; set; } = seconds;

    /// <summary>The step.</summary>
    public decimal Increment { get; set; } = increment;
}

/// <summary>The accelerations of a numeric up-down.</summary>
public class NumericUpDownAccelerationCollection : List<NumericUpDownAcceleration>
{
    /// <summary>Adds accelerations.</summary>
    /// <param name="accelerations">The accelerations.</param>
    public void AddRange(params NumericUpDownAcceleration[] accelerations) => base.AddRange(accelerations);
}

/// <summary>A box that steps through a list of strings with up and down buttons.</summary>
[DefaultEvent(nameof(SelectedItemChanged))]
[DefaultProperty(nameof(Items))]
public class DomainUpDown : UpDownBase
{
    private static readonly object EventSelectedItemChanged = new();
    private readonly DomainUpDownItemCollection _items;
    private int _selectedIndex = -1;
    private bool _sorted;

    /// <summary>Creates a domain up-down.</summary>
    public DomainUpDown()
    {
        _items = new DomainUpDownItemCollection(this);
    }

    /// <summary>Occurs when the selected item changes.</summary>
    public event EventHandler? SelectedItemChanged
    {
        add => Events.AddHandler(EventSelectedItemChanged, value);
        remove => Events.RemoveHandler(EventSelectedItemChanged, value);
    }

    /// <summary>The items.</summary>
    public DomainUpDownItemCollection Items => _items;

    /// <summary>The index of the selected item, or -1.</summary>
    public int SelectedIndex
    {
        get => UserEdit ? -1 : _selectedIndex;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, -1);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(value, _items.Count);
            if (value == _selectedIndex && !UserEdit)
            {
                return;
            }

            _selectedIndex = value;
            UserEdit = false;
            UpdateEditText();
            OnSelectedItemChanged(this, EventArgs.Empty);
        }
    }

    /// <summary>The selected item.</summary>
    public object? SelectedItem
    {
        get => SelectedIndex >= 0 ? _items[SelectedIndex] : null;
        set => SelectedIndex = value is null ? -1 : _items.IndexOf(value);
    }

    /// <summary>Whether the items are sorted.</summary>
    public bool Sorted
    {
        get => _sorted;
        set
        {
            _sorted = value;
            if (value)
            {
                _items.Sort(Comparer<object?>.Create((a, b) => CultureInfo.CurrentCulture.CompareInfo.Compare(Convert.ToString(a, CultureInfo.CurrentCulture), Convert.ToString(b, CultureInfo.CurrentCulture), CompareOptions.None)));
            }
        }
    }

    /// <summary>Whether stepping past the last item goes back to the first.</summary>
    public bool Wrap { get; set; }

    /// <inheritdoc />
    public override void DownButton()
    {
        if (_items.Count == 0)
        {
            return;
        }

        var index = _selectedIndex + 1;
        SelectedIndex = index < _items.Count ? index : Wrap ? 0 : _items.Count - 1;
    }

    /// <inheritdoc />
    public override void UpButton()
    {
        if (_items.Count == 0)
        {
            return;
        }

        var index = _selectedIndex - 1;
        SelectedIndex = index >= 0 ? index : Wrap ? _items.Count - 1 : 0;
    }

    /// <summary>Raises SelectedItemChanged.</summary>
    /// <param name="source">The source.</param>
    /// <param name="e">The event data.</param>
    protected void OnSelectedItemChanged(object? source, EventArgs e) => ((EventHandler?)Events[EventSelectedItemChanged])?.Invoke(this, e);

    /// <inheritdoc />
    protected override void UpdateEditText() =>
        SetTextFromValue(_selectedIndex >= 0 ? Convert.ToString(_items[_selectedIndex], CultureInfo.CurrentCulture) ?? string.Empty : string.Empty);

    /// <inheritdoc />
    protected override void ValidateEditText()
    {
        UserEdit = false;
        for (var i = 0; i < _items.Count; i++)
        {
            if (string.Equals(Convert.ToString(_items[i], CultureInfo.CurrentCulture), Text, StringComparison.OrdinalIgnoreCase))
            {
                SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>The items of a domain up-down.</summary>
    public class DomainUpDownItemCollection : ArrayList
    {
        private readonly DomainUpDown _owner;

        internal DomainUpDownItemCollection(DomainUpDown owner) => _owner = owner;

        /// <inheritdoc />
        public override int Add(object? value)
        {
            var index = base.Add(value);
            if (_owner._sorted)
            {
                _owner.Sorted = true;
            }

            return index;
        }

        /// <inheritdoc />
        public override void Remove(object? obj)
        {
            base.Remove(obj);
            if (_owner._selectedIndex >= Count)
            {
                _owner._selectedIndex = Count - 1;
                _owner.UpdateEditText();
            }
        }
    }
}
