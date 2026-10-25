using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using TabStudio.App.Editing;
using TabStudio.App.Editing.Extensions;
using TabStudio.App.Editing.Snippets;
using TabStudio.Compilation.Features;
using TabStudio.Extensions.Snippets;

namespace TabStudio.App.Views;

/// <summary>
/// Snippets from installed extensions, as VS Code expands them: they are offered in the completion list for the file's
/// language (and by Edit &gt; IntelliSense &gt; Insert Snippet), and once inserted, Tab and Shift+Tab move between their
/// tab stops, typing in a tab stop changes its linked copies, a choice tab stop lists its options, and Escape or moving
/// out of the snippet ends it.
/// </summary>
public sealed partial class CodeEditorView : ISnippetEditor
{
    private readonly List<TextReplacement> _snippetMirrors = [];
    private SnippetCompletionFeatures? _snippetCompletion;
    private SnippetTabStopRenderer? _snippetRenderer;
    private SnippetSession? _snippet;
    private TextAnchor? _snippetStart;
    private TextAnchor? _snippetEnd;
    private bool _applyingSnippetMirrors;
    private bool _followsCompletionCaret;

    /// <summary>The snippet being filled in, for tests; <see langword="null"/> when there is none.</summary>
    internal SnippetSession? Snippet => _snippet is { IsActive: true } snippet ? snippet : null;

    /// <inheritdoc />
    IReadOnlyList<SnippetDefinition> ISnippetEditor.Snippets => EditorExtensions.SnippetsFor(_document.FileType, _document.FilePath);

    /// <inheritdoc />
    int ISnippetEditor.Caret => _editor.CaretOffset;

    /// <inheritdoc />
    string ISnippetEditor.LineTextBefore(int offset)
    {
        var document = _editor.Document;
        offset = Math.Clamp(offset, 0, document.TextLength);
        var line = document.GetLineByOffset(offset);
        return document.GetText(line.Offset, offset - line.Offset);
    }

    /// <inheritdoc />
    void ISnippetEditor.InsertSnippet(SnippetDefinition snippet, int start, int length) => InsertSnippet(snippet, start, length);

    /// <inheritdoc />
    void ISnippetEditor.ChooseOption(string option)
    {
        if (_snippet is not { IsActive: true } session)
        {
            return;
        }

        foreach (var range in session.Ranges)
        {
            if (range.IsPrimary && range.Index == session.CurrentIndex)
            {
                _editor.Document.Replace(range.Start, range.Length, option);
                _editor.Select(range.Start, option.Length);
                ShowTabStops();
                return;
            }
        }
    }

    private void InitializeSnippets()
    {
        // Snippets come through the completion list: the document's own IntelliSense, if any, goes under them.
        var inner = _features;
        _snippetCompletion = new SnippetCompletionFeatures(inner, this);
        if (_completion is not null)
        {
            DisposeCompletion();
            if (_completionPopup is not null)
            {
                _popupHost?.Children.Remove(_completionPopup);
            }
        }

        _features = _snippetCompletion;
        InitializeCompletion(_snippetCompletion);
        if (inner is null)
        {
            // Without IntelliSense of its own, nothing else moves the list with the caret or closes it with the editor.
            _followsCompletionCaret = true;
            _editor.TextArea.Caret.PositionChanged += OnSnippetListCaretMoved;
            TextView.ScrollOffsetChanged += OnSnippetListScrolled;
            _editor.TextArea.LostFocus += OnSnippetListLostFocus;
        }

        _snippetRenderer = new SnippetTabStopRenderer();
        _snippetRenderer.SetTheme(EditorExtensions.Theme);
        TextView.BackgroundRenderers.Add(_snippetRenderer);
        _editor.TextArea.AddHandler(KeyDownEvent, OnSnippetKeyDown, RoutingStrategies.Tunnel);
        _editor.Document.Changed += OnSnippetDocumentChanged;
        _editor.Document.TextChanged += OnSnippetTextChanged;
        _editor.TextArea.Caret.PositionChanged += OnSnippetCaretMoved;
        _document.SnippetInsertRequested += OnSnippetInsertRequested;
    }

    private void DisposeSnippets()
    {
        // The completion list itself goes with the rest of IntelliSense.
        if (_followsCompletionCaret)
        {
            _editor.TextArea.Caret.PositionChanged -= OnSnippetListCaretMoved;
            TextView.ScrollOffsetChanged -= OnSnippetListScrolled;
            _editor.TextArea.LostFocus -= OnSnippetListLostFocus;
        }

        _editor.TextArea.RemoveHandler(KeyDownEvent, OnSnippetKeyDown);
        _editor.Document.Changed -= OnSnippetDocumentChanged;
        _editor.Document.TextChanged -= OnSnippetTextChanged;
        _editor.TextArea.Caret.PositionChanged -= OnSnippetCaretMoved;
        _document.SnippetInsertRequested -= OnSnippetInsertRequested;
    }

