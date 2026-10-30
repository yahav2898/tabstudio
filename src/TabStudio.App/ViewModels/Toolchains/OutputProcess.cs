using System.Text;
using TabStudio.App.Notebooks;
using TabStudio.Compilation.Running;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;
using TabStudio.Toolchains.Networking;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

/// <summary>How a program run in the Output panel ended.</summary>
/// <param name="Exit">How it exited, or <see langword="null"/> when it could not start.</param>
/// <param name="Lines">The first and last lines it printed, standard output and error together, for finding its errors and the one it ended with.</param>
/// <param name="StartError">Why it could not start, when it could not.</param>
public sealed record OutputProcessResult(ProcessExit? Exit, IReadOnlyList<string> Lines, string? StartError = null)
{
    /// <summary>Whether it ran and exited with code 0.</summary>
    public bool Succeeded => Exit is { Succeeded: true };

    /// <summary>
    /// Why no program ran although the build succeeded, such as a CMake build with several executables and none
    /// chosen; <see langword="null"/> when a program ran or could not start.
    /// </summary>
    public string? NothingToRunReason { get; init; }

    /// <summary>The result of a run that built its project but had no program to start, for a reason the user can act on.</summary>
    /// <param name="reason">One line that says why and how to choose a program.</param>
    public static OutputProcessResult NothingToRun(string reason) => new(null, [reason]) { NothingToRunReason = reason };
}

/// <summary>
/// Runs a toolchain program with its console in the Output panel: standard output and error stream in as they
/// arrive, and lines typed in the input box go to its standard input.
/// </summary>
/// <remarks>
/// A program in its own process may read its input at any time, so the panel cannot know when it waits for a line
/// as it does for a .NET program. When the program prints something without a newline, such as a prompt, and
/// then goes quiet, the input box is focused.
/// </remarks>
internal static class OutputProcess
{
    /// <summary>
    /// The lines kept from the start of a program's output and from its end. The errors of a build are usually
    /// first and the error a program ended with last; keeping only the last 400 lost a C or Rust build's first errors
    /// behind its warnings.
    /// </summary>
    internal const int KeptLines = 1000;
    private static readonly TimeSpan PromptQuiet = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DrainTime = TimeSpan.FromSeconds(3);

    /// <summary>Runs a program until it exits or is stopped.</summary>
    /// <param name="runtime">Where to run it.</param>
    /// <param name="spec">The program.</param>
    /// <param name="output">The Output panel.</param>
    /// <param name="input">Lines for its standard input, from <see cref="OutputViewModel.BeginProcessInput"/>, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Stops the program and everything it started.</param>
    /// <param name="started">Told the program once it started, before its output is read, such as for its process id.</param>
    /// <param name="listening">Told each port the program starts listening on, for the web preview.</param>
    /// <param name="toolchain">The toolchain that runs it, such as <c>rust</c>, for the patterns only it prints; every toolchain's output is colored by what it says, not by its stream.</param>
    public static async Task<OutputProcessResult> RunAsync(IToolchainRuntime runtime, ProcessStartSpec spec, OutputViewModel output, ConsoleInput? input, CancellationToken cancellationToken, Action<ToolchainProcess>? started = null, Action<int>? listening = null, string? toolchain = null)
    {
        ToolchainProcess process;
        try
        {
            process = runtime.Start(spec);
        }
        catch (ToolchainRuntimeException ex)
        {
            output.WriteLine(ex.Message, OutputKind.Failure);
            return new OutputProcessResult(null, [], ex.Message);
        }

        started?.Invoke(process);

        using (process)
        using (cancellationToken.Register(process.Stop))
        using (var prompt = new PromptWatcher(output))
        {
            using var ports = listening is null ? null : new ProcessPortWatcher(process, listening);
            var lines = new LineCollector();
            var stdout = PumpAsync(process.Output, OutputKind.Program, output, lines, prompt, ports, new StreamClassifier(toolchain));
            var stderr = process.Error is { } error ? PumpAsync(error, OutputKind.Error, output, lines, prompt, ports, new StreamClassifier(toolchain)) : Task.CompletedTask;
            if (input is null)
            {
                process.CloseInput();
            }
            else
            {
                _ = Task.Run(() => ForwardInput(input, process), CancellationToken.None);
            }

            var exit = await process.Completion.ConfigureAwait(false);

            // What the program wrote just before it ended is still in the pipes; a child it left behind could
            // keep them open, so the wait is short.
            await Task.WhenAny(Task.WhenAll(stdout, stderr), Task.Delay(DrainTime, CancellationToken.None)).ConfigureAwait(false);
            return new OutputProcessResult(exit, lines.Snapshot());
        }
    }

