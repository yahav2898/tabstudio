using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.OS;
using TabStudio.Toolchains.Microchip;
using System.Runtime.InteropServices;

namespace TabStudio.Android;

/// <summary>Android owns USB permission; Linux receives only the permitted connection's descriptor.</summary>
internal sealed class AndroidUsbHost(Context context) : IUsbHost, IUsbHotplugHost
{
    private readonly object _hotplugGate = new();
    private EventHandler<UsbDeviceChangedEventArgs>? _devicesChanged;
    private HotplugReceiver? _hotplugReceiver;
    private Dictionary<string, UsbDeviceInfo> _knownDevices = new(StringComparer.Ordinal);

    public event EventHandler<UsbDeviceChangedEventArgs>? DevicesChanged
    {
        add
        {
            if (value is null)
            {
                return;
            }
            lock (_hotplugGate)
            {
                if (_devicesChanged is null)
                {
                    StartHotplug();
                }
                _devicesChanged += value;
            }
        }
        remove
        {
            lock (_hotplugGate)
            {
                _devicesChanged -= value;
                if (_devicesChanged is null)
                {
                    StopHotplug();
                }
            }
        }
    }

    private void StartHotplug()
    {
        _knownDevices = Devices.ToDictionary(device => device.Id, StringComparer.Ordinal);
        var receiver = new HotplugReceiver(this);
        using var filter = new IntentFilter();
        filter.AddAction(UsbManager.ActionUsbDeviceAttached);
        filter.AddAction(UsbManager.ActionUsbDeviceDetached);
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                context.RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
            }
            else
            {
#pragma warning disable CA1422 // The scoped receiver-flags overload was introduced in API 33.
                context.RegisterReceiver(receiver, filter);
#pragma warning restore CA1422
            }
            _hotplugReceiver = receiver;
        }
        catch
        {
            receiver.Dispose();
            _knownDevices.Clear();
            throw;
        }
    }

    private void StopHotplug()
    {
        if (_hotplugReceiver is not { } receiver)
        {
            return;
        }
        _hotplugReceiver = null;
        try
        {
            context.UnregisterReceiver(receiver);
        }
        finally
        {
            receiver.Dispose();
            _knownDevices.Clear();
        }
    }

    private void RefreshHotplug()
    {
        List<UsbDeviceChangedEventArgs> changes = [];
        EventHandler<UsbDeviceChangedEventArgs>? listeners;
        lock (_hotplugGate)
        {
            if (_hotplugReceiver is null || _devicesChanged is null)
            {
                return;
            }
            // The USB service's current list is authoritative; broadcast extras are never trusted.
            var current = Devices.ToDictionary(device => device.Id, StringComparer.Ordinal);
            foreach (var previous in _knownDevices.Values)
            {
                if (!current.TryGetValue(previous.Id, out var now) || now.VendorId != previous.VendorId || now.ProductId != previous.ProductId)
                {
                    changes.Add(new(previous, UsbDeviceChangeKind.Detached));
                }
            }
            foreach (var device in current.Values)
            {
                if (!_knownDevices.TryGetValue(device.Id, out var previous) || previous.VendorId != device.VendorId || previous.ProductId != device.ProductId)
                {
                    changes.Add(new(device, UsbDeviceChangeKind.Attached));
                }
            }
            _knownDevices = current;
            listeners = _devicesChanged;
        }
        foreach (var change in changes)
        {
            listeners?.Invoke(this, change);
        }
    }

    private UsbManager Manager => (UsbManager?)context.GetSystemService(Context.UsbService)
        ?? throw new IOException("This Android device has no USB host service.");

    public IReadOnlyList<UsbDeviceInfo> Devices => Manager.DeviceList?.Values.Select(Describe).ToArray() ?? [];

    public async Task<IUsbConnection> OpenAsync(UsbDeviceInfo device, CancellationToken cancellationToken)
    {
        var manager = Manager;
        var attached = manager.DeviceList?.Values.FirstOrDefault(d => d.DeviceName == device.Id && d.VendorId == device.VendorId && d.ProductId == device.ProductId)
            ?? throw new IOException("The selected USB device was disconnected.");
        if (!manager.HasPermission(attached))
        {
            var action = context.PackageName + ".USB_PERMISSION." + Guid.NewGuid().ToString("N");
            using var receiver = new PermissionReceiver(action);
            using var intent = new Intent(action).SetPackage(context.PackageName);
            using var pending = PendingIntent.GetBroadcast(context, 0, intent, PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent)
                ?? throw new IOException("Could not create the USB permission request.");
            if (OperatingSystem.IsAndroidVersionAtLeast(33))
            {
                context.RegisterReceiver(receiver, new IntentFilter(action), ReceiverFlags.NotExported);
            }
            else
            {
#pragma warning disable CA1422 // The flags overload was introduced in API 33.
                context.RegisterReceiver(receiver, new IntentFilter(action));
#pragma warning restore CA1422
            }

            try
            {
                manager.RequestPermission(attached, pending);
                await receiver.Completed.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                context.UnregisterReceiver(receiver);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        // Trust the USB service's state, never broadcast extras supplied by another sender.
        if (!manager.HasPermission(attached))
        {
            throw new UnauthorizedAccessException("USB permission was not granted.");
        }

        var connection = manager.OpenDevice(attached) ?? throw new IOException("The USB device could not be opened. Reconnect it and try again.");
        var descriptorFlags = Fcntl(connection.FileDescriptor, 1, 0); // F_GETFD
        if (descriptorFlags < 0 || Fcntl(connection.FileDescriptor, 2, descriptorFlags | 1) < 0) // F_SETFD, FD_CLOEXEC
        {
            connection.Close();
            connection.Dispose();
            throw new IOException("Could not protect the USB descriptor from unrelated child processes.");
        }
        return new Connection(Describe(attached), connection);
    }

    [DllImport("libc", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int Fcntl(int fd, int command, int value);

    private static UsbDeviceInfo Describe(UsbDevice device)
    {
        var interfaces = new List<UsbInterfaceInfo>();
        for (var i = 0; i < device.InterfaceCount; i++)
        {
            var item = device.GetInterface(i)!;
            var endpoints = new List<UsbEndpointInfo>();
            for (var e = 0; e < item.EndpointCount; e++)
            {
                var endpoint = item.GetEndpoint(e)!;
                endpoints.Add(new UsbEndpointInfo((int)endpoint.Address, (int)endpoint.Type, endpoint.MaxPacketSize));
            }

            interfaces.Add(new UsbInterfaceInfo(item.Id, (int)item.InterfaceClass, (int)item.InterfaceSubclass, item.InterfaceProtocol, endpoints));
        }

        return new UsbDeviceInfo(device.DeviceName, device.VendorId, device.ProductId,
            device.ProductName ?? $"USB device {device.DeviceId}", interfaces);
    }

    private sealed class HotplugReceiver(AndroidUsbHost owner) : BroadcastReceiver
    {
        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action is UsbManager.ActionUsbDeviceAttached or UsbManager.ActionUsbDeviceDetached)
            {
                owner.RefreshHotplug();
            }
        }
    }

    private sealed class PermissionReceiver(string action) : BroadcastReceiver
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (intent?.Action == action)
            {
                Completed.TrySetResult();
            }
        }
    }

    private sealed class Connection(UsbDeviceInfo device, UsbDeviceConnection connection) : IUsbConnection
    {
        private bool _disposed;
        public UsbDeviceInfo Device => device;
        public int FileDescriptor => _disposed ? -1 : connection.FileDescriptor;
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                connection.Close();
                connection.Dispose();
            }
        }
    }
}
