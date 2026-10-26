using System.Text.Json;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.Toolchains.Microchip;

/// <summary>User-owned XC installations. No Microchip compiler binaries are part of the APK.</summary>
public static class MicrochipInstallation
{
    /// <summary>The scripts shared by XC installations and permissioned USB support.</summary>
    public static IReadOnlyList<string> SupportFiles { get; } = ["assets.lock.json", "packs.lock.json", "install.py", "project.py", "usb_device.py", "usb_serial.py", "usb_worker.py", "bootloaders.py", "programmers.py", "hid.py", "usb_fd.c", "pymcuprog.devices.json", "gpsim_dap.py", "gpsim.devices.json", "usb-serial.LICENSE"];
    /// <summary>Guest directory for the installer, pack catalog and project tools.</summary>
    public const string ToolsDirectory = "/opt/tabstudio/microchip";

    /// <summary>Microchip's software license, displayed before compiler downloads are authorized.</summary>
    public const string LicenseAddress = "https://www.microchip.com/en-us/about/license-agreement-end-user";

    /// <summary>Whether the Store entry is an XC compiler.</summary>
    /// <param name="id">Store id.</param>
    public static bool IsCompiler(string id) => id is "xc8" or "xc32";

    /// <summary>Text resources compiled into the app, including the exact download pins.</summary>
    /// <param name="name">Resource filename.</param>
    public static string Resource(string name)
    {
        using var stream = typeof(MicrochipInstallation).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Microchip." + name)
            ?? throw new InvalidOperationException($"The Microchip support resource '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Records the user's acceptance for this precise compiler distribution.</summary>
    /// <param name="environment">Device environment.</param>
    /// <param name="id">Compiler id.</param>
    /// <param name="licenseText">The terms the user was shown.</param>
    public static void AcceptLicense(AlpineEnvironment environment, string id, string licenseText)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentException.ThrowIfNullOrEmpty(licenseText);
        var path = ConsentPath(environment, id);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"{DateTimeOffset.UtcNow:O}\n{LicenseAddress}\n\n{licenseText}");
    }

    /// <summary>Refuses installation before consent and installs the APK's separately built translator.</summary>
    /// <param name="environment">Device environment.</param>
    /// <param name="id">Compiler id.</param>
    public static void CheckConsent(AlpineEnvironment environment, string id)
    {
        if (!File.Exists(ConsentPath(environment, id)))
        {
            throw new ToolchainInstallException(InstallFailure.CommandFailed, "Open the Toolchain Store and accept Microchip's software license before installing this compiler.");
        }
    }

    /// <summary>Copies the APK's translator into PRoot's file system. PRoot's loader executes it.</summary>
    /// <param name="environment">Device environment.</param>
    public static void PrepareTranslator(AlpineEnvironment environment)
    {
        if (environment.Architecture == "x86_64")
        {
            return;
        }

        if (environment.Architecture != "aarch64" || environment.NativeLibraryDirectory is not { } native
            || !File.Exists(Path.Combine(native, "libbox64.so")))
        {
            throw new ToolchainInstallException(InstallFailure.RuntimeUnavailable, "XC compilers need 64-bit Android and this APK's box64 translator. Install an ARM64 build that includes PIC support, or use the Windows Bridge.");
        }

        var destination = Path.Combine(environment.Rootfs, ToolsDirectory.TrimStart('/'), "box64");
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(Path.Combine(native, "libbox64.so"), destination, overwrite: true);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(destination, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    internal static ToolchainDefinition WithSetup(ToolchainDefinition definition)
    {
        var files = SupportFiles;
        var setup = files.Select(name => new SetupStepDefinition
        {
            Name = "PIC " + name,
            File = new FileDefinition { Path = ToolsDirectory + "/" + name, Content = Resource(name) },
        }).ToList();
        setup.Add(new SetupStepDefinition
        {
            Name = "Install " + definition.Name + " " + definition.PinnedVersion,
            Command = ["python3", ToolsDirectory + "/install.py", "--compiler", definition.Id, "--accept-microchip-license"],
            Removes = ["/opt/microchip/" + definition.Id, "/usr/local/bin/" + definition.Id + "-cc", "/usr/local/bin/" + definition.Id + "-gcc", "/usr/local/bin/" + definition.Id + "-bin2hex"],
        });
        return definition with { Setup = setup };
    }

    private static string ConsentPath(AlpineEnvironment environment, string id)
    {
        if (!IsCompiler(id))
        {
            throw new ArgumentException("Expected XC8 or XC32.", nameof(id));
        }

        using var pins = JsonDocument.Parse(Resource("assets.lock.json"));
        var digest = pins.RootElement.GetProperty("assets").GetProperty(id).GetProperty("sha256").GetString();
        return Path.Combine(environment.Root, "consents", id + "-" + digest + ".txt");
    }
}
