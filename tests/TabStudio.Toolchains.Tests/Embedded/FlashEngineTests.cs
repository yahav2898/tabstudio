using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class FlashEngineTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void Probe_identity_does_not_claim_target_chip()
    {
        var matches = new EmbeddedDeviceDatabase().Identify(new("attached-1", 0x0483, 0x3748, "ST-Link", []));
        var candidate = Assert.Single(matches);
        Assert.Equal("openocd", candidate.Route);
        Assert.Null(candidate.Target);
        Assert.Equal(DeviceMatchSource.UsbIdentity, candidate.Source);
    }

    [Fact]
    public void Generic_serial_adapter_does_not_claim_board()
    {
        var candidate = Assert.Single(new EmbeddedDeviceDatabase().Identify(new("adapter", 0x10C4, 0xEA60, "CP210x", [])));
        Assert.Equal("serial", candidate.Route);
        Assert.Null(candidate.Target);
    }

    [Fact]
    public async Task Database_round_trip_preserves_specific_serial_and_user_evidence()
    {
        var database = new EmbeddedDeviceDatabase();
        database.Remember(new(0x1234, 0x5678, "serial-a", "My board", "esptool", "esp32"));
        database.Remember(new(0x1234, 0x5678, "serial-a", "Renamed board", "esptool", "esp32s3"));
        var path = _temp.Resolve("devices.json");
        await database.SaveAsync(path);
        var restored = await EmbeddedDeviceDatabase.LoadAsync(path);
        Assert.Single(restored.Saved);
        Assert.Empty(restored.Identify(new("b", 0x1234, 0x5678, "Other", []), "serial-b"));
        var candidate = Assert.Single(restored.Identify(new("a", 0x1234, 0x5678, "Selected", []), "serial-a"));
        Assert.Equal("esp32s3", candidate.Target);
        Assert.Equal(DeviceMatchSource.UserSelection, candidate.Source);
        Assert.Equal("Renamed board", candidate.Name);
    }

    [Fact]
    public void Device_database_rejects_invalid_usb_ids() => Assert.Throws<ArgumentOutOfRangeException>(() =>
        new EmbeddedDeviceDatabase().Remember(new(-1, 1, null, "bad", "serial", null)));

    [Fact]
    public void Configuration_confirmation_binds_device_values_and_consequences()
    {
        var change = new TargetConfigurationChange("board serial A", TargetConfigurationKind.OptionBytes, "BOR", 0, 1, true, "Reset at 2.7 V");
        var confirmation = FlashSafety.RequiredConfirmation(change);
        Assert.Contains("0x0 -> 0x1", confirmation, StringComparison.Ordinal);
        Assert.Contains("reversible", confirmation, StringComparison.Ordinal);
        FlashSafety.AuthorizeConfigurationChange(change, confirmation);
        Assert.Throws<InvalidOperationException>(() => FlashSafety.AuthorizeConfigurationChange(change with { DeviceIdentity = "board serial B" }, confirmation));
        Assert.Throws<InvalidOperationException>(() => FlashSafety.AuthorizeConfigurationChange(change with { NewValue = 2 }, confirmation));
    }

    [Fact]
    public void Efuses_and_permanent_readout_protection_are_refused_even_with_confirmation()
    {
        var efuse = new TargetConfigurationChange("esp", TargetConfigurationKind.Efuses, "FLASH_CRYPT_CNT", 0, 1, false, "Permanent");
        Assert.Throws<NotSupportedException>(() => FlashSafety.RequiredConfirmation(efuse));
        var rdp = new TargetConfigurationChange("stm", TargetConfigurationKind.ReadoutProtection, "RDP", 0xAA, 0xCC, false, "Permanent", 2);
        Assert.Throws<NotSupportedException>(() => FlashSafety.AuthorizeConfigurationChange(rdp, "CONFIRM"));
        Assert.Throws<NotSupportedException>(() => FlashSafety.RequiredConfirmation(rdp with { Kind = TargetConfigurationKind.OptionBytes, ReadoutProtectionLevel = null }));
        Assert.Throws<NotSupportedException>(() => FlashSafety.RequiredConfirmation(efuse with { Kind = TargetConfigurationKind.Fuses, Register = "EFUSE_SPI_BOOT_CRYPT_CNT" }));
    }

    [Fact]
    public void Firmware_cannot_escape_application_region_or_overflow()
    {
        FlashSafety.ValidateFirmware([new(0x8000000, 16)], [new(0x8000000, 1024)]);
        Assert.Throws<InvalidOperationException>(() => FlashSafety.ValidateFirmware([new(0x1FFF0000, 16)], [new(0x8000000, 1024)]));
        Assert.Throws<OverflowException>(() => FlashSafety.ValidateFirmware([new(ulong.MaxValue, 2)], [new(0, ulong.MaxValue)]));
        Assert.Throws<InvalidOperationException>(() => FlashSafety.ValidateFirmware([], [new(0, 10)]));
    }

    [Fact]
    public void Hex_checksums_extended_addresses_and_eof_are_validated()
    {
        var ranges = FirmwareImageInspector.IntelHex(":020000040800F2\n:0400000001020304F2\n:00000001FF\n");
        Assert.Equal(new FlashAddressRange(0x08000000, 4), Assert.Single(ranges));
        Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.IntelHex(":0400000001020304F3\n:00000001FF"));
        Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.IntelHex(":0400000001020304F2"));
        Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.IntelHex(":00000001FF\n:0400000001020304F2"));
        Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.IntelHex(":0400000001020304F2\n:0400000001020304F2\n:00000001FF"));
    }

    [Fact]
    public void Uf2_validates_magic_family_block_counts_and_programmed_ranges()
    {
        var bytes = Uf2();
        Assert.Equal(new FlashAddressRange(0x10000000, 4), Assert.Single(FirmwareImageInspector.Uf2(bytes)));
        bytes[508] = 0;
        Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.Uf2(bytes));
        bytes = Uf2();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1); // non-main-flash payload
        Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.Uf2(bytes));
    }

    [Fact]
    public void Elf_uses_physical_load_address_and_checks_segment_file_bounds()
    {
        var bytes = Elf();
        Assert.Equal(new FlashAddressRange(0x08000000, 4), Assert.Single(FirmwareImageInspector.Elf(bytes)));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52 + 16), 0xFFFF);
        Assert.Throws<InvalidDataException>(() => FirmwareImageInspector.Elf(bytes));
    }

    [Fact]
    public void Planner_validates_actual_image_even_when_caller_claims_safe_ranges()
    {
        var image = _temp.Write("unsafe.hex", ":020000041FFFDC\n:0400000001020304F2\n:00000001FF\n");
        var request = Request(EmbeddedFlashProtocol.OpenOcd, image) with
        {
            FirmwareRegions = [new(0x08000000, 4)],
            InterfaceConfig = "interface/stlink.cfg",
            TargetConfig = "target/stm32f4x.cfg",
        };
        Assert.Throws<InvalidOperationException>(() => EmbeddedFlashPlanner.Plan(request));
    }

    [Theory]
    [InlineData(EmbeddedFlashProtocol.Esptool, "esptool.py")]
    [InlineData(EmbeddedFlashProtocol.Stm32Flash, "stm32flash")]
    [InlineData(EmbeddedFlashProtocol.Bossac, "bossac")]
    [InlineData(EmbeddedFlashProtocol.Picotool, "picotool")]
    [InlineData(EmbeddedFlashProtocol.Avrdude, "avrdude")]
    public void Upload_plans_keep_file_paths_as_arguments_and_include_verification(EmbeddedFlashProtocol protocol, string command)
    {
        var image = _temp.Resolve("image with spaces.bin");
        File.WriteAllBytes(image, [1, 2, 3, 4]);
        var plan = EmbeddedFlashPlanner.Plan(Request(protocol, image));
        Assert.Equal(command, plan.Steps[0].Program);
        Assert.Equal(image, plan.FirmwarePath);
        Assert.NotNull(plan.FirmwareSha256);
        Assert.DoesNotContain(plan.Steps, x => x.Program is "sh" or "bash");
        Assert.DoesNotContain(plan.Steps.SelectMany(x => x.Arguments), x => x is "-V" or "--no-verify" or "--force");
    }

    [Fact]
    public void Dfu_plans_independent_readback_before_success()
    {
        var image = _temp.Resolve("firmware.bin");
        File.WriteAllBytes(image, [1, 2, 3, 4]);
        var plan = EmbeddedFlashPlanner.Plan(Request(EmbeddedFlashProtocol.Stm32Dfu, image) with { UsbSelector = "0483:df11" });
        Assert.Equal(2, plan.Steps.Count);
        Assert.Contains("-D", plan.Steps[0].Arguments);
        Assert.Contains("-U", plan.Steps[1].Arguments);
        Assert.NotNull(plan.ReadbackPath);
        Assert.DoesNotContain(plan.Steps.SelectMany(x => x.Arguments), x => x.Contains("leave", StringComparison.Ordinal));
    }

    [Fact]
    public void Openocd_quotes_tcl_substitutions_in_filenames()
    {
        var image = _temp.Resolve("firmware[exec].bin");
        File.WriteAllBytes(image, [1, 2, 3, 4]);
        var plan = EmbeddedFlashPlanner.Plan(Request(EmbeddedFlashProtocol.OpenOcd, image) with
        {
            InterfaceConfig = "interface/stlink.cfg",
            TargetConfig = "target/stm32f4x.cfg",
        });
        Assert.Contains("firmware\\[exec\\].bin", plan.Steps[0].Arguments[^1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Backup_reads_twice_and_refuses_a_mismatch_or_overwrite()
    {
        var path = _temp.Resolve("backup.bin");
        var reads = 0;
        var hash = await FirmwareBackup.CreateAsync(path, async (stream, token) =>
        {
            reads++;
            await stream.WriteAsync(new byte[] { 1, 2, 3 }, token);
        });
        Assert.Equal(2, reads);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(new byte[] { 1, 2, 3 })), hash);
        await Assert.ThrowsAsync<IOException>(() => FirmwareBackup.CreateAsync(path, (_, _) => Task.CompletedTask));
        reads = 0;
        var badPath = _temp.Resolve("bad-backup.bin");
        await Assert.ThrowsAsync<InvalidDataException>(() => FirmwareBackup.CreateAsync(badPath, async (stream, token) =>
        {
            await stream.WriteAsync(new byte[] { (byte)++reads }, token);
        }));
        Assert.False(File.Exists(badPath));
    }

    [Fact]
    public async Task Dump_diff_counts_trailing_bytes_and_bounds_returned_ranges()
    {
        var left = _temp.Resolve("left.bin");
        var right = _temp.Resolve("right.bin");
        File.WriteAllBytes(left, [0, 0, 0, 0, 0]);
        File.WriteAllBytes(right, [1, 0, 1, 0, 1, 1]);
        var difference = await FirmwareBackup.CompareAsync(left, right, maximumRanges: 2);
        Assert.False(difference.Identical);
        Assert.Equal(4, difference.DifferentBytes);
        Assert.Equal(2, difference.Ranges.Count);
        Assert.True(difference.Truncated);
    }

    [Fact]
    public async Task Production_batch_limits_parallelism_and_logs_every_board_result()
    {
        var running = 0;
        var peak = 0;
        var jobs = Enumerable.Range(0, 8).Select(index => new ProductionFlashJob("board-" + index, async token =>
        {
            var current = Interlocked.Increment(ref running);
            int previous;
            do
            {
                previous = peak;
            }
            while (current > previous && Interlocked.CompareExchange(ref peak, current, previous) != previous);
            try
            {
                await Task.Delay(20, token);
                if (index == 3)
                {
                    throw new IOException("Verify failed");
                }
                return new EmbeddedFlashResult("digest", TimeSpan.Zero, 1);
            }
            finally
            {
                Interlocked.Decrement(ref running);
            }
        })).ToArray();
        var path = _temp.Resolve("production.jsonl");
        var results = await ProductionFlasher.RunAsync(jobs, path, parallelism: 2);
        Assert.InRange(peak, 1, 2);
        Assert.Equal(8, results.Count);
        Assert.Single(results, x => !x.Verified);
        Assert.Equal(8, File.ReadAllLines(path).Length);
    }

    [Fact]
    public async Task Production_batch_rejects_duplicate_physical_device()
    {
        var job = new ProductionFlashJob("one-board", _ => Task.FromResult(new EmbeddedFlashResult("digest", TimeSpan.Zero, 1)));
        await Assert.ThrowsAsync<ArgumentException>(() => ProductionFlasher.RunAsync([job, job], _temp.Resolve("bad.jsonl")));
    }

    [Fact]
    public void Ota_authentication_matches_independent_md5_and_pbkdf2_vectors()
    {
        var legacy = ArduinoOtaUploader.CreateAuthenticationResponse("secret", new('a', 32), new('b', 32));
        Assert.Equal("200 " + new string('b', 32) + " ebec5ac889f8f1d23b3711d956aa7ab5\n", legacy);
        var modern = ArduinoOtaUploader.CreateAuthenticationResponse("secret", new('a', 64), new('b', 64));
        Assert.Equal("200 " + new string('b', 64) + " 4bc0b72433fb7bd4f3ba09edac8058f445555bae956518534bdedb7674601a80\n", modern);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Ota_transfers_over_real_loopback_udp_tcp_and_requires_final_acceptance(bool authenticate)
    {
        var payload = Enumerable.Range(0, 2500).Select(x => (byte)x).ToArray();
        var path = _temp.Resolve("ota.bin");
        File.WriteAllBytes(path, payload);
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var target = (IPEndPoint)device.Client.LocalEndPoint!;
        var emulatedBoard = SimulateBoardAsync(device, payload, authenticate, timeout.Token);
        var result = await ArduinoOtaUploader.UploadAsync(new(path, target, "secret"), cancellationToken: timeout.Token);
        await emulatedBoard;
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(payload)), result.FirmwareSha256);
    }

    [Fact]
    public async Task Ota_rejected_final_response_never_reports_success()
    {
        byte[] payload = [1, 2, 3, 4];
        var path = _temp.Resolve("rejected.bin");
        File.WriteAllBytes(path, payload);
        using var device = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var emulatedBoard = SimulateBoardAsync(device, payload, false, deadline.Token, accept: false);
        await Assert.ThrowsAsync<IOException>(() => ArduinoOtaUploader.UploadAsync(new(path, (IPEndPoint)device.Client.LocalEndPoint!), cancellationToken: deadline.Token));
        await emulatedBoard;
    }

    [Fact]
    public void Readback_plan_cannot_disable_protection_and_requires_one_explicit_interval()
    {
        var request = Request(EmbeddedFlashProtocol.OpenOcd, "unused.bin") with
        {
            InterfaceConfig = "interface/stlink.cfg",
            TargetConfig = "target/stm32f4x.cfg",
        };
        var plan = EmbeddedReadbackPlanner.Plan(request, _temp.Resolve("backup.bin"));
        Assert.Equal("openocd", plan.Program);
        Assert.Contains("dump_image", plan.Arguments[^1], StringComparison.Ordinal);
        Assert.DoesNotContain("unlock", plan.CommandLine, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("erase", plan.CommandLine, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidOperationException>(() => EmbeddedReadbackPlanner.Plan(request with { ApplicationFlash = [] }, _temp.Resolve("backup.bin")));
        Assert.Throws<NotSupportedException>(() => EmbeddedReadbackPlanner.Plan(request with { Protocol = EmbeddedFlashProtocol.Esptool }, _temp.Resolve("backup.bin")));
    }

    [Fact]
    public async Task Executor_checks_firmware_stability_and_stops_after_the_first_failed_tool()
    {
        var image = _temp.Resolve("stable.bin");
        File.WriteAllBytes(image, [1, 2, 3, 4]);
        var digest = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(image)));
        var plan = new EmbeddedFlashPlan([new() { Program = "test-tool" }, new() { Program = "test-tool" }], false, "test-only", FirmwarePath: image, FirmwareSha256: digest);
        var runtime = new FakeFlashRuntime(exitCode: 1);
        await Assert.ThrowsAsync<IOException>(() => EmbeddedFlashExecutor.ExecuteAsync(runtime, plan));
        Assert.Single(runtime.Started);
        File.WriteAllBytes(image, [4, 3, 2, 1]);
        runtime = new FakeFlashRuntime(exitCode: 0);
        await Assert.ThrowsAsync<InvalidDataException>(() => EmbeddedFlashExecutor.ExecuteAsync(runtime, plan));
        Assert.Empty(runtime.Started);
    }

    [Fact]
    public async Task Executor_keeps_bridge_transform_and_never_runs_without_required_touch()
    {
        var image = _temp.Resolve("firmware.bin");
        File.WriteAllBytes(image, [1]);
        var plan = new EmbeddedFlashPlan([new() { Program = "test-tool" }], false, "test-only", FirmwarePath: image);
        var runtime = new FakeFlashRuntime(exitCode: 0);
        var result = await EmbeddedFlashExecutor.ExecuteAsync(runtime, plan, command => command with { Environment = new Dictionary<string, string?> { ["bridge"] = "selected-device" } });
        Assert.Equal(1, result.StepsCompleted);
        Assert.Equal("selected-device", Assert.Single(runtime.Started).Environment["bridge"]);
        runtime = new FakeFlashRuntime(exitCode: 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => EmbeddedFlashExecutor.ExecuteAsync(runtime, plan with { Touch1200Baud = true }));
        Assert.Empty(runtime.Started);
    }

    private static async Task SimulateBoardAsync(UdpClient device, byte[] expected, bool authenticate, CancellationToken token, bool accept = true)
    {
        var invite = await device.ReceiveAsync(token);
        var fields = Encoding.ASCII.GetString(invite.Buffer).Trim().Split(' ');
        Assert.Equal("0", fields[0]);
        Assert.Equal(expected.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), fields[2]);
        if (authenticate)
        {
            var nonce = new string('a', 64);
            await device.SendAsync(Encoding.ASCII.GetBytes("AUTH " + nonce), invite.RemoteEndPoint, token);
            var auth = await device.ReceiveAsync(token);
            var authFields = Encoding.ASCII.GetString(auth.Buffer).Trim().Split(' ');
            Assert.Equal(ArduinoOtaUploader.CreateAuthenticationResponse("secret", nonce, authFields[1]).Trim(), Encoding.ASCII.GetString(auth.Buffer).Trim());
            invite = auth;
        }
        await device.SendAsync("OK"u8.ToArray(), invite.RemoteEndPoint, token);
        using var tcp = new TcpClient();
        await tcp.ConnectAsync(invite.RemoteEndPoint.Address, int.Parse(fields[1], System.Globalization.CultureInfo.InvariantCulture), token);
        await using var stream = tcp.GetStream();
        var bytes = new byte[expected.Length];
        for (var offset = 0; offset < bytes.Length; offset += 1024)
        {
            var count = Math.Min(1024, bytes.Length - offset);
            await stream.ReadExactlyAsync(bytes.AsMemory(offset, count), token);
            var response = count.ToString(System.Globalization.CultureInfo.InvariantCulture) + (offset + count == bytes.Length ? accept ? "OK" : "NOTOK" : string.Empty);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response), token);
        }
        Assert.Equal(expected, bytes);
    }

    private EmbeddedFlashRequest Request(EmbeddedFlashProtocol protocol, string path) => new()
    {
        Protocol = protocol,
        FirmwarePath = path,
        WorkingDirectory = _temp.Path,
        Target = protocol == EmbeddedFlashProtocol.Esptool ? "esp32c3" : "m328p",
        SerialPort = "/dev/ttyUSB0",
        Programmer = "arduino",
        BinaryAddress = 0x08000000,
        ApplicationFlash = [new(0x08000000, 4096)],
    };

    private static byte[] Uf2()
    {
        var bytes = new byte[512];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x0A324655);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 0x9E5D5157);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12), 0x10000000);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(16), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(24), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(508), 0x0AB16F30);
        return bytes;
    }

    private static byte[] Elf()
    {
        var bytes = new byte[260];
        new byte[] { 0x7F, (byte)'E', (byte)'L', (byte)'F', 1, 1, 1 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28), 52);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 256);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(60), 0x20000000); // RAM VMA
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(64), 0x08000000); // flash LMA
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 4);
        return bytes;
    }

    private sealed class FakeFlashRuntime(int exitCode) : IToolchainRuntime
    {
        public List<ProcessStartSpec> Started { get; } = [];
        public string DisplayName => "Test runtime";
        public RuntimeKind Kind => RuntimeKind.Host;
        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            Started.Add(spec);
            return new ToolchainProcess(new FakeHandle(exitCode), spec);
        }
        public bool HasCommand(string command) => true;
        public string? Resolve(string command) => command;
        public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => throw new NotSupportedException();
        public void ShareFolder(string folder) { }
        public string? ToDevicePath(string programPath, string? workingDirectory) => programPath;
    }

    private sealed class FakeHandle(int exitCode) : IProcessHandle
    {
        public int Id => 1;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; } = new MemoryStream("tool output"u8.ToArray());
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => Task.FromResult<(int, int?)>((exitCode, null));
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() { }
        public void Dispose() { Input.Dispose(); Output.Dispose(); }
    }

    public void Dispose() => _temp.Dispose();
}
