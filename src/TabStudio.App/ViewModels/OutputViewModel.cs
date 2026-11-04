using System.Collections.Concurrent;
using System.Text;
using Avalonia.Media;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.Theming;
using TabStudio.Compilation.Running;

namespace TabStudio.App.ViewModels;

/// <summary>What a piece of Output panel text is, which decides its color.</summary>
public enum OutputKind
{
    /// <summary>Standard output of the running program.</summary>
    Program,

    /// <summary>Standard error of the running program, or a crash report.</summary>
    Error,

    /// <summary>A line the user typed into the program.</summary>
    Input,

    /// <summary>A message from TabStudio, such as "Build started".</summary>
    Info,

    /// <summary>A successful outcome.</summary>
    Success,

    /// <summary>A failed outcome.</summary>
    Failure,

    /// <summary>A warning a program or build printed, such as <c>warning: unused variable</c>.</summary>
    Warning,
}

/// <summary>
/// The Output panel: build messages and the running program's console, with an input box for
/// <see cref="Console.ReadLine"/>.
/// </summary>
/// <remarks>
/// The program writes from its own thread. Writes are queued and appended to the document on the UI thread in
/// batches behind input and drawing, at most <see cref="MaxCharactersPerFlush"/> characters at a time and one insertion
/// for each run of text of the same kind, so a program printing in a tight loop cannot freeze the IDE.
/// </remarks>
public sealed partial class OutputViewModel : ObservableObject, IConsoleSink
{
    private static IBrush ErrorBrush => ThemeBrushes.Get("Brush.Output.Error");

    private static IBrush WarningBrush => ThemeBrushes.Get("Brush.Output.Warning");

    private static IBrush InputBrush => ThemeBrushes.Get("Brush.Output.Input");

    private static IBrush InfoBrush => ThemeBrushes.Get("Brush.Output.Info");

    private static IBrush SuccessBrush => ThemeBrushes.Get("Brush.Output.Success");

    /// <summary>The most text one batch appends; the rest waits for the next batch, after a frame.</summary>
    internal const int MaxCharactersPerFlush = 64 * 1024;

    private readonly IUiDispatcher _ui;
    private readonly int _characterLimit;
    private readonly StringBuilder _run = new();
    private readonly List<(string Text, OutputKind Kind)> _written = [];
    private readonly Action _flushQueued;
    private readonly ConcurrentQueue<(string Text, OutputKind Kind)> _pending = new();
    private readonly List<TrackedRange<IBrush>> _segments = [];
    private int _flushScheduled;
    private ConsoleInput? _input;

    /// <summary>Creates the panel. Must be called on the UI thread, which owns the document.</summary>
    /// <param name="ui">The UI dispatcher.</param>
    /// <param name="characterLimit">How much text to keep before trimming the oldest output.</param>
    public OutputViewModel(IUiDispatcher ui, int characterLimit)
    {
        ArgumentNullException.ThrowIfNull(ui);
        _ui = ui;
        _characterLimit = Math.Max(10_000, characterLimit);
        Document.UndoStack.SizeLimit = 0;
        _flushQueued = FlushQueued;
    }

    /// <summary>The panel's text.</summary>
    public TextDocument Document { get; } = new();

    /// <summary>The colored parts of the text, sorted and disjoint.</summary>
    public IReadOnlyList<TrackedRange<IBrush>> Segments => _segments;

    /// <summary>Raised on the UI thread after text was appended, so the view can scroll to the end.</summary>
    public event EventHandler? TextAppended;

    /// <summary>Raised on the UI thread for each piece of text as it is appended, with what it is, as a TabStudio Live host shares it.</summary>
    public event EventHandler<OutputWrittenEventArgs>? Written;

    /// <summary>Raised on the UI thread after the text was cleared.</summary>
    public event EventHandler? Cleared;

    /// <summary>Whether a running program is connected to the input box.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitInputCommand))]
    public partial bool IsAcceptingInput { get; private set; }