    /// <summary>Inserts a snippet chosen with Insert Snippet at the caret, around the selection.</summary>
    private void OnSnippetInsertRequested(object? sender, SnippetDefinition snippet)
    {
        _completion?.Close();
        _editor.TextArea.Focus();
        InsertSnippet(snippet, _editor.SelectionStart, _editor.SelectionLength);
    }

    /// <summary>Replaces a range with a snippet, expanded with the line's indentation and the editor's variables, and starts its tab stops.</summary>
    private void InsertSnippet(SnippetDefinition snippet, int start, int length)
    {
        var document = _editor.Document;
        start = Math.Clamp(start, 0, document.TextLength);
        length = Math.Clamp(length, 0, document.TextLength - start);
        var line = document.GetLineByOffset(start);
        var lineText = document.GetText(line);
        var indentation = LeadingWhitespace(lineText);
        if (indentation.Length > start - line.Offset)
        {
            indentation = indentation[..(start - line.Offset)];
        }

        var variables = new StandardSnippetVariables
        {
            FilePath = _document.FilePath,
            WorkspaceFolder = Workspace?.Workspace?.RootDirectory,
            SelectedText = _editor.SelectionLength > 0 ? _editor.SelectedText : string.Empty,
            CurrentLine = lineText,
            CurrentWord = WordAt(lineText, start - line.Offset),
            LineIndex = line.LineNumber - 1,
            LineComment = _document.FileType.LineComment,
            BlockCommentStart = _document.FileType.BlockComment?.Open,
            BlockCommentEnd = _document.FileType.BlockComment?.Close,
        };
        var insertion = new SnippetInsertion(indentation, _editor.Options.IndentationString, TextUtilities.GetNewLineFromDocument(document, line.LineNumber));
        var expansion = SnippetExpander.Expand(snippet.Body, variables, insertion);

        EndSnippetSession();
        _editor.TextArea.ClearSelection();
        document.Replace(start, length, expansion.Text);
        StartSnippetSession(expansion, start);
    }

    private void StartSnippetSession(SnippetExpansion expansion, int offset)
    {
        var document = _editor.Document;
        var session = new SnippetSession(expansion, offset);
        if (expansion.Placeholders.All(p => p.Index == 0))
        {
            // Nothing to fill in: the caret goes to the end ($0).
            _editor.CaretOffset = Math.Clamp(offset + expansion.Placeholders[0].Start, 0, document.TextLength);
            return;
        }

        _snippet = session;
        _snippetStart = document.CreateAnchor(offset);
        _snippetStart.MovementType = AnchorMovementType.BeforeInsertion;
        _snippetEnd = document.CreateAnchor(Math.Min(document.TextLength, offset + expansion.Text.Length));
        _snippetEnd.MovementType = AnchorMovementType.AfterInsertion;
        GoToTabStop(session.MoveNext());
    }

    /// <summary>Selects a tab stop; at the last one (<c>$0</c>) the caret goes there and the snippet ends.</summary>
    private void GoToTabStop((int Start, int Length)? range)
    {
        if (_snippet is not { } session || range is not { } target)
        {
            EndSnippetSession();
            return;
        }

        if (!session.IsActive)
        {
            _editor.TextArea.ClearSelection();
            _editor.CaretOffset = target.Start + target.Length;
            EndSnippetSession();
            return;
        }

        if (session.CurrentChoices is { Count: > 0 } choices)
        {
            // The options are listed under the tab stop; typing replaces the selected text, as with any tab stop.
            _editor.CaretOffset = target.Start;
            _editor.TextArea.Selection = Selection.Create(_editor.TextArea, target.Start + target.Length, target.Start);
            var start = target.Start;
            Dispatcher.UIThread.Post(() => OfferChoices(start, choices), DispatcherPriority.Background);
        }
        else
        {
            _editor.Select(target.Start, target.Length);
        }

        _editor.TextArea.Caret.BringCaretToView();
        ShowTabStops();
    }

    private void OfferChoices(int start, IReadOnlyList<string> choices)
    {
        if (_snippetCompletion is null || _completion is null || _snippet is not { IsActive: true })
        {
            return;
        }

        _snippetCompletion.OfferChoices(start, choices);
        _ = _completion.RequestAsync(CompletionTrigger.Invoke);
    }

