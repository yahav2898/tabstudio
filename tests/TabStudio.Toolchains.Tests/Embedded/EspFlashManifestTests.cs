using System.Text.Json;
using System.Text.Json.Nodes;
using System.Buffers.Binary;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class EspFlashManifestTests : IDisposable
{
    private readonly TempDirectory _temporary = new();

    [Fact]
    public void Idf54_default_security_capabilities_are_not_security_enable_switches()
    {
        var path = Metadata("esp32c3", "0x0");
        // Reduced default configuration from pinned v5.4 bootloader/Kconfig.projbuild:
        // RSA support, default version preference and ROM-download capability are not enablement.
        File.WriteAllText(_temporary.Resolve("config/sdkconfig.json"), """
            {"SECURE_BOOT_V2_RSA_SUPPORTED":true,"SECURE_BOOT_V2_ECC_SUPPORTED":false,
             "SECURE_BOOT_V1_SUPPORTED":false,"SECURE_BOOT_V2_PREFERRED":true,
             "SECURE_ROM_DL_MODE_ENABLED":true,"SOC_FLASH_ENCRYPTED_XTS_AES_BLOCK_MAX":64,
             "SECURE_BOOT":false,"SECURE_BOOT_V2_ENABLED":false,"SECURE_FLASH_ENC_ENABLED":false,
             "ESP_CRYPTO_DPA_PROTECTION_AT_STARTUP":true}
            """);
        Assert.Equal("esp32c3", EspFlashManifestReader.Read(path).Chip);
    }

    [Theory]
    [InlineData("SECURE_BOOT")]
    [InlineData("SECURE_BOOT_V1_ENABLED")]
    [InlineData("SECURE_BOOT_V2_ENABLED")]
    [InlineData("SECURE_BOOT_V2_RSA_ENABLED")]
    [InlineData("SECURE_BOOT_V2_ECDSA_ENABLED")]
    [InlineData("SECURE_BOOT_FLASH_ENC_KEYS_BURN_TOGETHER")]
    [InlineData("SECURE_FLASH_ENC_ENABLED")]
    [InlineData("SECURE_FLASH_ENCRYPTION_MODE_DEVELOPMENT")]
    [InlineData("SECURE_FLASH_ENCRYPTION_MODE_RELEASE")]
    [InlineData("SECURE_FLASH_REQUIRE_ALREADY_ENABLED")]
    [InlineData("SECURE_SIGNED_APPS_NO_SECURE_BOOT")]
    [InlineData("SECURE_DISABLE_ROM_DL_MODE")]
    [InlineData("SECURE_ENABLE_SECURE_ROM_DL_MODE")]
    [InlineData("BOOTLOADER_APP_ANTI_ROLLBACK")]
    [InlineData("ESP_CRYPTO_FORCE_ECC_CONSTANT_TIME_POINT_MUL")]
    [InlineData("CONFIG_SECURE_FLASH_ENC_ENABLED")]
    public void Actual_security_enable_and_startup_efuse_switches_are_refused(string name)
    {
        var path = Metadata("esp32c3", "0x0");
        File.WriteAllText(_temporary.Resolve("config/sdkconfig.json"), JsonSerializer.Serialize(new Dictionary<string, bool> { [name] = true }));
        var error = Assert.Throws<NotSupportedException>(() => EspFlashManifestReader.Read(path));
        Assert.Contains(name.StartsWith("CONFIG_", StringComparison.Ordinal) ? name[7..] : name, error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("esp32", "0x1000")]
    [InlineData("esp32c3", "0x0")]
    [InlineData("esp32s3", "0x0")]
    public void Native_metadata_drives_chip_each_offset_and_separate_images_without_filling_nvs(string chip, string bootloader)
    {
        var path = Metadata(chip, bootloader);
        var manifest = EspFlashManifestReader.Read(path, chip);
        Assert.Equal(chip, manifest.Chip);
        Assert.Equal(_temporary.Resolve("application.bin"), manifest.ApplicationPath);
        Assert.Equal(3, manifest.Images.Count);
        var plan = EmbeddedFlashPlanner.Plan(Request(path));
        Assert.Equal(chip, plan.TargetChip);
        Assert.Equal(4, plan.Steps.Count);
        Assert.Equal(5, plan.FirmwareInputs.Count); // Manifest, security configuration and all three binaries.
        var write = plan.Steps[0].Arguments;
        Assert.Contains("write-flash", write);
        Assert.Contains(bootloader.ToUpperInvariant().Replace("0X", "0x", StringComparison.Ordinal), write);
        Assert.Contains("0x8000", write);
        Assert.Contains("0x10000", write);
        Assert.DoesNotContain("0x9000", write); // NVS gap is not filled with FF or included in an erase-all request.
        Assert.DoesNotContain(write, argument => argument.Contains("merged", StringComparison.Ordinal) || argument is "--erase-all" or "--force" or "--encrypt");
        Assert.All(plan.Steps.Skip(1), step =>
        {
            Assert.Contains("verify-flash", step.Arguments);
            Assert.Contains("--flash-mode", step.Arguments); // esptool adjusts bootloader header bytes using these parameters.
            Assert.Contains("--flash-size", step.Arguments);
        });
        Assert.Contains("hard-reset", plan.Steps[^1].Arguments);
        Assert.DoesNotContain("hard-reset", plan.Steps[0].Arguments);
        Assert.Throws<InvalidDataException>(() => EspFlashManifestReader.Read(path, "esp32h2"));
    }

    [Theory]
    [InlineData("encrypted")]
    [InlineData("efuse-config")]
    [InlineData("extra-flag")]
    [InlineData("extra-reset")]
    [InlineData("duplicate")]
    [InlineData("overlap")]
    [InlineData("sector-overlap")]
    [InlineData("path-traversal")]
    [InlineData("absolute-path")]
    [InlineData("flash-size")]
    [InlineData("missing-security")]
    [InlineData("nvs-overwrite")]
    public void Unsafe_or_ambiguous_metadata_is_refused_before_any_tool(string fault)
    {
        var path = Metadata("esp32c3", "0x0");
        var json = JsonNode.Parse(File.ReadAllText(path))!;
        switch (fault)
        {
            case "encrypted": json["bootloader"]!["encrypted"] = "true"; break;
            case "efuse-config": File.WriteAllText(_temporary.Resolve("config/sdkconfig.json"), "{\"SECURE_BOOT_V2_ENABLED\":true}"); break;
            case "extra-flag": json["write_flash_args"]!.AsArray().Add("--force"); break;
            case "extra-reset": json["extra_esptool_args"]!["force"] = true; break;
            case "overlap":
                json["flash_files"]!.AsObject().Remove("0x10000");
                json["flash_files"]!["0x8000"] = "application.bin";
                json["app"]!["offset"] = "0x8000";
                break;
            case "sector-overlap": File.WriteAllBytes(_temporary.Resolve("bootloader.bin"), new byte[0x8001]); break;
            case "path-traversal": json["flash_files"]!["0x10000"] = "../application.bin"; json["app"]!["file"] = "../application.bin"; break;
            case "absolute-path": json["flash_files"]!["0x10000"] = _temporary.Resolve("application.bin"); json["app"]!["file"] = _temporary.Resolve("application.bin"); break;
            case "flash-size": json["flash_settings"]!["flash_size"] = "detect"; break;
            case "missing-security": File.Delete(_temporary.Resolve("config/sdkconfig.json")); break;
            case "nvs-overwrite":
                var partitionBytes = File.ReadAllBytes(_temporary.Resolve("partitions.bin"));
                partitionBytes[2] = 1; partitionBytes[3] = 2; // Actual table says the requested app address belongs to NVS.
                File.WriteAllBytes(_temporary.Resolve("partitions.bin"), partitionBytes);
                break;
        }
        var serialized = json.ToJsonString();
        if (fault == "duplicate") serialized = serialized.Replace("\"flash_settings\":", "\"flash_files\":{},\"flash_settings\":", StringComparison.Ordinal);
        File.WriteAllText(path, serialized);
        var error = Record.Exception(() => EmbeddedFlashPlanner.Plan(Request(path)));
        Assert.True(error is InvalidDataException or NotSupportedException, error?.ToString());
    }

    [Fact]
    public void Bounded_paths_refuse_symlinks_even_when_they_point_to_a_valid_image()
    {
        var path = Metadata("esp32c3", "0x0");
        var image = _temporary.Resolve("application.bin");
        var original = _temporary.Resolve("actual.bin");
        File.Move(image, original);
        File.CreateSymbolicLink(image, original);
        Assert.Throws<InvalidDataException>(() => EspFlashManifestReader.Read(path));
    }

    [Theory]
    [InlineData("application.bin")]
    [InlineData("config/sdkconfig.json")]
    public async Task Every_image_and_security_configuration_is_bound_before_first_write(string changed)
    {
        var path = Metadata("esp32", "0x1000");
        var plan = EmbeddedFlashPlanner.Plan(Request(path));
        File.AppendAllText(_temporary.Resolve(changed), "changed");
        var runtime = new Runtime();
        await Assert.ThrowsAsync<InvalidDataException>(() => EmbeddedFlashExecutor.ExecuteAsync(runtime, plan));
        Assert.Equal(0, runtime.Starts);
    }

    [Fact]
    public async Task Changed_image_between_write_and_verify_stops_all_subsequent_tools()
    {
        var path = Metadata("esp32s3", "0x0");
        var plan = EmbeddedFlashPlanner.Plan(Request(path));
        var runtime = new Runtime(() => File.AppendAllText(_temporary.Resolve("application.bin"), "changed"));
        await Assert.ThrowsAsync<InvalidDataException>(() => EmbeddedFlashExecutor.ExecuteAsync(runtime, plan));
        Assert.Equal(1, runtime.Starts);
    }

    private string Metadata(string chip, string bootloader)
    {
        Directory.CreateDirectory(_temporary.Resolve("config"));
        File.WriteAllText(_temporary.Resolve("config/sdkconfig.json"), "{\"SECURE_BOOT\":false,\"SECURE_FLASH_ENC_ENABLED\":false}");
        foreach (var file in new[] { "bootloader.bin", "partitions.bin", "application.bin" }) File.WriteAllBytes(_temporary.Resolve(file), [1, 2, 3, 4]);
        var partitionTable = Enumerable.Repeat((byte)0xFF, 64).ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(partitionTable, 0xAA50);
        partitionTable[2] = 0; partitionTable[3] = 0;
        BinaryPrimitives.WriteUInt32LittleEndian(partitionTable.AsSpan(4), 0x10000);
        BinaryPrimitives.WriteUInt32LittleEndian(partitionTable.AsSpan(8), 0x100000);
        BinaryPrimitives.WriteUInt32LittleEndian(partitionTable.AsSpan(28), 0);
        File.WriteAllBytes(_temporary.Resolve("partitions.bin"), partitionTable);
        var metadata = new Dictionary<string, object>
        {
            ["write_flash_args"] = new[] { "--flash_mode", "dio", "--flash_size", "4MB", "--flash_freq", "40m" },
            ["flash_settings"] = new { flash_mode = "dio", flash_size = "4MB", flash_freq = "40m" },
            ["flash_files"] = new Dictionary<string, string> { [bootloader] = "bootloader.bin", ["0x8000"] = "partitions.bin", ["0x10000"] = "application.bin" },
            ["bootloader"] = new { offset = bootloader, file = "bootloader.bin", encrypted = "false" },
            ["partition-table"] = new { offset = "0x8000", file = "partitions.bin", encrypted = "false" },
            ["app"] = new { offset = "0x10000", file = "application.bin", encrypted = "false" },
            ["extra_esptool_args"] = new { chip, before = "default_reset", after = "hard_reset", stub = true },
        };
        return _temporary.Write("flasher_args.json", JsonSerializer.Serialize(metadata));
    }

    private EmbeddedFlashRequest Request(string path) => new()
    {
        Protocol = EmbeddedFlashProtocol.Esptool, Target = "FROM_METADATA", FirmwarePath = path,
        EspFlasherArguments = path, WorkingDirectory = _temporary.Path, SerialPort = "/dev/ttyUSB0",
    };

    private sealed class Runtime(Action? started = null) : IToolchainRuntime
    {
        public int Starts { get; private set; }
        public string DisplayName => "Explicit test double";
        public RuntimeKind Kind => RuntimeKind.Host;
        public bool HasCommand(string command) => true;
        public string? Resolve(string command) => command;
        public ToolchainProcess Start(ProcessStartSpec spec)
        {
            Starts++;
            started?.Invoke();
            return new(new Handle(), spec);
        }
        public ProcessStartSpec Shell(string workingDirectory, TerminalSize size) => throw new NotSupportedException();
        public void ShareFolder(string folder) { }
        public string? ToDevicePath(string programPath, string? workingDirectory) => programPath;
    }
    private sealed class Handle : IProcessHandle
    {
        public int Id => 1;
        public bool IsTerminal => false;
        public Stream Input { get; } = new MemoryStream();
        public Stream Output { get; } = new MemoryStream();
        public Stream? Error => null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => Task.FromResult<(int, int?)>((0, null));
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() { }
        public void Dispose() { Input.Dispose(); Output.Dispose(); }
    }
    public void Dispose() => _temporary.Dispose();
}
