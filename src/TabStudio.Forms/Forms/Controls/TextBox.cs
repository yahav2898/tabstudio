using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace System.Windows.Forms;

/// <summary>A text box: one line, several lines, or a password.</summary>
public class TextBox : TextBoxBase
{
    private static readonly object EventTextAlignChanged = new();
    private char _passwordChar;
    private bool _useSystemPasswordChar;
    private HorizontalAlignment _textAlign;
    private ScrollBars _scrollBars;
    private string _placeholderText = string.Empty;

    /// <summary>Occurs when <see cref="TextAlign"/> changes.</summary>
    public event EventHandler? TextAlignChanged
    {
        add => Events.AddHandler(EventTextAlignChanged, value);
        remove => Events.RemoveHandler(EventTextAlignChanged, value);
    }

    /// <summary>The character shown for each character of a password, or <c>\0</c>.</summary>
    [DefaultValue('\0')]
    public char PasswordChar
    {
        get => _useSystemPasswordChar ? '●' : _passwordChar;
        set
        {
            _passwordChar = value;
            Invalidate();
        }
    }

    /// <summary>Whether the text is shown as the system's password character.</summary>
    [DefaultValue(false)]
    public bool UseSystemPasswordChar
    {
        get => _useSystemPasswordChar;
        set
        {
            _useSystemPasswordChar = value;
            Invalidate();
        }
    }

