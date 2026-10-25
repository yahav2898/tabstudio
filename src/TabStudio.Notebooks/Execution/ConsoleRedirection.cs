namespace TabStudio.Notebooks.Execution;

/// <summary>
/// Points the process-wide console at running cells, one cell at a time across every kernel in the process.
/// </summary>
/// <remarks>
/// <see cref="Console"/> belongs to the whole process, like the one <c>ProgramRunner</c> redirects for F5.
/// While a cell runs, writes go to the cell that made them (see <see cref="CellTextWriter"/>), and reading
/// input returns end of file. Afterwards the console is put back as it was.
/// </remarks>
internal static class ConsoleRedirection
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Waits for the console, then redirects it to cells until the result is disposed.</summary>
    public static async Task<IDisposable> RedirectAsync(CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var restore = new Restore(Console.Out, Console.Error, Console.In);
        Console.SetOut(new CellTextWriter(isError: false, restore.Output));
        Console.SetError(new CellTextWriter(isError: true, restore.Error));
        Console.SetIn(TextReader.Null);
        return restore;
    }

    private sealed class Restore(TextWriter output, TextWriter error, TextReader input) : IDisposable
    {
        private int _disposed;

        public TextWriter Output { get; } = output;

        public TextWriter Error { get; } = error;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Console.SetOut(Output);
            Console.SetError(Error);
            Console.SetIn(input);
            Gate.Release();
        }
    }
}
