using System.Text;
using TabStudio.Compilation.Running;

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
/// The console is shared with programs started with F5 and with notebook cells: a run owns it through a
/// <see cref="ProcessConsole"/> lease for its whole duration.
/// </para>
/// </remarks>
internal static class ConsoleCapture
{
    private static readonly AsyncLocal<TestOutputSink?> CurrentSink = new();

    /// <summary>The sink of the test running on this logical thread of execution, if any.</summary>
    public static TestOutputSink? Current
    {
        get => CurrentSink.Value;
        set => CurrentSink.Value = value;
    }

    /// <summary>Points a console lease at the current test's sink for the rest of the lease.</summary>
    /// <param name="lease">The run's ownership of the console.</param>
    public static void Redirect(ConsoleLease lease) =>
        lease.Redirect(new RoutingWriter(), new RoutingWriter(), TextReader.Null);

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
