using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.TextMate;
using TabStudio.App.Editing;
using TabStudio.App.Theming;
using TabStudio.App.ViewModels.Notebooks;
using TabStudio.Notebooks;
using TabStudio.App.Accessibility;

namespace TabStudio.App.Views.Notebooks;

/// <summary>
/// The text editor of one notebook cell: AvaloniaEdit sized to its text, with C# syntax colors and squiggles
/// under compile errors, or TextMate colors for Markdown and other languages.
/// </summary>
/// <remarks>
/// Ctrl+Enter runs the cell, Shift+Enter runs it and moves to the next, Alt+Enter runs it and inserts a cell
/// below, and Escape leaves the editor for the notebook's command mode, as in Visual Studio Code.
/// </remarks>
public sealed class CellEditor : UserControl, IDisposable
{
    /// <summary>The cell whose text is edited.</summary>
    public static readonly StyledProperty<NotebookCellViewModel?> CellProperty =
        AvaloniaProperty.Register<CellEditor, NotebookCellViewModel?>(nameof(Cell));


    private readonly TextEditor _editor;
    private readonly ClassificationColorizer _colorizer = new();
    private readonly DiagnosticRenderer _diagnostics = new();
    private TextMate.Installation? _textMate;
    private NotebookCellViewModel? _connected;
    private bool _isAttached;

    static CellEditor()
    {
        CellProperty.Changed.AddClassHandler<CellEditor>((editor, _) => editor.Reconnect());
    }

    /// <summary>Creates the editor.</summary>
    public CellEditor()
    {
        _editor = new TextEditor
        {
            ShowLineNumbers = false,
            WordWrap = false,
            Background = Brushes.Transparent,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        };
        TypeScale.SetBaseFontSize(_editor, 13.5);
        Avalonia.Automation.AutomationProperties.SetName(_editor, "Cell");
        // A one-line cell is still a whole touch target (docs/design/README.md, "Touch targets").
        _editor.Bind(MinHeightProperty, _editor.GetResourceObservable("Target.Size"));
        _editor.Options.ConvertTabsToSpaces = true;
        _editor.Options.IndentationSize = 4;
        _editor.Options.HighlightCurrentLine = false;
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _editor.Options.AllowScrollBelowDocument = false;
        _editor.TextArea.Caret.CaretBrush = ThemeBrushes.Get("Editor.Caret");
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_diagnostics);

