using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using TabStudio.Forms.Rendering;
using TabStudio.Forms.Runtime;

namespace System.Windows.Forms;

/// <summary>
/// The base of text boxes. While a text box has focus, its text is edited in a native text box laid over it, so the
/// device's keyboard, selection handles and clipboard work; the Forms layer draws the text whenever it does not
/// have focus. KeyDown and KeyPress handlers run before the native box takes a key, so they can cancel or change it.
/// </summary>
[DefaultEvent(nameof(TextChanged))]
public abstract class TextBoxBase : Control, INativeTextEditor
{
    private static readonly object EventReadOnlyChanged = new();
    private static readonly object EventMultilineChanged = new();
    private static readonly object EventModifiedChanged = new();
    private static readonly object EventBorderStyleChanged = new();

    private int _textRevision;
    private int _selectionRevision;
    private int _selectionStart;
    private int _selectionLength;
    private bool _readOnly;
    private bool _multiline;
    private bool _wordWrap = true;
    private bool _modified;
    private int _maxLength = 32767;
    private BorderStyle _borderStyle = BorderStyle.Fixed3D;
    private string? _undoText;
    private bool _applyingEditor;
    private bool _placeCaretOnPress;

    /// <summary>Creates a text box.</summary>
    private protected TextBoxBase()
    {
        SetStyle(ControlStyles.StandardClick | ControlStyles.StandardDoubleClick | ControlStyles.UseTextForAccessibility, true);
        Cursor = Cursors.IBeam;
        AutoSize = true;
    }

    /// <summary>Occurs when <see cref="ReadOnly"/> changes.</summary>
    public event EventHandler? ReadOnlyChanged
    {
        add => Events.AddHandler(EventReadOnlyChanged, value);
        remove => Events.RemoveHandler(EventReadOnlyChanged, value);
    }

    /// <summary>Occurs when <see cref="Multiline"/> changes.</summary>
    public event EventHandler? MultilineChanged
    {
        add => Events.AddHandler(EventMultilineChanged, value);
        remove => Events.RemoveHandler(EventMultilineChanged, value);
    }

    /// <summary>Occurs when <see cref="Modified"/> changes.</summary>
    public event EventHandler? ModifiedChanged
    {
        add => Events.AddHandler(EventModifiedChanged, value);
        remove => Events.RemoveHandler(EventModifiedChanged, value);
    }

    /// <summary>Occurs when <see cref="BorderStyle"/> changes.</summary>
    public event EventHandler? BorderStyleChanged
    {
        add => Events.AddHandler(EventBorderStyleChanged, value);
        remove => Events.RemoveHandler(EventBorderStyleChanged, value);
    }

    /// <summary>The text. Setting it replaces what the user typed and puts the caret at the start.</summary>
    [AllowNull]
    public override string Text
    {
        get => base.Text;
        set
        {
            value ??= string.Empty;
            if (string.Equals(base.Text, value, StringComparison.Ordinal))
            {
                return;
            }

            if (!_applyingEditor)
            {
                _textRevision++;
                _selectionStart = 0;
                _selectionLength = 0;
                _selectionRevision++;
                _undoText = base.Text;
            }

            base.Text = value;
            if (!_applyingEditor)
            {
                Modified = false;
            }
        }
    }

    /// <summary>The background: white, or the dialog color when read-only or disabled, unless set.</summary>
    public override Color BackColor
    {
        get => HasOwnBackColor ? base.BackColor : _readOnly || !Enabled ? SystemColors.Control : SystemColors.Window;
        set
        {
            HasOwnBackColor = !value.IsEmpty;
            base.BackColor = value;
        }
    }

    /// <summary>The text color: the window text color unless set.</summary>
    public override Color ForeColor
    {
        get => HasOwnForeColor ? base.ForeColor : SystemColors.WindowText;
        set
        {
            HasOwnForeColor = !value.IsEmpty;
            base.ForeColor = value;
        }
    }

