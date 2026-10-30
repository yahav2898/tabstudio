using System.ComponentModel;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Editing;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.Compilation;
using TabStudio.Core.Diagnostics;

namespace TabStudio.App.ViewModels;

/// <summary>
/// An open document: its text, whether it has unsaved changes, and, for C# files, its live classification
/// and diagnostics from the language service.
/// </summary>
/// <remarks>
/// The text lives in an AvaloniaEdit <see cref="TextDocument"/>, which belongs to the UI thread: create,
/// read and edit documents there. Analysis runs a short moment after the user stops typing; results that
/// arrive after further edits are discarded, and the next analysis replaces them.
/// </remarks>
public sealed partial class DocumentViewModel : ObservableObject, IDisposable
{
    private readonly ILanguageService? _language;
    private readonly DeviceProfile _profile;
    private readonly TextFileContent _file;
    private CancellationTokenSource? _analysis;
    private Task? _analysisTask;
    private int _analysisEdits = -1;
    private bool _hasSemanticClassification;
    private int _edits;
    private int _syncedEdits = -1;
    private bool _disposed;

    /// <summary>Creates a document over text that was read from disk. Call on the UI thread.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="file">The file's text and encoding.</param>
    /// <param name="language">The language service, when the file is C# in a loaded project.</param>
    /// <param name="profile">The device profile, which sets how soon analysis runs after typing.</param>
    public DocumentViewModel(string filePath, TextFileContent file, ILanguageService? language, DeviceProfile profile)
    {
        ArgumentNullException.ThrowIfNull(filePath);
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(profile);

        FilePath = filePath;
        FileType = FileTypes.For(filePath);
        _file = file;
        _profile = profile;
        _language = (FileType.IsRoslyn || FileType.IsRazor) && language?.ContainsDocument(filePath) == true ? language : null;

        Document = new TextDocument(file.Text) { FileName = filePath };
        Document.UndoStack.MarkAsOriginalFile();
        Document.Changed += OnDocumentChanged;
        Document.UndoStack.PropertyChanged += OnUndoStackChanged;
        RecordDiskStamp();
    }

    /// <summary>Full path of the file.</summary>
    public string FilePath { get; }

    /// <summary>The file name, shown on the tab.</summary>
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>What kind of file this is.</summary>
    public FileType FileType { get; private set; }

    /// <summary>Whether Roslyn analyzes this document.</summary>
    public bool HasLanguageService => _language is not null;

    /// <summary>
    /// Whether Roslyn colors this document. A Razor file's diagnostics come from Roslyn, and its colors from its
    /// TextMate grammar.
    /// </summary>
    public bool UsesSemanticColoring => _language is not null && FileType.IsRoslyn;

    /// <summary>The text.</summary>
    public TextDocument Document { get; }

    /// <summary>The encoding, for the status bar.</summary>
    public string EncodingLabel => _file.EncodingLabel;

    /// <summary>The line endings, for the status bar.</summary>
    public string NewlineLabel => _file.NewlineLabel;

    /// <summary>The tab title: the file name, with an asterisk when there are unsaved changes.</summary>
    public string Title => IsDirty ? FileName + "*" : FileName;

    /// <summary>Whether the text differs from the file on disk.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsDirty { get; private set; }

    /// <summary>Whether there is something to undo.</summary>
    [ObservableProperty]
    public partial bool CanUndo { get; private set; }

    /// <summary>Whether there is something to redo.</summary>
    [ObservableProperty]
    public partial bool CanRedo { get; private set; }

    /// <summary>The caret line, 1-based. Updated by the editor view.</summary>
    [ObservableProperty]
    public partial int CaretLine { get; set; } = 1;

    /// <summary>The caret column, 1-based. Updated by the editor view.</summary>
    [ObservableProperty]
    public partial int CaretColumn { get; set; } = 1;

    /// <summary>The caret offset. Updated by the editor view and saved with the session.</summary>
    public int CaretOffset { get; set; }

    /// <summary>The diagnostic under the caret, for the status bar. Updated by the editor view.</summary>
    [ObservableProperty]
    public partial string? CaretMessage { get; set; }

    /// <summary>The latest classification of the whole text.</summary>
    public IReadOnlyList<ClassifiedRange> Classifications { get; private set; } = [];

