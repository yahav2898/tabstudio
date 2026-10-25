using System.Runtime.InteropServices;

namespace TabStudio.Toolchains.Linux;

/// <summary>
/// What the device offers toolchains. On Android the app head describes its native library folder, where PRoot
/// is, and the network's DNS servers; on a desktop computer toolchains run on the computer itself.
/// </summary>
public sealed record ToolchainPlatform
{
    /// <summary>A desktop computer, which runs the toolchains installed on it.</summary>
    public static ToolchainPlatform Desktop { get; } = new();

    /// <summary>
    /// The app's native library folder (<c>ApplicationInfo.nativeLibraryDir</c>), with <c>libproot.so</c> and
    /// <c>libproot-loader.so</c>; <see langword="null"/> on a desktop computer.
    /// </summary>
    public string? NativeLibraryDirectory { get; init; }

    /// <summary>The Android API level, or 0 on a desktop computer.</summary>
    public int AndroidApiLevel { get; init; }

    /// <summary>The DNS servers of the network the device is on now, for the Linux environment's <c>resolv.conf</c>.</summary>
    public Func<IReadOnlyList<string>> DnsServers { get; init; } = () => [];

    /// <summary>Folders on the device programs should see, besides TabStudio's projects folder, such as shared storage.</summary>
    public IReadOnlyList<string> SharedFolders { get; init; } = [];

    /// <summary>Whether toolchains run in TabStudio's Linux environment rather than on the computer.</summary>
    public bool UsesLinuxEnvironment => NativeLibraryDirectory is not null;

    /// <summary>Alpine's name for the processor architecture TabStudio runs on, or <see langword="null"/> for one Alpine has no build for.</summary>
    public static string? AlpineArchitecture => RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.Arm64 => "aarch64",
        Architecture.Arm => "armv7",
        Architecture.X64 => "x86_64",
        _ => null,
    };

    /// <summary>The path of PRoot, when there is a native library folder.</summary>
    public string? ProotPath => NativeLibraryDirectory is { } folder ? Path.Combine(folder, "libproot.so") : null;

    /// <summary>The path of PRoot's loader, when there is a native library folder.</summary>
    public string? LoaderPath => NativeLibraryDirectory is { } folder ? Path.Combine(folder, "libproot-loader.so") : null;
}
