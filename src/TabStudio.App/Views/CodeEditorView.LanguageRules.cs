using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Folding;
using TabStudio.App.Editing.Extensions;
using TabStudio.Extensions.Languages;

namespace TabStudio.App.Views;

/// <summary>
/// The language configuration an installed extension contributes for the file's language, as VS Code applies it while
/// you type: brackets and quotes close themselves and are typed over, a selection is surrounded by a typed bracket,
/// Backspace between an empty pair deletes both, Enter indents by the language's rules and continues comments, a
/// closing bracket typed at the start of a line outdents it, the bracket at the caret and its match are outlined, and
/// the file folds by indentation and region markers. Line and block comments (Toggle Comment, Ctrl+/) come with the
/// file's type. Files without a contributed configuration behave as before.
/// </summary>
public sealed partial class CodeEditorView
{
    private static readonly TimeSpan FoldingDelay = TimeSpan.FromMilliseconds(400);

    // How far around the caret a matching bracket is looked for.
    private const int BracketWindow = 20_000;

    private readonly List<(TextAnchor Anchor, string Close)> _autoClosed = [];
    private LanguageConfiguration? _rules;
    private BracketHighlightRenderer? _brackets;
    private DispatcherTimer? _foldingTimer;
    private AutoClosingPair? _pendingClose;
    private int _pendingCloseCaret = -1;
    private bool _lineMatchedDecrease;
    private int _autoClosedLine;

    /// <summary>The language configuration the editor follows, for tests; <see langword="null"/> when no extension contributes one.</summary>
    internal LanguageConfiguration? LanguageRules => _rules;

    /// <summary>The bracket at the caret and its match, as outlined now; for tests.</summary>
    internal BracketMatch? MatchedBracket => _brackets?.Match;

    private void InitializeLanguageRules()
    {
        var textArea = _editor.TextArea;
        textArea.TextEntering += OnRulesTextEntering;
        textArea.TextEntered += OnRulesTextEntered;
        textArea.AddHandler(KeyDownEvent, OnRulesKeyDown, RoutingStrategies.Tunnel);
        textArea.Caret.PositionChanged += OnRulesCaretMoved;
        _editor.Document.Changed += OnRulesDocumentChanged;
        UpdateLanguageRules();
    }

    private void DisposeLanguageRules()
    {
        var textArea = _editor.TextArea;
        textArea.TextEntering -= OnRulesTextEntering;
        textArea.TextEntered -= OnRulesTextEntered;
        textArea.RemoveHandler(KeyDownEvent, OnRulesKeyDown);
        textArea.Caret.PositionChanged -= OnRulesCaretMoved;
        _editor.Document.Changed -= OnRulesDocumentChanged;
        _foldingTimer?.Stop();
        RemoveFolding();
    }

    /// <summary>Follows the configuration for the file's language now, after the file's type or the extensions changed.</summary>
    private void UpdateLanguageRules()
    {
        var rules = EditorExtensions.ConfigurationFor(_document.FileType, _document.FilePath);
        if (ReferenceEquals(rules, _rules))
        {
            return;
        }

        _rules = rules;
        _autoClosed.Clear();
        var textView = _editor.TextArea.TextView;
        if (rules is { Brackets.Count: > 0 })
        {
            if (_brackets is null)
            {
                _brackets = new BracketHighlightRenderer();
                _brackets.SetTheme(EditorExtensions.Theme);
                textView.BackgroundRenderers.Add(_brackets);
            }
        }
        else if (_brackets is not null)
        {
            textView.BackgroundRenderers.Remove(_brackets);
            _brackets = null;
        }

        if (rules is null)
        {
            _foldingTimer?.Stop();
            if (!HasServerFolds)
            {
                RemoveFolding();
            }
        }
        else if (!HasServerFolds)
        {
            EnsureFolding();
            UpdateFoldings();
        }

        UpdateBracketHighlight();
        textView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
    }

