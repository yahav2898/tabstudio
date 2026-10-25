using TabStudio.App.Editing;

namespace TabStudio.App.Views;

/// <summary>
/// How the editor draws while it scrolls. AvaloniaEdit redoes work on every frame of a scroll that only depends on
/// what is in view: it formats every visible line number again, records every visible line of text again, and makes
/// a control per fold marker again. The editor uses parts that look and behave the same but keep that work: line
/// numbers from a cache (<see cref="CachedLineNumberMargin"/>), text that moves instead of being redrawn
/// (<see cref="ScrollingTextLayer"/>) and fold markers drawn by their margin (<see cref="DrawnFoldingMargin"/>, put in
/// place when the file starts folding, in <c>CodeEditorView.Folding.cs</c>). The colorizers and margins of the editor
/// allocate nothing per frame.
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