    /// <summary>The latest diagnostics of this file.</summary>
    public IReadOnlyList<BuildDiagnostic> Diagnostics { get; private set; } = [];

    /// <summary>A location the editor should move to once it is shown.</summary>
    public (int Line, int Column)? PendingNavigation { get; private set; }

    /// <summary>Raised on the UI thread when <see cref="Classifications"/> changes.</summary>
    public event EventHandler? ClassificationsChanged;

    /// <summary>Raised on the UI thread when <see cref="Diagnostics"/> changes.</summary>
    public event EventHandler? DiagnosticsChanged;

    /// <summary>Raised when the view should perform an editor action.</summary>
    public event EventHandler<EditorAction>? ActionRequested;

    /// <summary>Raised when <see cref="PendingNavigation"/> is set.</summary>
    public event EventHandler? NavigationRequested;

    /// <summary>Opens a file from disk. The text is read off the UI thread; the document is created on it.</summary>
    /// <param name="filePath">Full path of the file.</param>
    /// <param name="language">The workspace's language service, if any.</param>
    /// <param name="profile">The device profile.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <exception cref="IOException">The file cannot be opened as text.</exception>
    public static async Task<DocumentViewModel> OpenAsync(string filePath, ILanguageService? language, DeviceProfile profile, CancellationToken cancellationToken = default)
    {
        var file = await Task.Run(() => TextFile.ReadAsync(filePath, cancellationToken), cancellationToken);
        return new DocumentViewModel(filePath, file, language, profile);
    }

    /// <summary>Asks the editor to perform an action, such as opening the find bar.</summary>
    /// <param name="action">The action.</param>
    public void RequestAction(EditorAction action) => ActionRequested?.Invoke(this, action);

    /// <summary>Asks the editor to move the caret to a location and scroll it into view.</summary>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    public void NavigateTo(int line, int column)
    {
        PendingNavigation = (Math.Max(1, line), Math.Max(1, column));
        NavigationRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Takes the pending navigation, so it happens once.</summary>
    public (int Line, int Column)? TakePendingNavigation()
    {
        var pending = PendingNavigation;
        PendingNavigation = null;
        return pending;
    }

    /// <summary>Replaces the text with unsaved text from a restored session, keeping it undoable.</summary>
    /// <param name="text">The text the user had before TabStudio was closed.</param>
    public void RestoreUnsavedText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Notebook is { } notebook)
        {
            notebook.RestoreUnsavedText(text);
            return;
        }

