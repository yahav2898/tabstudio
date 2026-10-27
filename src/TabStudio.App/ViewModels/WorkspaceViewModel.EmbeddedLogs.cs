using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Streams native probe-rs RTT/defmt output from the matching ELF, without flashing or sharing a probe with an active debugger.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task StartEmbeddedLogsAsync()
    {
        try
        {
            if (_adapter is not null || IsDebugging || _embeddedDebugUsb is not null)
                throw new InvalidOperationException("Stop the active debugger before starting a separate RTT/defmt logger; one process owns the USB probe.");
            if (IsRemoteWorkspace || await SelectEmbeddedProjectAsync() is not { } project) return;
            await _embeddedDebugCleanup;
            var profilePath = Path.Combine(project.Directory, EmbeddedDebugProfile.FileName);
            if (!File.Exists(profilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(new EmbeddedDebugProfile
                {
                    Backend = EmbeddedDebugBackend.ProbeRs,
                    Firmware = "build/firmware.elf",
                    Chip = "SELECT_ACTUAL_CHIP",
                }, EmbeddedJson) + "\n");
                await OpenDocumentAsync(profilePath);
                Output.WriteLine("Set Backend ProbeRs, the actual chip, and the exact ELF for the firmware already running. Keep its _SEGGER_RTT symbol and defmt metadata. This command does not build or upload firmware.", OutputKind.Info);
                ShowOutputSource(Output);
                return;
            }
            if (new FileInfo(profilePath).Length > 65536) throw new InvalidDataException("The embedded debug profile exceeds 64 KiB.");
            var profile = EmbeddedDebugProfile.Parse(await File.ReadAllTextAsync(profilePath));
            if (profile.Backend != EmbeddedDebugBackend.ProbeRs)
                throw new InvalidOperationException("Native RTT/defmt logs require Backend ProbeRs and the actual Chip in embedded-debug.json. Stop any OpenOCD debug session before switching.");
            var firmware = profile.ResolveFirmware(project.Directory);
            if (new FileInfo(firmware).Length > 128 * 1024 * 1024) throw new InvalidDataException("Firmware ELF exceeds the 128 MiB logging metadata limit.");
            var address = await Task.Run(() => ProbeRsLogPlan.ControlBlockAddress(MemoryAnalyzer.AnalyzeElf(File.ReadAllBytes(firmware))));
            var toolchain = _toolchains.Catalog.Find("probe-rs") ?? throw new NotSupportedException("The pinned probe-rs toolchain is missing from this catalog.");
            if (!await EnsureToolchainReadyAsync(toolchain, "RTT/defmt output uses the pinned official probe-rs attach command and its native matching-ELF decoder.")) return;
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "RTT/defmt logs running · Stop to disconnect";
            ShowOutputSource(Output);
            IUsbConnection? connection = null;
            UsbfsInventory? inventory = null;
            try
            {
                string? selector = null;
                if (_toolchains.Environment is not null)
                {
                    await SerialMonitor.ReleaseAsync();
                    var devices = _toolchains.Usb.Devices;
                    if (devices.Count == 0) throw new IOException("Connect the selected debug probe using USB-OTG.");
                    var index = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("RTT/defmt probe", "Choose the probe connected to the already programmed target.", "Connect", devices.Select(device => device.ToString()).ToArray());
                    if (index is not { } selected) return;
                    var device = devices[selected];
                    if (device.Interfaces.Count > 0 && device.Interfaces.All(item => item.Class == 3))
                        throw new NotSupportedException("Native HID-only CMSIS-DAP v1 is not supported by the usbfs route. Select a supported bulk-USB probe.");
                    connection = await _toolchains.Usb.OpenAsync(device, cancellation.Token);
                    var granted = connection;
                    inventory = await Task.Run(() => UsbfsDeviceInventory.Create(granted, Path.Combine(_paths.Root, "embedded-usb")), cancellation.Token);
                    selector = string.Create(CultureInfo.InvariantCulture, $"{device.VendorId:x4}:{device.ProductId:x4}");
                }
                var command = ProbeRsLogPlan.Command(profile, project.Directory, firmware, address, selector);
                if (connection is not null && inventory is not null) command = UsbBridgeProcess.CreateUsbfs(connection, inventory, command);
                Output.WriteLine("Attaching to the current firmware for RTT logs. No flash/reset/erase operation is requested. Upstream selects the RTT core using the chip memory map and ELF control-block address.", OutputKind.Info);
                Output.WriteLine("The native decoder uses matching ELF metadata for the channel named defmt. RTT uses no-block-skip mode so a full buffer drops logs instead of blocking firmware; attach may resume the target.", OutputKind.Info);
                // Keep stdin open: upstream starts its readline task when RTT down channels exist, and EOF
                // would otherwise terminate that monitor. Reuse the IDE's existing explicit console input.
                var input = Output.BeginProcessInput();
                OutputProcessResult result;
                try { result = await OutputProcess.RunAsync(_toolchains.Runtime, command, Output, input, cancellation.Token); }
                finally { Output.EndInput(); }
                if (cancellation.IsCancellationRequested || result.Exit is { WasStopped: true })
                {
                    StatusText = "RTT/defmt logs stopped";
                }
                else if (result.Succeeded)
                {
                    StatusText = "RTT/defmt logger ended";
                }
                else
                {
                    throw new IOException("probe-rs log attachment failed: " + (result.StartError ?? string.Join('\n', result.Lines.TakeLast(6))));
                }
            }
            finally
            {
                inventory?.Dispose();
                connection?.Dispose();
                _run = null;
                if (State == IdeState.Running) State = IdeState.Ready;
            }
        }
        catch (OperationCanceledException)
        {
            State = IdeState.Ready;
            StatusText = "RTT/defmt logs stopped";
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }
}
