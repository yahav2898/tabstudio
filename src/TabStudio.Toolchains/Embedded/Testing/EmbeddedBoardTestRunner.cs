using System.Text;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Embedded.Testing;

/// <summary>A live native board-test run, retaining explicit harness completion separately from process success.</summary>
/// <param name="Results">Actual harness results, including incomplete-run/transport errors.</param>
/// <param name="Exit">Native command exit status.</param>
/// <param name="HarnessCompleted">The target emitted its final summary, rather than merely disconnecting.</param>
/// <param name="TimedOut">The configured timeout expired.</param>
public sealed record EmbeddedBoardTestRun(IReadOnlyList<EmbeddedTestResult> Results, ProcessExit Exit,
    bool HarnessCompleted, bool TimedOut);

/// <summary>Streams the full bounded native test output into the existing parser, not the Output panel's tail buffer.</summary>
public static class EmbeddedBoardTestRunner
{
    /// <summary>Flashes/verifies the stable test ELF, runs its harness, and stops only at summary, native exit, timeout or user Stop.</summary>
    /// <param name="runtime">The real native or granted-USB runtime.</param>
    /// <param name="plan">Already inspected, application-map-approved firmware snapshot.</param>
    /// <param name="command">The plan command with an optional permissioned USB bridge wrapped around it.</param>
    /// <param name="output">Receives every complete line and whether it came from standard error.</param>
    /// <param name="cancellationToken">User Stop, which never becomes a passing transport.</param>
    public static async Task<EmbeddedBoardTestRun> RunAsync(IToolchainRuntime runtime, EmbeddedBoardTestPlan plan,
        ProcessStartSpec command, Action<string, bool>? output = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        // Wrapping may add mounts/descriptors/environment, but cannot replace the validated command or ELF.
        if (command.Program != plan.Command.Program || !command.Arguments.SequenceEqual(plan.Command.Arguments)
            || plan.Command.Environment.Any(pair => !command.Environment.TryGetValue(pair.Key, out var value) || value != pair.Value))
        {
            throw new InvalidDataException("Board-test bridge changed the validated native command.");
        }
        plan.VerifySnapshot();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(plan.Options.TimeoutSeconds));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var drain = new CancellationTokenSource();
        using var process = runtime.Start(command);
        using var registration = stop.Token.Register(process.Stop);
        // Keep stdin open. The upstream RTT readline task treats EOF as monitor termination.
        var parser = new EmbeddedTestOutputParser(plan.Options.BoardId, plan.Options.Format);
        var gate = new Lock();
        var total = 0;
        var completed = false;
        var channel = plan.Options.RttChannelName ?? (plan.Options.Format == EmbeddedTestFormat.DefmtTest ? "defmt" : "Terminal");
        var prefix = new Regex(@"^\[" + Regex.Escape(channel) + @" *\] ", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

        void Line(string line, bool error)
        {
            output?.Invoke(line, error);
            lock (gate)
            {
                if (completed) return;
                parser.Feed(prefix.Replace(line, "", 1));
                if (plan.Options.Format != EmbeddedTestFormat.Rust && parser.HarnessCompleted)
                {
                    completed = true;
                    process.Stop();
                }
            }
        }

        async Task PumpAsync(Stream source, bool error)
        {
            using var reader = new StreamReader(source, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
            var partial = new StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), drain.Token).ConfigureAwait(false)) > 0)
            {
                lock (gate)
                {
                    total = checked(total + count);
                    if (total > 16 * 1024 * 1024) throw new InvalidDataException("Board-test output exceeds 16 MiB.");
                }
                for (var index = 0; index < count; index++)
                {
                    var character = buffer[index];
                    if (character == '\n')
                    {
                        Line(partial.ToString().TrimEnd('\r'), error);
                        partial.Clear();
                    }
                    else if (partial.Length < 64 * 1024)
                    {
                        partial.Append(character);
                    }
                    else
                    {
                        throw new InvalidDataException("Board-test output line exceeds 64 KiB.");
                    }
                }
            }
            if (partial.Length > 0) Line(partial.ToString().TrimEnd('\r'), error);
        }

        var pumps = Task.WhenAll(PumpAsync(process.Output, false),
            process.Error is { } error ? PumpAsync(error, true) : Task.CompletedTask);
        _ = pumps.ContinueWith(_ => process.Stop(), CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        var exit = await process.Completion.ConfigureAwait(false);
        var drained = await Task.WhenAny(pumps, Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None)).ConfigureAwait(false) == pumps;
        if (!drained) drain.Cancel();
        string? readError = null;
        try { await pumps.ConfigureAwait(false); }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or DecoderFallbackException or InvalidDataException)
        {
            readError = exception.Message;
        }
        var cancelled = cancellationToken.IsCancellationRequested;
        var timedOut = timeout.IsCancellationRequested;
        var transportSucceeded = !cancelled && !timedOut && drained && readError is null && (completed || exit.Succeeded);
        var results = parser.Complete(transportSucceeded).ToList();
        if (!transportSucceeded)
        {
            results.Add(new(plan.Options.BoardId, plan.Options.Format, "[board transport]", EmbeddedTestOutcome.Errored,
                cancelled ? "Board tests were stopped before successful completion." : timedOut ? "Board tests timed out."
                : readError ?? (drained ? "The native test command " + exit.Description + "." : "Test output did not drain after the native command ended.")));
        }
        return new(results, exit, parser.HarnessCompleted, timedOut);
    }
}