        if (!string.Equals(text, Document.Text, StringComparison.Ordinal))
        {
            Document.Text = text;
        }
    }

    /// <summary>Sends the current text to the language service if it changed since the last time.</summary>
    public void SyncText()
    {
        SyncXamlText();
        if (_language is not null && _syncedEdits != _edits)
        {
            _language.UpdateText(FilePath, Document.Text);
            _syncedEdits = _edits;
        }
    }

    /// <summary>Writes the text to disk in the file's original encoding.</summary>
    /// <param name="cancellationToken">Cancels the save.</param>
    public Func<CancellationToken, Task>? SaveExternalAsync { get; set; }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (IsExtensionsPage)
        {
            return;
        }

        if (Notebook is { } notebook)
        {
            await SaveNotebookAsync(notebook, cancellationToken);
            return;
        }

        // The text being written is the saved state from now on: typing while it is written makes the document
        // dirty again. Marked after the write, the late keystrokes were marked saved and never written.
        var text = Document.Text;
        var undo = Document.UndoStack;
        undo.MarkAsOriginalFile();
        try
        {
            await TextFile.WriteAsync(FilePath, text, _file.Encoding, cancellationToken);
            if (SaveExternalAsync is { } saveExternal) { await saveExternal(cancellationToken); }
        }
        catch
        {
            undo.DiscardOriginalFileMarker();
            IsDirty = true;
            throw;
        }

        RecordDiskStamp();
        IsDirty = !undo.IsOriginalFile || Document.Text != text;
    }

    /// <summary>
    /// Colors the document from syntax right away, then runs a full analysis. Called when the document opens.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_language is null)
        {
            await AnalyzeNowAsync();
            return;
        }

        // The semantic analysis starts at once; the syntax pass only fills the gap until it arrives.
        var semantic = AnalyzeNowAsync();
        if (!UsesSemanticColoring)
        {
            await semantic;
            return;
        }

        var text = Document.Text;
        var edits = _edits;
        var path = FilePath;
        var syntax = await Task.Run(() => SyntaxClassifier.ClassifyFile(path, text));
        if (edits == _edits && !_disposed && !_hasSemanticClassification)
        {
            Classifications = syntax;
            ClassificationsChanged?.Invoke(this, EventArgs.Empty);
        }

        await semantic;
    }

    /// <summary>
    /// Runs classification and diagnostics now, without waiting for the user to stop typing. If an analysis
    /// of the current text is already running, returns that one.
    /// </summary>
    public Task AnalyzeNowAsync()
    {
        if (_analysisTask is { IsCompleted: false } running && _analysisEdits == _edits)
        {
            return running;
        }

        var token = RestartAnalysis();
        _analysisEdits = _edits;
        _analysisTask = AnalyzeAsync(TimeSpan.Zero, token);
        return _analysisTask;
    }

    /// <summary>Applies Format Document as one undo step.</summary>
    /// <param name="cancellationToken">Cancels formatting.</param>
    /// <returns><see langword="true"/> when the document changed.</returns>
    public async Task<bool> FormatAsync(CancellationToken cancellationToken = default)
    {
        if (_language is null)
        {
            return await FormatWithServerAsync(cancellationToken);
        }

        SyncText();
        var edits = _edits;
        var changes = await _language.GetFormattingEditsAsync(FilePath, cancellationToken);
        if (changes.Count == 0 || edits != _edits)
        {
            return false;
        }

        Document.BeginUpdate();
        try
        {
            foreach (var change in changes.OrderByDescending(c => c.Start))
            {
                Document.Replace(change.Start, change.Length, change.NewText);
            }
        }
        finally
        {
            Document.EndUpdate();
        }

        return true;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _analysis?.Cancel();
        _analysis?.Dispose();
        Document.Changed -= OnDocumentChanged;
        Document.UndoStack.PropertyChanged -= OnUndoStackChanged;
        DisposeNotebook();
        DatabaseQuery?.Dispose();
        DisposeCiLog();
        DisposePullRequestReview();
        DisposeLanguageServer();
        _xamlHost = null;
        _xamlEngine = null;
        _xamlFeatures = null;
        _synchronizeXamlWorkspace = null;
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        _edits++;
        if (_language is not null || HasXamlFeatures)
        {
            var token = RestartAnalysis();
            _analysisEdits = _edits;
            _analysisTask = AnalyzeAsync(_profile.AnalysisDelay, token);
        }
    }

    private void OnUndoStackChanged(object? sender, PropertyChangedEventArgs e)
    {
        // A shared document's undo is this participant's own, in its binder; the editor's stack is off.
        if (Live is not null)
        {
            return;
        }

        IsDirty = !Document.UndoStack.IsOriginalFile;
        CanUndo = Document.UndoStack.CanUndo;
        CanRedo = Document.UndoStack.CanRedo;
    }

    private CancellationToken RestartAnalysis()
    {
        _analysis?.Cancel();
        _analysis?.Dispose();
        _analysis = new CancellationTokenSource();
        return _analysis.Token;
    }

    private async Task AnalyzeAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (_language is null)
        {
            await AnalyzeXamlAsync(delay, cancellationToken);
            return;
        }

        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            SyncText();
            var edits = _edits;
            var classifications = UsesSemanticColoring ? _language.GetClassificationsAsync(FilePath, cancellationToken) : null;
            var diagnostics = _language.GetDocumentDiagnosticsAsync(FilePath, cancellationToken);

            var classified = classifications is null ? null : await classifications;
            if (classified is not null && IsCurrent(edits, cancellationToken))
            {
                _hasSemanticClassification = true;
                Classifications = classified;
                ClassificationsChanged?.Invoke(this, EventArgs.Empty);
            }

            var found = await diagnostics;
            if (IsCurrent(edits, cancellationToken))
            {
                Diagnostics = found;
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // The user typed again; a newer analysis is on its way.
        }
    }

    private bool IsCurrent(int edits, CancellationToken cancellationToken) =>
        !_disposed && !cancellationToken.IsCancellationRequested && edits == _edits;
}
