using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>Native, source-pinned WCH-Link client installation; programming still requires the separate USB protocol guard.</summary>
public static class EmbeddedWlinkInstallation
{
    /// <summary>Auxiliary Toolchain Store identity.</summary>
    public const string Id = "embedded-wlink";
    /// <summary>Audited upstream CLI version.</summary>
    public const string Version = "0.1.3";
    /// <summary>Immutable upstream source commit, including its Cargo.lock.</summary>
    public const string Revision = "7ed4b99321fb8e88eb05f7553211b39d41196a6c";
    /// <summary>SHA-256 of the complete official source archive at the pinned commit.</summary>
    public const string Sha256 = "09f1220a9c238fc5f3537f2cf27cf7fc8a165b2845539c1b2da0938f7511e837";
    /// <summary>SHA-256 of the unchanged dependency lock file in that archive.</summary>
    public const string CargoLockSha256 = "048dc8ff2443e1739065d060c2b196cf53e8ea75dde268750ee99b429f1d91c7";
    /// <summary>Verified source archive byte count.</summary>
    public const long ArchiveBytes = 76_374;
    /// <summary>Private source, notices and native executable.</summary>
    public const string Prefix = "/opt/tabstudio/flash/wlink";
    /// <summary>Exact official source archive.</summary>
    public static Uri Address { get; } = new("https://github.com/ch32-rs/wlink/archive/" + Revision + ".tar.gz");
    /// <summary>Signed Alpine prerequisites. The pinned dependency set requires Rust 1.88 or newer.</summary>
    public static IReadOnlyList<string> Packages { get; } = ["python3", "build-base", "rust", "cargo", "pkgconf", "eudev-dev"];

    /// <summary>The exact app-owned native host installer also executed by CI.</summary>
    public static string InstallerSource
    {
        get
        {
            using var stream = typeof(EmbeddedWlinkInstallation).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Embedded.install_wlink_client.py")
                ?? throw new InvalidOperationException("The native WCH-Link installer resource is missing.");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }

    /// <summary>Reuses the shared verified source builder; no device is opened by installation.</summary>
    /// <param name="installerSource">The app-owned install_wlink_client.py resource.</param>
    public static IReadOnlyList<SetupStepDefinition> CreateSetup(string installerSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerSource);
        return
        [
            new()
            {
                Name = "WCH-Link 0.1.3 exact upstream source and dependency lock",
                Archive = new() { Url = Address, Sha256 = Sha256, Size = ArchiveBytes, Destination = Prefix },
            },
            new()
            {
                Name = "Shared native source installer",
                File = new() { Path = Prefix + "/tabstudio-source-installer.py", Content = EmbeddedFlashToolInstallation.SourceInstallerSource },
            },
            new()
            {
                Name = "Native WCH-Link host compiler selection",
                File = new() { Path = Prefix + "/tabstudio-install-wlink.py", Content = installerSource },
            },
            new()
            {
                Name = "Build locked native WCH-Link and validate its CLI without USB access",
                Command = ["python3", Prefix + "/tabstudio-install-wlink.py", Prefix + "/tabstudio-source-installer.py", Prefix],
                Removes = [Prefix, "/usr/local/bin/wlink"],
            },
        ];
    }
}
