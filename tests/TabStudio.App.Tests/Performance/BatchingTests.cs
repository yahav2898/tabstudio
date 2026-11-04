using System.Collections.Concurrent;
using System.Reflection;
using Avalonia.Headless.XUnit;
using TabStudio.Agent.Local;
using TabStudio.App.Editing;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;
using TabStudio.TestRunner;

namespace TabStudio.App.Tests.Performance;

/// <summary>
/// What reaches the UI thread when a program, a shell or a test runner produces faster than the UI can show: bounded
/// batches queued behind input and drawing, never one job that drains a stream that keeps refilling. Counted with a
/// dispatcher that only queues, so these hold on any machine.
/// </summary>
public sealed class BatchingTests
{
    [AvaloniaFact]
    public void Output_appends_a_flood_in_bounded_batches_behind_input_and_drawing()
    {
        var ui = new QueuedDispatcher();
        var output = new OutputViewModel(ui, 10_000_000);
        var inserts = 0;
        output.Document.Changed += (_, _) => inserts++;
        for (var i = 0; i < 100_000; i++)
        {
            output.WriteLine("Line of program output, about forty chars");
        }

        // One batch: at most the batch's size, in one insertion, and the next batch queued behind input and drawing.
        Assert.Equal((0, 1), (ui.Normal, ui.Background));
        ui.RunOne();
        Assert.InRange(output.Document.TextLength, OutputViewModel.MaxCharactersPerFlush, OutputViewModel.MaxCharactersPerFlush + 64);
        Assert.Equal(1, inserts);
        Assert.Equal((0, 2), (ui.Normal, ui.Background));

        ui.RunAll();
        Assert.Equal(100_000 * 42, output.Document.TextLength);
        Assert.True(inserts < 100, $"{inserts} insertions for 100,000 lines");
        Assert.Equal(0, ui.Normal);
    }

    [AvaloniaFact]
    public void Output_keeps_colors_and_order_when_kinds_alternate_in_a_batch()
    {
        var ui = new QueuedDispatcher();
        var output = new OutputViewModel(ui, 1_000_000);
        var written = new List<string>();
        output.Written += (_, e) => written.Add(e.Text);
        output.WriteLine("build started", OutputKind.Info);
        output.WriteLine("hello");
        output.WriteLine("oops", OutputKind.Error);
        output.WriteLine("bye");
        ui.RunAll();

        Assert.Equal("build started\nhello\noops\nbye\n", output.Document.Text);
        Assert.Equal(new[] { "build started\n", "hello\n", "oops\n", "bye\n" }, written);
        Assert.Equal(2, output.Segments.Count);
        Assert.Equal(("build started\n".Length + "hello\n".Length, "oops\n".Length), (output.Segments[1].Start, output.Segments[1].Length));
    }

