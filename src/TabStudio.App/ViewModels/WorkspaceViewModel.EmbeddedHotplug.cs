using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private IUsbHotplugHost? _embeddedHotplug;

    private void InitializeEmbeddedHotplug()
    {
        DisposeEmbeddedHotplug();
        if (_toolchains.Usb is IUsbHotplugHost host)
        {
            _embeddedHotplug = host;
            host.DevicesChanged += OnEmbeddedUsbChanged;
            if (!SerialMonitor.IsConnected)
            {
                SerialMonitor.Refresh();
            }
        }
    }

    private void DisposeEmbeddedHotplug()
    {
        if (_embeddedHotplug is { } host)
        {
            _embeddedHotplug = null;
            host.DevicesChanged -= OnEmbeddedUsbChanged;
        }
    }

    private void OnEmbeddedUsbChanged(object? sender, UsbDeviceChangedEventArgs change) =>
        _ui.Post(() => _ = HandleEmbeddedUsbChangeAsync(sender, change));

    private async Task HandleEmbeddedUsbChangeAsync(object? sender, UsbDeviceChangedEventArgs change)
    {
        if (_disposed || !ReferenceEquals(sender, _embeddedHotplug))
        {
            return;
        }
        try
        {
            if (change.Kind == UsbDeviceChangeKind.Detached && SerialMonitor.ConnectedDeviceId == change.Device.Id)
            {
                await SerialMonitor.ReleaseAsync();
            }
            if (_disposed || !ReferenceEquals(sender, _embeddedHotplug))
            {
                return;
            }
            if (!SerialMonitor.IsConnected)
            {
                SerialMonitor.Refresh();
            }
            var candidates = new EmbeddedDeviceDatabase().Identify(change.Device);
            var name = candidates.Count > 0 ? candidates[0].Name : change.Device.Name;
            var transition = change.Kind == UsbDeviceChangeKind.Attached ? "attached" : "detached";
            var message = $"USB {transition}: {name} ({change.Device.VendorId:X4}:{change.Device.ProductId:X4})";
            Output.WriteLine(message, OutputKind.Info);
            if (_run is null && State is IdeState.Ready or IdeState.Failed)
            {
                StatusText = message;
            }
        }
        catch (Exception error) when (error is IOException or InvalidOperationException)
        {
            if (!_disposed)
            {
                Output.WriteLine("USB attachment update: " + error.Message, OutputKind.Info);
            }
        }
    }
}
