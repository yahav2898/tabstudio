using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Installation;

/// <summary>Where a toolchain stands on this device.</summary>
public enum ToolchainState
{
    /// <summary>Not installed; the Store offers to install it.</summary>
    NotInstalled,

    /// <summary>Being installed.</summary>
    Installing,

    /// <summary>Installed and ready.</summary>
    Installed,

    /// <summary>Installed, and a newer version is available.</summary>
    UpdateAvailable,

    /// <summary>Being updated.</summary>
    Updating,

    /// <summary>Being removed.</summary>
    Removing,

    /// <summary>
    /// Not available on this device, such as on a desktop computer that does not have it installed, where
    /// TabStudio uses the computer's own toolchains.
    /// </summary>
    Unavailable,
}

/// <summary>A step of installing, updating or removing a toolchain.</summary>
public enum InstallStage
{
    /// <summary>Checking the device: the runtime, the architecture and free space.</summary>
    Preparing,

    /// <summary>Downloading and unpacking the Linux environment, the first time a toolchain is installed.</summary>
    SettingUpLinux,

    /// <summary>Reading the package index and working out what to download.</summary>
    Resolving,

    /// <summary>Downloading packages and archives.</summary>
    Downloading,

    /// <summary>Installing the downloaded packages.</summary>
    Installing,

    /// <summary>Running the toolchain's setup steps, such as installing its language server.</summary>
    Configuring,

    /// <summary>Removing the toolchain's packages and files.</summary>
    Removing,

    /// <summary>Done.</summary>
    Completed,
}

/// <summary>How far an operation on a toolchain has got.</summary>
/// <param name="Stage">The current step.</param>
/// <param name="Message">One line about what is happening, such as <c>Downloading python3 3.14.8</c>.</param>
/// <param name="Fraction">How much of the whole operation is done, from 0 to 1, or <see langword="null"/> when that is not known.</param>
/// <param name="BytesDone">Bytes downloaded so far, during downloads.</param>
/// <param name="BytesTotal">Bytes to download in all, during downloads.</param>
public sealed record InstallProgress(InstallStage Stage, string Message, double? Fraction, long BytesDone = 0, long BytesTotal = 0);

/// <summary>A toolchain's state, for the Toolchain Store and the IDE.</summary>
public sealed record ToolchainStatus
{
    /// <summary>The toolchain.</summary>
    public required ToolchainDefinition Toolchain { get; init; }

    /// <summary>Where it stands.</summary>
    public required ToolchainState State { get; init; }

    /// <summary>The installed version, such as <c>3.14.8</c>, when installed.</summary>
    public string? Version { get; init; }

    /// <summary>The version an update installs, when one is available.</summary>
    public string? AvailableVersion { get; init; }

    /// <summary>The space it takes on the device, in bytes, when installed.</summary>
    public long? InstalledSize { get; init; }

    /// <summary>What installing (or updating) it downloads, in bytes: exact once the package index is read, estimated before.</summary>
    public long DownloadSize { get; init; }

    /// <summary>The space installing it takes, in bytes: exact once the package index is read, estimated before.</summary>
    public long InstallSize { get; init; }

    /// <summary>Whether the sizes come from the package index rather than the catalog's estimate.</summary>
    public bool SizesAreExact { get; init; }

    /// <summary>A short note for the UI, such as why it is unavailable.</summary>
    public string? Note { get; init; }

    /// <summary>The operation in progress, while one is.</summary>
    public InstallProgress? Progress { get; init; }

    /// <summary>Why the last operation failed, until the next one starts.</summary>
    public string? Error { get; init; }

    /// <summary>Whether it can run programs now: installed, with or without an update waiting.</summary>
    public bool IsReady => State is ToolchainState.Installed or ToolchainState.UpdateAvailable;

    /// <summary>Whether an operation is running.</summary>
    public bool IsBusy => State is ToolchainState.Installing or ToolchainState.Updating or ToolchainState.Removing;
}

/// <summary>The space toolchains take on the device.</summary>
/// <param name="Toolchains">Installed toolchains, in bytes.</param>
/// <param name="Base">The Linux environment itself, without toolchains.</param>
/// <param name="Downloads">Downloads kept to resume or reuse.</param>
/// <param name="Free">Free space on the storage toolchains are kept on.</param>
public sealed record DiskUsage(long Toolchains, long Base, long Downloads, long Free)
{
    /// <summary>Nothing used, nothing known about free space.</summary>
    public static DiskUsage None { get; } = new(0, 0, 0, 0);

    /// <summary>Everything toolchains take.</summary>
    public long Total => Toolchains + Base + Downloads;
}
