using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Installation;

/// <summary>Toolchains whose parts differ by processor, such as Windows Compatibility's Wine and .NET runtime.</summary>
public sealed partial class LinuxToolchainInstaller
{
    /// <summary>The toolchain as it installs on this device's processor.</summary>
    /// <exception cref="ToolchainInstallException">It is not for this processor.</exception>
    private ToolchainDefinition Specialize(ToolchainDefinition toolchain)
    {
        if (!toolchain.SupportsArchitecture(_environment.Architecture))
        {
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, UnsupportedNote(toolchain));
        }

        return toolchain.ForArchitecture(_environment.Architecture);
    }

    /// <summary>The status of a toolchain this device's processor cannot run.</summary>
    private ToolchainStatus Unsupported(ToolchainDefinition toolchain) => new()
    {
        Toolchain = toolchain,
        State = ToolchainState.Unavailable,
        Note = UnsupportedNote(toolchain),
    };

    private string UnsupportedNote(ToolchainDefinition toolchain) =>
        $"{toolchain.Name} needs a tablet with a {string.Join(" or ", toolchain.Architectures.Keys.Order(StringComparer.Ordinal).Select(ProcessorName))} processor; this one's is {(_environment.Architecture is { } architecture ? ProcessorName(architecture) : "not one of them")}.";

    private static string ProcessorName(string architecture) => architecture switch
    {
        "aarch64" => "64-bit ARM",
        "x86_64" => "64-bit x86",
        "armv7" => "32-bit ARM",
        _ => architecture,
    };
}