    /// <summary>The border.</summary>
    [DefaultValue(BorderStyle.Fixed3D)]
    public BorderStyle BorderStyle
    {
        get => _borderStyle;
        set
        {
            if (_borderStyle == value)
            {
                return;
            }

            _borderStyle = value;
            UpdateClientSize();
            if (AutoSize && !_multiline)
            {
                Height = PreferredHeight;
            }

            Invalidate();
            ((EventHandler?)Events[EventBorderStyleChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether the user cannot change the text.</summary>
    [DefaultValue(false)]
    public bool ReadOnly
    {
        get => _readOnly;
        set
        {
            if (_readOnly == value)
            {
                return;
            }

            _readOnly = value;
            Invalidate();
            ((EventHandler?)Events[EventReadOnlyChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether the text box has several lines.</summary>
    [DefaultValue(false)]
    public virtual bool Multiline
    {
        get => _multiline;
        set
        {
            if (_multiline == value)
            {
                return;
            }

            _multiline = value;
            if (!value && AutoSize)
            {
                Height = PreferredHeight;
            }

            Invalidate();
            ((EventHandler?)Events[EventMultilineChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether the lines of a multiline text box wrap.</summary>
    [DefaultValue(true)]
    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            _wordWrap = value;
            Invalidate();
        }
    }

    /// <summary>Whether Tab types a tab in a multiline text box rather than moving focus.</summary>
    public bool AcceptsTab { get; set; }

    /// <summary>Whether the selection is hidden when the text box does not have focus.</summary>
    [DefaultValue(true)]
    public bool HideSelection { get; set; } = true;

    /// <summary>Whether the clipboard and undo shortcuts work.</summary>
    [DefaultValue(true)]
    public virtual bool ShortcutsEnabled { get; set; } = true;

    /// <summary>The most characters the user can type.</summary>
    [DefaultValue(32767)]
    public virtual int MaxLength
    {
        get => _maxLength;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            _maxLength = value == 0 ? int.MaxValue : value;
        }
    }

    /// <summary>Whether the user changed the text since it was last set.</summary>
    public bool Modified
    {
        get => _modified;
        set
        {
            if (_modified == value)
            {
                return;
            }

            _modified = value;
            ((EventHandler?)Events[EventModifiedChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Whether there is something to undo.</summary>
    public bool CanUndo => _undoText is not null;

    /// <summary>The lines of the text.</summary>
    public string[] Lines
    {
        get => Text.Split(["\r\n", "\n"], StringSplitOptions.None);
        set => Text = string.Join("\r\n", value ?? []);
    }

    /// <summary>The number of characters.</summary>
    public virtual int TextLength => Text.Length;

    /// <summary>Where the selection starts: the caret's position when nothing is selected.</summary>
    public virtual int SelectionStart
    {
        get => Math.Min(_selectionStart, TextLength);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Select(value, _selectionLength);
        }
    }

    /// <summary>The number of characters selected.</summary>
    public virtual int SelectionLength
    {
        get => Math.Min(_selectionLength, Math.Max(0, TextLength - SelectionStart));
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Select(_selectionStart, value);
        }
    }

    /// <summary>The selected text; setting it replaces the selection.</summary>
    [AllowNull]
    public virtual string SelectedText
    {
        get => Text.Substring(SelectionStart, SelectionLength);
        set
        {
            var start = SelectionStart;
            var insert = value ?? string.Empty;
            Text = Text.Remove(start, SelectionLength).Insert(start, insert);
            Select(start + insert.Length, 0);
        }
    }

    /// <summary>Whether the text box sizes its height to its font when it has one line.</summary>
    [DefaultValue(true)]
    public override bool AutoSize
    {
        get => base.AutoSize;
        set
        {
            base.AutoSize = value;
            if (value && !_multiline)
            {
                Height = PreferredHeight;
            }
        }
    }

    /// <summary>The height of a one-line text box in its font.</summary>
    public int PreferredHeight => Font.Height + (_borderStyle == BorderStyle.None ? 0 : 7);

    /// <inheritdoc />
    protected override Size DefaultSize => new(100, 23);

    /// <summary>The character shown in place of each character, for passwords, or <c>\0</c>.</summary>
    internal virtual char DisplayPasswordChar => '\0';

    /// <summary>The horizontal alignment of the text.</summary>
    internal virtual HorizontalAlignment Alignment => HorizontalAlignment.Left;

    /// <summary>The scroll bars of a multiline text box.</summary>
    internal virtual ScrollBars ScrollBarsShown => ScrollBars.None;

    /// <summary>Whether Enter types a new line.</summary>
    internal virtual bool EnterTypesNewLine => _multiline;

    /// <summary>The case typed characters are changed to.</summary>
    internal virtual CharacterCasing Casing => CharacterCasing.Normal;

    /// <summary>Text shown, grayed, while the text box is empty.</summary>
    internal virtual string Placeholder => string.Empty;

    /// <inheritdoc />
    internal override Padding NonClientInsets => new(ThemePainter.BorderWidth(_borderStyle));

    /// <inheritdoc />
    internal override bool ScrollsByTouch => false;

    private bool HasOwnBackColor { get; set; }

    private bool HasOwnForeColor { get; set; }

    /// <summary>Whether the native text box shows the text now, so the Forms layer leaves it out.</summary>
    private bool IsEditingNatively => Focused && Window is not null && Enabled;

    /// <summary>Adds text at the end and moves the caret there.</summary>
    /// <param name="text">The text.</param>
    public void AppendText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Text += text;
        Select(TextLength, 0);
    }

    /// <summary>Removes all the text.</summary>
    public void Clear() => Text = string.Empty;

    /// <summary>Forgets what can be undone.</summary>
    public void ClearUndo() => _undoText = null;

    /// <summary>Undoes the last change.</summary>
    public void Undo()
    {
        if (_undoText is { } previous)
        {
            var current = Text;
            Text = previous;
            _undoText = current;
        }
    }

    /// <summary>Selects part of the text.</summary>
    /// <param name="start">The first character.</param>
    /// <param name="length">The number of characters.</param>
    public void Select(int start, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        _selectionStart = Math.Min(start, TextLength);
        _selectionLength = Math.Max(0, Math.Min(length, TextLength - _selectionStart));
        _selectionRevision++;
        Invalidate();
    }

    /// <summary>Selects all the text.</summary>
    public void SelectAll() => Select(0, TextLength);

    /// <summary>Selects nothing, leaving the caret where the selection started.</summary>
    public void DeselectAll() => Select(SelectionStart, 0);

    /// <summary>Scrolls the caret into view; the native text box keeps it in view.</summary>
    public void ScrollToCaret() => Invalidate();

    /// <summary>Copies the selected text to the clipboard.</summary>
    public void Copy()
    {
        if (SelectionLength > 0)
        {
            Clipboard.SetText(SelectedText);
        }
    }

    /// <summary>Moves the selected text to the clipboard.</summary>
    public void Cut()
    {
        if (SelectionLength > 0 && !_readOnly)
        {
            Copy();
            SelectedText = string.Empty;
        }
    }

    /// <summary>Replaces the selection with the clipboard's text.</summary>
    public void Paste()
    {
        if (!_readOnly && Clipboard.GetText() is { Length: > 0 } text)
        {
            Paste(text);
        }
    }

    /// <summary>Replaces the selection with text.</summary>
    /// <param name="text">The text.</param>
    public void Paste(string? text)
    {
        if (!_readOnly)
        {
            SelectedText = text;
        }
    }

    /// <summary>The line a character is on, from 0.</summary>
    /// <param name="index">The character's index.</param>
    public int GetLineFromCharIndex(int index)
    {
        var text = Text;
        var line = 0;
        for (var i = 0; i < Math.Min(index, text.Length); i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    /// <summary>The index of the first character of a line.</summary>
    /// <param name="lineNumber">The line, from 0.</param>
    public int GetFirstCharIndexFromLine(int lineNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineNumber);
        var text = Text;
        var line = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (line == lineNumber)
            {
                return i;
            }

            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line == lineNumber ? text.Length : -1;
    }

    /// <summary>The index of the first character of the caret's line.</summary>
    public int GetFirstCharIndexOfCurrentLine() => GetFirstCharIndexFromLine(GetLineFromCharIndex(SelectionStart));

    /// <summary>The index of the character nearest to a point of the client area.</summary>
    /// <param name="pt">The point.</param>
    public virtual int GetCharIndexFromPosition(Point pt)
    {
        var area = TextArea();
        return TextMetrics.CharacterIndexAt(ShownText(), Font.Spec, pt.X - area.X, pt.Y - area.Y, _multiline && _wordWrap ? area.Width : double.PositiveInfinity);
    }

    /// <summary>The character nearest to a point of the client area.</summary>
    /// <param name="pt">The point.</param>
    public virtual char GetCharFromPosition(Point pt)
    {
        var index = GetCharIndexFromPosition(pt);
        return index < TextLength ? Text[index] : '\0';
    }

    /// <summary>Where a character is drawn, in client coordinates.</summary>
    /// <param name="index">The character's index.</param>
    public virtual Point GetPositionFromCharIndex(int index)
    {
        var area = TextArea();
        var before = Text[..Math.Clamp(index, 0, TextLength)];
        var line = GetLineFromCharIndex(before.Length);
        var lineStart = before.LastIndexOf('\n') + 1;
        var width = TextMetrics.Measure(before[lineStart..], Font.Spec).Width;
        return new Point(area.X + (int)width, area.Y + (int)(line * TextMetrics.LineHeight(Font.Spec)));
    }

    /// <summary>The navigation and editing keys belong to the text box; Enter and Tab when it takes them.</summary>
    /// <param name="keyData">The key.</param>
    protected override bool IsInputKey(Keys keyData)
    {
        if ((keyData & Keys.Alt) != 0)
        {
            return false;
        }

        return (keyData & Keys.KeyCode) switch
        {
            Keys.Left or Keys.Right or Keys.Home or Keys.End or Keys.Delete or Keys.Back => true,
            Keys.Up or Keys.Down or Keys.PageUp or Keys.PageDown => _multiline,
            Keys.Enter => _multiline && EnterTypesNewLine,
            Keys.Tab => _multiline && AcceptsTab && (keyData & Keys.Control) == 0,
            Keys.Escape => false,
            _ => base.IsInputKey(keyData),
        };
    }

    /// <summary>Selects all the text when focus arrives with the keyboard, as Windows does for one-line text boxes.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnGotFocus(EventArgs e)
    {
        if (Window?.ShowFocusCues == true && !_multiline)
        {
            SelectAll();
        }
        else
        {
            _placeCaretOnPress = true;
        }

        base.OnGotFocus(e);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        _placeCaretOnPress = false;
        base.OnLostFocus(e);
    }

    /// <summary>A press that gives the text box focus puts the caret where it lands.</summary>
    /// <param name="e">The event data.</param>
    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (_placeCaretOnPress)
        {
            _placeCaretOnPress = false;
            Select(GetCharIndexFromPosition(e.Location), 0);
        }

        base.OnMouseDown(e);
    }

    /// <summary>Keeps the height of a one-line text box at its font's.</summary>
    /// <inheritdoc />
    protected override void SetBoundsCore(int x, int y, int width, int height, BoundsSpecified specified)
    {
        if (AutoSize && !_multiline)
        {
            height = PreferredHeight;
        }

        base.SetBoundsCore(x, y, width, height, specified);
    }

    /// <inheritdoc />
    EditorFrame? INativeTextEditor.GetEditorFrame(Point clientOrigin)
    {
        if (!Enabled)
        {
            return null;
        }

        var area = TextArea();
        return new EditorFrame(
            (int)Handle,
            new Rectangle(clientOrigin.X + area.X, clientOrigin.Y + area.Y, area.Width, area.Height),
            Text,
            _textRevision,
            SelectionStart,
            SelectionLength,
            _selectionRevision,
            Font.Spec,
            ForeColor,
            BackColor,
            Alignment,
            _multiline,
            _wordWrap,
            _readOnly,
            DisplayPasswordChar,
            _maxLength,
            _multiline ? ScrollBarsShown : ScrollBars.None,
            EnterTypesNewLine,
            AcceptsTab,
            Casing);
    }

    /// <inheritdoc />
    void INativeTextEditor.ApplyEditorText(string text, int selectionStart, int selectionLength, int revision)
    {
        if (revision != _textRevision)
        {
            // The app replaced the text since the user typed this; the app's text wins.
            return;
        }

        _selectionStart = selectionStart;
        _selectionLength = selectionLength;
        if (string.Equals(text, base.Text, StringComparison.Ordinal))
        {
            return;
        }

        _undoText = base.Text;
        _applyingEditor = true;
        try
        {
            Text = text;
        }
        finally
        {
            _applyingEditor = false;
        }

        Modified = true;
    }

    /// <inheritdoc />
    void INativeTextEditor.ApplyEditorSelection(int selectionStart, int selectionLength)
    {
        _selectionStart = selectionStart;
        _selectionLength = selectionLength;
    }

    /// <inheritdoc />
    internal override void PaintContent(Graphics g)
    {
        if (IsEditingNatively)
        {
            return;
        }

        var area = TextArea();
        var shown = ShownText();
        if (shown.Length == 0 && Placeholder.Length > 0)
        {
            DrawText(g, Placeholder, SystemColors.GrayText, area);
            return;
        }

        DrawText(g, shown, Enabled ? ForeColor : Theme.DisabledText, area);
    }

    /// <inheritdoc />
    internal override void PaintNonClient(Graphics g) =>
        ThemePainter.DrawEditBorder(g, new Rectangle(Point.Empty, Size), _borderStyle, Focused, Enabled);

    /// <summary>Where the text goes, in client coordinates: the native text box covers this area.</summary>
    internal Rectangle TextArea()
    {
        var client = ClientSize;
        if (_multiline)
        {
            var scroll = ScrollBarsShown;
            var right = (scroll & ScrollBars.Vertical) != 0 ? Theme.ScrollBarSize : 0;
            return new Rectangle(2, 1, Math.Max(0, client.Width - 3 - right), Math.Max(0, client.Height - 2));
        }

        return new Rectangle(2, 0, Math.Max(0, client.Width - 4), client.Height);
    }

    private string ShownText() => DisplayPasswordChar is var mask and not '\0' ? new string(mask, TextLength) : Text;

    private void DrawText(Graphics g, string text, Color color, Rectangle area)
    {
        var horizontal = Alignment switch
        {
            HorizontalAlignment.Center => StringAlignment.Center,
            HorizontalAlignment.Right => StringAlignment.Far,
            _ => StringAlignment.Near,
        };
        var options = new TextOptions(horizontal, _multiline ? StringAlignment.Near : StringAlignment.Center, Wrap: _multiline && _wordWrap, Ellipsis: false, Clip: true);
        g.DrawText(text, Font, color, area, options);
    }
}