    /// <summary>The Output panel's kind for what a line says.</summary>
    /// <param name="kind">What the line says.</param>
    internal static OutputKind KindOf(OutputLineKind kind) => kind switch
    {
        OutputLineKind.Error => OutputKind.Error,
        OutputLineKind.Warning => OutputKind.Warning,
        _ => OutputKind.Program,
    };

    /// <summary>Writes a stream's text to the panel, each line colored by what it says.</summary>
    /// <param name="output">The panel.</param>
    /// <param name="classifier">The stream's classifier.</param>
    /// <param name="text">The text.</param>
    internal static void WriteClassified(OutputViewModel output, StreamClassifier classifier, string text)
    {
        foreach (var piece in classifier.Feed(text))
        {
            output.Write(piece.Text, KindOf(piece.Kind));
        }
    }

    private static async Task PumpAsync(Stream stream, OutputKind kind, OutputViewModel output, LineCollector lines, PromptWatcher prompt, ProcessPortWatcher? ports, StreamClassifier classifier)
    {
        var decoder = Encoding.UTF8.GetDecoder();
        var bytes = new byte[8192];
        var chars = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length) + 1];
        var carriageReturn = false;
        try
        {
            int read;
            while ((read = await stream.ReadAsync(bytes).ConfigureAwait(false)) > 0)
            {
                var count = decoder.GetChars(bytes, 0, read, chars, 0, flush: false);
                if (count == 0)
                {
                    continue;
                }

                var text = Normalize(new string(chars, 0, count), ref carriageReturn);
                if (text.Length > 0)
                {
                    // Compilers and package managers write progress to standard error: the color comes from the text.
                    WriteClassified(output, classifier, text);
                    lines.Add(text, kind);
                    prompt.Wrote(text);
                    ports?.Feed(text, kind == OutputKind.Error);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // The program ended, or was stopped.
        }

        lines.Complete(kind);
    }

    /// <summary>
    /// Text for the panel: no escape sequences (colors are for terminals; the Terminal shows them), and line
    /// endings as newlines, with a carriage return that redraws a progress line taken as a new line.
    /// </summary>
    private static string Normalize(string text, ref bool carriageReturn)
    {
        text = AnsiText.Strip(text);
        var result = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (carriageReturn && c != '\n')
            {
                result.Append('\n');
            }

            carriageReturn = c == '\r';
            if (!carriageReturn)
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    private static void ForwardInput(ConsoleInput input, ToolchainProcess process)
    {
        try
        {
            while (input.ReadLine() is { } line)
            {
                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                process.Input.Write(bytes);
                process.Input.Flush();
            }

            process.CloseInput();
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // The program ended before reading everything.
        }
    }

    /// <summary>The first and last lines a program printed, <see cref="KeptLines"/> of each, each stream's lines kept whole.</summary>
    internal sealed class LineCollector
    {
        private readonly Lock _gate = new();
        private readonly List<string> _head = [];
        private readonly Queue<string> _lines = new();
        private readonly Dictionary<OutputKind, StringBuilder> _partial = new();

        public void Add(string text, OutputKind kind)
        {
            lock (_gate)
            {
                var partial = _partial.TryGetValue(kind, out var builder) ? builder : _partial[kind] = new StringBuilder();
                foreach (var c in text)
                {
                    if (c == '\n')
                    {
                        Keep(partial.ToString());
                        partial.Clear();
                    }
                    else if (partial.Length < 4096)
                    {
                        partial.Append(c);
                    }
                }
            }
        }

        public void Complete(OutputKind kind)
        {
            lock (_gate)
            {
                if (_partial.Remove(kind, out var partial) && partial.Length > 0)
                {
                    Keep(partial.ToString());
                }
            }
        }

        public List<string> Snapshot()
        {
            lock (_gate)
            {
                foreach (var partial in _partial.Values.Where(p => p.Length > 0))
                {
                    Keep(partial.ToString());
                }

                _partial.Clear();
                return [.. _head, .. _lines];
            }
        }

        private void Keep(string line)
        {
            if (_head.Count < KeptLines)
            {
                _head.Add(line);
                return;
            }

            _lines.Enqueue(line);
            if (_lines.Count > KeptLines)
            {
                _lines.Dequeue();
            }
        }
    }

    /// <summary>Focuses the input box when the program printed a prompt and went quiet.</summary>
    private sealed class PromptWatcher : IDisposable
    {
        private readonly Timer _timer;
        private readonly Lock _gate = new();
        private bool _disposed;

        public PromptWatcher(OutputViewModel output) =>
            _timer = new Timer(_ => output.ShowWaitingForInput(), null, Timeout.Infinite, Timeout.Infinite);

        public void Wrote(string text)
        {
            lock (_gate)
            {
                if (!_disposed)
                {
                    _timer.Change(text.EndsWith('\n') ? Timeout.InfiniteTimeSpan : PromptQuiet, Timeout.InfiniteTimeSpan);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _disposed = true;
                _timer.Dispose();
            }
        }
    }
}
