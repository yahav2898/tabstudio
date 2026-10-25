namespace TabStudio.Compilation.Running;

/// <summary>
/// The process-wide console, shared by everything that runs user code inside TabStudio: a program started
/// with F5, a debug session, a test run and a notebook cell. Only one of them owns it at a time, so their output
/// never mixes; the others wait.
/// </summary>
public static class ProcessConsole
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Waits until nothing else owns the console, then takes it.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>The lease. Disposing it puts the console back as it was and lets the next owner in.</returns>
    public static async Task<ConsoleLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new ConsoleLease(Gate);
    }

    /// <summary>
    /// The current console input. Android, iOS and the browser throw from <see cref="Console.In"/> until
    /// something sets it, so an empty input stands in for it there.
    /// </summary>
    internal static TextReader CurrentInput() =>
        OperatingSystem.IsAndroid() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsBrowser()
            ? SafeInput()
            : Console.In;

    private static TextReader SafeInput()
    {
        try
        {
            return Console.In;
        }
        catch (PlatformNotSupportedException)
        {
            return TextReader.Null;
        }
    }
}

/// <summary>Ownership of the process-wide console, from <see cref="ProcessConsole.AcquireAsync"/>.</summary>
public sealed class ConsoleLease : IDisposable
{
    private readonly SemaphoreSlim _gate;
    private int _disposed;

    internal ConsoleLease(SemaphoreSlim gate)
    {
        _gate = gate;
        PreviousOut = Console.Out;
        PreviousError = Console.Error;
        PreviousIn = ProcessConsole.CurrentInput();
    }

    /// <summary>The standard output before the lease, which is restored when it ends.</summary>
    public TextWriter PreviousOut { get; }

    /// <summary>The standard error before the lease, which is restored when it ends.</summary>
    public TextWriter PreviousError { get; }

    /// <summary>The standard input before the lease, which is restored when it ends.</summary>
    public TextReader PreviousIn { get; }

    /// <summary>Points the console at new streams for as long as the lease lasts.</summary>
    /// <param name="output">Standard output.</param>
    /// <param name="error">Standard error.</param>
    /// <param name="input">Standard input.</param>
    public void Redirect(TextWriter output, TextWriter error, TextReader input)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);
        Console.SetOut(output);
        Console.SetError(error);
        Console.SetIn(input);
    }

    /// <summary>Restores the console and gives it to the next owner. Safe to call more than once.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Console.SetOut(PreviousOut);
        Console.SetError(PreviousError);
        Console.SetIn(PreviousIn);
        _gate.Release();
    }
}
