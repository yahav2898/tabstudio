using TabStudio.Containers.Images;
using TabStudio.Containers.Store;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Containers.Runtime;

/// <summary>Reuses an installed, verified PIC or embedded Box64 runtime for amd64 glibc images on ARM64.</summary>
public sealed class Box64ContainerEmulator(Func<string?> nativeRoot, string binary) : IContainerEmulator
{
    private const string GuestTools = "/opt/tabstudio-container-box64";
    private const string NativeLibraries = "/usr/lib/aarch64-linux-gnu";

    /// <inheritdoc />
    public string Description => "Box64 0.4.4, amd64 translation (slower than native ARM64; speed is image-dependent)";

    /// <inheritdoc />
    public IReadOnlyList<string> ProotArguments => [];

    /// <inheritdoc />
    public string? Problem(OciPlatform image, string rootfs)
    {
        if (image.Architecture != "amd64") return $"Box64 runs amd64 images; it cannot translate {image}.";
        if (!File.Exists(binary) || nativeRoot() is not { } native
            || !File.Exists(Path.Combine(native, "usr/lib/aarch64-linux-gnu/ld-linux-aarch64.so.1")))
        {
            return "This amd64 image needs Box64's native glibc runtime; prepare Box64 in the Toolchain Store, or choose an ARM64 image.";
        }
        if (File.Exists(SafePath.ResolveFollowing(rootfs, "/lib/ld-musl-x86_64.so.1")))
        {
            return "Box64's OCI adapter supports glibc images; this amd64 image uses musl. Choose its ARM64 image or a glibc tag.";
        }
        return null;
    }

    /// <inheritdoc />
    public ProcessStartSpec Wrap(ProcessStartSpec process)
    {
        var native = nativeRoot() ?? throw new ContainerException("Box64's native glibc runtime is not installed.");
        var environment = new Dictionary<string, string?>(process.Environment, StringComparer.Ordinal)
        {
            ["LD_LIBRARY_PATH"] = NativeLibraries,
            ["BOX64_LD_LIBRARY_PATH"] = "/lib/x86_64-linux-gnu:/usr/lib/x86_64-linux-gnu:/lib64:/usr/lib64:/lib:/usr/lib:/usr/local/lib",
            ["BOX64_PATH"] = process.Environment.GetValueOrDefault("PATH"),
            ["BOX64_NOBANNER"] = "1", ["BOX64_LOG"] = "0", ["BOX64_DYNACACHE"] = "0",
        };
        return process with
        {
            Program = GuestTools + "/" + Path.GetFileName(binary),
            Arguments = [process.Program, .. process.Arguments], Environment = environment,
            Mounts = [.. process.Mounts,
                new ProcessMount(Path.GetDirectoryName(binary)!, GuestTools),
                new ProcessMount(Path.Combine(native, "usr/lib/aarch64-linux-gnu"), NativeLibraries),
                new ProcessMount(Path.Combine(native, "usr/lib/aarch64-linux-gnu/ld-linux-aarch64.so.1"), "/lib/ld-linux-aarch64.so.1")],
        };
    }
}
