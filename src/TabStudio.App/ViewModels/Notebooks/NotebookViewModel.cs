using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Services;
using TabStudio.Notebooks;
using TabStudio.Notebooks.Formats;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>
/// An open notebook, like a Polyglot Notebook in Visual Studio Code: a column of code and Markdown cells, a
/// kernel that runs the code with state carried from cell to cell, and the outputs under each cell.
/// </summary>
/// <remarks>
/// This file holds the notebook's state, saving and undo; editing cells is in <c>NotebookViewModel.Editing.cs</c>
/// and running them in <c>NotebookViewModel.Running.cs</c>. Everything here runs on the UI thread.
/// </remarks>
public sealed partial class NotebookViewModel : ObservableObject, IDisposable
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly INotebookHost _host;
    private NotebookDocument _document;
    private int _changes;
    private int _savedChanges;
    private bool _disposed;

    /// <summary>Creates the notebook editor over a notebook read from a file. Call on the UI thread.</summary>
    /// <param name="document">The notebook.</param>
    /// <param name="format">The file format it is saved in.</param>
    /// <param name="host">The IDE around the notebook.</param>
    public NotebookViewModel(NotebookDocument document, NotebookFormat format, INotebookHost host)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(host);
        _document = document;
        _host = host;
        Format = format;
        Cells.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasCells));
        LoadCells(document);
    }

    /// <summary>The cells, in order.</summary>
    public ObservableCollection<NotebookCellViewModel> Cells { get; } = [];

    /// <summary>Whether the notebook has any cells.</summary>
    public bool HasCells => Cells.Count > 0;

    /// <summary>The file format: Jupyter (<c>.ipynb</c>) or Polyglot (<c>.dib</c>).</summary>
    public NotebookFormat Format { get; }

    /// <summary>Whether the file stores outputs, so running cells changes it. <c>.dib</c> files keep only code.</summary>
    public bool KeepsOutputs => Format == NotebookFormat.Jupyter;

    /// <summary>The selected cell, which shows its toolbar and receives keyboard commands.</summary>
    [ObservableProperty]
    public partial NotebookCellViewModel? SelectedCell { get; set; }

    /// <summary>Whether the notebook differs from its file.</summary>
    [ObservableProperty]
    public partial bool IsDirty { get; private set; }

    /// <summary>Whether Undo has something to undo: an edit in the selected cell, or a change to the cells.</summary>
    [ObservableProperty]
    public partial bool CanUndo { get; private set; }

    /// <summary>Whether Redo has something to redo in the selected cell.</summary>
    [ObservableProperty]
    public partial bool CanRedo { get; private set; }

    /// <summary>A short message for the notebook's toolbar, such as how long the last run took.</summary>
    [ObservableProperty]
    public partial string? StatusMessage { get; private set; }

    /// <summary>The caret line in the selected cell, for the status bar. Updated by the cell editor.</summary>
    [ObservableProperty]
    public partial int CaretLine { get; set; } = 1;

    /// <summary>The caret column in the selected cell, for the status bar. Updated by the cell editor.</summary>
    [ObservableProperty]
    public partial int CaretColumn { get; set; } = 1;

    /// <summary>The notebook as it would be saved now.</summary>
    public NotebookDocument ToDocument() => _document with { Cells = Cells.Select(c => c.ToCell()).ToList() };

    /// <summary>The text the file would have if it were saved now, for restoring unsaved work.</summary>
    public string Serialize() => NotebookSerializer.Serialize(ToDocument(), Format);

    /// <summary>Saves the notebook in its format. Cells that were not changed are written exactly as they were read.</summary>
    /// <param name="path">The file.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task SaveAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(path);
        var text = NotebookSerializer.Serialize(ToDocument(), NotebookSerializer.GetFormat(path) ?? Format);

        // What is saved is marked saved now, so edits made while the file is written still count as unsaved.
        _savedChanges = _changes;
        var textChanged = Cells.Where(c => c.IsTextChanged).ToList();
        foreach (var cell in textChanged)
        {
            cell.MarkSaved();
        }

        UpdateDirty();
        try
        {
            await TextFile.WriteAsync(path, text, Utf8WithoutBom, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            // Nothing was saved after all: the notebook is unsaved again.
            _savedChanges = -1;
            UpdateDirty();
            throw;
        }
    }

    /// <summary>Replaces the cells with unsaved work from a restored session.</summary>
    /// <param name="text">The notebook as <see cref="Serialize"/> wrote it.</param>
    public void RestoreUnsavedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        NotebookDocument document;
        try
        {
            document = NotebookSerializer.Parse(text, Format);
        }
        catch (NotebookFormatException)
        {
            // Unsaved work that cannot be read is dropped; the file itself is intact.
            return;
        }

        foreach (var cell in Cells)
        {
            cell.Dispose();
        }

        Cells.Clear();
        ForgetStructureChanges();
        LoadCells(document);
        MarkChanged();
    }

    /// <summary>Undoes the last edit: of the selected cell's text, or the last change to the cells, whichever came last.</summary>
    public void Undo()
    {
        var cellCanUndo = SelectedCell?.Source.UndoStack.CanUndo == true;
        if (_structureUndo.Count > 0 && (_lastChangeWasStructural || !cellCanUndo))
        {
            UndoStructureChange();
        }
        else if (cellCanUndo)
        {
            SelectedCell!.Source.UndoStack.Undo();
        }
    }

    /// <summary>Redoes an undone edit of the selected cell's text.</summary>
    public void Redo()
    {
        if (SelectedCell?.Source.UndoStack.CanRedo == true)
        {
            SelectedCell.Source.UndoStack.Redo();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        StopRunning();
        foreach (var cell in Cells.Concat(_removedCells))
        {
            cell.Dispose();
        }
    }

    /// <summary>Called by a cell when its text changes.</summary>
    /// <param name="cell">The cell.</param>
    internal void OnCellTextChanged(NotebookCellViewModel cell) => _lastChangeWasStructural = false;

    /// <summary>Called by a cell when its undo history changes, which decides whether the notebook is dirty.</summary>
    /// <param name="cell">The cell.</param>
    internal void OnCellUndoStateChanged(NotebookCellViewModel cell)
    {
        UpdateDirty();
        if (ReferenceEquals(cell, SelectedCell))
        {
            UpdateUndoState();
        }
    }

    partial void OnSelectedCellChanged(NotebookCellViewModel? oldValue, NotebookCellViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsSelected = false;

            // Leaving a Markdown cell shows it rendered again, as in Visual Studio Code.
            oldValue.IsEditing = false;
        }

        if (newValue is not null)
        {
            newValue.IsSelected = true;
        }

        UpdateUndoState();
    }

    private void LoadCells(NotebookDocument document)
    {
        _document = document;
        foreach (var cell in document.Cells)
        {
            Cells.Add(new NotebookCellViewModel(this, cell));
        }

        SelectedCell = Cells.Count > 0 ? Cells[0] : null;
    }

    private void MarkChanged()
    {
        _changes++;
        UpdateDirty();
    }

    private void UpdateDirty() => IsDirty = _changes != _savedChanges || Cells.Any(c => c.IsTextChanged);

    private void UpdateUndoState()
    {
        CanUndo = _structureUndo.Count > 0 || SelectedCell?.Source.UndoStack.CanUndo == true;
        CanRedo = SelectedCell?.Source.UndoStack.CanRedo == true;
    }
}
