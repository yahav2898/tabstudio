using System.Text;

namespace TabStudio.Notebooks.Execution;

/// <summary>Stands in for <see cref="Console.Out"/> or <see cref="Console.Error"/> while a cell runs.</summary>
internal sealed class CellTextWriter(CellRun run, bool isError) : TextWriter
{
    /// <inheritdoc />
    public override Encoding Encoding => Encoding.UTF8;

    /// <inheritdoc />
    public override void Write(char value) => run.Write(isError, new string(value, 1));

    /// <inheritdoc />
    public override void Write(string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            run.Write(isError, value);
        }
    }

    /// <inheritdoc />
    public override void Write(char[] buffer, int index, int count) => Write(new string(buffer, index, count));

    /// <inheritdoc />
    public override void Flush() => run.Flush(isError);
}
