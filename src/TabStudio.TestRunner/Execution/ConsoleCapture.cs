using System.Text;

namespace TabStudio.TestRunner.Execution;

/// <summary>
/// Sends console output to the test that wrote it, while a run is in progress.
/// </summary>
/// <remarks>
/// <para>
/// The console is process-wide, so during a run <see cref="Console.Out"/> and <see cref="Console.Error"/> are
/// replaced by writers that look up the current test in an <see cref="AsyncLocal{T}"/>. Each test's thread sets
/// it before the test starts, and it flows into every task and continuation the test starts, so output lands in
/// the right test even when several classes run in parallel. Output from code that belongs to no test is dropped.
/// </para>
/// <para>
/// Only one run redirects the console at a time: <see cref="Gate"/> serializes runs across every session.
/// </para>
/// </remarks>
internal static class ConsoleCapture
{
    private static readonly AsyncLocal<TestOutputSink?> CurrentSink = new();

    /// <summary>Held by the run that currently owns the console.</summary>
    public static SemaphoreSlim Gate { get; } = new(1, 1);

    /// <summary>The sink of the test running on this logical thread of execution, if any.</summary>
    public static TestOutputSink? Current
    {
        get => CurrentSink.Value;
        set => CurrentSink.Value = value;
    }

    /// <summary>Redirects the console until the returned object is disposed. The caller holds <see cref="Gate"/>.</summary>
    public static IDisposable Redirect() => new Redirection();

    /// <summary>Restores the console that was in place before a run.</summary>
    private sealed class Redirection : IDisposable
    {
        private readonly TextWriter _previousOut = Console.Out;
        private readonly TextWriter _previousError = Console.Error;
        private readonly TextReader _previousIn = Console.In;

        public Redirection()
        {
            Console.SetOut(new RoutingWriter());
            Console.SetError(new RoutingWriter());
            Console.SetIn(TextReader.Null);
        }

        public void Dispose()
        {
            Console.SetOut(_previousOut);
            Console.SetError(_previousError);
            Console.SetIn(_previousIn);
        }
    }

    /// <summary>A writer that forwards to the current test's sink.</summary>
    private sealed class RoutingWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => Current?.Write(new string(value, 1));

        public override void Write(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                Current?.Write(value);
            }
        }

        public override void Write(char[] buffer, int index, int count) => Write(new string(buffer, index, count));

        public override void WriteLine(string? value) => Write((value ?? string.Empty) + NewLine);
    }
}
