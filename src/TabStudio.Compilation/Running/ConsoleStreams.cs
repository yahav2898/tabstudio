using System.Collections.Concurrent;
using System.Text;

namespace TabStudio.Compilation.Running;

/// <summary>
/// Receives what a running program writes to the console. Implemented by the Output panel.
/// </summary>
/// <remarks>Methods are called on the program's thread, not on the UI thread.</remarks>
public interface IConsoleSink
{
    /// <summary>Text written to standard output.</summary>
    /// <param name="text">The text, which may contain newlines.</param>
    void WriteOutput(string text);

    /// <summary>Text written to standard error, or a crash report.</summary>
    /// <param name="text">The text, which may contain newlines.</param>
    void WriteError(string text);
}

/// <summary>
/// A <see cref="TextWriter"/> that forwards everything to a callback, used to capture <see cref="Console.Out"/>.
/// </summary>
/// <remarks>
/// Characters are buffered until a newline or a flush, so a program that writes one character at a time
/// does not flood the UI. After <see cref="Detach"/>, writes are dropped silently: this is how output from a
/// program the user stopped is cut off while its thread winds down.
/// </remarks>
internal sealed class CallbackTextWriter(Action<string> write) : TextWriter
{
    private readonly Lock _gate = new();
    private readonly StringBuilder _pending = new();
    private bool _detached;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value)
    {
        lock (_gate)
        {
            _pending.Append(value);
            if (value == '\n')
            {
                FlushPending();
            }
        }
    }

    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        lock (_gate)
        {
            _pending.Append(value);
            FlushPending();
        }
    }

    public override void Write(char[] buffer, int index, int count) => Write(new string(buffer, index, count));

    public override void WriteLine(string? value) => Write((value ?? string.Empty) + NewLine);

    public override void Flush()
    {
        lock (_gate)
        {
            FlushPending();
        }
    }

    /// <summary>Stops forwarding. Anything written afterwards is discarded.</summary>
    public void Detach()
    {
        lock (_gate)
        {
            FlushPending();
            _detached = true;
        }
    }

    private void FlushPending()
    {
        if (_pending.Length == 0)
        {
            return;
        }

        var text = _pending.ToString();
        _pending.Clear();
        if (!_detached)
        {
            write(text);
        }
    }
}

/// <summary>
/// Standard input for a running program, fed one line at a time from the Output panel's input box.
/// </summary>
/// <remarks>
/// <see cref="Console.ReadLine"/> in the program blocks until the user submits a line, the program is
/// stopped, or input is completed, in which case it returns <see langword="null"/> just as at the end of a file.
/// </remarks>
public sealed class ConsoleInput : TextReader
{
    private readonly BlockingCollection<string> _lines = new(new ConcurrentQueue<string>());
    private readonly CancellationTokenSource _closed = new();
    private string? _current;
    private int _position;

    /// <summary>Raised on the program's thread when it starts waiting for a line.</summary>
    public event EventHandler? WaitingForInput;

    /// <summary>Sends a line to the program, as if the user typed it and pressed Enter.</summary>
    /// <param name="line">The line, without a trailing newline.</param>
    public void SubmitLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (!_lines.IsAddingCompleted)
        {
            _lines.Add(line);
        }
    }

    /// <summary>Signals end of input; further reads return <see langword="null"/>.</summary>
    public void Complete()
    {
        _lines.CompleteAdding();
        _closed.Cancel();
    }

    /// <inheritdoc />
    public override string? ReadLine()
    {
        if (_current is not null)
        {
            var rest = _current[_position..].TrimEnd('\n');
            _current = null;
            return rest;
        }

        return Take();
    }

    /// <inheritdoc />
    public override int Read()
    {
        if (!EnsureCurrent())
        {
            return -1;
        }

        var c = _current![_position++];
        if (_position >= _current.Length)
        {
            _current = null;
        }

        return c;
    }

    /// <inheritdoc />
    public override int Peek() => EnsureCurrent() ? _current![_position] : -1;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Complete();
            _lines.Dispose();
            _closed.Dispose();
        }

        base.Dispose(disposing);
    }

    private bool EnsureCurrent()
    {
        if (_current is not null)
        {
            return true;
        }

        var line = Take();
        if (line is null)
        {
            return false;
        }

        _current = line + "\n";
        _position = 0;
        return true;
    }

    private string? Take()
    {
        if (_lines.TryTake(out var ready))
        {
            return ready;
        }

        WaitingForInput?.Invoke(this, EventArgs.Empty);
        try
        {
            return _lines.TryTake(out var line, Timeout.Infinite, _closed.Token) ? line : null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }
}
