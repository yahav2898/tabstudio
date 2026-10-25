using TabStudio.Compilation.Running;

namespace TabStudio.Notebooks.Execution;

/// <summary>
/// Points the process-wide console at running cells, one cell at a time across every kernel in the process.
/// </summary>
/// <remarks>
/// <see cref="Console"/> belongs to the whole process and is shared with programs started with F5 and with test
/// runs, through <see cref="ProcessConsole"/>. While a cell runs, writes go to the cell that made them (see
/// <see cref="CellTextWriter"/>), and reading input returns end of file. Afterwards the console is put back as it
/// was.
/// </remarks>
internal static class ConsoleRedirection
{
    /// <summary>Waits for the console, then redirects it to cells until the result is disposed.</summary>
    public static async Task<IDisposable> RedirectAsync(CancellationToken cancellationToken)
    {
        var lease = await ProcessConsole.AcquireAsync(cancellationToken).ConfigureAwait(false);
        lease.Redirect(
            new CellTextWriter(isError: false, lease.PreviousOut),
            new CellTextWriter(isError: true, lease.PreviousError),
            TextReader.Null);
        return lease;
    }
}
