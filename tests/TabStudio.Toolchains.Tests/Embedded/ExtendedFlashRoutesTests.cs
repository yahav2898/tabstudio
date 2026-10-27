using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Flashing;

namespace TabStudio.Toolchains.Tests.Embedded;

public sealed class ExtendedFlashRoutesTests : IDisposable
{
    private readonly TempDirectory _temporary = new();

    [Fact]
    public void Avrdude_cannot_rebase_a_raw_binary_away_from_flash_zero()
    {
        var request = Request(EmbeddedFlashProtocol.Avrdude, Binary("avr.bin")) with
        {
            Target = "m328p", Programmer = "arduino", BinaryAddress = 128,
        };
        Assert.Throws<ArgumentException>(() => EmbeddedFlashPlanner.Plan(request));
    }

    [Theory]
    [InlineData(0x2000ul, 0ul, "8192")]
    [InlineData(0x80000ul, 0x80000ul, "0")]
    public void Bossac_preserves_bootloader_and_physical_flash_base_offsets(ulong application, ulong flashBase, string offset)
    {
        var request = Request(EmbeddedFlashProtocol.Bossac, Binary("application.bin")) with
        {
            BinaryAddress = application, FlashBaseAddress = flashBase, ApplicationFlash = [new(application, 16384)],
        };
        var step = Assert.Single(EmbeddedFlashPlanner.Plan(request).Steps);
        var at = step.Arguments.ToList().IndexOf("-o");
        Assert.True(at >= 0);
        Assert.Equal(offset, step.Arguments[at + 1]);
        Assert.DoesNotContain(step.Arguments, argument => argument is "-b" or "--boot" or "-e" or "--erase");
        Assert.Throws<ArgumentException>(() => EmbeddedFlashPlanner.Plan(request with { FlashBaseAddress = application + 1 }));
    }

    [Fact]
    public void Teensy_transfer_does_not_pretend_verbose_is_verify_or_reboot_another_board()
    {
        var image = _temporary.Write("teensy.hex", ":0400000001020304F2\n:00000001FF\n");
        var plan = EmbeddedFlashPlanner.Plan(Request(EmbeddedFlashProtocol.Teensy, image) with { Target = "TEENSY41" });
        Assert.False(plan.Verified);
        Assert.Contains("does not offer flash readback", plan.Verification, StringComparison.Ordinal);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("teensy_loader_cli", step.Program);
        Assert.Equal(["--mcu=TEENSY41", "-v", image], step.Arguments);
        Assert.DoesNotContain("-s", step.Arguments);
        Assert.DoesNotContain("-r", step.Arguments);
    }

    [Fact]
    public void Wch_checks_isp_chip_and_retains_default_verify_without_unlocking()
    {
        var image = Binary("wch.bin");
        var request = Request(EmbeddedFlashProtocol.WchIsp, image) with { Target = "CH32V003F4P6", BinaryAddress = 0, UseSerialTransport = true };
        var plan = EmbeddedFlashPlanner.Plan(request);
        Assert.True(plan.Verified);
        Assert.True(EmbeddedFlashPlanner.RequiresSerial(request));
        Assert.Equal(2, plan.Steps.Count);
        Assert.Equal(["--serial", "--port", "/dev/ttyUSB0", "--baudrate", "115200", "info", "--chip", "CH32V003F4P6"], plan.Steps[0].Arguments);
        Assert.Equal(["--serial", "--port", "/dev/ttyUSB0", "--baudrate", "115200", "flash", image], plan.Steps[1].Arguments);
        Assert.DoesNotContain(plan.Steps.SelectMany(step => step.Arguments), argument => argument is "config" or "unprotect" or "--no-verify" or "--no-erase");
        Assert.Throws<ArgumentException>(() => EmbeddedFlashPlanner.Plan(request with { BinaryAddress = 128 }));
    }

