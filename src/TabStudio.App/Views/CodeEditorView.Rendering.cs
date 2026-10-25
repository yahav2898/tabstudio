using TabStudio.App.Editing;

namespace TabStudio.App.Views;

/// <summary>
/// How the editor draws while it scrolls. AvaloniaEdit redoes work on every frame of a scroll that only depends on
/// what is in view: it formats and draws every visible line number again, records every visible line of text again,
/// and makes a control per fold marker again; with TextMate, it also tokenizes the first visible line again. The editor
/// uses parts that look and behave the same but keep that work: line numbers and text that move instead of being
/// redrawn (<see cref="CachedLineNumberMargin"/> and <see cref="ScrollingTextLayer"/>), fold markers drawn by their
/// margin (<see cref="DrawnFoldingMargin"/>, put in place when the file starts folding, in
/// <c>CodeEditorView.Folding.cs</c>) and TextMate tokenizing on a scroll only what has no colors yet
/// (<see cref="TextMateScrollTokenizing"/>, with every TextMate installation). The colorizers and margins of the editor
/// allocate next to nothing per frame.
/// </summary>
public sealed partial class CodeEditorView
{
    private ScrollingTextLayer? _textLayer;

    /// <summary>The layer that draws the text, for tests; <see langword="null"/> if AvaloniaEdit had none to replace.</summary>
    internal ScrollingTextLayer? TextLayer => _textLayer;

    /// <summary>Puts the line numbers and the text layer in place. Called once, by the constructor, after line numbers are on.</summary>
    private void InitializeRendering()
    {
        CachedLineNumberMargin.ReplaceIn(_editor);
        _textLayer = ScrollingTextLayer.Install(_editor.TextArea.TextView);
    }
}
