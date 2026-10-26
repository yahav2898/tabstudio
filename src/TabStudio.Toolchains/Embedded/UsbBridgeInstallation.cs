using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.Toolchains.Embedded;

/// <summary>Installs the app-owned USB bridges without downloading any vendor compiler.</summary>
public static class UsbBridgeInstallation
{
    /// <summary>Private guest directory for bridge sources, drivers and native libraries.</summary>
    public const string ToolsDirectory = "/opt/tabstudio/embedded";

    /// <summary>Packages from signed Alpine indexes needed to compile and run the bridges.</summary>
    public static IReadOnlyList<string> Packages { get; } = ["python3", "libusb", "libusb-dev", "build-base"];

    /// <summary>Sources copied from the APK, reusing the existing permissioned PIC drivers.</summary>
    public static IReadOnlyDictionary<string, string> SupportFiles => new Dictionary<string, string>
    {
        ["usb_fd.c"] = MicrochipInstallation.Resource("usb_fd.c"),
        ["usb_device.py"] = MicrochipInstallation.Resource("usb_device.py"),
        ["usb_serial.py"] = MicrochipInstallation.Resource("usb_serial.py"),
        ["serial_bridge.py"] = Resource("serial_bridge.py"),
        ["serial_posix.c"] = Resource("serial_posix.c"),
        ["usbfs_fd.c"] = Resource("usbfs_fd.c"),
    };

    /// <summary>Native build commands; failures must be surfaced before any flashing attempt.</summary>
    public static IReadOnlyList<ProcessStartSpec> BuildCommands { get; } =
    [
        new() { Program = "cc", Arguments = ["-std=gnu17", "-O2", "-Wall", "-Wextra", "-Werror", "-shared", "-fPIC", ToolsDirectory + "/usb_fd.c", "-o", ToolsDirectory + "/libusb-fd.so", "-lusb-1.0", "-ldl", "-pthread"] },
        new() { Program = "cc", Arguments = ["-std=gnu17", "-O2", "-Wall", "-Wextra", "-Werror", "-shared", "-fPIC", ToolsDirectory + "/serial_posix.c", "-o", ToolsDirectory + "/serial-posix.so", "-ldl", "-pthread"] },
        new() { Program = "cc", Arguments = ["-std=gnu17", "-O2", "-Wall", "-Wextra", "-Werror", "-shared", "-fPIC", ToolsDirectory + "/usbfs_fd.c", "-o", ToolsDirectory + "/usbfs-fd.so", "-ldl", "-pthread"] },
    ];

    /// <summary>Toolchain Store setup, fingerprinted by source contents and rebuilt after source changes.</summary>
    public static IReadOnlyList<SetupStepDefinition> Setup =>
    [
        .. SupportFiles.Select(file => new SetupStepDefinition { Name = "USB bridge " + file.Key, File = new FileDefinition { Path = ToolsDirectory + "/" + file.Key, Content = file.Value } }),
        .. BuildCommands.Select(command => new SetupStepDefinition { Name = "Build " + Path.GetFileName(command.Arguments[7]), Command = [command.Program, .. command.Arguments], RunAfterSetupChanges = true }),
    ];

    private static string Resource(string name)
    {
        using var stream = typeof(UsbBridgeInstallation).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Embedded." + name)
            ?? throw new InvalidOperationException("The embedded USB bridge resource is missing: " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
