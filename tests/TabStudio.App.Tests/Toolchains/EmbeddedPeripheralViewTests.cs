using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Testing;

namespace TabStudio.App.Tests.Toolchains;

/// <summary>The dock browses actual vendor metadata without treating reset values as target reads.</summary>
public sealed class EmbeddedPeripheralViewTests
{
    [AvaloniaFact]
    public async Task Dock_loads_metadata_and_changes_selection_without_target_access_or_invented_values()
    {
        using var fixture = new WorkspaceFixture();
        using var project = new TempDirectory();
        project.Write("platformio.ini", "[env:board]\nplatform=ststm32\nboard=nucleo_f401re\n");
        var path = project.Write("device.svd", """
            <device><name>DemoChip</name><version>1</version><description>Fixture metadata</description>
              <cpu><name>CM4</name><endian>little</endian></cpu><addressUnitBits>8</addressUnitBits><width>32</width><size>32</size><access>read-write</access><resetValue>0</resetValue>
              <peripherals><peripheral><name>GPIO</name><description>Port</description><baseAddress>0x40000000</baseAddress><registers>
                <register><name>MODE</name><description>Pin mode</description><addressOffset>0</addressOffset><fields>
                  <field><name>MODE0</name><description>Mode selection</description><bitOffset>0</bitOffset><bitWidth>2</bitWidth></field>
                </fields></register>
                <register><name>DATA</name><description>Pin data</description><addressOffset>4</addressOffset><fields>
                  <field><name>PIN0</name><description>Data bit</description><bitOffset>0</bitOffset><bitWidth>1</bitWidth></field>
                </fields></register>
              </registers></peripheral></peripherals>
            </device>
            """);
        var workspace = fixture.Workspace;
        workspace.SetUpProjectsWhenTheyOpen = false;
        await workspace.LoadAsync(project.Path);
        var loading = workspace.LoadEmbeddedSvdCommand.ExecuteAsync(null);
        await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, TimeSpan.FromSeconds(5));
        workspace.Prompt.Text = path;
        workspace.Prompt.ConfirmCommand.Execute(null);
        await loading;
        workspace.ShowEmbeddedPeripheralsCommand.Execute(null);

        Assert.Equal(BottomPanel.Peripherals, workspace.SelectedPanel);
        Assert.Equal("DemoChip", workspace.EmbeddedPeripheralDevice);
        Assert.Equal("MODE", workspace.SelectedEmbeddedRegister?.Name);
        Assert.Equal("MODE0", Assert.Single(workspace.EmbeddedRegisterFields).Name);
        Assert.Equal("—", workspace.EmbeddedRegisterFields[0].Value);
        Assert.False(workspace.ReadSelectedEmbeddedRegisterCommand.CanExecute(null));
        Assert.False(workspace.EditSelectedEmbeddedRegisterCommand.CanExecute(null));
        workspace.SelectedEmbeddedRegister = workspace.EmbeddedRegisterCatalog[1];
        Assert.Equal("PIN0", Assert.Single(workspace.EmbeddedRegisterFields).Name);
        Assert.Contains("0x40000004", workspace.EmbeddedPeripheralDescription, StringComparison.Ordinal);
        Assert.Contains("Metadata only", workspace.EmbeddedPeripheralSnapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("readback verified", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.Prompt.IsOpen);

        var window = new Window { Width = 1100, Height = 650, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try { UiTestHelpers.SaveScreenshot(window, "embedded-02-peripherals-dock"); }
        finally { window.Close(); }
    }
}
