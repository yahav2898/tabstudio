using System.Text;
using System.Text.RegularExpressions;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Embedded.Testing;

/// <summary>A receive-only Unity run attributed to the actual selected USB attachment, with no firmware-identification claim.</summary>
/// <param name="Results">All individual observations and any harness/transport errors.</param>
/// <param name="Exit">The real worker's exit status.</param>
/// <param name="HarnessCompleted">Both the matching Unity summary and final trailer were captured successfully.</param>
/// <param name="TimedOut">The outer transport deadline elapsed.</param>
/// <param name="BoardIdentity">Actual USB attachment and optional operator label; not a verified MCU or firmware identity.</param>
public sealed record EmbeddedSerialTestRun(IReadOnlyList<EmbeddedTestResult> Results, ProcessExit Exit,
    bool HarnessCompleted, bool TimedOut, string BoardIdentity);

/// <summary>Captures tests from already-programmed firmware using the existing permissioned USB serial worker.</summary>
public static class EmbeddedSerialTestRunner
{
    private static readonly Regex Ansi = new("\\x1b\\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    /// <summary>Reads one complete Unity run without building, flashing, sending input or intentionally changing modem outputs.</summary>
    /// <param name="runtime">Prepared production runtime with the ABI-matching USB serial bridge.</param>
    /// <param name="connection">Caller-owned permissioned connection, retained until this task completes.</param>
    /// <param name="options">Explicit running-firmware serial settings.</param>
    /// <param name="directory">Workspace directory.</param>
    /// <param name="portAlias">Fresh private absolute PTY alias.</param>
    /// <param name="output">Every complete received line, including stderr marked separately.</param>
    /// <param name="cancellationToken">User Stop; never becomes a passing test transport.</param>
    public static async Task<EmbeddedSerialTestRun> RunAsync(IToolchainRuntime runtime, IUsbConnection connection,
        EmbeddedSerialTestOptions options, string directory, string portAlias, Action<string, bool>? output = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        var identity = options.BoardIdentity(connection.Device);
        var command = options.Command(connection, directory, portAlias);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds + 3));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        using var drain = new CancellationTokenSource();
        using var process = runtime.Start(command);
        using var registration = stop.Token.Register(process.Stop);
        process.CloseInput();
        var parser = new EmbeddedTestOutputParser(identity, EmbeddedTestFormat.Unity);
        var gate = new Lock();
        var total = 0;
        string? finalLine = null;

        void Line(string line, bool error)
        {
            output?.Invoke(line, error);
            if (!error)
            {
                parser.Feed(line);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    finalLine = Ansi.Replace(line, "").Trim();
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
                    if (total > 16 * 1024 * 1024)
                    {
                        throw new InvalidDataException("Serial test output exceeds 16 MiB.");
                    }
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
                        throw new InvalidDataException("Serial test output line exceeds 64 KiB.");
                    }
                }
            }
            if (partial.Length != 0)
            {
                throw new InvalidDataException("Serial test output ended with a truncated line.");
            }
        }

        var pumps = Task.WhenAll(PumpAsync(process.Output, false),
            process.Error is { } error ? PumpAsync(error, true) : Task.CompletedTask);
        _ = pumps.ContinueWith(_ => process.Stop(), CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        var exit = await process.Completion.ConfigureAwait(false);
        var drained = await Task.WhenAny(pumps, Task.Delay(TimeSpan.FromSeconds(3), CancellationToken.None)).ConfigureAwait(false) == pumps;
        if (!drained)
        {
            drain.Cancel();
        }
        string? readError = null;
        try
        {
            await pumps.ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or OperationCanceledException or DecoderFallbackException or InvalidDataException)
        {
            readError = exception.Message;
        }
        var cancelled = cancellationToken.IsCancellationRequested;
        var timedOut = timeout.IsCancellationRequested;
        var hasFailure = parser.Results.Any(test => test.Outcome == EmbeddedTestOutcome.Failed);
        var validTrailer = hasFailure ? finalLine is "FAIL" or "FAILED" : finalLine == "OK";
        validTrailer &= parser.Results.Count > 0;
        var transportSucceeded = exit.Succeeded && !cancelled && !timedOut && drained && readError is null && validTrailer;
        var results = parser.Complete(transportSucceeded).ToList();
        var completed = transportSucceeded && parser.HarnessCompleted && results.All(test => test.Outcome != EmbeddedTestOutcome.Errored);
        if (!transportSucceeded)
        {
            results.Add(new(identity, EmbeddedTestFormat.Unity, "[serial transport]", EmbeddedTestOutcome.Errored,
                cancelled ? "Serial tests were stopped; firmware completion was not confirmed."
                : timedOut ? "Serial tests timed out."
                : readError ?? (!drained ? "Serial test output did not drain." : !validTrailer ? "Unity's final newline-terminated OK/FAIL trailer was missing."
                    : "The serial test worker " + exit.Description + ".")));
        }
        return new(results, exit, completed, timedOut, identity);
    }
}