    private void ShowTabStops()
    {
        if (_snippetRenderer is null)
        {
            return;
        }

        if (_snippet is { IsActive: true } session)
        {
            _snippetRenderer.Show(session.Ranges, session.CurrentIndex);
        }
        else
        {
            _snippetRenderer.Clear();
        }

        TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
    }

    /// <summary>Ends the snippet being filled in, if any.</summary>
    private void EndSnippetSession()
    {
        if (_snippet is null)
        {
            return;
        }

        _snippet.End();
        _snippet = null;
        _snippetStart = null;
        _snippetEnd = null;
        _snippetMirrors.Clear();
        ShowTabStops();
    }

    /// <summary>Tab and Shift+Tab move between tab stops; Escape ends the snippet. The completion list sees these keys first.</summary>
    private void OnSnippetKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _snippet is not { IsActive: true } session || _completion?.IsOpen == true)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Tab when e.KeyModifiers == KeyModifiers.None:
                GoToTabStop(session.MoveNext());
                e.Handled = true;
                break;
            case Key.Tab when e.KeyModifiers == KeyModifiers.Shift:
                if (session.MovePrevious() is { } previous)
                {
                    GoToTabStop(previous);
                }

                e.Handled = true;
                break;
            case Key.Escape when e.KeyModifiers == KeyModifiers.None:
                EndSnippetSession();
                e.Handled = true;
                break;
        }
    }

    /// <summary>Keeps the tab stops in place through an edit and works out what the linked copies of the current one become.</summary>
    private void OnSnippetDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_snippet is not { IsActive: true } session)
        {
            return;
        }

        var document = _editor.Document;
        if (!document.UndoStack.AcceptChanges)
        {
            // Undo and redo end the snippet, as in VS Code.
            EndSnippetSession();
            return;
        }

        var mirrors = session.OnTextChanged(e.Offset, e.RemovalLength, e.InsertionLength, document.GetText, _applyingSnippetMirrors);
        if (!_applyingSnippetMirrors)
        {
            _snippetMirrors.Clear();
            _snippetMirrors.AddRange(mirrors);
        }
    }

    /// <summary>Applies the linked copies' changes inside the same update, so one undo step reverts the typing and its copies.</summary>
    private void OnSnippetTextChanged(object? sender, EventArgs e)
    {
        if (_applyingSnippetMirrors || _snippet is not { IsActive: true })
        {
            return;
        }

        if (_snippetMirrors.Count > 0)
        {
            var mirrors = _snippetMirrors.ToArray();
            _snippetMirrors.Clear();
            var document = _editor.Document;
            _applyingSnippetMirrors = true;
            try
            {
                foreach (var mirror in mirrors)
                {
                    document.Replace(mirror.Start, mirror.Length, mirror.Text);
                }
            }
            finally
            {
                _applyingSnippetMirrors = false;
            }
        }

        ShowTabStops();
    }

    /// <summary>Moving the caret out of the snippet ends it.</summary>
    private void OnSnippetCaretMoved(object? sender, EventArgs e)
    {
        if (_snippet is not { IsActive: true } || _snippetStart is null || _snippetEnd is null || _applyingSnippetMirrors)
        {
            return;
        }

        var caret = _editor.CaretOffset;
        if (_snippetStart.IsDeleted || _snippetEnd.IsDeleted || caret < _snippetStart.Offset || caret > _snippetEnd.Offset)
        {
            EndSnippetSession();
        }
    }

    private void OnSnippetListCaretMoved(object? sender, EventArgs e) => _completion?.Refilter();

    private void OnSnippetListScrolled(object? sender, EventArgs e) => PlaceCompletionPopup();

    private void OnSnippetListLostFocus(object? sender, RoutedEventArgs e)
    {
        if (!_editor.TextArea.IsKeyboardFocusWithin && _completion?.IsCommitting != true)
        {
            _completion?.Close();
        }
    }

    /// <summary>The word around a column of a line, for <c>TM_CURRENT_WORD</c>.</summary>
    private static string WordAt(string line, int column)
    {
        var start = Math.Clamp(column, 0, line.Length);
        var end = start;
        while (start > 0 && CompletionMatcher.IsIdentifierCharacter(line[start - 1]))
        {
            start--;
        }

        while (end < line.Length && CompletionMatcher.IsIdentifierCharacter(line[end]))
        {
            end++;
        }

        return line[start..end];
    }
}
