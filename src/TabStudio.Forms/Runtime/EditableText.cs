using System.Drawing;
using System.Windows.Forms;
using TabStudio.Forms.Rendering;

namespace TabStudio.Forms.Runtime;

/// <summary>
/// The text of a field edited in the native text box while its control has focus, such as an editable combo box or a
/// numeric up-down: the text, the selection, and the revisions that tell the native box when the app replaced them.
/// </summary>
internal sealed class EditableText
{
    private string _text = string.Empty;
    private int _selectionStart;
    private int _selectionLength;

    /// <summary>The text.</summary>
    public string Text => _text;

    /// <summary>Changes when the app sets the text.</summary>
    public int TextRevision { get; private set; }

    /// <summary>Changes when the app sets the selection.</summary>
    public int SelectionRevision { get; private set; }

    /// <summary>Where the selection starts.</summary>
    public int SelectionStart => Math.Min(_selectionStart, _text.Length);

    /// <summary>The selection's length.</summary>
    public int SelectionLength => Math.Min(_selectionLength, Math.Max(0, _text.Length - SelectionStart));

    /// <summary>Sets the text from the app, which replaces what the native box has.</summary>
    /// <param name="text">The text.</param>
    /// <returns>Whether it changed.</returns>
    public bool SetFromApp(string text)
    {
        if (string.Equals(_text, text, StringComparison.Ordinal))
        {
            return false;
        }

        _text = text;
        TextRevision++;
        _selectionStart = text.Length;
        _selectionLength = 0;
        SelectionRevision++;
        return true;
    }

    /// <summary>Takes text the user typed, unless the app replaced the text since.</summary>
    /// <returns>Whether the text changed.</returns>
    public bool ApplyFromEditor(string text, int selectionStart, int selectionLength, int revision)
    {
        if (revision != TextRevision)
        {
            return false;
        }

        _selectionStart = selectionStart;
        _selectionLength = selectionLength;
        if (string.Equals(_text, text, StringComparison.Ordinal))
        {
            return false;
        }

        _text = text;
        return true;
    }

    /// <summary>Takes the selection the user made.</summary>
    public void ApplySelection(int selectionStart, int selectionLength)
    {
        _selectionStart = selectionStart;
        _selectionLength = selectionLength;
    }

    /// <summary>Selects part of the text from the app.</summary>
    public void Select(int start, int length)
    {
        _selectionStart = Math.Clamp(start, 0, _text.Length);
        _selectionLength = Math.Clamp(length, 0, _text.Length - _selectionStart);
        SelectionRevision++;
    }

    /// <summary>The native box's frame over an area of a control.</summary>
    /// <param name="control">The control.</param>
    /// <param name="clientOrigin">Where the control's client area is in the window.</param>
    /// <param name="area">The text's area, in client coordinates.</param>
    /// <param name="alignment">The text's alignment.</param>
    /// <param name="readOnly">Whether the text cannot be changed.</param>
    /// <param name="maxLength">The most characters.</param>
    /// <param name="backColor">The background color.</param>
    public EditorFrame Frame(Control control, Point clientOrigin, Rectangle area, HorizontalAlignment alignment, bool readOnly, int maxLength, Color backColor) => new(
        (int)control.Handle,
        new Rectangle(clientOrigin.X + area.X, clientOrigin.Y + area.Y, area.Width, area.Height),
        _text,
        TextRevision,
        SelectionStart,
        SelectionLength,
        SelectionRevision,
        control.Font.Spec,
        control.Enabled ? control.ForeColor : Theme.DisabledText,
        backColor,
        alignment,
        Multiline: false,
        WordWrap: false,
        readOnly,
        PasswordChar: '\0',
        maxLength,
        ScrollBars.None,
        AcceptsReturn: false,
        AcceptsTab: false,
        CharacterCasing.Normal);
}
