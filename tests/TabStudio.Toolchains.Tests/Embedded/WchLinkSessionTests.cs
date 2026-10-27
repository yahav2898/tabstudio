using System.Text;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Installation;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class WchLinkSessionTests
{
    [Fact]
    public void Native_store_recipe_retains_exact_source_lock_and_never_routes_musl_client_to_glibc()
    {
        var definition = ToolchainCatalog.Default.Find(EmbeddedWlinkInstallation.Id)!;
        Assert.True(definition.Auxiliary);
        Assert.False(definition.Listed); // Enabled only after the actual unchanged CLI guard proof is green.
        Assert.Equal(EmbeddedWlinkInstallation.Id, EmbeddedFlashToolInstallation.ToolchainId(EmbeddedFlashProtocol.WchLink));
        Assert.Equal(EmbeddedWlinkInstallation.Packages, definition.Packages);
        var archive = Assert.Single(definition.Setup, step => step.Archive is not null).Archive!;
        Assert.Equal(EmbeddedWlinkInstallation.Address, archive.Url);
        Assert.Equal(EmbeddedWlinkInstallation.Sha256, archive.Sha256);
        Assert.Equal(EmbeddedWlinkInstallation.ArchiveBytes, archive.Size);
        Assert.Contains(EmbeddedWlinkInstallation.CargoLockSha256, EmbeddedWlinkInstallation.InstallerSource, StringComparison.Ordinal);
        Assert.Contains("--locked", EmbeddedWlinkInstallation.InstallerSource, StringComparison.Ordinal);
        Assert.False(EmbeddedGlibcRuntime.IsEmbeddedCommand("wlink"));
        Assert.False(EmbeddedGlibcRuntime.IsEmbeddedCommand(EmbeddedWlinkInstallation.Prefix + "/bin/wlink"));
    }

    [Fact]
    public void Whole_padded_program_interval_is_reviewed_before_any_grant_and_generic_execution_is_refused()
    {
        using var fixture = new Fixture();
        var request = fixture.Request();
        var plan = WchLinkSession.Plan(request);
        Assert.Equal(1037, plan.OriginalLength);
        Assert.Equal(1088, plan.ProgrammedLength);
        Assert.Throws<InvalidOperationException>(() => WchLinkSession.Plan(request with { ApplicationFlash = [new(0x08000000, 1037)] }));
        Assert.Throws<ArgumentException>(() => WchLinkSession.Plan(request with { BinaryAddress = 4 }));
        Assert.Throws<ArgumentException>(() => WchLinkSession.Plan(request with { Target = "CH32V203" }));
        Assert.Throws<ArgumentException>(() => WchLinkSession.Plan(request with { Touch1200Baud = true }));
        Assert.Throws<ArgumentException>(() => WchLinkSession.Plan(request with { UseSerialTransport = true }));
        Assert.Throws<NotSupportedException>(() => EmbeddedFlashPlanner.Plan(request));
        Assert.Equal(plan.ProgrammedLength, WchLinkSession.Plan(request with { BinaryAddress = 0, ApplicationFlash = [new(0, 16384)] }).ProgrammedLength);
    }

    [Theory]
    [InlineData("003005a0", "CH32V003F4P6")]
    [InlineData("00310520", "CH32V003F4U6")]
    [InlineData("00320500", "CH32V003A4M6")]
    [InlineData("00330570", "CH32V003J4M6")]
    public void Real_attach_report_preserves_exact_revision_while_validating_known_package(string id, string part)
    {
        var target = WchLinkSession.ParseTarget("12:34:56 [INFO] Attached chip: CH32V003 [" + part + "] (ChipID: 0x" + id + ")\n");
        Assert.Equal(Convert.ToUInt32(id, 16), target.ChipId);
        Assert.Equal(part, target.Part);
    }

    [Theory]
    [InlineData("Attached chip: CH32V003 [CH32V003F4P6] (ChipID: 0x00310500)")]
    [InlineData("Attached chip: CH32V003 [CH32V003F4P6] (ChipID: 0x00300501)")]
    [InlineData("Attached chip: CH32V103 (ChipID: 0x00300500)")]
    [InlineData("Probe connected. ChipID: 0x00300500")]
    public void Missing_unknown_or_inconsistent_attach_identity_is_refused(string text) => Assert.Throws<InvalidDataException>(() => WchLinkSession.ParseTarget(text));

    [Fact]
    public async Task Verified_upload_uses_fresh_exclusive_connections_and_resets_only_after_full_padded_dump_matches()
    {
        using var fixture = new Fixture();
        var result = await fixture.UploadAsync();
        Assert.True(result.Verified);
        Assert.Equal(4, result.StepsCompleted);
        Assert.Equal(WchLinkSession.Plan(fixture.Request()).FirmwareSha256, result.FirmwareSha256);
        Assert.Equal(4, fixture.Connections.Count);
        Assert.All(fixture.Connections, connection => Assert.True(connection.Disposed));
        var commands = fixture.Runtime.Commands;
        Assert.Equal("/bin/false", commands[0].Program);
        Assert.Equal("1", commands[0].Environment["TABSTUDIO_WCH_GUARD_CHECK"]);
        Assert.Equal(new[] { "ch32v003-read-only-v1", "ch32v003-v1", "ch32v003-read-only-v1", "ch32v003-reset-v1" }, commands.Skip(1).Select(command => command.Environment["TABSTUDIO_USB_WCH_GUARD"]));
        Assert.Null(commands[1].Environment["TABSTUDIO_WCH_CHIP_ID"]);
        Assert.All(commands.Skip(2), command => Assert.Equal("003005a0", command.Environment["TABSTUDIO_WCH_CHIP_ID"]));
        Assert.All(commands.Skip(1), command =>
        {
            Assert.Null(command.Environment["TABSTUDIO_WCH_GUARD_CHECK"]);
            Assert.Equal(3, Assert.Single(command.InheritedDescriptors).Value);
            Assert.Equal(2, command.Mounts.Count);
            Assert.DoesNotContain(command.Arguments, value => value is "--erase" or "unprotect" or "protect");
        });
        Assert.Contains("--no-run", commands[2].Arguments);
        Assert.Equal(new[] { "reset", "quit" }, commands[4].Arguments.TakeLast(2));
        Assert.Equal(1088, fixture.Runtime.Programmed!.Length);
        Assert.True(fixture.Runtime.Programmed.AsSpan(0, 1037).SequenceEqual(File.ReadAllBytes(fixture.Image)));
        Assert.True(fixture.Runtime.Programmed.AsSpan(1037).IndexOfAnyExcept((byte)0xff) < 0);
        Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Private));
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("TABSTUDIO_WCH_GUARD_V0\n", 0)]
    [InlineData("TABSTUDIO_WCH_GUARD_V1\nextra", 0)]
    [InlineData("TABSTUDIO_WCH_GUARD_V1\n", 1)]
    public async Task Missing_exact_native_preflight_refuses_before_any_grant(string marker, int exitCode)
    {
        using var fixture = new Fixture();
        fixture.Runtime.Marker = marker;
        fixture.Runtime.PreflightExit = exitCode;
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.UploadAsync());
        Assert.Empty(fixture.Connections);
        Assert.Single(fixture.Runtime.Commands);
    }

    [Fact]
    public async Task Package_mismatch_incomplete_discovery_and_changed_input_stop_before_writing()
    {
        foreach (var condition in new[] { "package", "short-read", "changed-input" })
        {
            using var fixture = new Fixture();
            if (condition == "package") fixture.ExpectedTarget = "CH32V003F4U6";
            if (condition == "short-read") fixture.Runtime.ShortDiscovery = true;
            if (condition == "changed-input") fixture.Runtime.AfterDiscovery = () => File.WriteAllBytes(fixture.Image, [1, 2, 3]);
            await Assert.ThrowsAsync<InvalidDataException>(() => fixture.UploadAsync());
            Assert.Equal(2, fixture.Runtime.Commands.Count);
            Assert.Null(fixture.Runtime.Programmed);
            Assert.All(fixture.Connections, connection => Assert.True(connection.Disposed));
        }
    }

    [Fact]
    public async Task Readback_mismatch_and_nonzero_worker_never_reset_and_close_every_owned_grant()
    {
        foreach (var condition in new[] { "mismatch", "exit" })
        {
            using var fixture = new Fixture();
            fixture.Runtime.CorruptReadback = condition == "mismatch";
            fixture.Runtime.FlashExit = condition == "exit" ? 1 : 0;
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.UploadAsync());
            Assert.DoesNotContain(fixture.Runtime.Commands, command => command.Arguments.Contains("reset"));
            Assert.All(fixture.Connections, connection => Assert.True(connection.Disposed));
            Assert.Empty(Directory.EnumerateFileSystemEntries(fixture.Private));
        }
    }

    [Fact]
    public async Task Changed_attachment_reused_connection_and_oversize_output_fail_closed()
    {
        foreach (var condition in new[] { "attachment", "reuse", "oversize" })
        {
            using var fixture = new Fixture();
            fixture.ChangeAttachment = condition == "attachment";
            fixture.ReuseConnection = condition == "reuse";
            fixture.Runtime.OversizeDiscovery = condition == "oversize";
            await Assert.ThrowsAnyAsync<Exception>(() => fixture.UploadAsync());
            Assert.Null(fixture.Runtime.Programmed);
            Assert.All(fixture.Connections, connection => Assert.True(connection.Disposed));
        }
    }

    [Fact]
    public async Task Cancellation_stops_active_worker_before_closing_its_connection_and_prevents_next_worker()
    {
        using var fixture = new Fixture();
        fixture.Runtime.HoldFlash = true;
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.UploadAsync(cancellation.Token));
        Assert.True(fixture.Runtime.LastHandle!.Killed);
        Assert.All(fixture.Connections, connection => Assert.True(connection.Disposed));
        Assert.Equal(3, fixture.Runtime.Commands.Count);
    }

    [Fact]
    public async Task Backup_uses_two_read_only_exact_id_bound_reads_and_preserves_no_incomplete_destination()
    {
        foreach (var mismatch in new[] { false, true })
        {
            using var fixture = new Fixture();
            fixture.Runtime.BackupMode = true;
            fixture.Runtime.CorruptReadback = mismatch;
            var destination = Path.Combine(fixture.Root, "backup.bin");
            var task = WchLinkSession.BackupAsync(fixture.Runtime, fixture.Open, fixture.Private, destination, length: 64);
            if (mismatch)
            {
                await Assert.ThrowsAsync<InvalidDataException>(() => task);
                Assert.False(File.Exists(destination));
            }
            else
            {
                Assert.Matches("^[a-f0-9]{64}$", await task);
                Assert.Equal(64, new FileInfo(destination).Length);
            }
            Assert.Equal(2, fixture.Connections.Count);
            Assert.All(fixture.Connections, connection => Assert.True(connection.Disposed));
            Assert.All(fixture.Runtime.Commands.Skip(1), command => Assert.Equal("ch32v003-read-only-v1", command.Environment["TABSTUDIO_USB_WCH_GUARD"]));
            Assert.Equal("003005a0", fixture.Runtime.Commands[2].Environment["TABSTUDIO_WCH_CHIP_ID"]);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tabstudio-wch-session-test-" + Guid.NewGuid().ToString("N"));
        public string Image => Path.Combine(Root, "application.bin");
        public string Private => Path.Combine(Root, "private");
        public string ExpectedTarget { get; set; } = "CH32V003F4P6";
        public bool ChangeAttachment { get; set; }
        public bool ReuseConnection { get; set; }
        public List<Connection> Connections { get; } = [];
        public FakeRuntime Runtime { get; } = new();
        public Fixture()
        {
            Directory.CreateDirectory(Private);
            File.WriteAllBytes(Image, Enumerable.Range(0, 1037).Select(value => (byte)(value * 73)).ToArray());
        }
        public EmbeddedFlashRequest Request() => new()
        {
            Protocol = EmbeddedFlashProtocol.WchLink, Target = ExpectedTarget, FirmwarePath = Image,
            WorkingDirectory = Root, BinaryAddress = 0x08000000, ApplicationFlash = [new(0x08000000, 16384)],
        };
        public Task<EmbeddedFlashResult> UploadAsync(CancellationToken token = default) => WchLinkSession.ExecuteAsync(Runtime, WchLinkSession.Plan(Request()), Open, Private, cancellationToken: token);
        public Task<IUsbConnection> Open(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.All(Connections, connection => Assert.True(connection.Disposed));
            if (ReuseConnection && Connections.Count > 0) return Task.FromResult<IUsbConnection>(Connections[0]);
            var result = new Connection(ChangeAttachment && Connections.Count > 0 ? "/dev/bus/usb/001/003" : "/dev/bus/usb/001/002");
            Connections.Add(result);
            return Task.FromResult<IUsbConnection>(result);
        }
        public void Dispose() => Directory.Delete(Root, true);
    }

    private sealed class Connection(string attachment) : IUsbConnection
    {
        public bool Disposed { get; private set; }
        public UsbDeviceInfo Device { get; } = new(attachment, 0x1a86, 0x8010, "Synthetic WCH-Link session contracts", []);
        public int FileDescriptor => 77;
        public int? ActiveConfiguration => 1;
        public ReadOnlyMemory<byte> RawDescriptors => new byte[]
        {
            18,1,0,2,0,0,0,64,0x86,0x1a,0x10,0x80,0,1,0,0,0,1,
            9,2,46,0,1,1,0,0x80,50, 9,4,0,0,4,0xff,0,0,0,
            7,5,1,2,64,0,0, 7,5,0x81,2,64,0,0,
            7,5,2,2,64,0,0, 7,5,0x82,2,64,0,0,
        };
        public void Dispose() => Disposed = true;
    }

    private sealed class FakeRuntime : IToolchainRuntime
    {
        public List<ProcessStartSpec> Commands { get; } = [];
        public string Marker { get; set; } = WchLinkSession.GuardMarker + "\n";
        public int PreflightExit { get; set; }
        public int FlashExit { get; set; }
        public bool ShortDiscovery { get; set; }
        public bool OversizeDiscovery { get; set; }
        public bool CorruptReadback { get; set; }
        public bool HoldFlash { get; set; }
        public bool BackupMode { get; set; }
        public Action? AfterDiscovery { get; set; }
        public byte[]? Programmed { get; private set; }
        public Handle? LastHandle { get; private set; }
        public string DisplayName => "Synthetic guarded WCH session";
        public RuntimeKind Kind => RuntimeKind.Linux;
        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            Commands.Add(spec);
            var text = "[INFO] Attached chip: CH32V003 [CH32V003F4P6] (ChipID: 0x003005a0)\n";
            var exit = 0;
            var hold = false;
            if (spec.Program == "/bin/false") { text = Marker; exit = PreflightExit; }
            else if (spec.Arguments.Contains("flash"))
            {
                Programmed = File.ReadAllBytes(spec.Arguments[^1]);
                exit = FlashExit; hold = HoldFlash;
            }
            else if (spec.Arguments.Contains("dump"))
            {
                var args = spec.Arguments.ToList();
                var count = int.Parse(args[args.IndexOf("dump") + 2], System.Globalization.CultureInfo.InvariantCulture);
                var bytes = Programmed?.ToArray() ?? Enumerable.Repeat((byte)0xff, count).ToArray();
                if (ShortDiscovery) bytes = [1];
                if (CorruptReadback && (Programmed is not null || BackupMode && Commands.Count == 3)) bytes[0] ^= 0xff;
                File.WriteAllBytes(spec.Arguments[^1], bytes);
                if (Commands.Count == 2) { AfterDiscovery?.Invoke(); if (OversizeDiscovery) text += new string('x', 65537); }
            }
            LastHandle = new Handle(Encoding.UTF8.GetBytes(text), exit, hold);
            return new ToolchainProcess(LastHandle, spec);
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
        public Handle(byte[] bytes, int code, bool hold)
        {
            Output = new MemoryStream(bytes);
            if (!hold) _exit.SetResult((code, null));
        }
        public bool Killed { get; private set; }
        public int Id => 1;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; }
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => _exit.Task;
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() { Killed = true; _exit.TrySetResult((137, 9)); }
        public void Dispose() { Input.Dispose(); Output.Dispose(); }
    }
}
