namespace TabStudio.Notebooks.Execution;

/// <summary>
/// Points the process-wide console at a running cell, one cell at a time across every kernel in the process.
/// </summary>
/// <remarks>
/// <see cref="Console"/> belongs to the whole process, like the one <c>ProgramRunner</c> redirects for F5.
/// While a cell runs, its writes go to the cell, and reading input returns end of file.
/// </remarks>
internal static class ConsoleRedirection
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Waits for the console, then redirects it to a cell until the result is disposed.</summary>
    public static async Task<IDisposable> RedirectAsync(CellRun run, CancellationToken cancellationToken)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var restore = new Restore(Console.Out, Console.Error, Console.In);
        Console.SetOut(new CellTextWriter(run, isError: false));
        Console.SetError(new CellTextWriter(run, isError: true));
        Console.SetIn(TextReader.Null);
        return restore;
    }

    private sealed class Restore(TextWriter output, TextWriter error, TextReader input) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }

            Console.SetOut(output);
            Console.SetError(error);
            Console.SetIn(input);
            Gate.Release();
        }
    }
}
