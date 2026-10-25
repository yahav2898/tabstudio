using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Debugging;

namespace TabStudio.Swarm.Device;

/// <summary>
/// A console app the tablet sent, running on the device with its console in the swarm: output goes to the tablet's
/// Output panel and the device's screen, and input comes from the tablet.
/// </summary>
internal sealed class SwarmConsoleRun : ISwarmAppRun
{
    private readonly ConsoleInput _input = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly SwarmOutputSender _output;
    private readonly DebugSession? _debugger;
    private int _disposed;

    private SwarmConsoleRun(SwarmOutputSender output, DebugSession? debugger)
    {
        _output = output;
        _debugger = debugger;
        Completion = Task.FromResult(new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero));
    }

    /// <inheritdoc />
    public Task<RunResult> Completion { get; private set; }

    /// <summary>Starts a console app, under the debugger when one is given.</summary>
    /// <param name="entry">The app's assembly.</param>
    /// <param name="dependencies">The assemblies of the projects it references.</param>
    /// <param name="packages">Its package assemblies, as files.</param>
    /// <param name="arguments">Its command-line arguments.</param>
    /// <param name="debugger">The debugger, or <see langword="null"/> to run it without one.</param>
    /// <param name="output">Where its output goes.</param>
    public static SwarmConsoleRun Start(
        CompiledAssembly entry,
        IReadOnlyList<CompiledAssembly> dependencies,
        IReadOnlyList<string> packages,
        IReadOnlyList<string> arguments,
        DebugSession? debugger,
        SwarmOutputSender output)
    {
        var run = new SwarmConsoleRun(output, debugger);
        run.Completion = Task.Run(() => run.RunAsync(entry, dependencies, packages, arguments));
        return run;
    }

    /// <summary>A line typed on the tablet, or the end of input.</summary>
    /// <param name="line">The line, or <see langword="null"/> for the end of input.</param>
    public void Submit(string? line)
    {
        if (line is null)
        {
            _input.Complete();
        }
        else
        {
            _input.SubmitLine(line);
        }
    }

    /// <summary>Stops the app, and waits for it a moment: .NET cannot abort a thread that ignores its input ending.</summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        _debugger?.Stop();
        await _stop.CancelAsync().ConfigureAwait(false);
        _input.Complete();
        await Task.WhenAny(Completion, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
        _stop.Dispose();
    }

    private async Task<RunResult> RunAsync(CompiledAssembly entry, IReadOnlyList<CompiledAssembly> dependencies, IReadOnlyList<string> packages, IReadOnlyList<string> arguments)
    {
        try
        {
            return _debugger is not null
                ? await _debugger.RunAsync(arguments, packages, _output, _input, _stop.Token).ConfigureAwait(false)
                : await new ProgramRunner().RunAsync(entry, dependencies, packages, arguments, _output, _input, _stop.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new RunResult(RunOutcome.Stopped, -1, TimeSpan.Zero);
        }
        finally
        {
            await _output.FlushAsync().ConfigureAwait(false);
        }
    }
}
