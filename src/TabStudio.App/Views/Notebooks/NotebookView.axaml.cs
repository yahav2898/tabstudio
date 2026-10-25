using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit.Editing;
using TabStudio.App.Editing;
using TabStudio.App.ViewModels;
using TabStudio.App.ViewModels.Notebooks;
using TabStudio.Notebooks;

namespace TabStudio.App.Views.Notebooks;

/// <summary>
/// The notebook editor for one open notebook: its toolbar, and the column of cells with their outputs.
/// </summary>
/// <remarks>
/// <para>
/// Like Visual Studio Code, it has an edit mode, with the keyboard in a cell's editor, and a command mode, after
/// Escape or a tap outside the editors, where keys act on cells: Up and Down select, Enter edits, A and B insert
/// a code cell above or below, M and Y turn a cell into Markdown or code, D twice deletes it and Ctrl+Z undoes.
/// Ctrl+Enter, Shift+Enter and Alt+Enter run cells in both modes. Every action also has a button, for touch.
/// </para>
/// <para>
/// The IDE's Undo, Redo and Toggle Comment act on the selected cell, and Find (Ctrl+F) searches every cell.
/// </para>
/// </remarks>
public sealed partial class NotebookView : UserControl, IDisposable
{
    /// <summary>Below this width the toolbar is compact: its secondary buttons show only their icons.</summary>
    private const double CompactWidth = 900;

    private readonly DocumentViewModel? _document;
    private NotebookViewModel? _notebook;
    private bool _deletePending;

