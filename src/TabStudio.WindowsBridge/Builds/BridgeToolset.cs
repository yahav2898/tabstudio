namespace TabStudio.WindowsBridge.Builds;

/// <summary>The build tools a bridge found on its PC, as the builds need them.</summary>
/// <param name="DotNet">The <c>dotnet</c> executable, or <see langword="null"/> when no .NET SDK is installed.</param>
/// <param name="MsBuild">MSBuild from Visual Studio or its Build Tools, or <see langword="null"/>.</param>
/// <param name="MsBuildVersion">That MSBuild's version, such as <c>17.14.8</c>.</param>
/// <param name="HasMsvc">Whether the MSVC C++ toolset is installed with it.</param>
/// <param name="HasWindowsSdk">Whether a Windows SDK is installed.</param>
/// <param name="HasWindowsAppRuntime">Whether the Windows App SDK runtime is installed, so WinUI apps need not carry it.</param>
/// <param name="HasNetFrameworkPack">Whether a .NET Framework 4.x targeting pack is installed.</param>
/// <param name="Netcoredbg">netcoredbg, the debug adapter for .NET apps, or <see langword="null"/> when it is not installed.</param>
public sealed record BridgeToolset(
    string? DotNet,
    string? MsBuild,
    string? MsBuildVersion,
    bool HasMsvc,
    bool HasWindowsSdk,
    bool HasWindowsAppRuntime,
    bool HasNetFrameworkPack,
    string? Netcoredbg)
{
    /// <summary>No tools at all.</summary>
    public static BridgeToolset None { get; } = new(null, null, null, false, false, false, false, null);
}
