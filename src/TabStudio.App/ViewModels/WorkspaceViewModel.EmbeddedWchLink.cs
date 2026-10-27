using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private async Task UploadGuardedWchLinkAsync(EmbeddedFlashRequest request, UsbDeviceInfo device)
    {
        var plan = WchLinkSession.Plan(request);
        using var cancellation = new CancellationTokenSource();
        _run = cancellation;
        State = IdeState.Running;
        StatusText = "Identifying and programming guarded WCH-Link target";
        ShowEmbeddedFlashOutput();
        try
        {
            await SerialMonitor.ReleaseAsync();
            await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
            await PrepareEmbeddedProtocolAsync(request, cancellation.Token);
            var result = await WchLinkSession.ExecuteAsync(_toolchains.Runtime, plan,
                token => _toolchains.Usb.OpenAsync(device, token), Path.Combine(_paths.Root, "embedded-usb"),
                new Progress<string>(line => Output.WriteLine(line.TrimEnd(), OutputKind.Info)), cancellation.Token);
            await RememberEmbeddedDeviceAsync(device, request.Target, request);
            _lastEmbeddedSerialDevice = null;
            Output.WriteLine("Independent WCH-Link flash readback matched. Original firmware SHA-256: " + result.FirmwareSha256, OutputKind.Info);
            State = IdeState.Ready;
            StatusText = "Firmware upload verified";
        }
        finally
        {
            _run = null;
            if (State == IdeState.Running) State = IdeState.Ready;
        }
    }

    private async Task<string> BackupGuardedWchLinkAsync(EmbeddedFlashRequest request, string destination, CancellationToken token)
    {
        if (_toolchains.Environment is null) throw new NotSupportedException("The guarded WCH-Link route requires Android's permissioned USB connection.");
        if (request.ApplicationFlash.Count != 1) throw new InvalidOperationException("Choose exactly one CH32V003 application-flash interval to back up.");
        var interval = request.ApplicationFlash[0];
        var start = interval.Start < 0x08000000 ? checked(interval.Start + 0x08000000) : interval.Start;
        var devices = _toolchains.Usb.Devices;
        if (devices.Count == 0) throw new IOException("Attach the WCH-Link probe using USB-OTG.");
        var choice = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("WCH-Link flash backup", "Choose the exact probe whose CH32V003 code flash should be read twice.", "Select", devices.Select(item => item.ToString()).ToArray());
        if (choice is null) throw new OperationCanceledException(token);
        var device = devices[choice.Value];
        await SerialMonitor.ReleaseAsync();
        await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), token);
        return await WchLinkSession.BackupAsync(_toolchains.Runtime, cancellation => _toolchains.Usb.OpenAsync(device, cancellation),
            Path.Combine(_paths.Root, "embedded-usb"), destination, checked((uint)start), checked((int)interval.Length),
            new Progress<string>(line => Output.WriteLine(line.TrimEnd(), OutputKind.Info)), token);
    }
}