    /// <summary>Creates the view. Set its data context to a <see cref="NotebookViewModel"/>.</summary>
    public NotebookView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
        Scroller.AddHandler(PointerPressedEvent, OnBackgroundPressed, RoutingStrategies.Bubble);
        FindBox.KeyDown += OnFindKeyDown;
    }

    /// <summary>Creates the view of a notebook document.</summary>
    /// <param name="document">The document, whose <see cref="DocumentViewModel.Notebook"/> is shown.</param>
    public NotebookView(DocumentViewModel document)
        : this()
    {
        ArgumentNullException.ThrowIfNull(document);
        _document = document;
        DataContext = document.Notebook;
        document.ActionRequested += OnActionRequested;
    }

    private NotebookViewModel? Notebook => DataContext as NotebookViewModel;

    /// <summary>The editor of a cell, when the cell is shown.</summary>
    /// <param name="cell">The cell.</param>
    public CellEditor? EditorFor(NotebookCellViewModel cell) =>
        this.GetVisualDescendants().OfType<CellEditor>().FirstOrDefault(e => ReferenceEquals(e.Cell, cell));

    /// <inheritdoc />
    public void Dispose()
    {
        if (_document is not null)
        {
            _document.ActionRequested -= OnActionRequested;
        }

        if (_notebook is not null)
        {
            _notebook.FindRequested -= OnFindRequested;
            _notebook = null;
        }

        foreach (var editor in this.GetVisualDescendants().OfType<CellEditor>().ToList())
        {
            editor.Dispose();
        }
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_notebook is not null)
        {
            _notebook.FindRequested -= OnFindRequested;
        }

        _notebook = DataContext as NotebookViewModel;
        if (_notebook is not null)
        {
            _notebook.FindRequested += OnFindRequested;
        }
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        Toolbar.Classes.Set("compact", e.NewSize.Width < CompactWidth);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsVisibleProperty && IsVisible)
        {
            // Shown again (its tab was selected): the keyboard goes to the notebook, unless a cell asked for it.
            Dispatcher.UIThread.Post(() =>
            {
                if (Notebook?.Cells.Any(c => c.HasPendingFocus) != true && !IsKeyboardFocusWithin)
                {
                    Focus();
                }
            }, DispatcherPriority.Background);
        }
    }

    private void OnBackgroundPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<NotebookCellView>(includeSelf: true) is null
            && source.FindAncestorOfType<Button>(includeSelf: true) is null)
        {
            Focus();
        }
    }

    private void OnActionRequested(object? sender, EditorAction action)
    {
        if (Notebook is not { } notebook)
        {
            return;
        }

        var selected = notebook.SelectedCell;
        switch (action)
        {
            case EditorAction.Undo:
                notebook.Undo();
                break;
            case EditorAction.Redo:
                notebook.Redo();
                break;
            case EditorAction.ToggleComment when selected is not null:
                EditorFor(selected)?.ToggleComment();
                break;
            case EditorAction.SelectAll when selected is not null:
                EditorFor(selected)?.Editor.SelectAll();
                break;
            case EditorAction.Focus when selected is { ShowsEditor: true }:
                selected.RequestFocus();
                break;
            case EditorAction.Focus:
                Focus();
                break;
            case EditorAction.Find or EditorAction.Replace:
                notebook.OpenFind();
                break;
        }
    }

    private void OnFindRequested(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        FindBox.Focus();
        FindBox.SelectAll();
    }, DispatcherPriority.Background);

    private void OnFindKeyDown(object? sender, KeyEventArgs e)
    {
        if (Notebook is not { } notebook)
        {
            return;
        }

        switch (e.Key, e.KeyModifiers)
        {
            case (Key.Enter, KeyModifiers.None):
                notebook.FindNextCommand.Execute(null);
                e.Handled = true;
                break;
            case (Key.Enter, KeyModifiers.Shift):
                notebook.FindPreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case (Key.Escape, KeyModifiers.None):
                notebook.CloseFindCommand.Execute(null);
                Focus();
                e.Handled = true;
                break;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || Notebook is not { } notebook)
        {
            return;
        }

        if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
        {
            notebook.OpenFind();
            e.Handled = true;
            return;
        }

        if (IsTyping(e.Source))
        {
            return;
        }

        var cell = notebook.SelectedCell;
        var wasDeletePending = _deletePending;
        _deletePending = false;
        e.Handled = true;
        switch (e.Key, e.KeyModifiers)
        {
            case (Key.Up, KeyModifiers.None) or (Key.K, KeyModifiers.None):
                notebook.SelectAdjacentCell(-1);
                BringSelectedIntoView(notebook);
                break;
            case (Key.Down, KeyModifiers.None) or (Key.J, KeyModifiers.None):
                notebook.SelectAdjacentCell(1);
                BringSelectedIntoView(notebook);
                break;
            case (Key.Z, KeyModifiers.Control):
                notebook.Undo();
                break;
            case (Key.Enter, KeyModifiers.None) when cell is not null:
                notebook.EditCell(cell);
                break;
            case (Key.Enter, KeyModifiers.Control) when cell is not null:
                _ = notebook.RunCellAsync(cell);
                break;
            case (Key.Enter, KeyModifiers.Shift) when cell is not null:
                _ = notebook.RunCellAndSelectNextAsync(cell);
                break;
            case (Key.Enter, KeyModifiers.Alt) when cell is not null:
                _ = notebook.RunCellAndInsertBelowAsync(cell);
                break;
            case (Key.A, KeyModifiers.None) when cell is not null:
                notebook.InsertCellAbove(cell, NotebookCellKind.Code);
                break;
            case (Key.B, KeyModifiers.None) when cell is not null:
                notebook.InsertCellBelow(cell, NotebookCellKind.Code);
                break;
            case (Key.M, KeyModifiers.None) when cell is not null:
                notebook.ChangeCellKind(cell, NotebookCellKind.Markdown);
                break;
            case (Key.Y, KeyModifiers.None) when cell is not null:
                notebook.ChangeCellKind(cell, NotebookCellKind.Code);
                break;
            case (Key.D, KeyModifiers.None) when cell is not null:
                if (wasDeletePending)
                {
                    notebook.DeleteCell(cell);
                }
                else
                {
                    _deletePending = true;
                }

                break;
            default:
                e.Handled = false;
                break;
        }
    }

    private void BringSelectedIntoView(NotebookViewModel notebook)
    {
        var view = this.GetVisualDescendants().OfType<NotebookCellView>()
            .FirstOrDefault(v => ReferenceEquals(v.DataContext, notebook.SelectedCell));
        view?.BringIntoView();
    }

    /// <summary>Whether a key went to something the user types into, where it must not act on cells.</summary>
    private static bool IsTyping(object? source) =>
        source is Visual visual
        && (visual.FindAncestorOfType<TextArea>(includeSelf: true) is not null
            || visual.FindAncestorOfType<TextBox>(includeSelf: true) is not null);
}
