using Avalonia.Media;
using AvaloniaEdit.Folding;
using TabStudio.App.Editing;
using TabStudio.App.Editing.Extensions;
using TabStudio.App.Theming;

namespace TabStudio.App.Views;

/// <summary>
/// The editor's folds, from whichever source knows the file best: the language server's folding ranges when it sends
/// them, otherwise the indentation and region markers of an installed extension's language configuration.
/// </summary>
public sealed partial class CodeEditorView
{
    private static readonly IBrush FoldMarkerBrush = ThemeBrushes.Editor("Editor.FoldMarker");
    private static readonly IBrush FoldMarkerBackground = ThemeBrushes.Editor("Editor.FoldMarkerBackground");

    private FoldingManager? _folding;
    private DrawnFoldingMargin? _foldingMargin;

    /// <summary>The folds of the document, while something folds it; for tests.</summary>
    internal FoldingManager? Folding => _folding;

    /// <summary>Whether the language server's folding ranges decide the folds, rather than the language configuration.</summary>
    private bool HasServerFolds => _document.HasLanguageServer && _document.FoldingRanges.Count > 0;

    /// <summary>Installs the folding margin if it is not there yet, in the editor theme's colors.</summary>
    private FoldingManager EnsureFolding()
    {
        if (_folding is null)
        {
            _folding = FoldingManager.Install(_editor.TextArea);

            // AvaloniaEdit's margin makes a control per marker again on every frame of a scroll; this one draws them.
            _foldingMargin = DrawnFoldingMargin.ReplaceIn(_editor.TextArea, _folding);
            if (EditorExtensions.Theme is { } theme)
            {
                SetFoldingColors(theme);
            }
            else
            {
                ClearFoldingColors();
            }
        }

        return _folding;
    }

    /// <summary>Removes the folding margin and every fold.</summary>
    private void RemoveFolding()
    {
        if (_folding is not null)
        {
            FoldingManager.Uninstall(_folding);
            _folding = null;
            if (_foldingMargin is not null)
            {
                _editor.TextArea.LeftMargins.Remove(_foldingMargin);
                _foldingMargin = null;
            }
        }
    }
}
