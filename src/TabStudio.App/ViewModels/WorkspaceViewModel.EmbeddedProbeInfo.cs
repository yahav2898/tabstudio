using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Reads upstream probe-rs debug-port/IDCODE/CPUID information from exactly one permissioned USB probe.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task IdentifyEmbeddedTargetAsync()
    {
        try
        {
            if (IsRemoteWorkspace || _toolchains.Environment is null)
                throw new NotSupportedException("Identify target uses an Android USB-OTG connection. On a desktop, use its installed probe-rs info command.");
            var devices = _toolchains.Usb.Devices;
            if (devices.Count == 0) throw new IOException("Connect the debug probe using USB-OTG.");
            var index = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Identify target", "Choose the debug probe connected to the target.", "Identify", devices.Select(device => device.ToString()).ToArray());
            if (index is not { } choice) return;
            var selected = devices[choice];
            if (selected.Interfaces.Count > 0 && selected.Interfaces.All(item => item.Class == 3))
                throw new NotSupportedException("This probe exposes only a HID interface. Stock probe-rs CMSIS-DAP v1 uses native hidraw; choose a supported bulk-USB probe or its OpenOCD/HIDAPI-libusb route.");
            var toolchain = _toolchains.Catalog.Find("probe-rs") ?? throw new NotSupportedException("The pinned probe-rs toolchain is missing from this Toolchain Store catalog.");
            if (!await EnsureToolchainReadyAsync(toolchain, "Target identification uses the pinned official probe-rs executable in the embedded glibc environment.")) return;
            await SerialMonitor.ReleaseAsync();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "Reading target identity";
            ShowOutputSource(Output);
            try
            {
                using var connection = await _toolchains.Usb.OpenAsync(selected, cancellation.Token);
                var privateParent = Path.Combine(_paths.Root, "embedded-usb");
                using var inventory = await Task.Run(() => UsbfsDeviceInventory.Create(connection, privateParent), cancellation.Token);
                var command = UsbBridgeProcess.CreateUsbfs(connection, inventory,
                    ProbeTargetInfoReader.Command(connection.Device, Workspace?.RootDirectory ?? _paths.Root));
                Output.WriteLine("Reading upstream probe-rs debug facts. Debug attach can affect live execution; erase, unlock, reset and power-cycle overrides are disabled.", OutputKind.Info);
                var result = await CommandRunner.RunAsync(_toolchains.Runtime, command,
                    line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
                var report = ProbeTargetInfoReader.Parse(result.Output);
                if (!result.Succeeded)
                    throw new IOException("probe-rs could not complete target identification: " + result.Tail());
                if (!report.HasFacts)
                {
                    Output.WriteLine("No concrete IDCODE, debug-port or core facts were reported. A USB VID/PID hint does not identify the target chip.", OutputKind.Info);
                    StatusText = "Target identity not determined";
                    return;
                }
                foreach (var fact in report.Facts)
                {
                    var context = string.Join(" / ", new[] { fact.Protocol, fact.Architecture,
                        fact.CoreOrdinal is { } core ? "CPUID block " + core : null }.Where(value => value is not null));
                    Output.WriteLine($"[target fact, output line {fact.Line}] {context}: {fact.Kind} = {fact.Value}", OutputKind.Info);
                }
                Output.WriteLine("These are tool-reported target facts. Flash/debug profiles still require the actual chip and memory map; no profile was changed.", OutputKind.Info);
                StatusText = $"Read {report.Facts.Count} target facts";
            }
            finally
            {
                _run = null;
                if (State == IdeState.Running) State = IdeState.Ready;
            }
        }
        catch (OperationCanceledException)
        {
            State = IdeState.Ready;
            StatusText = "Target identification stopped or timed out";
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }
}
