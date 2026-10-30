using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Folding;
using AvaloniaEdit.Search;
using TabStudio.App.Editing;
using TabStudio.App.Services.Ci;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Actions;
using TabStudio.App.Accessibility;

namespace TabStudio.App.Views.Actions;

/// <summary>
/// A job's log in a read-only editor: steps and <c>##[group]</c>s fold, errors are red and warnings amber, and
/// Find (Ctrl+F), Go to Line (Ctrl+G), selection and copying work as in any file. F8 and Shift+F8 go to the next
/// and previous error, as in Visual Studio; a failed job's log opens at its first error. Tapping a step's or
/// group's heading folds or unfolds it.
/// </summary>
public sealed class CiLogView : UserControl, IDisposable
{

    private readonly DocumentViewModel _document;
    private readonly CiLogViewModel _log;
    private readonly TextEditor _editor;
    private readonly FoldingManager _folding;
    private readonly SearchPanel _search;
    private readonly CiLogHeaderView _header;
    private bool _positioned;

    /// <summary>Creates the view of a log tab.</summary>
    /// <param name="document">A document made by <see cref="DocumentViewModel.ForCiLog"/>.</param>
    public CiLogView(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        _log = document.CiLog ?? throw new ArgumentException("The document is not a job log.", nameof(document));
        _document = document;
        DataContext = document;

        _editor = new TextEditor
        {
            Document = document.Document,
            IsReadOnly = true,
            ShowLineNumbers = true,
            WordWrap = false,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            LineNumbersMargin = new Thickness(14, 0, 8, 0),
            Padding = new Thickness(0, 4, 0, 0),
        };
        TypeScale.SetBaseFontSize(_editor, 13);
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.HighlightCurrentLine = true;
        _editor.Options.AllowScrollBelowDocument = true;
        _editor.TextArea.TextView.CurrentLineBackground = ThemeBrushes.Get("Brush.CurrentLine");
        _editor.TextArea.TextView.CurrentLineBorder = null;
        _editor.SetValue(FoldingMargin.FoldingMarkerBrushProperty, ThemeBrushes.Get("Editor.FoldMarker"));
        _editor.SetValue(FoldingMargin.FoldingMarkerBackgroundBrushProperty, ThemeBrushes.Get("Editor.FoldMarkerBackground"));
        _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBrushProperty, ThemeBrushes.Get("Editor.FoldMarkerSelected"));
        _editor.SetValue(FoldingMargin.SelectedFoldingMarkerBackgroundBrushProperty, ThemeBrushes.Get("Editor.FoldMarkerBackground"));

        var brushes = new CiLogBrushes();
        _folding = FoldingManager.Install(_editor.TextArea);
        _editor.TextArea.TextView.BackgroundRenderers.Add(new CiLogBackgroundRenderer(() => _log.Log, brushes));
        _editor.TextArea.TextView.LineTransformers.Add(new CiLogColorizer(() => _log.Log, brushes));
        _search = SearchPanel.Install(_editor);

        _header = new CiLogHeaderView { DataContext = _log };
        DockPanel.SetDock(_header, Dock.Top);
        Content = new DockPanel { Children = { _header, _editor } };

