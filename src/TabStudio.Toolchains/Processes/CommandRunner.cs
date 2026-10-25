using System.Text;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Processes;

/// <summary>A command that ran to the end: how it ended and everything it printed.</summary>
/// <param name="Exit">How it ended.</param>
/// <param name="Output">Its standard output and error, line by line in the order they arrived.</param>
public sealed record CommandResult(ProcessExit Exit, string Output)
{
    /// <summary>Whether it exited with code 0.</summary>
    public bool Succeeded => Exit.Succeeded;

    /// <summary>The last lines it printed, for a message: what usually says why it failed.</summary>
    /// <param name="count">How many lines.</param>
    public string Tail(int count = 8) =>
        string.Join('\n', Output.Split('\n', StringSplitOptions.RemoveEmptyEntries).TakeLast(count));
}

/// <summary>Runs a command to the end and collects what it prints, line by line: for installers and version checks.</summary>
public static class CommandRunner
{
    /// <summary>Runs a command with its standard input closed.</summary>
    /// <param name="runtime">Where to run it.</param>
    /// <param name="spec">What to run; it runs on pipes.</param>
    /// <param name="line">Receives each line as it is printed, from any thread; may be <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the command.</param>
    /// <exception cref="ToolchainRuntimeException">It could not be started.</exception>
    public static async Task<CommandResult> RunAsync(IToolchainRuntime runtime, ProcessStartSpec spec, Action<string>? line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(spec);
        using var process = runtime.Start(spec with { Terminal = null });
        process.CloseInput();
        var output = new StringBuilder();
        var gate = new Lock();

        void Collect(string text)
        {
            lock (gate)
            {
                output.Append(text).Append('\n');
            }

            line?.Invoke(text);
        }

        var readers = Task.WhenAll(
            Task.Run(() => ReadLines(process.Output, Collect), CancellationToken.None),
            process.Error is { } error ? Task.Run(() => ReadLines(error, Collect), CancellationToken.None) : Task.CompletedTask);
        var exit = await process.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await readers.WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // A process it started still holds its output open; what arrived is enough.
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (gate)
        {
            return new CommandResult(exit, output.ToString());
        }
    }

    private static void ReadLines(Stream stream, Action<string> line)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        try
        {
            while (reader.ReadLine() is { } text)
            {
                line(text);
            }
        }
        catch (IOException)
        {
            // The program is gone; what it printed was read.
        }
    }
}
