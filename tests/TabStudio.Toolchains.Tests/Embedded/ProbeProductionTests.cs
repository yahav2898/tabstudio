using System.Buffers.Binary;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.Toolchains.Tests.Embedded;

/// <summary>Two identical probes must retain distinct grants and private enumeration through a batch.</summary>
public sealed class ProbeProductionTests
{
    [Fact]
    public void Identical_probe_workers_never_share_fd_mount_or_original_single_probe_selector()
    {
        using var temporary = new TempDirectory();
        var request = Request(temporary);
        var reviewed = EmbeddedFlashPlanner.Plan(request);
        using var first = new Connection(42, "/dev/bus/usb/001/002");
        using var second = new Connection(43, "/dev/bus/usb/001/003");
        using var left = ProbeRsProductionLease.Create(request, reviewed, first, temporary.Resolve("private"));
        using var right = ProbeRsProductionLease.Create(request, reviewed, second, temporary.Resolve("private"));
        var a = left.Wrap(left.Plan.Steps[0]);
        var b = right.Wrap(right.Plan.Steps[0]);
        Assert.Equal(reviewed.FirmwareSha256, left.Plan.FirmwareSha256);
        Assert.Equal(reviewed.FirmwareSha256, right.Plan.FirmwareSha256);
        Assert.Contains("0483:3748", a.Arguments);
        Assert.Contains("--binary-format", a.Arguments);
        Assert.Contains("elf", a.Arguments);
        Assert.Contains("--restore-unwritten", a.Arguments);
        Assert.Contains("--preverify", a.Arguments);
        Assert.Contains("--reset", a.Arguments);
        Assert.Null(a.Environment["PROBE_RS_ALLOW_ERASE_ALL"]);
        Assert.Null(a.Environment["PROBE_RS_CONFIG_PRESET"]);
        Assert.DoesNotContain("0483:3748:original-single-probe", a.Arguments);
        Assert.Equal(3, Assert.Single(a.InheritedDescriptors).Value);
        Assert.Equal(42, Assert.Single(a.InheritedDescriptors).Key);
        Assert.Equal(43, Assert.Single(b.InheritedDescriptors).Key);
        Assert.Empty(a.Mounts.Select(mount => mount.Source).Intersect(b.Mounts.Select(mount => mount.Source), StringComparer.Ordinal));
        Assert.Equal(a.Mounts.Select(mount => mount.Target), b.Mounts.Select(mount => mount.Target));
        left.Dispose();
        Assert.All(b.Mounts, mount => Assert.True(Directory.Exists(mount.Source)));
        Assert.False(first.Disposed);
        Assert.Throws<ObjectDisposedException>(() => left.Wrap(left.Plan.Steps[0]));
    }

    [Fact]
    public void Batch_preserves_the_explicit_probe_channel_and_refuses_reusing_it_on_another_probe_type()
    {
        var device = new UsbDeviceInfo("/dev/bus/usb/001/002", 0x0403, 0x6010, "FTDI channel fixture", []);
        Assert.Equal("0403:6010-1", ProbeRsProductionLease.Selector(device, "0403:6010-1:single-serial"));
        Assert.Throws<InvalidOperationException>(() => ProbeRsProductionLease.Selector(device, "0403:6011-1:single-serial"));
        Assert.Throws<InvalidOperationException>(() => ProbeRsProductionLease.Selector(device, "0403:6010-256:single-serial"));
        Assert.Throws<InvalidOperationException>(() => ProbeRsProductionLease.Selector(device, "0403:6010-invalid:single-serial"));
    }

    [Fact]
    public void Changed_firmware_is_refused_before_a_worker_namespace_is_created()
    {
        using var temporary = new TempDirectory();
        var request = Request(temporary);
        var reviewed = EmbeddedFlashPlanner.Plan(request);
        var changed = File.ReadAllBytes(request.FirmwarePath);
        changed[^1] ^= 1;
        File.WriteAllBytes(request.FirmwarePath, changed);
        using var connection = new Connection(42, "/dev/bus/usb/001/002");
        var root = temporary.Resolve("private");
        Assert.Throws<InvalidDataException>(() => ProbeRsProductionLease.Create(request, reviewed, connection, root));
        Assert.False(Directory.Exists(root));
    }

    private static EmbeddedFlashRequest Request(TempDirectory temporary)
    {
        var firmware = temporary.Resolve("app.elf");
        var image = new byte[88];
        new byte[] { 0x7F, 69, 76, 70, 1, 1, 1 }.CopyTo(image, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(16), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(18), 40);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(28), 52);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(40), 52);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(42), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(image.AsSpan(44), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(56), 84);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(60), 0x08000000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(64), 0x08000000);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(68), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(72), 4);
        BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(76), 5);
        new byte[] { 1, 2, 3, 4 }.CopyTo(image, 84);
        File.WriteAllBytes(firmware, image);
        return new()
        {
            Protocol = EmbeddedFlashProtocol.ProbeRs, FirmwarePath = firmware, WorkingDirectory = temporary.Path,
            Target = "STM32F401RETx", BinaryAddress = 0x08000000, ApplicationFlash = [new(0x08000000, 4096)],
            ProbeSelector = "0483:3748:original-single-probe",
        };
    }

    private sealed class Connection(int descriptor, string identity) : IUsbConnection
    {
        public UsbDeviceInfo Device { get; } = new(identity, 0x0483, 0x3748, "Synthetic identical ST-Link", []);
        public int FileDescriptor => descriptor;
        public bool Disposed { get; private set; }
        public int? ActiveConfiguration => 1;
        public ReadOnlyMemory<byte> RawDescriptors => new byte[]
        {
            18, 1, 0, 2, 0, 0, 0, 64, 0x83, 0x04, 0x48, 0x37, 0, 1, 0, 0, 0, 1,
            9, 2, 18, 0, 1, 1, 0, 0x80, 50, 9, 4, 0, 0, 0, 0xFF, 0, 0, 0,
        };
        public void Dispose() => Disposed = true;
    }
}