    /// <summary>The horizontal alignment of the text.</summary>
    [DefaultValue(HorizontalAlignment.Left)]
    public HorizontalAlignment TextAlign
    {
        get => _textAlign;
        set
        {
            if (_textAlign == value)
            {
                return;
            }

            _textAlign = value;
            Invalidate();
            ((EventHandler?)Events[EventTextAlignChanged])?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The scroll bars of a multiline text box.</summary>
    [DefaultValue(ScrollBars.None)]
    public ScrollBars ScrollBars
    {
        get => _scrollBars;
        set
        {
            _scrollBars = value;
            Invalidate();
        }
    }

    /// <summary>Whether Enter types a new line in a multiline text box rather than pressing the form's accept button.</summary>
    [DefaultValue(false)]
    public bool AcceptsReturn { get; set; }

    /// <summary>The case typed characters are changed to.</summary>
    [DefaultValue(CharacterCasing.Normal)]
    public CharacterCasing CharacterCasing { get; set; }

    /// <summary>Text shown, grayed, while the text box is empty and does not have focus.</summary>
    [AllowNull]
    public virtual string PlaceholderText
    {
        get => _placeholderText;
        set
        {
            _placeholderText = value ?? string.Empty;
            Invalidate();
        }
    }

    /// <summary>How the text box completes what is typed. Kept for compatibility.</summary>
    public AutoCompleteMode AutoCompleteMode { get; set; }

    /// <summary>Where completions come from. Kept for compatibility.</summary>
    public AutoCompleteSource AutoCompleteSource { get; set; } = AutoCompleteSource.None;

    /// <summary>The completions of a custom source. Kept for compatibility.</summary>
    public AutoCompleteStringCollection AutoCompleteCustomSource { get; set; } = [];

    /// <inheritdoc />
    internal override char DisplayPasswordChar => Multiline ? '\0' : PasswordChar;

    /// <inheritdoc />
    internal override HorizontalAlignment Alignment => _textAlign;

    /// <inheritdoc />
    internal override ScrollBars ScrollBarsShown => _scrollBars;

    /// <inheritdoc />
    internal override bool EnterTypesNewLine => Multiline && AcceptsReturn;

    /// <inheritdoc />
    internal override CharacterCasing Casing => CharacterCasing;

    /// <inheritdoc />
    internal override string Placeholder => _placeholderText;
}

/// <summary>A collection of strings for auto-completion.</summary>
public class AutoCompleteStringCollection : StringCollection
{
}

/// <summary>
/// A multiline text box for longer text, such as a log. Its text is plain here: runs of colors and fonts are kept
/// for compatibility but drawn in the control's color and font.
/// </summary>
public class RichTextBox : TextBoxBase
{
    /// <summary>Creates a rich text box.</summary>
    public RichTextBox()
    {
        Multiline = true;
        AutoSize = false;
    }

    /// <summary>Whether the rich text box has several lines: it always does.</summary>
    [DefaultValue(true)]
    public override bool Multiline
    {
        get => base.Multiline;
        set => base.Multiline = value;
    }

    /// <summary>The scroll bars.</summary>
    public RichTextBoxScrollBars ScrollBars { get; set; } = RichTextBoxScrollBars.Both;

    /// <summary>Whether addresses in the text are shown as links. Kept for compatibility.</summary>
    public bool DetectUrls { get; set; } = true;

    /// <summary>The zoom of the text. Kept for compatibility.</summary>
    public float ZoomFactor { get; set; } = 1f;

    /// <summary>The color of the selected text. Kept for compatibility; text is drawn in the control's color.</summary>
    public Color SelectionColor { get; set; }

    /// <summary>The background of the selected text. Kept for compatibility.</summary>
    public Color SelectionBackColor { get; set; }

    /// <summary>The font of the selected text. Kept for compatibility; text is drawn in the control's font.</summary>
    public Font? SelectionFont { get; set; }

    /// <summary>The alignment of the selected paragraph. Kept for compatibility.</summary>
    public HorizontalAlignment SelectionAlignment { get; set; }

    /// <summary>Whether the selection is a bulleted list. Kept for compatibility.</summary>
    public bool SelectionBullet { get; set; }

    /// <summary>The text as RTF. Rich text format is not supported on this device.</summary>
    /// <exception cref="NotSupportedException">Always.</exception>
    public string Rtf
    {
        get => throw new NotSupportedException("RichTextBox.Rtf is not supported by TabStudio's Forms layer: use Text.");
        set => throw new NotSupportedException("RichTextBox.Rtf is not supported by TabStudio's Forms layer: use Text.");
    }

    /// <inheritdoc />
    protected override Size DefaultSize => new(100, 96);

    /// <inheritdoc />
    internal override ScrollBars ScrollBarsShown => ScrollBars switch
    {
        RichTextBoxScrollBars.Vertical or RichTextBoxScrollBars.ForcedVertical => System.Windows.Forms.ScrollBars.Vertical,
        RichTextBoxScrollBars.Horizontal or RichTextBoxScrollBars.ForcedHorizontal => System.Windows.Forms.ScrollBars.Horizontal,
        RichTextBoxScrollBars.None => System.Windows.Forms.ScrollBars.None,
        _ => System.Windows.Forms.ScrollBars.Both,
    };

    /// <inheritdoc />
    internal override bool EnterTypesNewLine => true;

    /// <summary>Finds text.</summary>
    /// <param name="str">The text to find.</param>
    /// <returns>Where it starts, or -1.</returns>
    public int Find(string str) => Find(str, 0, RichTextBoxFinds.None);

    /// <summary>Finds text from a position.</summary>
    /// <param name="str">The text to find.</param>
    /// <param name="start">Where to start.</param>
    /// <param name="options">How to search.</param>
    /// <returns>Where it starts, or -1.</returns>
    public int Find(string str, int start, RichTextBoxFinds options)
    {
        ArgumentNullException.ThrowIfNull(str);
        var comparison = (options & RichTextBoxFinds.MatchCase) != 0 ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var index = (options & RichTextBoxFinds.Reverse) != 0
            ? Text.LastIndexOf(str, Math.Max(0, Text.Length - 1), comparison)
            : Text.IndexOf(str, Math.Clamp(start, 0, Text.Length), comparison);
        if (index >= 0)
        {
            Select(index, str.Length);
        }

        return index;
    }

    /// <summary>Loads a plain text file.</summary>
    /// <param name="path">The file's path.</param>
    public void LoadFile(string path) => LoadFile(path, RichTextBoxStreamType.PlainText);

    /// <summary>Loads a file.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="fileType">The file's format: plain text.</param>
    /// <exception cref="NotSupportedException">The format is rich text.</exception>
    public void LoadFile(string path, RichTextBoxStreamType fileType)
    {
        ArgumentNullException.ThrowIfNull(path);
        RequirePlainText(fileType, nameof(LoadFile));
        Text = File.ReadAllText(path);
    }

    /// <summary>Saves the text to a plain text file.</summary>
    /// <param name="path">The file's path.</param>
    public void SaveFile(string path) => SaveFile(path, RichTextBoxStreamType.PlainText);

    /// <summary>Saves the text to a file.</summary>
    /// <param name="path">The file's path.</param>
    /// <param name="fileType">The file's format: plain text.</param>
    /// <exception cref="NotSupportedException">The format is rich text.</exception>
    public void SaveFile(string path, RichTextBoxStreamType fileType)
    {
        ArgumentNullException.ThrowIfNull(path);
        RequirePlainText(fileType, nameof(SaveFile));
        File.WriteAllText(path, Text);
    }

    private static void RequirePlainText(RichTextBoxStreamType fileType, string member)
    {
        if (fileType is not (RichTextBoxStreamType.PlainText or RichTextBoxStreamType.UnicodePlainText))
        {
            throw new NotSupportedException($"RichTextBox.{member} with {fileType} is not supported by TabStudio's Forms layer: rich text format is not supported; use RichTextBoxStreamType.PlainText.");
        }
    }
}

/// <summary>The scroll bars of a rich text box.</summary>
public enum RichTextBoxScrollBars
{
    /// <summary>None.</summary>
    None = 0,

    /// <summary>A horizontal scroll bar when needed.</summary>
    Horizontal = 1,

    /// <summary>A vertical scroll bar when needed.</summary>
    Vertical = 2,

    /// <summary>Both when needed.</summary>
    Both = 3,

    /// <summary>Always a horizontal scroll bar.</summary>
    ForcedHorizontal = 17,

    /// <summary>Always a vertical scroll bar.</summary>
    ForcedVertical = 18,

    /// <summary>Always both.</summary>
    ForcedBoth = 19,
}

/// <summary>How <see cref="RichTextBox.Find(string, int, RichTextBoxFinds)"/> searches.</summary>
[Flags]
public enum RichTextBoxFinds
{
    /// <summary>Case-insensitive, forward.</summary>
    None = 0,

    /// <summary>Whole words only.</summary>
    WholeWord = 2,

    /// <summary>Case-sensitive.</summary>
    MatchCase = 4,

    /// <summary>No highlight.</summary>
    NoHighlight = 8,

    /// <summary>Backward.</summary>
    Reverse = 16,
}

/// <summary>The format of a file a rich text box loads or saves.</summary>
public enum RichTextBoxStreamType
{
    /// <summary>Rich text format.</summary>
    RichText = 0,

    /// <summary>Plain text.</summary>
    PlainText = 1,

    /// <summary>Rich text without OLE objects.</summary>
    RichNoOleObjs = 2,

    /// <summary>Rich text with text representations of OLE objects.</summary>
    TextTextOleObjs = 3,

    /// <summary>Unicode plain text.</summary>
    UnicodePlainText = 4,
}
