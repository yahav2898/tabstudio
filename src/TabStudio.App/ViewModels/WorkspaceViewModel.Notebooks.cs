using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Notebooks;
using TabStudio.App.Services;
using TabStudio.App.ViewModels.Notebooks;
using TabStudio.Core.Workspaces;
using TabStudio.Notebooks;
using TabStudio.Notebooks.Formats;

namespace TabStudio.App.ViewModels;

/// <summary>
/// Notebooks: <c>.ipynb</c> and <c>.dib</c> files open in the notebook editor, File > New > C# Notebook creates
/// one, and notebook cells and programs started with F5 take turns, because both use the process's console.
/// </summary>
public sealed partial class WorkspaceViewModel : INotebookHost
{
    /// <summary>Whether a notebook is running cells. F5 waits until it is done.</summary>
    public bool IsNotebookRunning => Documents.Any(d => d.Notebook?.IsBusy == true);

    /// <inheritdoc />
    IUiDispatcher INotebookHost.Ui => _ui;

    /// <inheritdoc />
    string? INotebookHost.CannotRunCellsReason => State == IdeState.Running
        ? "Stop the running program first (Shift+F5): it is using the console that cells write to."
        : null;

    /// <inheritdoc />
    INotebookKernel INotebookHost.CreateKernel() => new LocalNotebookKernel(_compiler);

    /// <inheritdoc />
    void INotebookHost.OnNotebookBusyChanged(NotebookViewModel notebook)
    {
        OnPropertyChanged(nameof(IsNotebookRunning));
        RunCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// The notebook a workspace path points at, when the path is a notebook file rather than a solution, project
    /// or folder: such a path opens the notebook's folder with the notebook in it.
    /// </summary>
    /// <param name="path">The path given to <see cref="LoadAsync"/>.</param>
    private static string? NotebookToOpen(string path) =>
        NotebookSerializer.IsNotebook(path) && File.Exists(path) ? Path.GetFullPath(path) : null;

    /// <summary>Opens a file as a document: notebooks in the notebook editor, everything else as text.</summary>
    private async Task<DocumentViewModel> CreateDocumentAsync(string path)
    {
        if (NotebookSerializer.GetFormat(path) is not { } format)
        {
            return await DocumentViewModel.OpenAsync(path, _language, _profile);
        }

        var file = await Task.Run(() => TextFile.ReadAsync(path));
        NotebookDocument notebook;
        try
        {
            notebook = string.IsNullOrWhiteSpace(file.Text)
                ? NotebookFiles.CreateNew()
                : await Task.Run(() => NotebookSerializer.Parse(file.Text, format));
        }
        catch (NotebookFormatException ex)
        {
            // A notebook that cannot be read opens as text, so it can be repaired.
            StatusText = $"{Path.GetFileName(path)} opened as text: {ex.Message}";
            return new DocumentViewModel(path, file, _language, _profile);
        }

        return DocumentViewModel.ForNotebook(path, file, new NotebookViewModel(notebook, format, this), _profile);
    }

    /// <summary>File > New > C# Notebook: creates <c>Untitled.ipynb</c> next to the code being worked on and opens it.</summary>
    [RelayCommand]
    private async Task NewNotebookAsync()
    {
        if (Workspace is not { } workspace)
        {
            return;
        }

        string path;
        try
        {
            path = await NotebookFiles.CreateAsync(NewNotebookFolder(workspace));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Could not create a notebook: {ex.Message}";
            return;
        }

        await ReloadWorkspaceAsync();
        await OpenDocumentAsync(path);
        Explorer.Reveal(path);
        StatusText = $"Created {Path.GetFileName(path)}";
    }

    /// <summary>
    /// Where a new notebook goes: next to what is selected in the Solution Explorer, as Add New Item does, or else
    /// in the startup project, so it shows up in the Solution Explorer.
    /// </summary>
    private string NewNotebookFolder(Workspace workspace)
    {
        if (Explorer.SelectedItem?.Node is { } node && node.Kind != WorkspaceNodeKind.Solution)
        {
            if (DirectoryOf(node) is { } folder)
            {
                return folder;
            }

            if (node.IsOpenableFile && Path.GetDirectoryName(node.FullPath) is { } parent)
            {
                return parent;
            }
        }

        return workspace.Solution is not null && StartupProject is { } project ? project.DirectoryPath : workspace.RootDirectory;
    }
}
