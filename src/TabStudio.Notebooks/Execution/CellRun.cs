using System.Text;
using TabStudio.Notebooks.Formatting;
using TabStudio.Notebooks.Outputs;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// Collects the outputs of one running cell, in the order they happen: console text, displayed values and the
/// cell's result.
/// </summary>
/// <remarks>
/// <para>
/// The run a piece of code belongs to is found through its execution context (<see cref="Current"/>), which
/// flows into the tasks, timers and threads the cell starts. So output lands in the cell that produced it even
/// when several cells' code is alive at once, such as a cell that was interrupted but kept running.
/// </para>
/// <para>
/// Console text is buffered until a line ends, or until the cell displays a value, so output keeps its order
/// and a cell that writes one character at a time does not flood the view. Once the run completes, anything
/// else the cell's code writes or displays, for example from a background task, is dropped.
/// </para>
/// </remarks>
internal sealed class CellRun(ValueFormatter formatter, Action<NotebookOutput> published, CancellationToken cancellationToken)
{
    private static readonly AsyncLocal<CellRun?> CurrentRun = new();

    private readonly Lock _gate = new();
    private readonly List<NotebookOutput> _outputs = [];
    private readonly StringBuilder _pendingOutput = new();
    private readonly StringBuilder _pendingError = new();
    private bool _completed;

    /// <summary>
    /// The run of the cell whose code is executing here, or <see langword="null"/> outside cells. Set once at the
    /// start of a cell's thread; tasks and threads the cell starts inherit it.
    /// </summary>
    public static CellRun? Current
    {
        get => CurrentRun.Value;
        set => CurrentRun.Value = value;
    }

    /// <summary>Cancelled when the cell is interrupted; cells read it as <c>CancellationToken</c>.</summary>
    public CancellationToken CancellationToken { get; } = cancellationToken;

    /// <summary>Adds console text.</summary>
    public void Write(bool isError, string text)
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            // Keep the order of text written to the two streams.
            FlushUnderGate(!isError);
            var pending = isError ? _pendingError : _pendingOutput;
            pending.Append(text);
            if (text.Contains('\n', StringComparison.Ordinal))
            {
                FlushUnderGate(isError);
            }
        }
    }

    /// <summary>Publishes buffered console text.</summary>
    public void Flush(bool isError)
    {
        lock (_gate)
        {
            FlushUnderGate(isError);
        }
    }

    /// <summary>Formats and adds a value the cell displayed.</summary>
    /// <param name="value">The value.</param>
    /// <param name="mimeTypes">MIME types the cell asked for. A string with MIME types is shown as those types as it is.</param>
    public void Display(object? value, string[] mimeTypes)
    {
        FormattedValue formatted;
        if (mimeTypes.Length > 0 && value is string content)
        {
            formatted = new FormattedValue(MimeBundle.Create(mimeTypes.Select(m => new KeyValuePair<string, string>(m, content))));
        }
        else
        {
            formatted = formatter.Format(value);
            if (mimeTypes.Length > 0 && formatted.Data.Only(mimeTypes) is { Count: > 0 } chosen)
            {
                formatted = formatted with { Data = chosen };
            }
        }

        Add(new DisplayOutput(formatted.Data) { Table = formatted.Table });
    }

    /// <summary>Formats and adds the value of the cell's last expression.</summary>
    public void AddResult(object? value, int executionCount)
    {
        var formatted = formatter.Format(value);
        Add(new DisplayOutput(formatted.Data) { Table = formatted.Table, IsResult = true, ExecutionCount = executionCount });
    }

    /// <summary>Adds an output after any console text written before it.</summary>
    public void Add(NotebookOutput output)
    {
        lock (_gate)
        {
            if (_completed)
            {
                return;
            }

            FlushUnderGate(isError: false);
            FlushUnderGate(isError: true);
            Publish(output);
        }
    }

    /// <summary>Ends the run, dropping anything added afterwards, and returns the outputs with consecutive text of a stream joined.</summary>
    public List<NotebookOutput> Complete()
    {
        lock (_gate)
        {
            if (!_completed)
            {
                FlushUnderGate(isError: false);
                FlushUnderGate(isError: true);
                _completed = true;
            }

            return JoinStreams(_outputs);
        }
    }

    private void FlushUnderGate(bool isError)
    {
        var pending = isError ? _pendingError : _pendingOutput;
        if (pending.Length == 0)
        {
            return;
        }

        var text = pending.ToString();
        pending.Clear();
        Publish(new StreamOutput(isError ? StreamOutput.StandardError : StreamOutput.StandardOutput, text));
    }

    private void Publish(NotebookOutput output)
    {
        _outputs.Add(output);
        published(output);
    }

    private static List<NotebookOutput> JoinStreams(List<NotebookOutput> outputs)
    {
        var joined = new List<NotebookOutput>(outputs.Count);
        foreach (var output in outputs)
        {
            if (output is StreamOutput stream && joined.Count > 0 && joined[^1] is StreamOutput previous && previous.Name == stream.Name)
            {
                joined[^1] = previous with { Text = previous.Text + stream.Text };
            }
            else
            {
                joined.Add(output);
            }
        }

        return joined;
    }
}
