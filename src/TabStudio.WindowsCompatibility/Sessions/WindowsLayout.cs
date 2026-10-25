using TabStudio.Toolchains.Runtimes;

namespace TabStudio.WindowsCompatibility.Sessions;

/// <summary>
/// Where Windows Compatibility keeps its parts: Wine, the .NET runtime for Windows, the Wine prefix (the Windows
/// installation's C: drive and registry) and the files of a running session, as programs in the environment see
/// them and as TabStudio reads them on the device.
/// </summary>
/// <remarks>
/// In TabStudio's Linux environment the parts are at fixed paths inside it (<see cref="LinuxRoot"/>), which the
/// Toolchain Store installs; on a desktop computer, where Wine and Xvfb are the computer's own, they live in
/// TabStudio's data folder. Either way a program path is what Wine and Xvfb are given, and
/// <see cref="ToDevicePath"/> turns it into the file TabStudio itself opens.
/// </remarks>
public sealed class WindowsLayout
{
    /// <summary>The folder the Windows Compatibility toolchain installs into, in the Linux environment.</summary>
    public const string LinuxRoot = "/opt/tabstudio/windows";

    /// <summary>Where TabStudio's own build of Wine is, where the distribution has none, as on ARM.</summary>
    public const string BundledWine = "/opt/tabstudio/wine/bin/wine";

    private readonly string? _rootfs;

    private WindowsLayout(IToolchainRuntime runtime, string root, string? rootfs)
    {
        Runtime = runtime;
        _rootfs = rootfs;
        Root = root;
        DotNetRoot = root + "/dotnet";
        Prefix = root + "/prefix";
        SessionFolder = rootfs is null ? root + "/session" : "/tmp/tabstudio-windows";
    }

    /// <summary>Where the programs run.</summary>
    public IToolchainRuntime Runtime { get; }

    /// <summary>The folder everything else is in, as programs see it.</summary>
    public string Root { get; }

    /// <summary>The .NET runtime for Windows: <c>dotnet.exe</c> with Microsoft.NETCore.App and Microsoft.WindowsDesktop.App.</summary>
    public string DotNetRoot { get; }

    /// <summary>The Wine prefix: its <c>drive_c</c>, registry and settings.</summary>
    public string Prefix { get; }

    /// <summary>The running session's files: the framebuffer, the X server's cookie, logs.</summary>
    public string SessionFolder { get; }

    /// <summary>The Xvfb framebuffer folder, given to <c>-fbdir</c>.</summary>
    public string FramebufferFolder => SessionFolder + "/fb";

    /// <summary>The X server's Xauthority file.</summary>
    public string AuthorityFile => SessionFolder + "/Xauthority";

    /// <summary>Wine's launcher, or <see langword="null"/> when Wine is not installed.</summary>
    public string? Wine => Runtime.Resolve(BundledWine) ?? Runtime.Resolve("wine");

    /// <summary>Wine's server, next to its launcher.</summary>
    public string? WineServer => Wine is { } wine ? Runtime.Resolve(wine[..(wine.LastIndexOf('/') + 1)] + "wineserver") ?? Runtime.Resolve("wineserver") : null;

    /// <summary>Whether everything a session needs is installed: Wine, Xvfb and the .NET runtime for Windows.</summary>
    public bool IsComplete => Wine is not null && WineServer is not null && Runtime.HasCommand("Xvfb") && File.Exists(ToDevicePath(DotNetRoot + "/dotnet.exe"));

    /// <summary>The layout in a runtime: fixed paths in TabStudio's Linux environment, or a data folder on a desktop computer.</summary>
    /// <param name="runtime">Where programs run.</param>
    /// <param name="hostDataFolder">TabStudio's folder for Windows Compatibility on a desktop computer.</param>
    public static WindowsLayout For(IToolchainRuntime runtime, string hostDataFolder)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrEmpty(hostDataFolder);
        return runtime is ProotRuntime proot
            ? new WindowsLayout(runtime, LinuxRoot, proot.Rootfs)
            : new WindowsLayout(runtime, Path.GetFullPath(hostDataFolder).TrimEnd('/'), null);
    }

    /// <summary>
    /// The file TabStudio opens for a path inside the environment, such as the X server's socket. Folders of the
    /// device that the environment shows at their own paths, such as a workspace, need no translation.
    /// </summary>
    /// <param name="programPath">The path, such as <c>/tmp/.X11-unix/X20</c>.</param>
    public string ToDevicePath(string programPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(programPath);
        return _rootfs is null ? programPath : Path.Combine(_rootfs, programPath.TrimStart('/'));
    }

    /// <summary>A Windows path for a file programs see at a Unix path: drive Z: is the environment's root.</summary>
    /// <param name="programPath">The path, such as <c>/opt/tabstudio/windows/dotnet/dotnet.exe</c>.</param>
    public static string ToWindowsPath(string programPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(programPath);
        return "Z:" + programPath.Replace('/', '\\');
    }
}
