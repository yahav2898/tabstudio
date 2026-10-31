using System.Globalization;
using Avalonia.Automation;

namespace TabStudio.App.Views;

/// <summary>
/// The editor as TalkBack reads it (docs/design/README.md, "Screen readers"): its name is the file, the caret's line
/// and that line's text ("Program.cs, line 12 of 40: Console.WriteLine(name);"), kept up to date as the caret moves
/// and the line changes, so moving through the code reads it line by line.
/// </summary>
public sealed partial class CodeEditorView
{
    /// <summary>The most of a line that is read.</summary>
    private const int MaxSpokenLine = 200;

    private int _spokenLine = -1;
    private string? _spokenText;

    /// <summary>The editor's accessible name now; for tests.</summary>
    internal string? AccessibleName => AutomationProperties.GetName(_editor.TextArea);

    private void InitializeAccessibility()
    {
        AutomationProperties.SetHelpText(_editor.TextArea, "Code editor. Double tap to edit; the touch bar has Rename, Peek, Comment, Format and Run.");
        _editor.TextArea.Caret.PositionChanged += OnAccessibleCaretMoved;
        _document.Document.Changed += OnAccessibleTextChanged;
        UpdateAccessibleName();
    }

    private void DisposeAccessibility()
    {
        _editor.TextArea.Caret.PositionChanged -= OnAccessibleCaretMoved;
        _document.Document.Changed -= OnAccessibleTextChanged;
    }

    private void OnAccessibleCaretMoved(object? sender, EventArgs e)
    {
        if (_editor.TextArea.Caret.Line != _spokenLine)
        {
            UpdateAccessibleName();
        }
    }

    private void OnAccessibleTextChanged(object? sender, AvaloniaEdit.Document.DocumentChangeEventArgs e) => UpdateAccessibleName();

    private void UpdateAccessibleName()
    {
        var document = _editor.Document;
        if (document is null)
        {
            return;
        }

        var number = Math.Clamp(_editor.TextArea.Caret.Line, 1, Math.Max(1, document.LineCount));
        var line = document.GetLineByNumber(number);
        var text = document.GetText(line.Offset, Math.Min(line.Length, MaxSpokenLine)).Trim();
        if (number == _spokenLine && text == _spokenText)
        {
            return;
        }

        _spokenLine = number;
        _spokenText = text;
        var name = string.Create(CultureInfo.InvariantCulture, $"{_document.FileName}, line {number} of {document.LineCount}: {(text.Length == 0 ? "blank" : text)}");
        AutomationProperties.SetName(_editor.TextArea, name);
        AutomationProperties.SetName(_editor, name);
    }
}
