using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.Core.Workspaces;
using TabStudio.Live.Sessions;

namespace TabStudio.App.ViewModels;

/// <summary>Open documents, navigation and editing commands.</summary>
public sealed partial class WorkspaceViewModel
{
    private readonly Dictionary<string, Task<DocumentViewModel?>> _openingDocuments = new(StringComparer.Ordinal);
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private bool _navigatingHistory;
    private CancellationTokenSource? _sessionSave;
    private bool _sessionSavePending;

    /// <summary>The open documents, in tab order.</summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    /// <summary>The selected tab.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDocuments), nameof(CaretText), nameof(Breadcrumb))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand), nameof(FormatDocumentCommand), nameof(ToggleCommentCommand), nameof(FindCommand), nameof(ReplaceCommand), nameof(UndoCommand), nameof(RedoCommand))]
    public partial DocumentViewModel? ActiveDocument { get; set; }

    /// <summary>Whether any document is open.</summary>
    public bool HasDocuments => ActiveDocument is not null;

    /// <summary>"Ln 12, Col 5", for the status bar.</summary>
    public string CaretText => ActiveDocument is { } document ? $"Ln {document.CaretLine}, Col {document.CaretColumn}" : string.Empty;

    /// <summary>Opens a file in a tab, or selects its tab if it is already open.</summary>
    /// <param name="path">Full path of the file.</param>
    /// <returns>The document, or <see langword="null"/> when the file could not be opened.</returns>
    public async Task<DocumentViewModel?> OpenDocumentAsync(string path)
    {
        if (Documents.FirstOrDefault(d => string.Equals(d.FilePath, path, StringComparison.Ordinal)) is { } open)
        {
            ActiveDocument = open;
            return open;
        }

        // A file asked for again while it is still being read, as a toolchain project's entry file is when the
        // workspace opens it by itself and the user opens it too, waits for that first opening. Two openings would
        // give it two tabs and two documents on its language server, which sends the file's diagnostics to only one.
        if (_openingDocuments.TryGetValue(path, out var opening))
        {
            var opened = await opening;
            if (opened is not null && Documents.Contains(opened))
            {
                ActiveDocument = opened;
            }

            return opened;
        }

        var task = OpenNewDocumentAsync(path);
        _openingDocuments[path] = task;
        try
        {
            return await task;
        }
        finally
        {
            _openingDocuments.Remove(path);
        }
    }

