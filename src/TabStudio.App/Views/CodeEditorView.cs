using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Search;
using AvaloniaEdit.TextMate;
using TabStudio.App.Editing;
using TabStudio.App.Input;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.Views;

/// <summary>
/// The code editor for one document: AvaloniaEdit with Roslyn coloring and squiggles for C#, TextMate
/// coloring for other languages, find and replace, and the editor actions the view model asks for.
/// </summary>
public sealed partial class CodeEditorView : UserControl, IDisposable
{
    private static readonly IBrush CurrentLineBrush = ThemeBrushes.Get("Brush.CurrentLine");
    private static readonly IBrush CaretBrush = ThemeBrushes.Editor("Editor.Caret");

    private readonly DocumentViewModel _document;
    private readonly TextEditor _editor;
    private readonly ClassificationColorizer? _colorizer;
    private readonly DiagnosticRenderer? _diagnostics;
    private readonly TextMate.Installation? _textMate;
    private readonly SearchPanel _search;
    private bool _restoredCaret;

    /// <summary>Creates the editor for a document.</summary>
    /// <param name="document">The document to edit.</param>
    /// <param name="inlineCompletion">Optional completion provider, also used by editor integration tests.</param>
    public CodeEditorView(DocumentViewModel document, InlineCompletionController? inlineCompletion = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        DataContext = document;

        _editor = new TextEditor
        {
            Document = document.Document,
            ShowLineNumbers = true,
            WordWrap = false,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            LineNumbersMargin = new Thickness(18, 0, 16, 0),
            Padding = new Thickness(0, 4, 0, 0),
        };
        var indentation = IndentationStyle.For(document.FilePath, document.Document.Text);
        _editor.Options.ConvertTabsToSpaces = !indentation.UsesTabs;
        _editor.Options.IndentationSize = indentation.Size;
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.AllowScrollBelowDocument = true;
        _editor.Options.EnableRectangularSelection = true;
        _editor.TextArea.TextView.CurrentLineBackground = CurrentLineBrush;
        _editor.TextArea.TextView.CurrentLineBorder = null;
        _editor.TextArea.Caret.CaretBrush = CaretBrush;
        _editor.TextArea.RightClickMovesCaret = true;
        InitializeRendering();

        if (document.UsesSemanticColoring)
        {
            _colorizer = new ClassificationColorizer();
            _diagnostics = new DiagnosticRenderer();
            _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
            _editor.TextArea.TextView.BackgroundRenderers.Add(_diagnostics);
            ApplyClassifications();
            ApplyDiagnostics();
        }
        else if (document.FileType.HighlightingExtension is { } extension)
        {
            _textMate = TextMateHighlighting.TryInstall(_editor, extension);
        }

        if (document.HasLanguageServer)
        {
            InstallLanguageServerRendering(out _colorizer, out _diagnostics);
        }
        else if ((document.HasLanguageService || document.HasXamlFeatures) && !document.UsesSemanticColoring)
        {
            // Razor and WPF XAML keep their grammar's colors and add the language engine's squiggles.
            _diagnostics = new DiagnosticRenderer();
            _editor.TextArea.TextView.BackgroundRenderers.Add(_diagnostics);
            ApplyDiagnostics();
        }

        _search = SearchPanel.Install(_editor);
        UseRegexReplace();
        _editor.TextArea.TextView.ElementGenerators.Add(new BidiControlGenerator());
        Content = _editor;

        document.Document.Changed += OnTextChanged;
        document.ClassificationsChanged += OnClassificationsChanged;
        document.DiagnosticsChanged += OnDiagnosticsChanged;
        document.ActionRequested += OnActionRequested;
        document.NavigationRequested += OnNavigationRequested;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _editor.TextArea.TextView.PointerHover += OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped += OnPointerHoverStopped;
        InitializeInlineCompletion(inlineCompletion);
        InitializeIntelliSense();
        InitializeDebugging();
        InstallTestCommands();
        InitializePeek();
        InitializeReviewThreads();
        InitializeLiveTesting();
        InitializeLanguageServer();
        InitializeExtensions();
        InitializeTouchScrolling();
        InitializeLive();
        InitializeAccessibility();
    }

