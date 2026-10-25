using System.ComponentModel;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.Notebooks;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Notebook documents (<c>.ipynb</c> and <c>.dib</c>): the tab, dirty state, saving and session restore of a
/// <see cref="DocumentViewModel"/>, with a <see cref="NotebookViewModel"/> as the editor instead of the text.
/// </summary>
public sealed partial class DocumentViewModel
{
    /// <summary>The notebook editor, when this document is a notebook; otherwise <see langword="null"/>.</summary>
    public NotebookViewModel? Notebook { get; private init; }

    /// <summary>Whether this document is a notebook.</summary>
    public bool IsNotebook => Notebook is not null;

    /// <summary>
    /// Whether the floating editing actions (Comment, Format, Find, Run) apply to this document. A notebook has
    /// its own toolbar instead, and so does a CI job's log, which is read-only.
    /// </summary>
    public bool ShowsEditorActions => Notebook is null && CiLog is null;

    /// <summary>The text the file would have if it were saved now, for restoring unsaved work.</summary>
    public string CurrentText => Notebook?.Serialize() ?? Document.Text;

    /// <summary>Creates the document of an open notebook. Call on the UI thread.</summary>
    /// <param name="filePath">Full path of the notebook file.</param>
    /// <param name="file">The file's text and encoding, as read from disk.</param>
    /// <param name="notebook">The notebook editor.</param>
    /// <param name="profile">The device profile.</param>
    public static DocumentViewModel ForNotebook(string filePath, TextFileContent file, NotebookViewModel notebook, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(notebook);
        var document = new DocumentViewModel(filePath, file, language: null, profile) { Notebook = notebook };
        notebook.PropertyChanged += document.OnNotebookPropertyChanged;
        return document;
    }

    private void OnNotebookPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Notebook is not { } notebook)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(NotebookViewModel.IsDirty):
                IsDirty = notebook.IsDirty;
                break;
            case nameof(NotebookViewModel.CanUndo):
                CanUndo = notebook.CanUndo;
                break;
            case nameof(NotebookViewModel.CanRedo):
                CanRedo = notebook.CanRedo;
                break;
            case nameof(NotebookViewModel.CaretLine):
                CaretLine = notebook.CaretLine;
                break;
            case nameof(NotebookViewModel.CaretColumn):
                CaretColumn = notebook.CaretColumn;
                break;
        }
    }

    /// <summary>Saves a notebook document in its own format.</summary>
    private async Task SaveNotebookAsync(NotebookViewModel notebook, CancellationToken cancellationToken)
    {
        await notebook.SaveAsync(FilePath, cancellationToken);
        IsDirty = notebook.IsDirty;
    }

    /// <summary>Closes the notebook's kernel along with the document.</summary>
    private void DisposeNotebook()
    {
        if (Notebook is { } notebook)
        {
            notebook.PropertyChanged -= OnNotebookPropertyChanged;
            notebook.Dispose();
        }
    }
}
