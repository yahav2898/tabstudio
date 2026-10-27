using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.Testing;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using System.Reflection;

namespace TabStudio.App.Tests.Toolchains;

public sealed class EmbeddedSerialMonitorReservationTests
{
    [AvaloniaFact]
    public async Task Reservation_blocks_real_connect_command_and_setup_until_every_owner_releases()
    {
        using var temporary = new TempDirectory();
        var host = new CountingHost();
        using var tools = new ToolchainServices(new AppPaths(temporary.Path), usb: host);
        var ui = new Dispatcher();
        using var monitor = new SerialMonitorViewModel(tools, ui);
        monitor.SelectedDevice = host.Devices[0];
        Assert.True(monitor.ConnectCommand.CanExecute(null));
        var outer = monitor.ReserveDeviceOperation();
        var inner = monitor.ReserveDeviceOperation();
        Assert.False(monitor.ConnectCommand.CanExecute(null));
        // Direct execution bypasses a button's CanExecute check; the method
        // itself must refuse before PreparePicSupportAsync or USB permission.
        await monitor.ConnectCommand.ExecuteAsync(null);
        Assert.False(monitor.IsConnected);
        Assert.True(monitor.Output.IsEmpty);
        Assert.Equal(0, host.Opens);
        inner.Dispose();
        inner.Dispose();
        Assert.False(monitor.ConnectCommand.CanExecute(null));
        await monitor.ReleaseAsync();
        Assert.False(monitor.ConnectCommand.CanExecute(null));
        outer.Dispose();
        Assert.True(monitor.ConnectCommand.CanExecute(null));
        await monitor.ConnectCommand.ExecuteAsync(null);
        // Desktop setup deterministically refuses Android USB. Seeing this
        // only after release proves ordinary setup still executes unreserved.
        Assert.Contains("USB-OTG requires the Android Linux environment", monitor.Output.Text, StringComparison.Ordinal);
        Assert.False(monitor.IsConnected);
        Assert.True(monitor.ConnectCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Reservation_disables_the_method_before_validating_monitor_configuration()
    {
        using var temporary = new TempDirectory();
        using var tools = new ToolchainServices(new AppPaths(temporary.Path));
        using var monitor = new SerialMonitorViewModel(tools, new Dispatcher());
        monitor.SelectedDevice = new("attachment", 0x10c4, 0xea60, "adapter", []);
        monitor.Baud = "invalid";
        using (monitor.ReserveDeviceOperation())
        {
            await monitor.ConnectCommand.ExecuteAsync(null);
            Assert.True(monitor.Output.IsEmpty);
        }
        await monitor.ConnectCommand.ExecuteAsync(null);
        Assert.Contains("Enter a baud rate", monitor.Output.Text, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Scoped_debug_reservation_blocks_its_compound_device_and_allows_a_different_serial_adapter()
    {
        using var temporary = new TempDirectory();
        using var tools = new ToolchainServices(new AppPaths(temporary.Path));
        using var monitor = new SerialMonitorViewModel(tools, new Dispatcher());
        var probe = new UsbDeviceInfo("debug-probe", 0x0483, 0x374f, "ST-Link with CDC", []);
        var serial = new UsbDeviceInfo("independent-adapter", 0x10c4, 0xea60, "CP210x", []);
        using var reservation = monitor.ReserveDeviceOperation(probe.Id);
        monitor.SelectedDevice = probe;
        Assert.False(monitor.ConnectCommand.CanExecute(null));
        await monitor.ConnectCommand.ExecuteAsync(null);
        Assert.True(monitor.Output.IsEmpty);
        monitor.SelectedDevice = serial;
        Assert.True(monitor.ConnectCommand.CanExecute(null));
        await monitor.ConnectCommand.ExecuteAsync(null);
        Assert.Contains("USB-OTG requires the Android Linux environment", monitor.Output.Text, StringComparison.Ordinal);
        monitor.SelectedDevice = probe;
        Assert.False(monitor.ConnectCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task Debug_cleanup_keeps_the_monitor_reserved_until_server_exit_and_USB_disposal()
    {
        using var temporary = new TempDirectory();
        using var tools = new ToolchainServices(new AppPaths(temporary.Path));
        using var monitor = new SerialMonitorViewModel(tools, new Dispatcher());
        var handle = new DelayedHandle();
        var server = new ToolchainProcess(handle, new() { Program = "synthetic-debug-server" });
        var usb = new Connection();
        var reservation = monitor.ReserveDeviceOperation();
        var cleanup = (Task)typeof(WorkspaceViewModel).GetMethod("DisposeEmbeddedBackendAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [server, usb, null, null, reservation])!;
        Assert.False(cleanup.IsCompleted);
        Assert.False(monitor.ConnectCommand.CanExecute(null));
        Assert.False(usb.Disposed);
        handle.Exited.SetResult((0, null));
        await cleanup;
        Assert.True(handle.Disposed);
        Assert.True(usb.Disposed);
        Assert.True(monitor.ConnectCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public void Cross_thread_reservation_changes_are_immediate_but_command_notifications_use_dispatcher()
    {
        using var temporary = new TempDirectory();
        using var tools = new ToolchainServices(new AppPaths(temporary.Path));
        var ui = new Dispatcher { HasAccess = false };
        using var monitor = new SerialMonitorViewModel(tools, ui);
        var notifications = 0;
        monitor.ConnectCommand.CanExecuteChanged += (_, _) => notifications++;
        var reservation = monitor.ReserveDeviceOperation();
        Assert.False(monitor.ConnectCommand.CanExecute(null));
        Assert.Equal(0, notifications);
        ui.Drain();
        Assert.Equal(1, notifications);
        reservation.Dispose();
        Assert.True(monitor.ConnectCommand.CanExecute(null));
        Assert.Equal(1, notifications);
        ui.Drain();
        Assert.Equal(2, notifications);
        reservation.Dispose();
        ui.Drain();
        Assert.Equal(2, notifications);
    }

    private sealed class CountingHost : IUsbHost
    {
        public int Opens { get; private set; }
        public IReadOnlyList<UsbDeviceInfo> Devices { get; } = [new("attachment", 0x10c4, 0xea60, "adapter", [])];
        public Task<IUsbConnection> OpenAsync(UsbDeviceInfo device, CancellationToken cancellationToken)
        {
            Opens++;
            return Task.FromException<IUsbConnection>(new IOException("No physical USB in this test."));
        }
    }

    private sealed class Dispatcher : IUiDispatcher
    {
        private readonly Queue<Action> _posted = new();
        public bool HasAccess { get; set; } = true;
        public bool CheckAccess() => HasAccess;
        public void Post(Action action) => _posted.Enqueue(action);
        public Task InvokeAsync(Action action) { action(); return Task.CompletedTask; }
        public void Drain() { while (_posted.TryDequeue(out var action)) action(); }
    }

    private sealed class DelayedHandle : IProcessHandle
    {
        public TaskCompletionSource<(int, int?)> Exited { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public int Id => 1;
        public bool IsTerminal => false;
        public Stream Input => Stream.Null;
        public Stream Output => Stream.Null;
        public Stream? Error => Stream.Null;
        public Task<(int ExitCode, int? Signal)> WaitForExitAsync() => Exited.Task;
        public void Resize(TerminalSize size) { }
        public void CloseInput() { }
        public void KillTree() { }
        public void Dispose() => Disposed = true;
    }

    private sealed class Connection : IUsbConnection
    {
        public bool Disposed { get; private set; }
        public UsbDeviceInfo Device { get; } = new("debug-probe", 0x0483, 0x374f, "probe", []);
        public int FileDescriptor => 3;
        public void Dispose() => Disposed = true;
    }
}