        _editor.AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        _editor.TextArea.GotFocus += OnGotFocus;
        _editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
        _editor.TextArea.TextView.PointerHover += OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped += OnPointerHoverStopped;
        Content = _editor;
    }

    /// <inheritdoc cref="CellProperty"/>
    public NotebookCellViewModel? Cell
    {
        get => GetValue(CellProperty);
        set => SetValue(CellProperty, value);
    }

    /// <summary>The AvaloniaEdit editor, for tests and keyboard focus.</summary>
    public TextEditor Editor => _editor;

    /// <summary>Puts the keyboard in the editor and scrolls the cell into view.</summary>
    public void FocusEditor()
    {
        _editor.TextArea.Focus();
        this.BringIntoView();
    }

    /// <summary>Toggles line comments on the selected lines of a code cell.</summary>
    public void ToggleComment()
    {
        if (_connected is not { IsCode: true } cell)
        {
            return;
        }

        var document = _editor.Document;
        var start = document.GetLineByOffset(_editor.SelectionStart).LineNumber;
        var endOffset = _editor.SelectionStart + _editor.SelectionLength;
        var end = document.GetLineByOffset(endOffset).LineNumber;
        if (end > start && document.GetLineByNumber(end).Offset == endOffset)
        {
            end--;
        }

        CommentToggler.Toggle(document, start, end, FileTypes.For(cell.IsCSharp ? "cell.cs" : "cell" + ExtensionFor(cell.Language)));
        _editor.TextArea.Focus();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Disconnect();
        _editor.TextArea.GotFocus -= OnGotFocus;
        _editor.TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        _editor.TextArea.TextView.PointerHover -= OnPointerHover;
        _editor.TextArea.TextView.PointerHoverStopped -= OnPointerHoverStopped;
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttached = true;
        Reconnect();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _isAttached = false;
        Disconnect();
    }

    private void Reconnect()
    {
        Disconnect();
        if (!_isAttached || Cell is not { } cell)
        {
            return;
        }

        _connected = cell;
        _editor.Document = cell.Source;
        InstallHighlighting(cell);
        cell.Source.Changed += OnTextChanged;
        cell.PropertyChanged += OnCellPropertyChanged;
        cell.ClassificationsChanged += OnClassificationsChanged;
        cell.DiagnosticsChanged += OnDiagnosticsChanged;
        cell.FocusRequested += OnFocusRequested;
        cell.NavigationRequested += OnNavigationRequested;
        cell.SelectionRequested += OnNavigationRequested;
        ApplyClassifications();
        ApplyDiagnostics();

        if (cell.HasPendingFocus || cell.PendingNavigation is not null || cell.PendingSelection is not null)
        {
            Dispatcher.UIThread.Post(ApplyPendingRequests, DispatcherPriority.Background);
        }
    }

    private void Disconnect()
    {
        if (_connected is not { } cell)
        {
            return;
        }

        cell.Source.Changed -= OnTextChanged;
        cell.PropertyChanged -= OnCellPropertyChanged;
        cell.ClassificationsChanged -= OnClassificationsChanged;
        cell.DiagnosticsChanged -= OnDiagnosticsChanged;
        cell.FocusRequested -= OnFocusRequested;
        cell.NavigationRequested -= OnNavigationRequested;
        cell.SelectionRequested -= OnNavigationRequested;
        _textMate?.Dispose();
        _textMate = null;
        _editor.Document = new TextDocument();
        _connected = null;
    }

    private void InstallHighlighting(NotebookCellViewModel cell)
    {
        _textMate?.Dispose();
        _textMate = null;
        if (cell.IsCSharp)
        {
            return;
        }

        var extension = cell.IsMarkdown ? ".md" : ExtensionFor(cell.Language);
        if (extension.Length > 0)
        {
            _textMate = TextMateHighlighting.TryInstall(_editor, extension);
        }
    }

    private static string ExtensionFor(string language) => NotebookLanguages.Normalize(language) switch
    {
        NotebookLanguages.FSharp => ".fs",
        NotebookLanguages.PowerShell => ".ps1",
        NotebookLanguages.JavaScript => ".js",
        NotebookLanguages.Html => ".html",
        NotebookLanguages.Markdown => ".md",
        "sql" => ".sql",
        _ => string.Empty,
    };

    private void OnCellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NotebookCellViewModel.Kind) && _connected is { } cell)
        {
            InstallHighlighting(cell);
            ApplyClassifications();
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void OnTextChanged(object? sender, DocumentChangeEventArgs e)
    {
        _colorizer.OnDocumentChanged(e);
        _diagnostics.OnDocumentChanged(e);
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
    }

    private void ApplyClassifications() =>
        _colorizer.SetClassifications((_connected?.Classifications ?? []).Select(c => (c.Start, c.Length, c.Classification)));

    private void ApplyDiagnostics() =>
        _diagnostics.SetDiagnostics(_connected?.Diagnostics ?? [], _editor.Document.TextLength);

    private void OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ApplyPendingRequests, DispatcherPriority.Background);

    private void OnNavigationRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(ApplyPendingRequests, DispatcherPriority.Background);

    private void ApplyPendingRequests()
    {
        if (_connected is not { } cell || !IsEffectivelyVisible)
        {
            return;
        }

        if (cell.TakePendingSelection() is { } selection)
        {
            // A match of Find: selected and shown, while the keyboard stays in the find bar.
            var length = _editor.Document.TextLength;
            var start = Math.Clamp(selection.Start, 0, length);
            _editor.Select(start, Math.Clamp(selection.Length, 0, length - start));
            var location = _editor.Document.GetLocation(start);
            _editor.ScrollTo(location.Line, location.Column);
            this.BringIntoView();
        }

        if (cell.TakePendingNavigation() is { } target)
        {
            var document = _editor.Document;
            var line = document.GetLineByNumber(Math.Clamp(target.Line, 1, document.LineCount));
            _editor.CaretOffset = line.Offset + Math.Clamp(target.Column - 1, 0, line.Length);
            cell.TakePendingFocus();
            FocusEditor();
        }
        else if (cell.TakePendingFocus())
        {
            FocusEditor();
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        if (_connected is { } cell)
        {
            cell.Notebook.SelectedCell = cell;
            cell.Notebook.CaretLine = _editor.TextArea.Caret.Line;
            cell.Notebook.CaretColumn = _editor.TextArea.Caret.Column;
        }
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (_connected is { IsSelected: true } cell)
        {
            cell.Notebook.CaretLine = _editor.TextArea.Caret.Line;
            cell.Notebook.CaretColumn = _editor.TextArea.Caret.Column;
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (_connected is not { } cell)
        {
            return;
        }

        var notebook = cell.Notebook;
        switch (e.Key, e.KeyModifiers)
        {
            case (Key.Enter, KeyModifiers.Control):
                _ = notebook.RunCellAsync(cell);
                e.Handled = true;
                break;
            case (Key.Enter, KeyModifiers.Shift):
                _ = notebook.RunCellAndSelectNextAsync(cell);
                e.Handled = true;
                break;
            case (Key.Enter, KeyModifiers.Alt):
                _ = notebook.RunCellAndInsertBelowAsync(cell);
                e.Handled = true;
                break;
            case (Key.Escape, KeyModifiers.None):
                // Leave the editor for command mode, where arrow keys move between cells.
                cell.IsEditing = false;
                this.FindAncestorOfType<NotebookView>()?.Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnPointerHover(object? sender, PointerEventArgs e)
    {
        if (_editor.GetPositionFromPoint(e.GetPosition(_editor)) is not { } position)
        {
            return;
        }

        var offset = _editor.Document.GetOffset(position.Location);
        if (_diagnostics.DiagnosticAt(offset) is { } diagnostic)
        {
            ToolTip.SetTip(_editor, $"{diagnostic.Id}: {diagnostic.Message}");
            ToolTip.SetIsOpen(_editor, true);
            e.Handled = true;
        }
    }

    private void OnPointerHoverStopped(object? sender, PointerEventArgs e)
    {
        ToolTip.SetIsOpen(_editor, false);
        ToolTip.SetTip(_editor, null);
    }
}