    private void OnRulesTextEntering(object? sender, TextInputEventArgs e)
    {
        _pendingClose = null;
        _lineMatchedDecrease = true;
        if (_rules is not { } rules || e.Handled || e.Text is not { Length: > 0 } text || _completion?.IsCommitting == true)
        {
            return;
        }

        if (text is "\n" or "\r" or "\r\n")
        {
            e.Handled = TypeNewLine(rules);
            return;
        }

        if (text.Length != 1)
        {
            return;
        }

        var typed = text[0];
        var textArea = _editor.TextArea;
        if (!textArea.Selection.IsEmpty)
        {
            e.Handled = Surround(rules, typed);
            return;
        }

        var document = _editor.Document;
        var caret = _editor.CaretOffset;
        var line = document.GetLineByOffset(caret);
        var before = document.GetText(line.Offset, caret - line.Offset);
        var after = document.GetText(caret, line.EndOffset - caret);
        if (AutoClosingRules.ShouldTypeOver(rules, typed, after, TakeAutoClosed(caret, typed)))
        {
            _editor.CaretOffset = caret + 1;
            e.Handled = true;
            return;
        }

        _lineMatchedDecrease = EnterRules.ShouldOutdent(rules, document.GetText(line));
        _pendingClose = AutoClosingRules.FindPairToClose(rules, before, typed, after, TokenContextAt(caret > line.Offset ? caret - 1 : caret));
        _pendingCloseCaret = caret + 1;
    }

    private void OnRulesTextEntered(object? sender, TextInputEventArgs e)
    {
        if (_rules is not { } rules || e.Text is not { Length: 1 })
        {
            return;
        }

        var document = _editor.Document;
        var caret = _editor.CaretOffset;
        if (_pendingClose is { } pair && caret == _pendingCloseCaret)
        {
            // The closing text joins the typed character's undo step; the caret stays between them.
            document.UndoStack.StartContinuedUndoGroup();
            try
            {
                document.Insert(caret, pair.Close, AnchorMovementType.BeforeInsertion);
            }
            finally
            {
                document.UndoStack.EndUndoGroup();
            }

            _editor.CaretOffset = caret;
            var anchor = document.CreateAnchor(caret);
            anchor.MovementType = AnchorMovementType.AfterInsertion;
            _autoClosed.Add((anchor, pair.Close));
            _autoClosedLine = document.GetLineByOffset(caret).LineNumber;
        }

        _pendingClose = null;
        OutdentIfClosing(rules);
    }