    [Fact]
    public void The_terminal_feeds_a_flood_to_its_emulator_in_bounded_batches()
    {
        using var temp = new TempDirectory();
        using var toolchains = new ToolchainServices(new AppPaths(temp.Path));
        var ui = new QueuedDispatcher();
        using var terminal = new TerminalViewModel(toolchains, ui, () => temp.Path, _ => { });
        var queue = (ConcurrentQueue<byte[]>)typeof(TerminalViewModel).GetField("_output", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(terminal)!;
        var fed = 0;
        terminal.ShellOutput += (_, bytes) => fed += bytes.Length;
        var line = "y\r\n"u8.ToArray();
        var chunk = Enumerable.Repeat(line, 16384 / line.Length).SelectMany(b => b).ToArray();
        for (var i = 0; i < 128; i++)
        {
            queue.Enqueue(chunk);
        }

        // As the shell's reader does: one batch queued behind input and drawing.
        typeof(TerminalViewModel).GetMethod("FlushQueued", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(terminal, null);
        Assert.InRange(fed, TerminalViewModel.MaxBytesPerFlush, TerminalViewModel.MaxBytesPerFlush + chunk.Length);
        Assert.Equal((0, 1), (ui.Normal, ui.Background));
        ui.RunAll();
        Assert.Equal(128 * chunk.Length, fed);
    }

    [Fact]
    public void Test_results_from_the_runners_thread_are_shown_in_one_batch()
    {
        var ui = new QueuedDispatcher();
        var project = new ProjectInfo { Name = "Big.Tests", FilePath = "/work/Big.Tests/Big.Tests.csproj" };
        var explorer = new TestExplorerViewModel(new NoRuns(), ui);
        explorer.SetProjects([project]);
        var cases = Enumerable.Range(0, 2000).Select(i => new TestCase
        {
            Id = $"Big.Tests.Feature{i / 50}Tests.Behaves_{i % 50}",
            FullyQualifiedName = $"Big.Tests.Feature{i / 50}Tests.Behaves_{i % 50}",
            DisplayName = $"Behaves_{i % 50}",
            Framework = TestFramework.XUnit,
            AssemblyName = "Big.Tests",
            TypeFullName = $"Big.Tests.Feature{i / 50}Tests",
            MethodName = $"Behaves_{i % 50}",
        }).ToList();
        explorer.SetBuiltTests(project, cases);
        explorer.ReportQueued(project.FilePath, cases);
        ui.RunAll();
        ui.ResetCounts();

        Parallel.ForEach(cases, test =>
        {
            explorer.QueueStarted(project.FilePath, test);
            explorer.QueueFinished(project.FilePath, new TestResult { Test = test, Outcome = TestOutcome.Passed });
        });

        Assert.Equal((0, 1), (ui.Normal, ui.Background));
        ui.RunAll();
        Assert.Equal(2000, explorer.PassedCount);
        Assert.Equal(TestState.Passed, explorer.Rows.First(r => r.Kind == TestNodeKind.Project).State);
    }

    [Fact]
    public async Task Local_AI_reads_the_file_once_typing_pauses_not_on_every_keystroke()
    {
        var asked = new List<string>();
        using var controller = new InlineCompletionController(
            (_, text, _, _, _) =>
            {
                asked.Add(text);
                return Task.FromResult<string?>("done");
            },
            TimeSpan.FromMilliseconds(100));
        var reads = 0;
        var neighbours = 0;
        var keystrokes = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            var typed = new string('x', i + 1);
            keystrokes.Add(controller.RequestAsync("a.py", () => { reads++; return typed; }, i + 1, () => { neighbours++; return []; }));
        }

        await Task.WhenAll(keystrokes);

        // Five keystrokes: one read of the text and its neighbours, after the last, for the one request that went out.
        Assert.Equal((1, 1), (reads, neighbours));
        Assert.Equal(new[] { "xxxxx" }, asked);
        Assert.Equal("done", controller.Text);
    }

    /// <summary>A UI thread that only queues, and runs what was queued when the test says, counting how it was asked.</summary>
    private sealed class QueuedDispatcher : IUiDispatcher
    {
        private readonly ConcurrentQueue<Action> _queue = new();

        public int Normal { get; private set; }

        public int Background { get; private set; }

        public bool CheckAccess() => true;

        public void Post(Action action)
        {
            Normal++;
            _queue.Enqueue(action);
        }

        public void PostBackground(Action action)
        {
            Background++;
            _queue.Enqueue(action);
        }

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public void ResetCounts() => (Normal, Background) = (0, 0);

        public void RunOne()
        {
            Assert.True(_queue.TryDequeue(out var action));
            action();
        }

        public void RunAll()
        {
            for (var i = 0; _queue.TryDequeue(out var action); i++)
            {
                Assert.True(i < 100_000, "the queue never empties");
                action();
            }
        }
    }

    private sealed class NoRuns : ITestRunHost
    {
        public bool CanStartTestRun => true;

        public Task RunTestsAsync(TestRunRequest request) => Task.CompletedTask;

        public void CancelTestRun()
        {
        }

        public Task NavigateToAsync(string path, int line, int column) => Task.CompletedTask;
    }
}
