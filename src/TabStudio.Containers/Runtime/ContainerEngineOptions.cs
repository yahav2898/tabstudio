using TabStudio.Containers.Images;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Containers.Runtime;

/// <summary>Where the container engine keeps its data and finds PRoot.</summary>
public sealed record ContainerEngineOptions
{
    /// <summary>The store's folder in the app's storage.</summary>
    public required string StoreRoot { get; init; }

    /// <summary>PRoot (<c>libproot.so</c> on Android); <see langword="null"/> where containers cannot run.</summary>
    public string? ProotPath { get; init; }

    /// <summary>PRoot's loader.</summary>
    public string? LoaderPath { get; init; }

    /// <summary>
    /// A root file system with stand-ins for the <c>/proc</c> files Android hides (TabStudio's Alpine environment),
    /// shared by every container.
    /// </summary>
    public string? ProcStandInRoot { get; init; }

    /// <summary>The DNS servers containers use; the device's own network's on Android.</summary>
    public Func<IReadOnlyList<string>> DnsServers { get; init; } = () => [];

    /// <summary>The device's platform.</summary>
    public OciPlatform Host { get; init; } = OciPlatform.Host;

    /// <summary>
    /// Whether the CPU runs 32-bit ARM programs. Many recent ARM64 phones and tablets do not, so arm/v7 images are
    /// only chosen where it does.
    /// </summary>
    public bool Supports32BitArm { get; init; }

    /// <summary>Runs images of another architecture, such as amd64 through Box64, when one is installed.</summary>
    public IContainerEmulator? Emulator { get; init; }

    /// <summary>Why containers cannot run on this device, when they cannot; shown in one line.</summary>
    public string? Problem => ProotPath is null || !File.Exists(ProotPath)
        ? "Containers need TabStudio's Linux runtime (PRoot), which this build of TabStudio does not include."
        : null;

    /// <summary>The platforms to pull, best first.</summary>
    public IReadOnlyList<OciPlatform> Preference =>
        [.. OciPlatform.Preference(Host).Where(p => (Supports32BitArm || p.Architecture != "arm" || Host.Architecture == "arm")
            && (!p.NeedsEmulation(Host) || Emulator is not null || Host.Architecture == "arm64"))];
}

/// <summary>Runs a container whose image is for another CPU architecture.</summary>
public interface IContainerEmulator
{
    /// <summary>What it is, such as "Box64 0.4.4", and how much slower, for the one-line note.</summary>
    string Description { get; }

    /// <summary>Why this image cannot run through it, or <see langword="null"/> when it can.</summary>
    /// <param name="image">The image's platform.</param>
    /// <param name="rootfs">The container's root file system, to look at its C library.</param>
    string? Problem(OciPlatform image, string rootfs);

    /// <summary>Changes a process to run through the emulator: its program, mounts and environment.</summary>
    ProcessStartSpec Wrap(ProcessStartSpec process);

    /// <summary>PRoot options the emulator needs.</summary>
    IReadOnlyList<string> ProotArguments { get; }
}