    /// <summary>Backspace between an empty pair that closed itself deletes both halves.</summary>
    private void OnRulesKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || _rules is not { } rules || e.Key != Key.Back || e.KeyModifiers != KeyModifiers.None || !_editor.TextArea.Selection.IsEmpty)
        {
            return;
        }

        var document = _editor.Document;
        var caret = _editor.CaretOffset;
        if (caret == 0 || caret >= document.TextLength)
        {
            return;
        }

        var open = document.GetCharAt(caret - 1);
        var close = document.GetCharAt(caret);
        if (AutoClosingRules.ShouldDeletePair(rules, open, close) && TakeAutoClosed(caret, close))
        {
            document.Remove(caret - 1, 2);
            e.Handled = true;
        }
    }

    private void OnRulesCaretMoved(object? sender, EventArgs e)
    {
        if (_rules is null)
        {
            return;
        }

        // Pairs closed on another line are no longer typed over, as in VS Code.
        if (_autoClosed.Count > 0 && _editor.TextArea.Caret.Line != _autoClosedLine)
        {
            _autoClosed.Clear();
        }

        UpdateBracketHighlight();
    }

    private void OnRulesDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_folding is null)
        {
            return;
        }

        if (_foldingTimer is null)
        {
            _foldingTimer = new DispatcherTimer { Interval = FoldingDelay };
            _foldingTimer.Tick += (_, _) =>
            {
                _foldingTimer.Stop();
                UpdateFoldings();
            };
        }

        _foldingTimer.Stop();
        _foldingTimer.Start();
    }

    /// <summary>Enter by the language's rules: indent after an opening bracket, put a closing one on its own line, continue a comment.</summary>
    /// <returns>Whether a rule applied; otherwise the editor starts the line as usual, with the same indentation.</returns>
    private bool TypeNewLine(LanguageConfiguration rules)
    {
        var textArea = _editor.TextArea;
        if (!textArea.Selection.IsEmpty)
        {
            return false;
        }

        var document = _editor.Document;
        var caret = _editor.CaretOffset;
        var line = document.GetLineByOffset(caret);
        var before = document.GetText(line.Offset, caret - line.Offset);
        var after = document.GetText(caret, line.EndOffset - caret);
        var previous = line.PreviousLine is { } above ? document.GetText(above) : string.Empty;
        if (EnterRules.Decide(rules, before, after, previous) is not { } action)
        {
            return false;
        }

        var unit = _editor.Options.IndentationString;
        var indentation = LeadingWhitespace(document.GetText(line));
        if (indentation.Length > before.Length)
        {
            indentation = indentation[..before.Length];
        }

        var newLine = TextUtilities.GetNewLineFromDocument(document, line.LineNumber);
        var next = action.Indent switch
        {
            IndentAction.Indent or IndentAction.IndentOutdent => indentation + unit,
            IndentAction.Outdent => Outdent(indentation, unit),
            _ => indentation,
        };
        if (action.RemoveText > 0)
        {
            next = next[..Math.Max(0, next.Length - action.RemoveText)];
        }

        next += action.AppendText ?? string.Empty;
        var insertion = newLine + next;
        var caretAfter = insertion.Length;
        if (action.Indent == IndentAction.IndentOutdent)
        {
            insertion += newLine + indentation;
        }

        // The text after the caret moves to the new line without its leading spaces.
        var spaces = after.Length - after.TrimStart(' ', '\t').Length;
        document.Replace(caret, spaces, insertion);
        _editor.CaretOffset = caret + caretAfter;
        _editor.TextArea.Caret.BringCaretToView();
        return true;
    }

    /// <summary>Surrounds the selection with the pair whose opening character was typed.</summary>
    private bool Surround(LanguageConfiguration rules, char typed)
    {
        var textArea = _editor.TextArea;
        if (textArea.Selection is RectangleSelection || AutoClosingRules.FindSurroundingPair(rules, typed) is not { } pair)
        {
            return false;
        }

        var document = _editor.Document;
        var start = _editor.SelectionStart;
        var length = _editor.SelectionLength;
        document.BeginUpdate();
        try
        {
            document.Insert(start + length, pair.Close);
            document.Insert(start, pair.Open);
        }
        finally
        {
            document.EndUpdate();
        }

        _editor.Select(start + pair.Open.Length, length);
        return true;
    }

    /// <summary>Outdents the line once the typed character makes it start with a closing bracket, such as <c>}</c>.</summary>
    private void OutdentIfClosing(LanguageConfiguration rules)
    {
        var document = _editor.Document;
        var line = document.GetLineByOffset(_editor.CaretOffset);
        var text = document.GetText(line);
        if (_lineMatchedDecrease || !EnterRules.ShouldOutdent(rules, text))
        {
            return;
        }

        var above = line.PreviousLine;
        while (above is not null && string.IsNullOrWhiteSpace(document.GetText(above)))
        {
            above = above.PreviousLine;
        }

        if (above is null)
        {
            return;
        }

        var aboveText = document.GetText(above);
        var aboveIndentation = LeadingWhitespace(aboveText);
        var target = EnterRules.IncreasesIndent(rules, aboveText) ? aboveIndentation : Outdent(aboveIndentation, _editor.Options.IndentationString);
        var current = LeadingWhitespace(text);
        if (target.Length < current.Length)
        {
            document.UndoStack.StartContinuedUndoGroup();
            try
            {
                document.Replace(line.Offset, current.Length, target);
            }
            finally
            {
                document.UndoStack.EndUndoGroup();
            }
        }
    }

    /// <summary>Whether the closing text at an offset was inserted by auto-closing; it is forgotten once typed over or deleted.</summary>
    private bool TakeAutoClosed(int offset, char close)
    {
        var document = _editor.Document;
        for (var i = _autoClosed.Count - 1; i >= 0; i--)
        {
            var (anchor, text) = _autoClosed[i];
            if (anchor.IsDeleted)
            {
                _autoClosed.RemoveAt(i);
            }
            else if (anchor.Offset == offset && text.Length > 0 && text[0] == close
                && offset + text.Length <= document.TextLength && document.GetText(offset, text.Length) == text)
            {
                _autoClosed.RemoveAt(i);
                if (text.Length > 1)
                {
                    // A closing text of several characters, such as " */", is typed over one character at a time.
                    var rest = document.CreateAnchor(offset + 1);
                    rest.MovementType = AnchorMovementType.AfterInsertion;
                    _autoClosed.Add((rest, text[1..]));
                }

                return true;
            }
        }

        return false;
    }

    private void UpdateBracketHighlight()
    {
        if (_brackets is null || _rules is not { } rules)
        {
            return;
        }

        var document = _editor.Document;
        var caret = _editor.CaretOffset;
        BracketMatch? match = null;
        if (NextToBracket(document, caret, rules))
        {
            var start = Math.Max(0, caret - BracketWindow);
            var end = Math.Min(document.TextLength, caret + BracketWindow);
            var found = BracketMatcher.Find(document.GetText(start, end - start), caret - start, rules.Brackets, offset => TokenContextAt(start + offset) == TokenContexts.None);
            if (found is { } window)
            {
                match = window with { OpenStart = window.OpenStart + start, CloseStart = window.CloseStart + start };
            }
        }

        if (_brackets.SetMatch(match))
        {
            _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
        }
    }

    private static bool NextToBracket(TextDocument document, int caret, LanguageConfiguration rules)
    {
        var before = caret > 0 ? document.GetCharAt(caret - 1) : '\0';
        var after = caret < document.TextLength ? document.GetCharAt(caret) : '\0';
        foreach (var pair in rules.Brackets)
        {
            if (pair.Open.Length > 0 && pair.Close.Length > 0
                && (pair.Open[^1] == before || pair.Close[^1] == before || pair.Open[0] == after || pair.Close[0] == after))
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateFoldings()
    {
        // The language server's folding ranges, when it sends them, know the file better than indentation does.
        if (_folding is null || _rules is not { } rules || HasServerFolds)
        {
            return;
        }

        var document = _editor.Document;
        var lines = new string[document.LineCount];
        for (var i = 0; i < lines.Length; i++)
        {
            lines[i] = document.GetText(document.GetLineByNumber(i + 1));
        }

        var foldings = new List<NewFolding>();
        foreach (var range in FoldingRanges.Compute(lines, rules.Folding, _editor.Options.IndentationSize))
        {
            if (range.EndLine >= lines.Length || range.EndLine <= range.StartLine)
            {
                continue;
            }

            var start = document.GetLineByNumber(range.StartLine + 1).EndOffset;
            var end = document.GetLineByNumber(range.EndLine + 1).EndOffset;
            if (end > start)
            {
                foldings.Add(new NewFolding(start, end) { Name = "…" });
            }
        }

        foldings.Sort((a, b) => a.StartOffset.CompareTo(b.StartOffset));
        _folding.UpdateFoldings(foldings, -1);
    }

    /// <summary>Whether an offset is in a string, a comment or a regular expression, from the TextMate tokens of its line.</summary>
    private TokenContexts TokenContextAt(int offset)
    {
        if (TextMateInstallation?.EditorModel is not { } model)
        {
            return TokenContexts.None;
        }

        var document = _editor.Document;
        var line = document.GetLineByOffset(Math.Clamp(offset, 0, document.TextLength));
        var tokens = model.Get(line.LineNumber - 1)?.Tokens;
        if (tokens is null || tokens.Count == 0)
        {
            return TokenContexts.None;
        }

        var column = offset - line.Offset;
        TextMateSharp.Model.TMToken? token = null;
        foreach (var candidate in tokens)
        {
            if (candidate.StartIndex > column)
            {
                break;
            }

            token = candidate;
        }

        var context = TokenContexts.None;
        foreach (var scope in token?.Scopes ?? [])
        {
            if (scope.StartsWith("comment", StringComparison.Ordinal))
            {
                context |= TokenContexts.Comment;
            }
            else if (scope.StartsWith("string.regexp", StringComparison.Ordinal))
            {
                context |= TokenContexts.RegularExpression;
            }
            else if (scope.StartsWith("string", StringComparison.Ordinal))
            {
                context |= TokenContexts.StringLiteral;
            }
        }

        return context;
    }

    private static string LeadingWhitespace(string text)
    {
        var length = 0;
        while (length < text.Length && text[length] is ' ' or '\t')
        {
            length++;
        }

        return text[..length];
    }

    private static string Outdent(string indentation, string unit)
    {
        if (indentation.EndsWith('\t'))
        {
            return indentation[..^1];
        }

        var remove = 0;
        while (remove < unit.Length && remove < indentation.Length && indentation[^(remove + 1)] == ' ')
        {
            remove++;
        }

        return indentation[..^remove];
    }
}
