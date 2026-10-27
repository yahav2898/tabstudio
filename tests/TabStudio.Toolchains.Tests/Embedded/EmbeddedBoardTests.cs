using System.Buffers.Binary;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Testing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EmbeddedBoardTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Plan_binds_actual_application_image_chip_probe_and_safe_native_run_flags()
    {
        using var plan = Plan();
        Assert.Equal("run", plan.Command.Arguments[0]);
        Assert.Equal(plan.FirmwarePath, plan.Command.Arguments[^1]);
        Assert.Contains("--verify", plan.Command.Arguments);
        Assert.Contains("--restore-unwritten", plan.Command.Arguments);
        Assert.Contains("--preverify", plan.Command.Arguments);
        Assert.Contains("0x20000020", plan.Command.Arguments);
        Assert.Contains("{m}", plan.Command.Arguments);
        Assert.DoesNotContain(plan.Command.Arguments, argument => argument is "--chip-erase" or "--allow-erase-all" or "--connect-under-reset" or "--reset");
        Assert.Null(plan.Command.Environment["PROBE_RS_ALLOW_ERASE_ALL"]);
        Assert.Null(plan.Command.Environment["PROBE_RS_PREFER_FLASH_ALGO"]);
        File.WriteAllText(_temp.Resolve("firmware.elf"), "rebuilt after validation");
        plan.VerifySnapshot();
        Assert.Equal(64, plan.FirmwareSha256.Length);
    }

    [Fact]
    public void Planning_refuses_forged_ranges_wrong_chip_mismatched_elf_probe_and_host_harness()
    {
        var (flash, debug) = Profiles();
        var options = new EmbeddedBoardTestOptions { BoardId = "actual-board-serial-9" };
        Assert.Throws<InvalidOperationException>(() => EmbeddedBoardTestPlan.Create(options, flash with { ApplicationFlash = [new(0x1fff0000, 4096)], FirmwareRegions = [new(0x1fff0000, 4)] }, debug, _temp.Path));
        Assert.Throws<InvalidDataException>(() => EmbeddedBoardTestPlan.Create(options, flash with { Target = "different_chip" }, debug, _temp.Path));
        Assert.Throws<InvalidDataException>(() => EmbeddedBoardTestPlan.Create(options, flash, debug with { Firmware = _temp.Resolve("other.elf") }, _temp.Path));
        Assert.Throws<InvalidDataException>(() => EmbeddedBoardTestPlan.Create(options, flash with { ProbeSelector = "1366:0105:other" }, debug, _temp.Path));
        Assert.Throws<InvalidDataException>(() => EmbeddedBoardTestPlan.Create(options with { Format = EmbeddedTestFormat.GoogleTest }, flash, debug, _temp.Path));
        Assert.Throws<InvalidDataException>(() => EmbeddedBoardTestPlan.Create(options with { BoardId = "" }, flash, debug, _temp.Path));
        Assert.Throws<InvalidDataException>(() => EmbeddedBoardTestPlan.Create(options with { BoardId = "SELECT_ACTUAL_BOARD_SERIAL" }, flash, debug, _temp.Path));
        Assert.Throws<InvalidOperationException>(() => EmbeddedBoardTestPlan.Create(options, flash with { Target = "MK64FN1M0VLL12" }, debug with { Chip = "MK64FN1M0VLL12" }, _temp.Path));
    }

    [Fact]
    public async Task Modified_snapshot_and_changed_native_command_start_no_process()
    {
        using var plan = Plan();
        var runtime = new FakeRuntime("", immediateExit: true);
        await Assert.ThrowsAsync<InvalidDataException>(() => EmbeddedBoardTestRunner.RunAsync(runtime, plan, plan.Command with { Arguments = ["erase"] }));
        var unsafeEnvironment = new Dictionary<string, string?>(plan.Command.Environment) { ["PROBE_RS_ALLOW_ERASE_ALL"] = "1" };
        await Assert.ThrowsAsync<InvalidDataException>(() => EmbeddedBoardTestRunner.RunAsync(runtime, plan, plan.Command with { Environment = unsafeEnvironment }));
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(plan.FirmwarePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.WriteAllText(plan.FirmwarePath, "changed");
        await Assert.ThrowsAsync<InvalidDataException>(() => EmbeddedBoardTestRunner.RunAsync(runtime, plan, plan.Command));
        Assert.Equal(0, runtime.Started);
    }

    [Fact]
    public async Task Unity_full_stream_completes_only_after_matching_summary_and_retains_board_identity()
    {
        using var plan = Plan();
        var output = string.Join('\n', Enumerable.Range(1, 1200).Select(index => $"test.c:{index}:test_{index}:PASS"))
            + "\n1200 Tests 0 Failures 0 Ignored\n";
        var runtime = new FakeRuntime(output);
        var displayed = new List<string>();
        var result = await EmbeddedBoardTestRunner.RunAsync(runtime, plan, plan.Command, (line, _) => displayed.Add(line));
        Assert.True(result.HarnessCompleted);
        Assert.True(result.Exit.WasStopped);
        Assert.Equal(1200, result.Results.Count);
        Assert.All(result.Results, test =>
        {
            Assert.Equal("actual-board-serial-9", test.BoardId);
            Assert.Equal(EmbeddedTestOutcome.Passed, test.Outcome);
        });
        Assert.Equal(1201, displayed.Count);
    }

    [Theory]
    [InlineData("test.c:1:only_test:PASS\n", 0)]
    [InlineData("", 0)]
    [InlineData("test.c:1:only_test:PASS\n", 1)]
    public async Task Exit_or_disconnect_never_turns_an_incomplete_harness_green(string output, int exitCode)
    {
        using var plan = Plan();
        var result = await EmbeddedBoardTestRunner.RunAsync(new FakeRuntime(output, immediateExit: true, exitCode), plan, plan.Command);
        Assert.False(result.HarnessCompleted);
        Assert.Contains(result.Results, test => test.Outcome == EmbeddedTestOutcome.Errored);
    }

    [Fact]
    public async Task Actual_decoded_defmt_test_progress_finishes_native_monitor_and_handles_upstream_channel_prefix()
    {
        using var plan = Plan(EmbeddedTestFormat.DefmtTest);
        var runtime = new FakeRuntime("[defmt] (1/2) running `math::addition`...\n[defmt] (2/2) ignoring `slow`...\n[defmt] all tests passed!\n");
        var result = await EmbeddedBoardTestRunner.RunAsync(runtime, plan, plan.Command);
        Assert.True(result.HarnessCompleted);
        Assert.Equal(2, result.Results.Count);
        Assert.Equal(EmbeddedTestOutcome.Passed, result.Results[0].Outcome);
        Assert.Equal(EmbeddedTestOutcome.Skipped, result.Results[1].Outcome);
    }

    [Fact]
    public async Task Native_embedded_test_rust_report_requires_real_process_success()
    {
        using var plan = Plan(EmbeddedTestFormat.Rust);
        Assert.Contains("--format", plan.Command.Arguments);
        var runtime = new FakeRuntime("running 1 test\ntest math::addition ... ok\ntest result: ok. 1 passed; 0 failed; 0 ignored; 0 measured; 0 filtered out; finished in 0.1s\n", immediateExit: true);
        var result = await EmbeddedBoardTestRunner.RunAsync(runtime, plan, plan.Command);
        Assert.Equal(EmbeddedTestOutcome.Passed, Assert.Single(result.Results).Outcome);
        Assert.False(result.Exit.WasStopped);
    }

    [Fact]
    public void Actual_nonallocated_INFO_test_metadata_is_not_mistaken_for_application_flash()
    {
        var (flash, debug) = Profiles();
        var bytes = File.ReadAllBytes(flash.FirmwarePath);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 2); // No PT_LOAD for INFO metadata.
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(192 + 4 * 40 + 8), 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(192 + 4 * 40 + 12), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(1436), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(824), 1);
        File.WriteAllBytes(flash.FirmwarePath, bytes);
        using var plan = EmbeddedBoardTestPlan.Create(new() { BoardId = "board-info", Format = EmbeddedTestFormat.Rust }, flash, debug, _temp.Path);
        Assert.Contains("--format", plan.Command.Arguments);
        bytes[824] = 99;
        File.WriteAllBytes(flash.FirmwarePath, bytes);
        Assert.Throws<InvalidDataException>(() => EmbeddedBoardTestPlan.Create(new() { BoardId = "board-info", Format = EmbeddedTestFormat.Rust }, flash, debug, _temp.Path));
    }

    [Fact]
    public async Task Oversize_line_is_an_error_and_user_stop_cannot_report_transport_success()
    {
        using var plan = Plan();
        var oversized = await EmbeddedBoardTestRunner.RunAsync(new FakeRuntime(new string('X', 65537) + "\n"), plan, plan.Command);
        Assert.Contains(oversized.Results, test => test.Outcome == EmbeddedTestOutcome.Errored);
        using var cancellation = new CancellationTokenSource();
        var stopped = await EmbeddedBoardTestRunner.RunAsync(new FakeRuntime("test.c:1:one:PASS\n"), plan, plan.Command,
            (_, _) => cancellation.Cancel(), cancellation.Token);
        Assert.Contains(stopped.Results, test => test.Outcome == EmbeddedTestOutcome.Errored && test.Name == "[board transport]");
    }

    [Fact]
    public async Task Configured_timeout_stops_native_monitor_and_marks_the_board_result_incomplete()
    {
        var (flash, debug) = Profiles();
        using var plan = EmbeddedBoardTestPlan.Create(new() { BoardId = "timed-board", TimeoutSeconds = 5 }, flash, debug, _temp.Path);
        var result = await EmbeddedBoardTestRunner.RunAsync(new FakeRuntime(""), plan, plan.Command);
        Assert.True(result.TimedOut);
        Assert.True(result.Exit.WasStopped);
        Assert.Contains(result.Results, test => test.Name == "[board transport]" && test.Outcome == EmbeddedTestOutcome.Errored);
    }

    private EmbeddedBoardTestPlan Plan(EmbeddedTestFormat format = EmbeddedTestFormat.Unity)
    {
        var (flash, debug) = Profiles();
        return EmbeddedBoardTestPlan.Create(new() { BoardId = "actual-board-serial-9", Format = format }, flash, debug, _temp.Path);
    }

    private (EmbeddedFlashRequest Flash, EmbeddedDebugProfile Debug) Profiles()
    {
        var firmware = _temp.Resolve("firmware.elf");
        File.WriteAllBytes(firmware, Elf());
        File.WriteAllBytes(_temp.Resolve("other.elf"), Elf());
        return (new()
        {
            Protocol = EmbeddedFlashProtocol.ProbeRs, Target = "nRF52840_xxAA", FirmwarePath = firmware,
            WorkingDirectory = _temp.Path, ApplicationFlash = [new(0x08000000, 4096)],
        }, new() { Backend = EmbeddedDebugBackend.ProbeRs, Chip = "nRF52840_xxAA", Firmware = firmware, ProbeSelector = "1366:0105:actual-probe-serial" });
    }

    private static byte[] Elf()
    {
        var bytes = new byte[2048];
        void W16(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), value);
        void W32(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F', 1, 1, 1 }.CopyTo(bytes, 0);
        W16(16, 2); W16(18, 40); W32(20, 1); W32(28, 52); W32(32, 192);
        W16(40, 52); W16(42, 32); W16(44, 3); W16(46, 40); W16(48, 8); W16(50, 5);
        void Segment(int index, uint offset, uint address, uint physical, uint fileSize, uint memorySize)
        {
            var at = 52 + index * 32;
            W32(at, 1); W32(at + 4, offset); W32(at + 8, address); W32(at + 12, physical); W32(at + 16, fileSize); W32(at + 20, memorySize);
        }
        Segment(0, 800, 0x08000000, 0x08000000, 16, 16);
        Segment(1, 816, 0x20000000, 0x08000010, 8, 136);
        Segment(2, 824, 0x08000018, 0x08000018, 4, 4);
        var names = Encoding.ASCII.GetBytes("\0.text\0.data\0.bss\0.embedded_test\0.shstrtab\0.strtab\0.symtab\0");
        names.CopyTo(bytes, 1024);
        void Section(int index, uint name, uint type, uint flags, uint address, uint offset, uint size)
        {
            var at = 192 + index * 40;
            W32(at, name); W32(at + 4, type); W32(at + 8, flags); W32(at + 12, address); W32(at + 16, offset); W32(at + 20, size);
        }
        Section(1, 1, 1, 6, 0x08000000, 800, 16);
        Section(2, 7, 1, 3, 0x20000000, 816, 8);
        Section(3, 13, 8, 3, 0x20000008, 824, 128);
        Section(4, 18, 1, 2, 0x08000018, 824, 4);
        Section(5, 33, 3, 0, 0, 1024, (uint)names.Length);
        var strings = Encoding.ASCII.GetBytes("\0_SEGGER_RTT\0EMBEDDED_TEST_VERSION\0");
        strings.CopyTo(bytes, 1200);
        Section(6, 43, 3, 0, 0, 1200, (uint)strings.Length);
        Section(7, 51, 2, 0, 0, 1400, 48);
        W32(192 + 7 * 40 + 24, 6); W32(192 + 7 * 40 + 36, 16);
        W32(1416, 1); W32(1420, 0x20000020); W32(1424, 64); bytes[1428] = 0x11; W16(1430, 3);
        W32(1432, 13); W32(1436, 0x08000018); W32(1440, 4); bytes[1444] = 0x11; W16(1446, 4);
        return bytes;
    }

    private sealed class FakeRuntime(string output, bool immediateExit = false, int exitCode = 0) : IToolchainRuntime
    {
        public int Started { get; private set; }
        public string DisplayName => "Board fixture";
        public RuntimeKind Kind => RuntimeKind.Host;
        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            Started++;
            return new ToolchainProcess(new FakeHandle(output, immediateExit, exitCode), spec);
        }
        public bool HasCommand(string command) => true;
        public string? Resolve(string command) => command;
        public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => throw new NotSupportedException();
        public void ShareFolder(string folder) { }
        public string? ToDevicePath(string programPath, string? workingDirectory) => programPath;
    }

    private sealed class FakeHandle : IProcessHandle
    {
        private readonly TaskCompletionSource<(int, int?)> _exit = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public FakeHandle(string output, bool immediateExit, int exitCode)
        {
            Output = new MemoryStream(Encoding.UTF8.GetBytes(output));
            if (immediateExit) _exit.TrySetResult((exitCode, null));
        }
        public int Id => 1;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; }
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit.Task;
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() => _exit.TrySetResult((137, 9));
        public void Dispose() { Input.Dispose(); Output.Dispose(); }
    }

    public void Dispose() => _temp.Dispose();
}
