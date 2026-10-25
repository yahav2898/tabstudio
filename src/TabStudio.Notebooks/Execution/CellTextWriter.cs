using System.Text;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// Stands in for <see cref="Console.Out"/> or <see cref="Console.Error"/> while a cell runs, and sends each
/// write to the cell whose code made it (<see cref="CellRun.Current"/>).
/// </summary>
/// <remarks>
/// Text from code that is not part of any cell, such as TabStudio's own threads, goes to the console that was
/// there before. Text from a cell that has already finished, such as one that was interrupted but kept
/// running, is dropped instead of showing up in the cell that runs next.
/// </remarks>
internal sealed class CellTextWriter(bool isError, TextWriter fallback) : TextWriter
{
    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <inheritdoc />
    public override void Write(char value) => Write(new string(value, 1));

    /// <inheritdoc />
    public override void Write(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        if (CellRun.Current is { } run)
        {
            run.Write(isError, value);
        }
        else
        {
            fallback.Write(value);
        }
    }

    /// <inheritdoc />
    public override void Write(char[] buffer, int index, int count) => Write(new string(buffer, index, count));

    /// <inheritdoc />
    public override void Flush()
    {
        if (CellRun.Current is { } run)
        {
            run.Flush(isError);
        }
        else
        {
            fallback.Flush();
        }
    }
}
