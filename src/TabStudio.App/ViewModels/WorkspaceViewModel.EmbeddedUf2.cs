using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded.Storage;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Copies an addressed, family-checked UF2 to the selected RP ROM volume through USB BOT and FAT.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task UploadUf2StorageAsync()
    {
        try
        {
            if (_toolchains.Environment is null) throw new NotSupportedException("Permissioned BOOTSEL storage upload uses Android USB-OTG. On a desktop, use the board's mounted drive or picotool.");
            if (await SelectEmbeddedProjectAsync() is not { } project || !await BuildEmbeddedCoreAsync(project)) return;
            var request = await LoadEmbeddedFlashRequestAsync(project);
            if (!request.FirmwarePath.EndsWith(".uf2", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Set the actual UF2 firmware and application-flash ranges in .tabstudio/embedded-flash.json first.");
            if (new FileInfo(request.FirmwarePath).Length > 64 * 1024 * 1024)
                throw new InvalidDataException("This BOOTSEL route accepts UF2 containers up to 64 MiB for the supported XIP application range.");
            var bytes = await File.ReadAllBytesAsync(request.FirmwarePath);
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "Reading BOOTSEL volume";
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            try
            {
                await SerialMonitor.ReleaseAsync();
                await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
                var devices = _toolchains.Usb.Devices.Where(Uf2BootselUpload.IsSupportedDevice).ToArray();
                if (devices.Length == 0) throw new IOException("Hold BOOTSEL while connecting the RP2040/RP2350 board. This command refuses ordinary USB disks and custom unidentified bootloaders.");
                var index = devices.Length == 1 ? 0 : await Prompt.ChooseAsync("BOOTSEL board", "Choose the RP ROM bootloader receiving this application image.", "Upload", devices.Select(d => d.ToString()).ToArray());
                if (index is null) return;
                using var connection = await _toolchains.Usb.OpenAsync(devices[index.Value], cancellation.Token);
                await using var disk = await UsbMassStorageBlockDevice.OpenAsync(_toolchains.Runtime, connection, project.Directory, cancellation.Token);
                var volume = await FatVolume.OpenAsync(disk, cancellation.Token);
                var plan = await Uf2BootselUpload.PrepareAsync(volume, connection.Device, bytes, request.ApplicationFlash, cancellation.Token);
                Output.WriteLine($"{connection.Device}: {plan.VolumeInfo.Kind}, {plan.VolumeInfo.VolumeSectors} sectors; {plan.FileBytes} UF2 bytes, family 0x{plan.FamilyId:X8}.", OutputKind.Info);
                Output.WriteLine(plan.BoardInformation.TrimEnd(), OutputKind.Info);
                Output.WriteLine("UF2 SHA-256: " + plan.FirmwareSha256, OutputKind.Info);
                cancellation.Token.ThrowIfCancellationRequested();
                // The explicit Upload action authorizes this already validated, selected application copy.
                await disk.EnableWritesAsync(plan, cancellation.Token);
                StatusText = "Submitting UF2 through BOOTSEL storage";
                var result = await Uf2BootselUpload.ExecuteAsync(plan, cancellation.Token);
                if (result.ResetDuringFinalStatus)
                {
                    StatusText = "UF2 submitted; board disconnected before final status";
                    Output.WriteLine("All UF2 payload bytes reached USB, then the board disconnected before final command status. Check the running firmware; flash verification is unavailable.", OutputKind.Info);
                }
                else
                {
                    StatusText = result.AllCommandsAcknowledged ? "UF2 transferred; flash readback unavailable" : "UF2 transfer incomplete";
                    Output.WriteLine($"Submitted {result.BytesSubmitted} bytes as {result.FileName}. Command acknowledgments: {result.AllCommandsAcknowledged}; filesystem readback: {(result.FileReadbackMatched is { } matched ? matched.ToString() : "unavailable")}. These results do not verify MCU flash.", OutputKind.Info);
                }
                State = result.AllCommandsAcknowledged || result.ResetDuringFinalStatus ? IdeState.Ready : IdeState.Failed;
            }
            finally { _run = null; if (State == IdeState.Running) State = IdeState.Ready; }
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "BOOTSEL copy stopped; inspect the board before retrying"; }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }
}