    private async Task<DocumentViewModel?> OpenNewDocumentAsync(string path)
    {
        DocumentViewModel document;
        try
        {
            document = await CreateDocumentAsync(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = ex.Message;
            return null;
        }

        Attach(document);
        if (IsRemoteWorkspace)
        {
            document.SaveExternalAsync = token => _remoteWorkspace is { } remote
                ? remote.SaveAsync(document.FilePath, token)
                : Task.FromException(new IOException("Reconnect in Remote Explorer before saving remote files."));
        }
        var index = ActiveDocument is null ? Documents.Count : Documents.IndexOf(ActiveDocument) + 1;
        Documents.Insert(index, document);
        ActiveDocument = document;
        _ = document.InitializeAsync();
        return document;
    }

    /// <summary>Opens a file and moves the caret to a line and column.</summary>
    /// <param name="path">Full path of the file.</param>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    public async Task NavigateToAsync(string path, int line, int column)
    {
        if (await OpenDocumentAsync(path) is { } document)
        {
            document.NavigateTo(line, column);
        }
    }

    /// <summary>Saves every document with unsaved changes.</summary>
    /// <returns><see langword="true"/> when everything was saved.</returns>
    public async Task<bool> SaveAllCoreAsync()
    {
        var success = true;
        foreach (var document in Documents.Where(d => d.IsDirty).ToList())
        {
            success &= await SaveDocumentAsync(document);
        }

        return success;
    }

    /// <summary>Saves the session now, for example when Android sends the app to the background.</summary>
    public void SaveSessionNow()
    {
        // A guest's copy of another device's workspace keeps no session: it is deleted when the session ends.
        if (Workspace is not { } workspace || IsLiveGuest)
        {
            return;
        }

        var documents = Documents
            .Select(d => new SessionDocument(d.FilePath, d.CaretOffset, d.IsDirty ? d.CurrentText : null))
            .ToList();
        var path = workspace.Solution?.FilePath ?? workspace.RootDirectory;
        _sessions.Save(WithShellState(WithDebuggingState(new SessionState(path, documents, ActiveDocument?.FilePath, StartupProject?.FilePath))));
    }

    [RelayCommand]
    private async Task CloseDocumentAsync(DocumentViewModel? document)
    {
        document ??= ActiveDocument;
        if (document is null)
        {
            return;
        }

        // Unsaved changes are kept rather than lost: save them first, as an editor on a tablet has no
        // dialog the user might dismiss by accident.
        if (document.IsDirty && !(IsLiveGuest && document.Live is not null) && !await SaveDocumentAsync(document))
        {
            return;
        }

        var index = Documents.IndexOf(document);
        Documents.Remove(document);
        Detach(document);
        document.Dispose();
        if (ReferenceEquals(ActiveDocument, document) || ActiveDocument is null)
        {
            ActiveDocument = Documents.Count == 0 ? null : Documents[Math.Clamp(index, 0, Documents.Count - 1)];
        }

        ScheduleSessionSave();
    }

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private async Task SaveAsync()
    {
        if (ActiveDocument is { } document)
        {
            await SaveDocumentAsync(document);
        }
    }

    [RelayCommand]
    private async Task SaveAllAsync() => await SaveAllCoreAsync();

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private async Task FormatDocumentAsync()
    {
        if (ActiveDocument is { } document && !await document.FormatAsync())
        {
            StatusText = document.UsesSemanticColoring || document.LanguageServer?.Capabilities.Formatting == true
                ? "Already formatted"
                : document.HasLanguageServer ? $"{document.LanguageServer!.ServerName} cannot format documents" : "Format Document is available for C# files and files with a language server";
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private void ToggleComment() => ActiveDocument?.RequestAction(EditorAction.ToggleComment);

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private void Find() => ActiveDocument?.RequestAction(EditorAction.Find);

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private void Replace() => ActiveDocument?.RequestAction(EditorAction.Replace);

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private void Undo() => ActiveDocument?.RequestAction(EditorAction.Undo);

    [RelayCommand(CanExecute = nameof(HasDocuments))]
    private void Redo() => ActiveDocument?.RequestAction(EditorAction.Redo);

    [RelayCommand]
    private void NextDocument() => CycleDocument(1);

    [RelayCommand]
    private void PreviousDocument() => CycleDocument(-1);

    [RelayCommand]
    private void NavigateBack() => MoveInHistory(-1);

    [RelayCommand]
    private void NavigateForward() => MoveInHistory(1);

    [RelayCommand]
    private void SyncWithActiveDocument()
    {
        if (ActiveDocument is { } document)
        {
            IsSideBarVisible = true;
            Explorer.Reveal(document.FilePath);
        }
    }

    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        ErrorList.CurrentDocumentPath = newValue?.FilePath;
        OnPropertyChanged(nameof(ShowsSideBarProperties));
        UpdateNetFrameworkBanner();
        if (newValue is not null && !_navigatingHistory)
        {
            RecordHistory(newValue.FilePath);
        }

        ScheduleSessionSave();
    }

    private async Task<bool> SaveDocumentAsync(DocumentViewModel document)
    {
        if (document.IsComparison || document.IsCiLog)
        {
            // A comparison or log tab is read-only and has no file of its own.
            return true;
        }

        // A guest's shared document is saved by the host, whose file it is.
        if (LiveSession is LiveGuest guest && document.Live is { } binder)
        {
            return await SaveGuestDocumentAsync(guest, document, binder);
        }

        try
        {
            await document.SaveAsync();
            StatusText = $"Saved {document.FileName}";
            OnDocumentSavedForTests(document);
            if (!await TryHotReloadOnSaveAsync(document)) OnDocumentSavedForLiveApp(document);
            OnDocumentSavedForSwarm(document);
            OnDocumentSavedForXamlDesigner(document);
            OnDocumentSavedForGenerators(document);
            OnDocumentSavedForLiveTesting(document);
            OnDocumentSavedForLanguageServer(document);
            OnDocumentSavedForStaticSite(document);
            OnDocumentSavedForProjectSetup(document);
            OnDocumentSavedForFlutter(document);
            OnDocumentSavedForWindowsBridge(document);
            OnDocumentSavedForLive(document);
            RefreshPicCommands();
            await OnProjectFileSavedAsync(document);
            GitChanges.ScheduleRefresh();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText = $"Could not save {document.FileName}: {ex.Message}";
            return false;
        }
    }

    private void Attach(DocumentViewModel document)
    {
        document.LocalAI = LocalAI;
        document.LocalAINeighbors = () => Documents.Where(d => !ReferenceEquals(d, document) && !d.IsExtensionsPage).Select(d => new TabStudio.Agent.Local.OpenFileContext(d.FilePath, d.Document.Text)).ToList();
        document.DiagnosticsChanged += OnDocumentDiagnosticsChanged;
        document.PropertyChanged += OnDocumentPropertyChanged;
        document.Document.TextChanged += OnDocumentTextChanged;
        AttachDebugging(document);
        AttachXamlDesigner(document);
        AttachWpfXaml(document);
        AttachLanguageServer(document);
        AttachFormsDesigner(document);
        BindLiveDocument(document);
    }

    private void Detach(DocumentViewModel document)
    {
        document.LocalAINeighbors = null;
        document.DiagnosticsChanged -= OnDocumentDiagnosticsChanged;
        document.PropertyChanged -= OnDocumentPropertyChanged;
        document.Document.TextChanged -= OnDocumentTextChanged;
        DetachDebugging(document);
        DetachXamlDesigner(document);
        DetachFormsDesigner(document);
        UnbindLiveDocument(document);
    }

    private void OnDocumentDiagnosticsChanged(object? sender, EventArgs e)
    {
        if (sender is DocumentViewModel document)
        {
            ErrorList.SetFileDiagnostics(document.FilePath, document.Diagnostics);
            ScheduleProjectAnalysis(document);
            RefreshWpfXaml(document);
        }
    }

    private void OnDocumentPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, ActiveDocument))
        {
            if (e.PropertyName == nameof(DocumentViewModel.IsDirty))
            {
                ScheduleSessionSave();
            }

            return;
        }

