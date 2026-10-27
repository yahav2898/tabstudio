using System.Text;
using TabStudio.Toolchains.Embedded.Testing;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedSerialTestTests
{
    private const string Complete = "test_sensor.c:9:test_ready:PASS\n1 Tests 0 Failures 0 Ignored\nOK\n";

    [Fact]
    public void Capture_requires_explicit_baud_and_keeps_actual_attachment_separate_from_operator_label()
    {
        var options = new EmbeddedSerialTestOptions();
        Assert.Throws<InvalidDataException>(options.Validate);
        options = options with { BaudRate = 230400, BoardLabel = "Sensor prototype" };
        var identity = options.BoardIdentity(Device());
        Assert.Contains("Sensor prototype", identity, StringComparison.Ordinal);
        Assert.Contains("2341:0043", identity, StringComparison.Ordinal);
        Assert.Contains("/dev/bus/usb/001/012", identity, StringComparison.Ordinal);
        Assert.NotEqual(identity, options.BoardIdentity(Device() with { Id = "/dev/bus/usb/001/013" }));
        Assert.Throws<InvalidDataException>(() => (options with { TimeoutSeconds = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (options with { InterfaceNumber = 256 }).Validate());
        Assert.Throws<InvalidDataException>(() => (options with { BoardLabel = "claimed\nchip" }).Validate());
    }

    [Fact]
    public async Task Full_stream_is_captured_beyond_tail_limit_with_actual_device_identity_and_no_upload_command()
    {
        var text = string.Join('\n', Enumerable.Range(1, 1200).Select(index => $"sensor.c:{index}:test_{index}:PASS"))
            + "\n1200 Tests 0 Failures 0 Ignored\nOK\n";
        var runtime = new FakeRuntime(Encoding.UTF8.GetBytes(text));
        using var connection = new Connection();
        var displayed = 0;
        var run = await EmbeddedSerialTestRunner.RunAsync(runtime, connection, Options(), "/workspace", "/workspace/.tabstudio/dev/test", (_, _) => displayed++);
        Assert.True(run.HarnessCompleted);
        Assert.True(run.Exit.Succeeded);
        Assert.Equal(1200, run.Results.Count);
        Assert.Equal(1202, displayed);
        Assert.All(run.Results, result =>
        {
            Assert.Equal(EmbeddedTestOutcome.Passed, result.Outcome);
            Assert.Equal(run.BoardIdentity, result.BoardId);
        });
        var command = Assert.Single(runtime.Started);
        Assert.Equal("python3", command.Program);
        Assert.Contains("--preserve-lines", command.Arguments);
        Assert.Contains("/opt/tabstudio/embedded/serial_unity_reader.py", command.Arguments);
        Assert.DoesNotContain(command.Arguments, value => value is "flash" or "run" or "reset" or "--touch");
        Assert.Equal(3, command.InheritedDescriptors[77]);
        Assert.False(connection.Disposed); // The caller retains it for the entire worker lifetime.
    }

    [Theory]
    [InlineData("test.c:1:only:PASS\n", 0)]
    [InlineData("test.c:1:only:PASS\n1 Tests 0 Failures 0 Ignored\n", 0)]
    [InlineData("test.c:1:only:PASS\n1 Tests 0 Failures 0 Ignored\nOK", 0)]
    [InlineData("test.c:1:only:PASS\n2 Tests 0 Failures 0 Ignored\nOK\n", 0)]
    [InlineData("test.c:1:only:PASS\n1 Tests 0 Failures 0 Ignored\nOK\n", 1)]
    [InlineData("test.c:1:only:FAIL:bad\n1 Tests 1 Failures 0 Ignored\nOK\n", 0)]
    [InlineData("0 Tests 0 Failures 0 Ignored\nOK\n", 0)]
    public async Task Disconnect_truncation_nonzero_missing_or_mismatched_completion_never_passes(string text, int exitCode)
    {
        using var connection = new Connection();
        var run = await EmbeddedSerialTestRunner.RunAsync(new FakeRuntime(Encoding.UTF8.GetBytes(text), exitCode), connection,
            Options(), "/workspace", "/workspace/.tabstudio/dev/test");
        Assert.False(run.HarnessCompleted);
        Assert.Contains(run.Results, result => result.Outcome == EmbeddedTestOutcome.Errored);
    }

    [Fact]
    public async Task Assertion_failures_remain_completed_failed_tests_instead_of_becoming_passed_transport_results()
    {
        using var connection = new Connection();
        var run = await EmbeddedSerialTestRunner.RunAsync(new FakeRuntime(Encoding.UTF8.GetBytes("test.c:1:only:FAIL:bad\n1 Tests 1 Failures 0 Ignored\nFAILED\n")),
            connection, Options(), "/workspace", "/workspace/.tabstudio/dev/test");
        Assert.True(run.HarnessCompleted);
        Assert.Equal(EmbeddedTestOutcome.Failed, Assert.Single(run.Results).Outcome);
    }

    [Fact]
    public async Task Invalid_utf8_oversize_lines_and_results_only_on_stderr_never_pass()
    {
        foreach (var bytes in new[] { new byte[] { 0xff, 0x0a }, Encoding.UTF8.GetBytes(new string('x', 65537) + "\n") })
        {
            using var connection = new Connection();
            var run = await EmbeddedSerialTestRunner.RunAsync(new FakeRuntime(bytes), connection, Options(), "/workspace", "/workspace/.tabstudio/dev/test");
            Assert.False(run.HarnessCompleted);
            Assert.Contains(run.Results, result => result.Outcome == EmbeddedTestOutcome.Errored);
        }
        using var other = new Connection();
        var stderr = await EmbeddedSerialTestRunner.RunAsync(new FakeRuntime([], error: Encoding.UTF8.GetBytes(Complete)), other,
            Options(), "/workspace", "/workspace/.tabstudio/dev/test");
        Assert.False(stderr.HarnessCompleted);
        Assert.DoesNotContain(stderr.Results, result => result.Outcome == EmbeddedTestOutcome.Passed);
    }

    [Fact]
    public async Task Stop_and_timeout_end_the_worker_and_connection_is_released_by_the_owner()
    {
        var connection = new Connection();
        var runtime = new FakeRuntime([], hold: true);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        try
        {
            var run = await EmbeddedSerialTestRunner.RunAsync(runtime, connection, Options(), "/workspace", "/workspace/.tabstudio/dev/test",
                cancellationToken: cancellation.Token);
            Assert.True(run.Exit.WasStopped);
            Assert.False(run.HarnessCompleted);
            Assert.Contains(run.Results, result => result.Name == "[serial transport]" && result.Outcome == EmbeddedTestOutcome.Errored);
        }
        finally
        {
            connection.Dispose();
        }
        Assert.True(connection.Disposed);
        using var second = new Connection();
        var timedOut = await EmbeddedSerialTestRunner.RunAsync(new FakeRuntime([], hold: true), second,
            Options() with { TimeoutSeconds = 1 }, "/workspace", "/workspace/.tabstudio/dev/test");
        Assert.True(timedOut.TimedOut);
        Assert.True(timedOut.Exit.WasStopped);
        Assert.False(timedOut.HarnessCompleted);
    }

    private static EmbeddedSerialTestOptions Options() => new() { BaudRate = 230400, BoardLabel = "label only" };
    private static UsbDeviceInfo Device() => new("/dev/bus/usb/001/012", 0x2341, 0x0043, "USB serial adapter", []);

    private sealed class Connection : IUsbConnection
    {
        public UsbDeviceInfo Device => EmbeddedSerialTestTests.Device();
        public int FileDescriptor => 77;
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeRuntime(byte[] bytes, int exitCode = 0, byte[]? error = null, bool hold = false) : IToolchainRuntime
    {
        public List<ProcessStartSpec> Started { get; } = [];
        public string DisplayName => "Synthetic serial worker output";
        public RuntimeKind Kind => RuntimeKind.Host;
        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            Started.Add(spec);
            return new ToolchainProcess(new Handle(bytes, exitCode, error, hold), spec);
        }
        public bool HasCommand(string command) => true;
        public string? Resolve(string command) => command;
        public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => throw new NotSupportedException();
        public void ShareFolder(string folder) { }
        public string? ToDevicePath(string programPath, string? workingDirectory) => programPath;
    }

    private sealed class Handle : IProcessHandle
    {
        private readonly TaskCompletionSource<(int, int?)> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Handle(byte[] bytes, int exitCode, byte[]? error, bool hold)
        {
            Output = new MemoryStream(bytes);
            Error = error is null ? null : new MemoryStream(error);
            if (!hold)
            {
                _exit.SetResult((exitCode, null));
            }
        }
        public int Id => 1;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; }
        public Stream? Error { get; }
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit.Task;
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() => _exit.TrySetResult((137, 9));
        public void Dispose() { Input.Dispose(); Output.Dispose(); Error?.Dispose(); }
    }
}
