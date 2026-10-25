namespace TabStudio.WindowsBridge.Protocol;

/// <summary>What kind of tool a <see cref="BridgeToolchain"/> is.</summary>
public enum BridgeToolchainKind
{
    /// <summary>A .NET SDK, from <c>dotnet --list-sdks</c>.</summary>
    DotNetSdk,

    /// <summary>Visual Studio or the Visual Studio Build Tools, from vswhere.</summary>
    VisualStudio,

    /// <summary>MSBuild, which builds C++, .NET Framework and WinUI projects.</summary>
    MSBuild,

    /// <summary>The MSVC C++ compiler and linker.</summary>
    Msvc,

    /// <summary>The Windows SDK, with the headers and libraries of Windows.</summary>
    WindowsSdk,

    /// <summary>The Windows App SDK, which WinUI 3 apps run on.</summary>
    WindowsAppSdk,

    /// <summary>A .NET Framework targeting pack, such as 4.8.</summary>
    NetFramework,

    /// <summary>netcoredbg, the debug adapter for .NET apps.</summary>
    Debugger,
}

/// <summary>An SDK or tool the bridge found on the PC.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Name">Its name, such as <c>Visual Studio Community 2022</c>.</param>
/// <param name="Version">Its version, such as <c>17.14.2</c>.</param>
/// <param name="Location">Where it is installed, when that is a single folder.</param>
public sealed record BridgeToolchain(BridgeToolchainKind Kind, string Name, string Version, string? Location);

/// <summary>What the PC's tools let the bridge build and debug.</summary>
[Flags]
public enum BridgeCapabilities
{
    /// <summary>Nothing: no .NET SDK and no MSBuild.</summary>
    None = 0,

    /// <summary>SDK-style .NET projects, with <c>dotnet build</c>.</summary>
    DotNet = 1,

    /// <summary>WPF and Windows Forms on .NET: a .NET SDK on Windows.</summary>
    WindowsDesktop = 2,

    /// <summary>MSBuild from Visual Studio or its Build Tools.</summary>
    MSBuild = 4,

    /// <summary>Visual C++ projects: MSBuild with the MSVC toolset and the Windows SDK.</summary>
    Cpp = 8,

    /// <summary>WinUI 3 apps: the Windows SDK, and the Windows App SDK from NuGet.</summary>
    WinUI = 16,

    /// <summary>.NET Framework projects: MSBuild with a targeting pack.</summary>
    NetFramework = 32,

    /// <summary>Debugging .NET apps with netcoredbg.</summary>
    DebugDotNet = 64,
}

/// <summary>Whether the PC can serve the tablet right now.</summary>
public enum BridgePcState
{
    /// <summary>Awake and unlocked: programs run and are shown.</summary>
    Awake,

    /// <summary>Locked: programs keep running, but Windows does not draw them until someone signs in again.</summary>
    Locked,

    /// <summary>About to sleep or hibernate: the connection is about to drop.</summary>
    Sleeping,

    /// <summary>Shutting down, or TabStudio closing on the PC.</summary>
    ShuttingDown,
}