        switch (e.PropertyName)
        {
            case nameof(DocumentViewModel.CaretLine) or nameof(DocumentViewModel.CaretColumn):
                OnPropertyChanged(nameof(CaretText));
                break;
            case nameof(DocumentViewModel.IsDirty):
                ScheduleSessionSave();
                break;
            case nameof(DocumentViewModel.ShowsEditorActions):
                OnPropertyChanged(nameof(ShowsEditorTouchBar));
                break;
        }
    }

    private async Task RestoreDocumentsAsync(Workspace workspace, SessionState? session)
    {
        if (session is not null)
        {
            foreach (var saved in session.Documents.Where(d => File.Exists(d.Path)))
            {
                if (await OpenDocumentAsync(saved.Path) is { } document)
                {
                    if (saved.UnsavedText is { } text)
                    {
                        document.RestoreUnsavedText(text);
                    }

                    document.CaretOffset = Math.Clamp(saved.CaretOffset, 0, document.Document.TextLength);
                }
            }

            if (session.ActiveDocument is { } active && Documents.FirstOrDefault(d => d.FilePath == active) is { } activeDocument)
            {
                ActiveDocument = activeDocument;
            }

            if (Documents.Count > 0)
            {
                return;
            }
        }

        if (FindFirstDocument(workspace) is { } first)
        {
            await OpenDocumentAsync(first);
        }
    }

    private string? FindFirstDocument(Workspace workspace)
    {
        if (StartupProject is { } project)
        {
            var program = Path.Combine(project.DirectoryPath, "Program.cs");
            if (File.Exists(program))
            {
                return program;
            }

            var sources = ProjectSourceFiles.Enumerate(project, workspace.Projects.Select(p => p.DirectoryPath).ToList());
            if (sources.Count > 0)
            {
                return sources[0];
            }
        }

        var readme = Path.Combine(workspace.RootDirectory, "README.md");
        return File.Exists(readme) ? readme : null;
    }

    private void CycleDocument(int step)
    {
        if (Documents.Count < 2 || ActiveDocument is null)
        {
            return;
        }

        var index = (Documents.IndexOf(ActiveDocument) + step + Documents.Count) % Documents.Count;
        ActiveDocument = Documents[index];
    }

    private void RecordHistory(string path)
    {
        if (_historyIndex >= 0 && _history[_historyIndex] == path)
        {
            return;
        }

        _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        _history.Add(path);
        _historyIndex = _history.Count - 1;
    }

    private void MoveInHistory(int step)
    {
        var target = _historyIndex + step;
        while (target >= 0 && target < _history.Count)
        {
            if (Documents.FirstOrDefault(d => d.FilePath == _history[target]) is { } document)
            {
                _historyIndex = target;
                _navigatingHistory = true;
                ActiveDocument = document;
                _navigatingHistory = false;
                return;
            }

            target += step;
        }
    }

    /// <summary>
    /// Typing keeps the session's copy of unsaved text current, at most every two seconds: it was saved only when a
    /// document first became dirty, so after Android ended the app the restored text was that first moment's.
    /// </summary>
    private void OnDocumentTextChanged(object? sender, EventArgs e) => ScheduleSessionSave(throttle: true);

    private void ScheduleSessionSave(bool throttle = false)
    {
        if (_disposed || (throttle && _sessionSavePending))
        {
            return;
        }

        _sessionSavePending = true;
        _sessionSave?.Cancel();
        _sessionSave?.Dispose();
        _sessionSave = new CancellationTokenSource();
        var token = _sessionSave.Token;
        _ = Task.Delay(TimeSpan.FromSeconds(2), token).ContinueWith(
            _ => _ui.Post(() =>
            {
                if (!token.IsCancellationRequested && !_disposed)
                {
                    _sessionSavePending = false;
                    SaveSessionNow();
                }
            }),
            token,
            TaskContinuationOptions.OnlyOnRanToCompletion,
            TaskScheduler.Default);
    }
}
