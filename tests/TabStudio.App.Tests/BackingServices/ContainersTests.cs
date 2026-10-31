using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Containers.Images;
using TabStudio.Containers.Runtime;
using TabStudio.Containers.Store;
using TabStudio.Testing;

namespace TabStudio.App.Tests.BackingServices;

public sealed class ContainersTests
{
    [AvaloniaFact]
    public async Task Containers_window_shows_persisted_images_status_logs_and_touch_controls()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var id = new string('a', 64);
        var containerFolder = Path.Combine(paths.Root, "Containers", "containers", id);
        Directory.CreateDirectory(containerFolder);
        File.WriteAllText(Path.Combine(containerFolder, "container.json"), JsonSerializer.Serialize(new ContainerInfo
        {
            Id = id, Name = "project-db", ImageId = "sha256:" + id, ImageName = "postgres:17-alpine",
            Spec = new ContainerSpec { Image = "postgres:17-alpine" }, Status = ContainerStatus.Exited,
        }));
        File.WriteAllText(Path.Combine(containerFolder, "log.jsonl"), JsonSerializer.Serialize(new ContainerLogEntry(DateTimeOffset.UtcNow, 1,
            "database system is ready to accept connections")) + "\n");
        var solution = new ProjectTemplates(paths).Create(ProjectTemplates.ConsoleId, "ContainerApp");
        var compiler = new CompilerServices(paths);
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, false));
        using var workspace = new WorkspaceViewModel(compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(paths), paths);
        await workspace.LoadAsync(solution);
        var image = new StoredImage
        {
            Id = "sha256:" + id, Platform = OciPlatform.Linux("arm64"), Config = new(),
            Tags = ["docker.io/library/postgres:17-alpine"], Size = 200_000_000,
        };
        Directory.CreateDirectory(workspace.Containers.Engine.Store.RootfsOf(image.Id));
        await workspace.Containers.Engine.Store.RecordAsync(image);
        await workspace.Containers.RefreshAsync();
        workspace.Containers.ImageName = "redis:7-alpine";
        await workspace.Containers.RefreshAsync();
        Assert.Equal("redis:7-alpine", workspace.Containers.ImageName);
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Containers);
        Assert.True(workspace.IsContainersSelected);
        Assert.Contains(workspace.Activity.Items, i => i.Title == "Containers");
        Assert.Single(workspace.Containers.Images);
        Assert.Equal("project-db", Assert.Single(workspace.Containers.Items).Name);
        Assert.Contains("ready to accept", workspace.Containers.Logs, StringComparison.Ordinal);
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => window.IsVisible);
            UiTestHelpers.SaveScreenshot(window, "61-containers");
            window.Width = 800; window.Height = 1280;
            window.UpdateLayout();
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Containers);
            UiTestHelpers.SaveScreenshot(window, "61-containers-portrait");
        }
        finally { window.Close(); }
    }
}