    [Fact]
    public void Msp430_disables_startup_scripts_uses_compare_verify_and_refuses_auto_unlock_bsl()
    {
        var image = _temporary.Write("MSP firmware.hex", ":04C000000102030432\n:00000001FF\n");
        var request = Request(EmbeddedFlashProtocol.MspDebug, image) with
        {
            Target = "MSP430G2553", Programmer = "rf2500", ApplicationFlash = [new(0xC000, 0x4000)],
        };
        var plan = EmbeddedFlashPlanner.Plan(request);
        Assert.True(plan.Verified);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("mspdebug", step.Program);
        Assert.Equal("-n", step.Arguments[0]);
        Assert.Contains("prog \"" + image + "\"", step.Arguments);
        Assert.Contains("verify \"" + image + "\"", step.Arguments);
        Assert.Throws<ArgumentException>(() => EmbeddedFlashPlanner.Plan(request with { Programmer = "flash-bsl" }));
        Assert.Throws<ArgumentException>(() => EmbeddedFlashPlanner.Plan(request with { Programmer = "rom-bsl" }));
    }

    [Fact]
    public void Updi_uses_pinned_python_support_and_readback_without_high_voltage_or_chip_unlock()
    {
        var image = _temporary.Write("updi.hex", ":0400000001020304F2\n:00000001FF\n");
        var request = Request(EmbeddedFlashProtocol.PymcuprogUpdi, image) with { Target = "ATmega4809" };
        var step = Assert.Single(EmbeddedFlashPlanner.Plan(request).Steps);
        Assert.True(EmbeddedFlashPlanner.RequiresSerial(request));
        Assert.Equal("python3", step.Program);
        Assert.Equal("pymcuprog.pymcuprog", step.Arguments[1]);
        Assert.Contains("--verify", step.Arguments);
        Assert.Contains("uart", step.Arguments);
        Assert.Contains("atmega4809", step.Arguments);
        Assert.Contains("/opt/tabstudio/microchip/python", step.Environment["PYTHONPATH"], StringComparison.Ordinal);
        Assert.DoesNotContain(step.Arguments, argument => argument is "--erase" or "--high-voltage" or "-H" or "--chip-erase-locked-device" or "--user-row-locked-device");
    }

    [Fact]
    public void Nordic_application_package_checks_sha256_and_signed_envelope_without_claiming_trust()
    {
        var plain = NrfDfuPackage.Inspect(Package(), 0x26000);
        Assert.Equal(new FlashAddressRange(0x26000, 4), plain.ApplicationRange);
        Assert.False(plain.HasSignature);
        var signed = NrfDfuPackage.Inspect(Package(signed: true), 0x26000);
        Assert.True(signed.HasSignature); // Format only: a target with a trusted key must still validate this signature.
        Assert.Equal(plain.ApplicationSha256, signed.ApplicationSha256);
    }

    [Theory]
    [InlineData("type")]
    [InlineData("debug")]
    [InlineData("hash")]
    [InlineData("size")]
    [InlineData("duplicate")]
    [InlineData("manifest")]
    [InlineData("path")]
    public void Nordic_packages_cannot_smuggle_bootloader_debug_or_mismatched_payloads(string fault) =>
        Assert.Throws<InvalidDataException>(() => NrfDfuPackage.Inspect(Package(fault: fault), 0x26000));