    /// <summary>The editor control, for tests and keyboard focus.</summary>
    public TextEditor Editor => _editor;

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeAccessibility();
        DisposeInlineCompletion();
        DisposeReviewThreads();
        DisposePeek();
        DisposeLiveTesting();
        DisposeIntelliSense();
        _document.Document.Changed -= OnTextChanged;
        _document.ClassificationsChanged -= OnClassificationsChanged;
        _document.DiagnosticsChanged -= OnDiagnosticsChanged;
        _document.ActionRequested -= OnActionRequested;
        _document.NavigationRequested -= OnNavigationRequested;
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.TextArea.TextView.PointerHover -= OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped -= OnPointerHoverStopped;
        _search.Uninstall();
        _textMate?.Dispose();
        DisposeDebugging();
        DisposeLanguageServer();
        DisposeExtensions();
        DisposeTouchScrolling();
        DisposeLive();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CloseCaretPopups();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && !IsVisible)
        {
            CloseCaretPopups();
        }

        if (change.Property == IsVisibleProperty && IsVisible)
        {
            // Restore the caret once the editor has a size, then honor any navigation that arrived while hidden.
            Dispatcher.UIThread.Post(() =>
            {
                RestoreCaretOnce();
                ApplyPendingNavigation();
                InputModality.FocusUnlessTouch(_editor.TextArea);
            }, DispatcherPriority.Background);
        }
    }

    private void RestoreCaretOnce()
    {
        if (_restoredCaret)
        {
            return;
        }

        _restoredCaret = true;
        if (_document.CaretOffset > 0 && _document.CaretOffset <= _editor.Document.TextLength)
        {
            _editor.CaretOffset = _document.CaretOffset;
            _editor.TextArea.Caret.BringCaretToView();
        }
    }

    private void ApplyPendingNavigation()
    {
        if (!IsVisible || _document.TakePendingNavigation() is not { } target)
        {
            return;
        }

        var line = Math.Clamp(target.Line, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(line);
        var column = Math.Clamp(target.Column, 1, documentLine.Length + 1);
        _editor.CaretOffset = documentLine.Offset + column - 1;
        _editor.ScrollTo(line, column);
        _editor.TextArea.Caret.BringCaretToView(40);
        InputModality.FocusUnlessTouch(_editor.TextArea);
    }

    private void OnNavigationRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ApplyPendingNavigation, DispatcherPriority.Background);

    private void OnTextChanged(object? sender, DocumentChangeEventArgs e)
    {
        _colorizer?.OnDocumentChanged(e);
        _diagnostics?.OnDocumentChanged(e);
    }

    private void OnClassificationsChanged(object? sender, EventArgs e)
    {
        ApplyClassifications();
        _editor.TextArea.TextView.Redraw();
    }

    private void OnDiagnosticsChanged(object? sender, EventArgs e)
    {
        ApplyDiagnostics();
        _editor.TextArea.TextView.InvalidateLayer(AvaloniaEdit.Rendering.KnownLayer.Selection);
        UpdateCaretMessage();
    }

    private void ApplyClassifications() =>
        _colorizer?.SetClassifications(_document.Classifications.Select(c => (c.Start, c.Length, c.Classification)));

    private void ApplyDiagnostics() =>
        _diagnostics?.SetDiagnostics(
            _document.Diagnostics.Where(d => d.Location?.FilePath == _document.FilePath),
            _editor.Document.TextLength);

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        var caret = _editor.TextArea.Caret;
        _document.CaretLine = caret.Line;
        _document.CaretColumn = caret.Column;
        _document.CaretOffset = caret.Offset;
        UpdateCaretMessage();
    }

    private void UpdateCaretMessage() =>
        _document.CaretMessage = _diagnostics?.DiagnosticAt(_editor.CaretOffset) is { } diagnostic
            ? $"{diagnostic.Id}: {diagnostic.Message}"
            : null;

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (_diagnostics is null || _editor.GetPositionFromPoint(e.GetPosition(_editor)) is not { } position)
        {
            return;
        }

        var offset = _editor.Document.GetOffset(position.Location);
        if (_diagnostics.DiagnosticAt(offset) is { } diagnostic)
        {
            ToolTip.SetTip(_editor, Describe(diagnostic));
            ToolTip.SetIsOpen(_editor, true);
            e.Handled = true;
        }
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e)
    {
        ToolTip.SetIsOpen(_editor, false);
        ToolTip.SetTip(_editor, null);
    }

    private void OnActionRequested(object? sender, EditorAction action)
    {
        switch (action)
        {
            case EditorAction.Find:
                _search.IsReplaceMode = false;
                _search.Open();
                _search.Reactivate();
                break;
            case EditorAction.Replace:
                _search.IsReplaceMode = true;
                _search.Open();
                _search.Reactivate();
                break;
            case EditorAction.ToggleComment:
                ToggleComment();
                break;
            case EditorAction.Undo:
                UndoEdit();
                break;
            case EditorAction.Redo:
                RedoEdit();
                break;
            case EditorAction.SelectAll:
                _editor.SelectAll();
                break;
            case EditorAction.Focus:
                InputModality.FocusUnlessTouch(_editor.TextArea);
                break;
        }
    }

    private void ToggleComment()
    {
        var document = _editor.Document;
        var start = document.GetLineByOffset(_editor.SelectionStart).LineNumber;
        var endOffset = _editor.SelectionStart + _editor.SelectionLength;
        var end = document.GetLineByOffset(endOffset).LineNumber;

        // A selection that ends at the very start of a line does not include that line, as in Visual Studio.
        if (end > start && document.GetLineByNumber(end).Offset == endOffset)
        {
            end--;
        }

        CommentToggler.Toggle(document, start, end, _document.FileType);
        _editor.TextArea.Focus();
    }

    private static string Describe(BuildDiagnostic diagnostic)
    {
        var severity = diagnostic.Severity switch
        {
            DiagnosticSeverityLevel.Error => "error",
            DiagnosticSeverityLevel.Warning => "warning",
            _ => "message",
        };
        return $"{diagnostic.Id} ({severity}): {diagnostic.Message}";
    }
}
