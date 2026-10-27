using System.Text.Json;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private static readonly string[] EspMetadataFileNames = ["tabstudio-flasher-args.json", "flasher_args.json"];

    private async Task TryCreateEspMetadataProfileAsync(EmbeddedProject project, string profilePath)
    {
        string? metadata = null;
        if (project.Kind == EmbeddedProjectKind.EspIdf)
        {
            metadata = Path.Combine(project.Directory, ".tabstudio", "embedded", "build", "flasher_args.json");
        }
        else if (project.Kind == EmbeddedProjectKind.PlatformIO)
        {
            var environment = EmbeddedBuildPlanner.LoadOptions(project.Directory).Environment ?? project.DefaultEnvironment ??
                (project.Environments.Count == 1 ? project.Environments[0].Name : null);
            if (environment is not null)
            {
                var build = Path.Combine(project.Directory, ".pio", "build", environment);
                metadata = EspMetadataFileNames.Select(name => Path.Combine(build, name)).FirstOrDefault(File.Exists);
            }
        }
        if (metadata is null || !File.Exists(metadata)) return;
        var manifest = await Task.Run(() => EspFlashManifestReader.Read(metadata));
        var relative = Path.GetRelativePath(project.Directory, metadata).Replace(Path.DirectorySeparatorChar, '/');
        var request = new EmbeddedFlashRequest
        {
            Protocol = EmbeddedFlashProtocol.Esptool, Target = manifest.Chip, FirmwarePath = relative,
            EspFlasherArguments = relative, WorkingDirectory = project.Directory, SerialPort = "/dev/ttyUSB0",
        };
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(request, EmbeddedJson) + "\n");
        Output.WriteLine("Created ESP flash profile from validated build metadata: " + manifest.Chip + ", " + manifest.Images.Count + " separate images; unmentioned erase sectors are preserved.", OutputKind.Info);
    }
}
