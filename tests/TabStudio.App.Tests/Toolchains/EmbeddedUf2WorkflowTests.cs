using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>BOOTSEL storage upload is reachable but cannot open a host computer's disks.</summary>
public sealed class EmbeddedUf2WorkflowTests
{
    [AvaloniaFact]
    public async Task Host_command_refuses_before_build_permission_or_storage_writes()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:pico]\nplatform = raspberrypi\nboard = pico\nframework = arduino\n");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);

        Assert.True(workspace.UploadUf2StorageCommand.CanExecute(null));
        await workspace.UploadUf2StorageCommand.ExecuteAsync(null);

        Assert.Contains("Permissioned BOOTSEL storage upload uses Android USB-OTG", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.Prompt.IsOpen);
        Assert.False(Directory.Exists(Path.Combine(project.Path, ".pio")));
        Assert.DoesNotContain("UF2 transferred", workspace.Output.Text, StringComparison.Ordinal);
    }
}