        _header.ErrorMoveRequested += OnErrorMoveRequested;
        _header.FoldRequested += OnFoldRequested;
        _header.GoToLineRequested += OnGoToLineRequested;
        _header.GoToLineBarRequested += OnGoToLineBarRequested;
        _header.GoToLineClosed += OnGoToLineClosed;
        _editor.TextArea.AddHandler(TappedEvent, OnTapped, RoutingStrategies.Bubble, handledEventsToo: true);
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        document.CiLogTextChanged += OnLogTextChanged;
        document.ActionRequested += OnActionRequested;
        _log.StepRequested += OnStepRequested;
        if (_log.Log is not null)
        {
            ApplyFolds();
        }
    }

    /// <summary>The editor control, for tests and keyboard focus.</summary>
    public TextEditor Editor => _editor;

    /// <summary>The log's foldings, for tests.</summary>
    public FoldingManager Folding => _folding;

    /// <inheritdoc />
    public void Dispose()
    {
        _header.ErrorMoveRequested -= OnErrorMoveRequested;
        _header.FoldRequested -= OnFoldRequested;
        _header.GoToLineRequested -= OnGoToLineRequested;
        _header.GoToLineBarRequested -= OnGoToLineBarRequested;
        _header.GoToLineClosed -= OnGoToLineClosed;
        _editor.TextArea.RemoveHandler(TappedEvent, OnTapped);
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        RemoveHandler(KeyDownEvent, OnKeyDown);
        _document.CiLogTextChanged -= OnLogTextChanged;
        _document.ActionRequested -= OnActionRequested;
        _log.StepRequested -= OnStepRequested;
        _search.Uninstall();
        FoldingManager.Uninstall(_folding);
    }

    /// <summary>Moves the caret to the next or previous error line, unfolding what hides it, and scrolls it into view.</summary>
    /// <param name="direction">1 for the next error, -1 for the previous one; the search wraps around.</param>
    /// <returns><see langword="true"/> when the log has an error to move to.</returns>
    public bool MoveToError(int direction)
    {
        if (_log.Log is not { ErrorLines.Count: > 0 } log)
        {
            return false;
        }

        var current = _editor.TextArea.Caret.Line;
        var lines = log.ErrorLines;
        int target;
        if (direction >= 0)
        {
            target = lines.FirstOrDefault(l => l > current);
            target = target == 0 ? lines[0] : target;
        }
        else
        {
            target = lines.LastOrDefault(l => l < current);
            target = target == 0 ? lines[^1] : target;
        }

        GoToLine(target);
        return true;
    }

    /// <summary>Moves the caret to the start of a line, unfolding what hides it, and scrolls it into view.</summary>
    /// <param name="lineNumber">The 1-based line.</param>
    public void GoToLine(int lineNumber)
    {
        var document = _editor.Document;
        var line = document.GetLineByNumber(Math.Clamp(lineNumber, 1, document.LineCount));
        foreach (var fold in _folding.GetFoldingsContaining(line.Offset).Where(f => f.StartOffset < line.Offset))
        {
            fold.IsFolded = false;
        }

        var prefix = Math.Min(_log.Log?.PrefixLength ?? 0, line.Length);
        _editor.TextArea.Caret.Offset = line.Offset + prefix;
        _editor.TextArea.ClearSelection();
        _editor.ScrollTo(line.LineNumber, 1);
        _editor.TextArea.Caret.BringCaretToView(60);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            // The editor needs its size before it can scroll to the first error.
            Dispatcher.UIThread.Post(PositionOnce, DispatcherPriority.Background);
        }
    }

    /// <summary>Folds the steps and groups as the log says, then goes where the user asked or to the first error.</summary>
    private void ApplyFolds()
    {
        if (_log.Log is not { } log)
        {
            return;
        }

        _folding.Clear();
        var document = _editor.Document;
        foreach (var fold in log.Folds)
        {
            if (fold.EndLine > document.LineCount || fold.StartLine >= fold.EndLine)
            {
                continue;
            }

            var start = document.GetLineByNumber(fold.StartLine).EndOffset;
            var end = document.GetLineByNumber(fold.EndLine).EndOffset;
            if (end > start)
            {
                var section = _folding.CreateFolding(start, end);
                section.Title = "  ···  ";
                section.IsFolded = fold.StartsFolded;
            }
        }

        _positioned = false;
        if (IsVisible)
        {
            Dispatcher.UIThread.Post(PositionOnce, DispatcherPriority.Background);
        }
    }

    /// <summary>The first time a log shows: the step asked for, else a failed job's first error, else the top.</summary>
    private void PositionOnce()
    {
        if (_positioned || _log.Log is not { } log || !IsVisible)
        {
            return;
        }

        _positioned = true;
        if (_log.PendingStep is { } number && log.Steps.FirstOrDefault(s => s.Number == number) is { } step)
        {
            ShowStep(step);
        }
        else if (log.FirstErrorLine is { } error && _log.Source.Job.IsFailed)
        {
            GoToLine(error);
        }

        InputModalityFocus();
    }

    private void ShowStep(CiLogStep step)
    {
        var line = _editor.Document.GetLineByNumber(Math.Clamp(step.Line, 1, _editor.Document.LineCount));
        foreach (var fold in _folding.GetFoldingsAt(line.EndOffset))
        {
            fold.IsFolded = false;
        }

        GoToLine(step.Line);
        _editor.ScrollToLine(step.Line);
    }

    private void InputModalityFocus() => Input.InputModality.FocusUnlessTouch(_editor.TextArea);

    private void OnLogTextChanged(object? sender, EventArgs e) => ApplyFolds();

    private void OnStepRequested(object? sender, int? stepNumber)
    {
        if (_log.Log is not { } log)
        {
            return;
        }

        if (stepNumber is { } number && log.Steps.FirstOrDefault(s => s.Number == number) is { } step)
        {
            ShowStep(step);
        }
        else if (log.FirstErrorLine is { } error)
        {
            GoToLine(error);
        }
    }

    private void OnErrorMoveRequested(object? sender, int direction)
    {
        MoveToError(direction);
        _editor.TextArea.Focus();
    }

    private void OnFoldRequested(object? sender, bool fold)
    {
        foreach (var section in _folding.AllFoldings)
        {
            section.IsFolded = fold;
        }
    }

    private void OnGoToLineBarRequested(object? sender, EventArgs e) =>
        _header.OpenGoToLine(_editor.TextArea.Caret.Line, _editor.Document.LineCount);

    private void OnGoToLineRequested(object? sender, int line) => GoToLine(line);

    private void OnGoToLineClosed(object? sender, EventArgs e) => _editor.TextArea.Focus();

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        var caret = _editor.TextArea.Caret;
        _document.CaretLine = caret.Line;
        _document.CaretColumn = caret.Column;
    }

    /// <summary>F8 and Shift+F8 move between errors, as in Visual Studio; Ctrl+G goes to a line.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F8 && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            MoveToError(e.KeyModifiers == KeyModifiers.Shift ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Key.G && e.KeyModifiers == KeyModifiers.Control && _log.Log is not null)
        {
            _header.OpenGoToLine(_editor.TextArea.Caret.Line, _editor.Document.LineCount);
            e.Handled = true;
        }
    }

    /// <summary>A tap on a step's or group's heading folds or unfolds it, which a finger finds easier than the margin.</summary>
    private void OnTapped(object? sender, TappedEventArgs e)
    {
        var textView = _editor.TextArea.TextView;
        if (_log.Log is not { } log || !textView.VisualLinesValid || _editor.TextArea.Selection.Length > 0)
        {
            return;
        }

        var line = textView.GetDocumentLineByVisualTop(e.GetPosition(textView).Y + textView.VerticalOffset);
        if (line is null || log.KindOf(line.LineNumber) is not (CiLogLineKind.Step or CiLogLineKind.Group))
        {
            return;
        }

        foreach (var fold in _folding.GetFoldingsAt(line.EndOffset))
        {
            fold.IsFolded = !fold.IsFolded;
        }
    }

    private void OnActionRequested(object? sender, EditorAction action)
    {
        switch (action)
        {
            case EditorAction.Find or EditorAction.Replace:
                _search.IsReplaceMode = false;
                _search.Open();
                _search.Reactivate();
                break;
            case EditorAction.SelectAll:
                _editor.SelectAll();
                break;
            case EditorAction.Focus:
                _editor.TextArea.Focus();
                break;
        }
    }
}
