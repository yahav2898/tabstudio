using System.ComponentModel;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using TabStudio.App.Notebooks.Markdown;
using TabStudio.Compilation;
using TabStudio.Notebooks;

namespace TabStudio.App.ViewModels.Notebooks;

/// <summary>How a cell's last run went, shown in its gutter.</summary>
public enum CellRunState
{
    /// <summary>Not run in this session, or reset.</summary>
    Idle,

    /// <summary>Waiting for the cells before it to finish.</summary>
    Queued,

    /// <summary>Running now.</summary>
    Running,

    /// <summary>Ran to the end.</summary>
    Succeeded,

    /// <summary>Did not compile, or threw.</summary>
    Failed,

    /// <summary>Stopped by Interrupt or Restart.</summary>
    Interrupted,
}

/// <summary>
/// One cell of an open notebook: its text in an editor document, its outputs and the state of its last run.
/// The cell commands (run, move, delete and so on) act on the notebook that owns it.
/// </summary>
/// <remarks>
/// The text lives in an AvaloniaEdit <see cref="TextDocument"/>, which belongs to the UI thread, like the text of
/// a <see cref="DocumentViewModel"/>. C# cells are colored from syntax as the user types; semantic colors and
/// IntelliSense are not available in cells.
/// </remarks>
public sealed partial class NotebookCellViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan ClassificationDelay = TimeSpan.FromMilliseconds(120);

    private NotebookCell _cell;
    private CancellationTokenSource? _classification;
    private int _version;
    private bool _disposed;

    /// <summary>Creates the cell. Call on the UI thread.</summary>
    /// <param name="notebook">The notebook it belongs to.</param>
    /// <param name="cell">The cell as read from the file or created.</param>
    public NotebookCellViewModel(NotebookViewModel notebook, NotebookCell cell)
    {
        ArgumentNullException.ThrowIfNull(notebook);
        ArgumentNullException.ThrowIfNull(cell);
        Notebook = notebook;
        _cell = cell;
        _outputs = cell.Outputs;
        Kind = cell.Kind;
        ExecutionCount = cell.ExecutionCount;
        MarkdownBlocks = cell.Kind == NotebookCellKind.Markdown ? MarkdownParser.Parse(cell.Source) : [];

        Source = new TextDocument(cell.Source);
        Source.UndoStack.MarkAsOriginalFile();
        Source.Changed += OnSourceChanged;
        Source.UndoStack.PropertyChanged += OnUndoStackChanged;
        Outputs.CollectionChanged += OnOutputsChanged;
        DiagnosticItems.CollectionChanged += OnDiagnosticItemsChanged;

        ShowOutputs(cell.Outputs, hideCompilationErrors: false);
        ScheduleClassification(TimeSpan.Zero);
    }

    /// <summary>The notebook the cell belongs to.</summary>
    public NotebookViewModel Notebook { get; }

    /// <summary>The cell's id in the notebook.</summary>
    public string Id => _cell.Id;

    /// <summary>Whether the cell holds code, Markdown or raw text.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCode), nameof(IsMarkdown), nameof(IsCSharp), nameof(LanguageName), nameof(IsRendered), nameof(ShowsEditor), nameof(ShowsMarkdownPlaceholder))]
    public partial NotebookCellKind Kind { get; private set; }

    /// <summary>Whether this is a code cell.</summary>
    public bool IsCode => Kind == NotebookCellKind.Code;

    /// <summary>Whether this is a Markdown cell.</summary>
    public bool IsMarkdown => Kind == NotebookCellKind.Markdown;

    /// <summary>Whether this is a C# code cell, which TabStudio's kernel runs.</summary>
    public bool IsCSharp => IsCode && NotebookLanguages.IsCSharp(_cell.Language);

    /// <summary>The cell's language as a kernel name, such as <c>csharp</c>.</summary>
    public string Language => _cell.Language;

    /// <summary>The language shown on the cell, such as "C#" or "Markdown".</summary>
    public string LanguageName => Kind switch
    {
        NotebookCellKind.Code => NotebookLanguages.GetDisplayName(_cell.Language),
        NotebookCellKind.Markdown => "Markdown",
        _ => "Raw",
    };

    /// <summary>The cell's text.</summary>
    public TextDocument Source { get; }

    /// <summary>The syntax colors of a C# cell.</summary>
    public IReadOnlyList<ClassifiedRange> Classifications { get; private set; } = [];

    /// <summary>Whether the cell is the selected one, which shows its toolbar.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Whether a Markdown cell shows its text for editing instead of rendered.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRendered), nameof(ShowsEditor), nameof(ShowsMarkdownPlaceholder))]
    public partial bool IsEditing { get; set; }

    /// <summary>Whether the cell shows rendered Markdown.</summary>
    public bool IsRendered => IsMarkdown && !IsEditing;

    /// <summary>Whether the cell shows its text editor.</summary>
    public bool ShowsEditor => !IsRendered;

    /// <summary>The rendered Markdown of a Markdown cell, updated when editing ends.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyMarkdown), nameof(ShowsMarkdownPlaceholder))]
    public partial IReadOnlyList<MarkdownBlock> MarkdownBlocks { get; private set; }

    /// <summary>Whether a Markdown cell has nothing to show yet.</summary>
    public bool IsEmptyMarkdown => MarkdownBlocks.Count == 0;

    /// <summary>Whether the text changed since the notebook was opened or saved.</summary>
    public bool IsTextChanged => !Source.UndoStack.IsOriginalFile;

    /// <summary>Whether the editor should take keyboard focus as soon as it is shown.</summary>
    public bool HasPendingFocus { get; private set; }

    /// <summary>A location the editor should move the caret to.</summary>
    public (int Line, int Column)? PendingNavigation { get; private set; }

    /// <summary>A span of the text the editor should select, such as a match of Find.</summary>
    public (int Start, int Length)? PendingSelection { get; private set; }

    /// <summary>Raised on the UI thread when <see cref="Classifications"/> changes.</summary>
    public event EventHandler? ClassificationsChanged;

    /// <summary>Raised when the editor should take keyboard focus.</summary>
    public event EventHandler? FocusRequested;

    /// <summary>Raised when <see cref="PendingNavigation"/> is set.</summary>
    public event EventHandler? NavigationRequested;

    /// <summary>Raised when <see cref="PendingSelection"/> is set.</summary>
    public event EventHandler? SelectionRequested;

    /// <summary>The cell as it would be saved: its current text, last outputs and metadata.</summary>
    public NotebookCell ToCell() => _cell with
    {
        Kind = Kind,
        Source = Source.Text,
        Outputs = _outputs,
        ExecutionCount = ExecutionCount,
    };

    /// <summary>Asks the editor to take keyboard focus, now or as soon as it is shown.</summary>
    public void RequestFocus()
    {
        HasPendingFocus = true;
        FocusRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Takes the pending focus request, so it happens once.</summary>
    public bool TakePendingFocus()
    {
        var pending = HasPendingFocus;
        HasPendingFocus = false;
        return pending;
    }

    /// <summary>Moves the caret to a location in the cell, such as a compile error.</summary>
    /// <param name="line">1-based line.</param>
    /// <param name="column">1-based column.</param>
    public void NavigateTo(int line, int column)
    {
        Notebook.SelectedCell = this;
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

    /// <summary>Selects a span of the text and scrolls to it, leaving the keyboard where it is.</summary>
    /// <param name="start">Where the span starts.</param>
    /// <param name="length">How long it is.</param>
    public void Highlight(int start, int length)
    {
        PendingSelection = (start, length);
        SelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Takes the pending selection, so it happens once.</summary>
    public (int Start, int Length)? TakePendingSelection()
    {
        var pending = PendingSelection;
        PendingSelection = null;
        return pending;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _classification?.Cancel();
        _classification?.Dispose();
        Source.Changed -= OnSourceChanged;
        Source.UndoStack.PropertyChanged -= OnUndoStackChanged;
    }

    /// <summary>Marks the text as saved.</summary>
    internal void MarkSaved() => Source.UndoStack.MarkAsOriginalFile();

    /// <summary>Turns the cell into a code or Markdown cell, keeping its text. Outputs are removed.</summary>
    /// <param name="kind">The new kind.</param>
    internal void ChangeKind(NotebookCellKind kind)
    {
        if (kind == Kind)
        {
            return;
        }

        var language = kind switch
        {
            NotebookCellKind.Code => NotebookLanguages.CSharp,
            NotebookCellKind.Markdown => NotebookLanguages.Markdown,
            _ => NotebookLanguages.Raw,
        };
        _cell = _cell with { Kind = kind, Language = language };
        ClearOutputs();
        IsEditing = false;
        Kind = kind;
        MarkdownBlocks = kind == NotebookCellKind.Markdown ? MarkdownParser.Parse(Source.Text) : [];
        OnPropertyChanged(nameof(Language));
        ScheduleClassification(TimeSpan.Zero);
    }

    partial void OnIsEditingChanged(bool value)
    {
        if (!value && IsMarkdown)
        {
            MarkdownBlocks = MarkdownParser.Parse(Source.Text);
        }
    }

    private void OnSourceChanged(object? sender, DocumentChangeEventArgs e)
    {
        _version++;
        ScheduleClassification(ClassificationDelay);
        Notebook.OnCellTextChanged(this);
    }

    private void OnUndoStackChanged(object? sender, PropertyChangedEventArgs e) => Notebook.OnCellUndoStateChanged(this);

    private void ScheduleClassification(TimeSpan delay)
    {
        _classification?.Cancel();
        _classification?.Dispose();
        _classification = null;
        if (!IsCSharp)
        {
            if (Classifications.Count > 0)
            {
                Classifications = [];
                ClassificationsChanged?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        _classification = new CancellationTokenSource();
        _ = ClassifyAsync(Source.Text, _version, delay, _classification.Token);
    }

    private async Task ClassifyAsync(string text, int version, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken);
            }

            var ranges = await Task.Run(() => SyntaxClassifier.Classify(text, cancellationToken), cancellationToken);
            if (!_disposed && version == _version && !cancellationToken.IsCancellationRequested)
            {
                Classifications = ranges;
                ClassificationsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (OperationCanceledException)
        {
            // The user typed again; a newer classification is on its way.
        }
    }
}
