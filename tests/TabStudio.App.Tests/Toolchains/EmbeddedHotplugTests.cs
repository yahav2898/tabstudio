using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.Tests.ViewModels;
using TabStudio.Testing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.App.Tests.Toolchains;

public sealed class EmbeddedHotplugTests
{
    [AvaloniaFact]
    public async Task Attachment_changes_refresh_disconnected_monitor_without_permission_or_auto_programming()
    {
        using var temporary = new TempDirectory();
        var host = new FakeHotplugHost();
        using var tools = new ToolchainServices(new AppPaths(temporary.Path), usb: host);
        using var fixture = new WorkspaceFixture(toolchains: tools);
        var workspace = fixture.Workspace;
        Assert.Equal(1, host.SubscriberCount);
        var device = new UsbDeviceInfo("attachment-1", 0x0483, 0x3748, "ST-Link", []);
        host.Devices = [device];
        host.Publish(device, UsbDeviceChangeKind.Attached);
        await UiTestHelpers.WaitUntilAsync(() => workspace.SerialMonitor.Devices.Count == 1, because: "the attachment notification to refresh the monitor");
        Assert.Equal(device.Id, workspace.SerialMonitor.SelectedDevice?.Id);
        Assert.Contains("ST-Link probe (target must be queried)", workspace.Output.Text, StringComparison.Ordinal);
        Assert.False(workspace.SerialMonitor.IsConnected);
        Assert.Equal(0, host.PermissionRequests);

        host.Devices = [];
        host.Publish(device, UsbDeviceChangeKind.Detached);
        await UiTestHelpers.WaitUntilAsync(() => workspace.SerialMonitor.Devices.Count == 0, because: "detach to remove the disconnected device");
        Assert.Contains("USB detached", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(0, host.PermissionRequests);
    }

    [AvaloniaFact]
    public async Task Workspace_disposal_unsubscribes_and_ignores_queued_notifications()
    {
        using var temporary = new TempDirectory();
        var host = new FakeHotplugHost();
        using var tools = new ToolchainServices(new AppPaths(temporary.Path), usb: host);
        using var fixture = new WorkspaceFixture(toolchains: tools);
        Assert.Equal(1, host.SubscriberCount);
        var device = new UsbDeviceInfo("queued-attachment", 0x10C4, 0xEA60, "CP210x", []);
        host.Devices = [device];
        host.Publish(device, UsbDeviceChangeKind.Attached);
        fixture.Workspace.Dispose();
        Assert.Equal(0, host.SubscriberCount);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Empty(fixture.Workspace.SerialMonitor.Devices);
        Assert.Equal(0, host.PermissionRequests);
    }

    private sealed class FakeHotplugHost : IUsbHost, IUsbHotplugHost
    {
        private EventHandler<UsbDeviceChangedEventArgs>? _changed;
        public IReadOnlyList<UsbDeviceInfo> Devices { get; set; } = [];
        public int PermissionRequests { get; private set; }
        public int SubscriberCount => _changed?.GetInvocationList().Length ?? 0;
        public event EventHandler<UsbDeviceChangedEventArgs>? DevicesChanged
        {
            add => _changed += value;
            remove => _changed -= value;
        }
        public void Publish(UsbDeviceInfo device, UsbDeviceChangeKind kind) => _changed?.Invoke(this, new(device, kind));
        public Task<IUsbConnection> OpenAsync(UsbDeviceInfo device, CancellationToken cancellationToken)
        {
            PermissionRequests++;
            return Task.FromException<IUsbConnection>(new NotSupportedException("This host never grants a device."));
        }
    }
}
