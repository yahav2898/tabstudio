namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    public ContainersViewModel Containers { get; private set; } = null!;
    public bool IsContainersSelected => SelectedSideBarPanel == SideBarPanel.Containers;

    private void InitializeContainers()
    {
        Containers = new ContainersViewModel(_toolchains, _paths, _ui, Operations, address =>
        {
            WebPreview.Show("Container", address, null);
            SelectSideBarPanel(SideBarPanel.WebPreview);
        });
        Terminal.BeforeLocalShell = Containers.PrepareTerminalCommand;
        // Android ends an app with every process it started; a container stops first so the IDE stays.
        _device.MemoryPressureRaised += (_, pressure) => _ = Containers.RelieveMemoryAsync(pressure == TabStudio.App.Platform.MemoryPressure.Critical);
    }
}
