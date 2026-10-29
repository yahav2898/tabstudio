using System.Threading.Channels;
using TabStudio.Compilation;
using TabStudio.Compilation.Running;
using TabStudio.Debugging.Building;

// Debug sessions share the process-wide console and AppContext, so tests that run programs never run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TabStudio.Debugging.Tests;

/// <summary>
/// Compiles a small program in memory, runs it under the debugger on a background thread and records every pause,
/// with time limits so a debugger bug fails the test instead of hanging the run.
/// </summary>
internal sealed class DebuggedProgram : IDisposable
{
    public const string FilePath = "Program.cs";

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly Channel<DebugPausedEventArgs> _pauses = Channel.CreateUnbounded<DebugPausedEventArgs>();
    private Task<RunResult>? _run;

    public DebuggedProgram(string source, params int[] breakpointLines)
        : this(Compile(source), breakpointLines)
    {
    }

    public DebuggedProgram(DebugBuild build, params int[] breakpointLines)
        : this(build, FilePath, breakpointLines)
    {
    }

    public DebuggedProgram(DebugBuild build, string breakpointFile, params int[] breakpointLines)
    {
        Build = build;
        Session = new DebugSession(build);
        foreach (var line in breakpointLines)
        {
            Session.SetBreakpoint(breakpointFile, line);
        }

        Session.Paused += (_, pause) => _pauses.Writer.TryWrite(pause);
    }

    public DebugBuild Build { get; }

    public DebugSession Session { get; }

    public CapturingConsole Console { get; } = new();

    public Task<RunResult> Run => _run ?? throw new InvalidOperationException("Call Start first.");

    /// <summary>Compiles a program and instruments it, failing the test on compile errors.</summary>
    public static DebugBuild Compile(string source, string filePath = FilePath)
    {
        var compilation = new CSharpCompiler().CreateCompilation(new CompilationRequest
        {
            AssemblyName = "Debuggee" + Guid.NewGuid().ToString("N")[..8],
            Documents = [new SourceDocument(filePath, source)],
        });

        var build = DebugBuilder.Build(compilation);
        Assert.True(build.Success, string.Join('\n', build.Result.Diagnostics));
        return build;
    }

    public void Start(ConsoleInput? input = null) => _run = Task.Run(() => Session.RunAsync([], Console, input));

    /// <summary>Waits for the next pause; fails if the program ends first or nothing happens in time.</summary>
    public async Task<DebugPausedEventArgs> NextPauseAsync()
    {
        using var cancel = new CancellationTokenSource(Timeout);
        var read = _pauses.Reader.ReadAsync(cancel.Token).AsTask();
        await Task.WhenAny(read, Run).WaitAsync(Timeout);
        if (read.IsCompletedSuccessfully)
        {
            return await read;
        }

        if (_pauses.Reader.TryRead(out var late))
        {
            return late;
        }

        await cancel.CancelAsync();
        var result = await Run;
        throw new Xunit.Sdk.XunitException(
            $"The program ended ({result.Outcome}) without pausing.\nOutput: {Console.Output}\nError: {Console.Error}\n{result.Exception}");
    }

    /// <summary>Waits for the next pause, or returns <see langword="null"/> when the program ends first.</summary>
    public async Task<DebugPausedEventArgs?> TryNextPauseAsync()
    {
        using var cancel = new CancellationTokenSource(Timeout);
        var read = _pauses.Reader.ReadAsync(cancel.Token).AsTask();
        await Task.WhenAny(read, Run).WaitAsync(Timeout);
        if (read.IsCompletedSuccessfully)
        {
            return await read;
        }

        await cancel.CancelAsync();
        return _pauses.Reader.TryRead(out var late) ? late : null;
    }

    /// <summary>Waits until the program has written some text.</summary>
    public async Task WaitForOutputAsync(string text)
    {
        var deadline = DateTime.UtcNow + Timeout;
        while (!Console.Output.Contains(text, StringComparison.Ordinal))
        {
            Assert.True(DateTime.UtcNow < deadline, $"The program did not write '{text}' in time.");
            Assert.False(Run.IsCompleted, $"The program ended before writing '{text}'.");
            await Task.Delay(10);
        }
    }

    /// <summary>Waits for the program to end.</summary>
    public async Task<RunResult> FinishAsync() => await Run.WaitAsync(Timeout);

    public void Dispose() => Session.Dispose();

    public static DebugValue Local(DebugPausedEventArgs pause, string name) =>
        Assert.Single(pause.Locals, local => local.Name == name);
}
