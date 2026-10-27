using TabStudio.Toolchains.Catalog;

namespace TabStudio.Toolchains.Embedded.Installation;

/// <summary>Builds or validates hash-pinned flashing tools and adds explicit PATH aliases in their Alpine or glibc environment.</summary>
public static class EmbeddedFlashToolInstallation
{
    /// <summary>Catalog IDs whose native download is followed by the common validation/launcher step.</summary>
    public static bool Handles(string id) => id is "embedded-esptool" or "embedded-picotool" or "embedded-bossac" or "embedded-teensy" or "embedded-wchisp" or "embedded-mspdebug" or EmbeddedNordicInstallation.Id or EmbeddedWlinkInstallation.Id;

    /// <summary>The auxiliary Store entry needed for a built-in flashing route, where it can be installed automatically.</summary>
    public static string? ToolchainId(Flashing.EmbeddedFlashProtocol protocol) => protocol switch
    {
        Flashing.EmbeddedFlashProtocol.Esptool => "embedded-esptool",
        Flashing.EmbeddedFlashProtocol.Picotool => "embedded-picotool",
        Flashing.EmbeddedFlashProtocol.Bossac => "embedded-bossac",
        Flashing.EmbeddedFlashProtocol.Teensy => "embedded-teensy",
        Flashing.EmbeddedFlashProtocol.WchIsp => "embedded-wchisp",
        Flashing.EmbeddedFlashProtocol.WchLink => EmbeddedWlinkInstallation.Id,
        Flashing.EmbeddedFlashProtocol.MspDebug => "embedded-mspdebug",
        Flashing.EmbeddedFlashProtocol.NrfDfu => EmbeddedNordicInstallation.Id,
        Flashing.EmbeddedFlashProtocol.ProbeRs => "probe-rs",
        Flashing.EmbeddedFlashProtocol.Avrdude or Flashing.EmbeddedFlashProtocol.Stm32Dfu or Flashing.EmbeddedFlashProtocol.Stm32Flash or Flashing.EmbeddedFlashProtocol.OpenOcd => "embedded-support",
        _ => null,
    };

    /// <summary>Adds the app-owned installer script and validation step after the catalog's verified architecture-specific archive.</summary>
    public static ToolchainDefinition WithSetup(ToolchainDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!Handles(definition.Id))
        {
            return definition;
        }
        if (definition.Id == EmbeddedNordicInstallation.Id)
        {
            return definition with { Setup = [.. definition.Setup, .. EmbeddedNordicInstallation.Setup] };
        }
        if (definition.Id == EmbeddedWlinkInstallation.Id)
        {
            return definition with { Setup = [.. definition.Setup, .. EmbeddedWlinkInstallation.CreateSetup(EmbeddedWlinkInstallation.InstallerSource)] };
        }
        var tool = definition.Id switch
        {
            "embedded-esptool" => "esptool",
            "embedded-picotool" => "picotool",
            "embedded-bossac" => "bossac",
            "embedded-teensy" => "teensy_loader_cli",
            "embedded-wchisp" => "wchisp",
            "embedded-mspdebug" => "mspdebug",
            _ => throw new InvalidOperationException("Unsupported native flash client."),
        };
        var fromSource = definition.Id is not ("embedded-esptool" or "embedded-picotool");
        var directory = "/opt/tabstudio/flash/" + tool;
        var script = directory + "/tabstudio-install.py";
        var licenseSteps = tool == "picotool" ? new SetupStepDefinition[]
        {
            new()
            {
                Name = "Raspberry Pi picotool BSD-3-Clause notice",
                File = new() { Path = directory + "/LICENSE.TXT", Content = ReadSource("picotool.LICENSE") },
            },
        } : [];
        return definition with
        {
            Setup =
            [
                .. definition.Setup,
                .. licenseSteps,
                new()
                {
                    Name = "Native " + tool + " launcher installer",
                    File = new() { Path = script, Content = fromSource ? SourceInstallerSource : InstallerSource },
                },
                new()
                {
                    Name = "Validate native " + tool + " and expose its PATH aliases",
                    Command = ["python3", script, tool, directory, definition.PinnedVersion ?? throw new InvalidDataException("Native flash tool has no pinned version.")],
                    RunAfterSetupChanges = true,
                    Removes = tool == "esptool" ? ["/usr/local/bin/esptool", "/usr/local/bin/esptool.py"] : ["/usr/local/bin/" + tool],
                },
            ],
        };
    }

    /// <summary>The installer source, shared by product setup and native-version CI.</summary>
    public static string InstallerSource => ReadSource("install_native_client.py");

    /// <summary>Native source build recipes, shared by product setup and native-build CI.</summary>
    public static string SourceInstallerSource => ReadSource("install_source_client.py");

    /// <summary>The template's post-build exporter of resolved PlatformIO ESP upload inputs.</summary>
    public static string PlatformIoEspExporterSource => ReadSource("export_platformio_esp.py");

    private static string ReadSource(string name)
    {
        using var stream = typeof(EmbeddedFlashToolInstallation).Assembly.GetManifestResourceStream("TabStudio.Toolchains.Embedded." + name)
            ?? throw new InvalidOperationException("Native client installer resource is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
