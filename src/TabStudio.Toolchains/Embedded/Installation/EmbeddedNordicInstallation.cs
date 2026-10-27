using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>Official unchanged legacy Nordic serial DFU bundle, native on x64 and explicitly translated on ARM64.</summary>
public static class EmbeddedNordicInstallation
{
    /// <summary>Auxiliary Toolchain Store identity.</summary>
    public const string Id = "embedded-nrfutil";
    /// <summary>The reviewed original CLI version inside the vendor's nrf5sdk-tools 1.1.0 package.</summary>
    public const string Version = "6.1.7";
    /// <summary>SHA-256 of the complete original vendor archive, independently matched to its registry checksum.</summary>
    public const string Sha256 = "84b4214c46b917696b171a692a656b0f93e7200dc93dc3ff33fafd6d88b31e4f";
    /// <summary>Original archive URL, including exact package/version/host architecture.</summary>
    public static Uri Address { get; } = new("https://files.nordicsemi.com/artifactory/swtools/external/nrfutil/packages/nrfutil-nrf5sdk-tools/nrfutil-nrf5sdk-tools-x86_64-unknown-linux-gnu-1.1.0.tar.gz");
    /// <summary>Original archive size, in bytes.</summary>
    public const long ArchiveBytes = 32_471_565;
    /// <summary>Private unchanged package extraction, separate from source workspaces.</summary>
    public const string DownloadDirectory = "/opt/tabstudio/nordic-dfu-download";
    /// <summary>Private installed vendor data, preserving all licenses and documentation.</summary>
    public const string Prefix = "/opt/tabstudio/nordic-dfu";
    /// <summary>Path to the x64 control shim built natively or by the signed Ubuntu cross compiler.</summary>
    public const string X64SerialShim = "/opt/tabstudio/embedded/serial-posix-x86_64.so";

    /// <summary>Archive and install steps; shared USB support files must already have been prepared in glibc.</summary>
    public static IReadOnlyList<SetupStepDefinition> Setup { get; } =
    [
        new()
        {
            Name = "Verified official Nordic legacy serial DFU bundle",
            Archive = new() { Url = Address, Sha256 = Sha256, Size = ArchiveBytes, Destination = DownloadDirectory },
        },
        new()
        {
            Name = "Validate unchanged Nordic serial DFU and create its dedicated launcher",
            Command = ["python3", "/opt/tabstudio/embedded/install_nordic_client.py"],
            Removes = [Prefix, "/usr/local/bin/nrfutil"],
        },
    ];

}