    [Fact]
    public void Nordic_plan_uses_explicit_serial_port_with_native_cdc_or_uart_transport()
    {
        var path = _temporary.Resolve("application.zip");
        File.WriteAllBytes(path, Package());
        var request = Request(EmbeddedFlashProtocol.NrfDfu, path) with { Target = "nrf52840", BinaryAddress = 0x26000, ApplicationFlash = [new(0x26000, 0x10000)] };
        var plan = EmbeddedFlashPlanner.Plan(request);
        Assert.True(EmbeddedFlashPlanner.RequiresSerial(request));
        Assert.True(plan.Verified);
        Assert.Contains("usb-serial", Assert.Single(plan.Steps).Arguments);
        Assert.Contains("serial", Assert.Single(EmbeddedFlashPlanner.Plan(request with { NrfDfuUart = true }).Steps).Arguments);
        Assert.DoesNotContain(plan.Steps.SelectMany(step => step.Arguments), argument => argument.Contains("bootloader", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Production_rejects_transfer_only_jobs_before_any_board_is_opened()
    {
        var called = false;
        var job = new ProductionFlashJob("teensy", _ =>
        {
            called = true;
            return Task.FromResult(new EmbeddedFlashResult("hash", TimeSpan.Zero, 1, Verified: false));
        }, ProvidesVerification: false);
        await Assert.ThrowsAsync<NotSupportedException>(() => ProductionFlasher.RunAsync([job], _temporary.Resolve("batch.jsonl")));
        Assert.False(called);
    }

    [Fact]
    public async Task Production_never_marks_an_unexpected_transfer_only_result_verified()
    {
        var job = new ProductionFlashJob("transport", _ => Task.FromResult(new EmbeddedFlashResult("hash", TimeSpan.Zero, 1, Verified: false)));
        var result = Assert.Single(await ProductionFlasher.RunAsync([job], _temporary.Resolve("result.jsonl")));
        Assert.False(result.Verified);
        Assert.Contains("verification", result.Error, StringComparison.Ordinal);
    }

    private string Binary(string name)
    {
        var path = _temporary.Resolve(name);
        File.WriteAllBytes(path, [1, 2, 3, 4]);
        return path;
    }

    private EmbeddedFlashRequest Request(EmbeddedFlashProtocol protocol, string image) => new()
    {
        Protocol = protocol, FirmwarePath = image, WorkingDirectory = _temporary.Path, Target = "explicit",
        SerialPort = "/dev/ttyUSB0", ApplicationFlash = [new(0, 16384)],
    };

    private static byte[] Package(string? fault = null, bool signed = false)
    {
        byte[] payload = [1, 2, 3, 4];
        var hash = SHA256.HashData(payload).Reverse().ToArray();
        if (fault == "hash")
        {
            hash[0] ^= 1;
        }
        var init = new List<byte>();
        init.AddRange(Scalar(4, fault == "type" ? 2U : 0U));
        if (fault == "duplicate")
        {
            init.AddRange(Scalar(4, 0));
        }
        init.AddRange(Scalar(7, fault == "size" ? 5U : 4U));
        init.AddRange(Bytes(8, [.. Scalar(1, 3), .. Bytes(2, hash)]));
        init.AddRange(Scalar(9, fault == "debug" ? 1U : 0U));
        byte[] command = [.. Scalar(1, 1), .. Bytes(2, [.. init])];
        byte[] packet = signed ? Bytes(2, [.. Bytes(1, command), .. Scalar(2, 0), .. Bytes(3, new byte[64])]) : Bytes(1, command);
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open(), new UTF8Encoding(false)))
            {
                writer.Write(fault == "manifest" ? "{\"manifest\":{\"bootloader\":{\"bin_file\":\"app.bin\",\"dat_file\":\"app.dat\"}}}" : "{\"manifest\":{\"application\":{\"bin_file\":\"app.bin\",\"dat_file\":\"app.dat\"}}}");
            }
            using (var binary = zip.CreateEntry(fault == "path" ? "../app.bin" : "app.bin").Open())
            {
                binary.Write(payload);
            }
            using var dat = zip.CreateEntry("app.dat").Open();
            dat.Write(packet);
        }
        return output.ToArray();
    }

    private static byte[] Scalar(uint field, uint value) => [.. Varint(field << 3), .. Varint(value)];
    private static byte[] Bytes(uint field, byte[] value) => [.. Varint((field << 3) | 2), .. Varint((uint)value.Length), .. value];
    private static byte[] Varint(uint value)
    {
        var bytes = new List<byte>();
        while (value >= 128)
        {
            bytes.Add((byte)((value & 127) | 128));
            value >>= 7;
        }
        bytes.Add((byte)value);
        return [.. bytes];
    }

    public void Dispose() => _temporary.Dispose();
}
