using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.LanguageServers.Configuration;
using TabStudio.Testing;
using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.App.Tests.Toolchains;

public sealed class EmbeddedDebugWorkflowTests
{
    [Fact]
    public void Explicit_on_chip_adapter_does_not_replace_normal_native_file_debugging()
    {
        var registry = LanguageServerRegistry.BuiltIn;
        var adapter = registry.FindAdapter("embedded-gdb");
        Assert.NotNull(adapter);
        Assert.Equal("gdb-multiarch", adapter.Command);
        Assert.Equal(new[] { "--quiet", "--nx", "--interpreter=dap" }, adapter.Arguments);
        Assert.Equal("embedded-debug", adapter.Package);
        var probeRs = registry.FindAdapter("embedded-probe-rs");
        Assert.NotNull(probeRs);
        Assert.Equal("probe-rs", probeRs.Command);
        Assert.Equal("probe-rs", probeRs.Package);
        Assert.Equal(new[] { "dap-server" }, probeRs.Arguments);
        Assert.Equal("lldb-dap", registry.FindAdaptersForLanguage("cpp").First().Id);
        Assert.Equal("lldb-dap", registry.FindAdaptersForLanguage("c").First().Id);
    }

    [AvaloniaFact]
    public async Task Debug_command_opens_explicit_configuration_before_starting_any_target()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:board]\nplatform=ststm32\nboard=nucleo_f401re\n");
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        Assert.True(workspace.DebugEmbeddedChipCommand.CanExecute(null));
        await workspace.DebugEmbeddedChipCommand.ExecuteAsync(null);
        var path = Path.Combine(project.Path, EmbeddedDebugProfile.FileName);
        Assert.True(File.Exists(path));
        Assert.Equal(path, workspace.ActiveDocument?.FilePath);
        Assert.Contains("SELECT_ACTUAL_PROBE", File.ReadAllText(path), StringComparison.Ordinal);
        Assert.Contains("Upload that same firmware", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.IsDebuggingWithAdapter);
        Assert.False(workspace.Prompt.IsOpen);
    }

    [AvaloniaFact]
    public async Task Tcl_injection_is_reported_before_build_install_or_USB_access()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:board]\nplatform=ststm32\nboard=nucleo_f401re\n");
        project.Write(EmbeddedDebugProfile.FileName, """
            {"firmware":"app.elf","interfaceConfig":"interface/stlink.cfg","targetConfig":"target/stm32f4x.cfg; mass_erase"}
            """);
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        await workspace.DebugEmbeddedChipCommand.ExecuteAsync(null);
        Assert.Equal(IdeState.Failed, workspace.State);
        Assert.Contains("Select an installed OpenOCD", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.IsDebuggingWithAdapter);
        Assert.False(workspace.Prompt.IsOpen);
    }
}