    /// <summary>Whether the program is blocked in <see cref="Console.ReadLine"/> right now.</summary>
    [ObservableProperty]
    public partial bool IsWaitingForInput { get; private set; }

    /// <summary>The text in the input box.</summary>
    [ObservableProperty]
    public partial string InputText { get; set; } = string.Empty;

    /// <summary>The full text, for tests and for copying.</summary>
    public string Text
    {
        get
        {
            Flush();
            return Document.Text;
        }
    }

    /// <summary>Appends text. Safe to call from any thread.</summary>
    /// <param name="text">The text, which may contain newlines.</param>
    /// <param name="kind">What the text is.</param>
    public void Write(string text, OutputKind kind = OutputKind.Program)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        _pending.Enqueue((text, kind));
        if (Interlocked.Exchange(ref _flushScheduled, 1) == 0)
        {
            _ui.PostBackground(_flushQueued);
        }
    }

    /// <summary>Appends a line. Safe to call from any thread.</summary>
    /// <param name="text">The line, without a newline.</param>
    /// <param name="kind">What the line is.</param>
    public void WriteLine(string text = "", OutputKind kind = OutputKind.Program) => Write(text + "\n", kind);

    /// <inheritdoc />
    void IConsoleSink.WriteOutput(string text) => Write(text, OutputKind.Program);

    /// <inheritdoc />
    void IConsoleSink.WriteError(string text) => Write(text, OutputKind.Error);

    /// <summary>Connects a new standard input to the input box, for a program about to start.</summary>
    /// <returns>The input to pass to the program.</returns>
    public ConsoleInput BeginInput()
    {
        EndInput();
        var input = new ConsoleInput();
        input.WaitingForInput += OnWaitingForInput;
        _input = input;
        IsAcceptingInput = true;
        return input;
    }

    /// <summary>Disconnects the input box from the program that just ended.</summary>
    public void EndInput()
    {
        if (_input is { } input)
        {
            input.WaitingForInput -= OnWaitingForInput;
            input.Complete();
            _input = null;
        }

        IsAcceptingInput = false;
        IsWaitingForInput = false;
    }

    /// <summary>Whether there is no text yet, or none since <see cref="Clear"/>: the panel's empty state shows.</summary>
    public bool IsEmpty => Document.TextLength == 0 && _pending.IsEmpty;

    /// <summary>Removes all text.</summary>
    [RelayCommand]
    public void Clear()
    {
        Flush();
        Document.Text = string.Empty;
        Document.UndoStack.ClearAll();
        _segments.Clear();
        OnPropertyChanged(nameof(IsEmpty));
        Cleared?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Sends the input box's text to the program, as if typed in a console and followed by Enter.</summary>
    [RelayCommand(CanExecute = nameof(IsAcceptingInput))]
    private void SubmitInput()
    {
        if (_input is not { } input)
        {
            return;
        }

        var line = InputText;
        InputText = string.Empty;
        IsWaitingForInput = false;
        Write(line + "\n", OutputKind.Input);
        input.SubmitLine(line);
    }

    private void OnWaitingForInput(object? sender, EventArgs e) => _ui.Post(() =>
    {
        if (ReferenceEquals(sender, _input))
        {
            IsWaitingForInput = true;
        }
    });

    /// <summary>Appends everything queued, at once, for the text and for copying. Runs on the UI thread.</summary>
    private void Flush()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);
        while (!_pending.IsEmpty)
        {
            Append(int.MaxValue);
        }
    }

    /// <summary>
    /// Appends the next batch of what is queued, and queues the next batch behind input and drawing when more is
    /// waiting: a program that never stops printing gets a batch per frame rather than the whole UI thread.
    /// </summary>
    private void FlushQueued()
    {
        Interlocked.Exchange(ref _flushScheduled, 0);
        Append(MaxCharactersPerFlush);
        if (!_pending.IsEmpty && Interlocked.Exchange(ref _flushScheduled, 1) == 0)
        {
            _ui.PostBackground(_flushQueued);
        }
    }

    /// <summary>Appends queued text, up to about a number of characters, one insertion for each run of one kind.</summary>
    private void Append(int maxCharacters)
    {
        if (_pending.IsEmpty)
        {
            return;
        }

        var wasEmpty = Document.TextLength == 0;
        var characters = 0;
        OutputKind? kind = null;
        Document.BeginUpdate();
        try
        {
            while (characters < maxCharacters && _pending.TryDequeue(out var item))
            {
                if (kind is { } current && current != item.Kind)
                {
                    InsertRun(current);
                }

                kind = item.Kind;
                _run.Append(item.Text);
                _written.Add(item);
                characters += item.Text.Length;
            }

            if (kind is { } last)
            {
                InsertRun(last);
            }

            TrimToLimit();
        }
        finally
        {
            Document.EndUpdate();
        }

        TextAppended?.Invoke(this, EventArgs.Empty);
        if (wasEmpty && Document.TextLength > 0)
        {
            OnPropertyChanged(nameof(IsEmpty));
        }
    }

    /// <summary>Inserts the text gathered for one kind in one go, and tells the listeners of each piece of it.</summary>
    private void InsertRun(OutputKind kind)
    {
        var start = Document.TextLength;
        var text = _run.ToString();
        _run.Clear();
        Document.Insert(start, text);
        if (BrushFor(kind) is { } brush)
        {
            AddSegment(new TrackedRange<IBrush>(start, text.Length, brush));
        }

        // A listener may read the text, which appends what is queued: it gets a list of its own.
        var written = _written.ToArray();
        _written.Clear();
        foreach (var (piece, pieceKind) in written)
        {
            Written?.Invoke(this, new OutputWrittenEventArgs(piece, pieceKind));
        }
    }

    private void AddSegment(TrackedRange<IBrush> segment)
    {
        // Merge with the previous segment when it continues it in the same color.
        if (_segments.Count > 0 && _segments[^1].End == segment.Start && ReferenceEquals(_segments[^1].Value, segment.Value))
        {
            _segments[^1] = _segments[^1] with { Length = _segments[^1].Length + segment.Length };
        }
        else
        {
            _segments.Add(segment);
        }
    }

    private void TrimToLimit()
    {
        if (Document.TextLength <= _characterLimit)
        {
            return;
        }

        // Cut at a line boundary a little past the excess, so trimming happens in chunks, not on every write.
        var excess = Document.TextLength - (_characterLimit * 3 / 4);
        var cut = Document.GetLineByOffset(Math.Min(excess, Document.TextLength)).EndOffset;
        cut = Math.Min(Document.TextLength, cut + 1);
        Document.Remove(0, cut);

        var kept = _segments
            .Where(s => s.End > cut)
            .Select(s => new TrackedRange<IBrush>(Math.Max(0, s.Start - cut), s.End - Math.Max(s.Start, cut), s.Value))
            .ToList();
        _segments.Clear();
        _segments.AddRange(kept);
    }

    private static IBrush? BrushFor(OutputKind kind) => kind switch
    {
        OutputKind.Error or OutputKind.Failure => ErrorBrush,
        OutputKind.Warning => WarningBrush,
        OutputKind.Input => InputBrush,
        OutputKind.Info => InfoBrush,
        OutputKind.Success => SuccessBrush,
        _ => null,
    };
}

/// <summary>A piece of text the Output panel appended.</summary>
/// <param name="text">The text.</param>
/// <param name="kind">What it is.</param>
public sealed class OutputWrittenEventArgs(string text, OutputKind kind) : EventArgs
{
    /// <summary>The text.</summary>
    public string Text { get; } = text;

    /// <summary>What it is.</summary>
    public OutputKind Kind { get; } = kind;
}
