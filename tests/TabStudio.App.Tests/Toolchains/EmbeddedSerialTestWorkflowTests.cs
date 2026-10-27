using System.Text.Json;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Testing;

namespace TabStudio.App.Tests.Toolchains;

public sealed class EmbeddedSerialTestWorkflowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    [AvaloniaFact]
    public async Task Serial_capture_command_creates_distinct_explicit_settings_and_explains_existing_firmware_prerequisite()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:uno]\nboard=uno\nplatform=atmelavr\n");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        Assert.True(workspace.RunSerialEmbeddedTestsCommand.CanExecute(null));
        await workspace.RunSerialEmbeddedTestsCommand.ExecuteAsync(null);
        var path = project.Resolve(EmbeddedSerialTestOptions.FileName);
        var options = JsonSerializer.Deserialize<EmbeddedSerialTestOptions>(await File.ReadAllTextAsync(path), JsonOptions);
        Assert.NotNull(options);
        Assert.Equal(0, options.BaudRate);
        Assert.Contains("Program the board with your Unity test firmware first", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Contains("does not build, upload", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(0, workspace.Tests.PassedCount);
        Assert.False(File.Exists(project.Resolve(".tabstudio/embedded-tests.json")));
        Assert.False(Directory.Exists(project.Resolve(".pio")));
    }

    [AvaloniaFact]
    public async Task Configured_capture_on_host_refuses_USB_before_any_test_or_build_is_claimed()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:uno]\nboard=uno\nplatform=atmelavr\n");
        project.Write(EmbeddedSerialTestOptions.FileName, "{\"BaudRate\":115200,\"BoardLabel\":\"user label is not chip proof\"}");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        await workspace.RunSerialEmbeddedTestsCommand.ExecuteAsync(null);
        Assert.Contains("USB-OTG serial device on Android", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(0, workspace.Tests.PassedCount);
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.False(workspace.Prompt.IsOpen);
        Assert.False(Directory.Exists(project.Resolve(".pio")));
    }
}
