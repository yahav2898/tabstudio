using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Embedded.Testing;
using TabStudio.Toolchains.Microchip;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Runs an explicitly selected test ELF on one actual board, with verified application-only flashing and native RTT decoding.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunEmbeddedBoardTestsAsync()
    {
        try
        {
            if (_adapter is not null || IsDebugging || _embeddedDebugUsb is not null)
                throw new InvalidOperationException("Stop the active debugger before running board tests; the native runner exclusively owns its probe.");
            if (IsRemoteWorkspace || await SelectEmbeddedProjectAsync() is not { } project || !await SaveAllCoreAsync()) return;
            await _embeddedDebugCleanup;
            var optionsPath = Path.Combine(project.Directory, EmbeddedHostTestPlanner.OptionsFile);
            if (!File.Exists(optionsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(optionsPath)!);
                await File.WriteAllTextAsync(optionsPath, JsonSerializer.Serialize(new EmbeddedBoardTestOptions
                {
                    BoardId = "SELECT_ACTUAL_BOARD_SERIAL",
                }, EmbeddedJson) + "\n");
                await OpenDocumentAsync(optionsPath);
                ShowOutputSource(Output);
                Output.WriteLine("Set Format Unity, DefmtTest, or Rust (native embedded-test), the actual BoardId and timeout. Point ProbeRs flash/debug profiles at the same already-built TEST ELF and the exact application-flash map. Running board tests will flash, verify, reset and execute that test firmware.", OutputKind.Info);
                return;
            }
            if (new FileInfo(optionsPath).Length > 65536) throw new InvalidDataException("Embedded test configuration exceeds 64 KiB.");
            var options = JsonSerializer.Deserialize<EmbeddedBoardTestOptions>(await File.ReadAllTextAsync(optionsPath), EmbeddedJson)
                ?? throw new InvalidDataException("Embedded board-test configuration is empty.");
            var debugPath = Path.Combine(project.Directory, EmbeddedDebugProfile.FileName);
            if (!File.Exists(debugPath) || new FileInfo(debugPath).Length > 65536)
                throw new InvalidDataException("Configure the exact ProbeRs chip, probe and test ELF in embedded-debug.json first.");
            var debug = EmbeddedDebugProfile.Parse(await File.ReadAllTextAsync(debugPath));
            var flash = await LoadEmbeddedFlashRequestAsync(project);
            var toolchain = _toolchains.Catalog.Find("probe-rs") ?? throw new NotSupportedException("The pinned official probe-rs toolchain is missing from the catalog.");
            if (!await EnsureToolchainReadyAsync(toolchain, "Run the explicitly configured test firmware on the selected board, using native RTT/defmt decoding.")) return;
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            _testRun = cancellation;
            State = IdeState.Running;
            StatusText = "Running tests on board " + options.BoardId;
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
                    if (devices.Count == 0) throw new IOException("Attach the selected board's debug probe over USB-OTG.");
                    var choice = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Board-test probe", "This probe will flash and execute the configured test firmware on its target.", "Run tests", devices.Select(device => device.ToString()).ToArray());
                    if (choice is not { } selected) return;
                    var device = devices[selected];
                    if (device.Interfaces.Count > 0 && device.Interfaces.All(item => item.Class == 3))
                        throw new NotSupportedException("Native HID-only CMSIS-DAP v1 is not supported by this usbfs route; select a supported bulk-USB probe.");
                    connection = await _toolchains.Usb.OpenAsync(device, cancellation.Token);
                    var granted = connection;
                    inventory = await Task.Run(() => UsbfsDeviceInventory.Create(granted, Path.Combine(_paths.Root, "embedded-usb")), cancellation.Token);
                    selector = string.Create(CultureInfo.InvariantCulture, $"{device.VendorId:x4}:{device.ProductId:x4}");
                }
                using var plan = await Task.Run(() => EmbeddedBoardTestPlan.Create(options, flash, debug,
                    Path.Combine(_paths.Root, "embedded-board-tests"), selector), cancellation.Token);
                var command = connection is not null && inventory is not null ? UsbBridgeProcess.CreateUsbfs(connection, inventory, plan.Command) : plan.Command;
                Output.WriteLine($"Board {options.BoardId}: application-only test ELF SHA-256 {plan.FirmwareSha256}. probe-rs run flashes/verifies and resets this firmware; chip erase and protection changes are not requested. RTT blocks when full to preserve test results.", OutputKind.Info);
                Output.WriteLine("> " + command.CommandLine, OutputKind.Info);
                var started = Stopwatch.GetTimestamp();
                var result = await EmbeddedBoardTestRunner.RunAsync(_toolchains.Runtime, plan, command,
                    (line, error) => Output.WriteLine(line, error ? OutputKind.Error : OutputKind.Program), cancellation.Token);
                ShowEmbeddedTestResults(project, result.Results, Stopwatch.GetElapsedTime(started));
                var failures = result.Results.Count(test => test.Outcome is EmbeddedTestOutcome.Failed or EmbeddedTestOutcome.Errored);
                State = failures > 0 ? IdeState.Failed : IdeState.Ready;
                StatusText = $"Board {options.BoardId}: {result.Results.Count(test => test.Outcome == EmbeddedTestOutcome.Passed)} passed, {failures} failed";
            }
            finally
            {
                inventory?.Dispose();
                connection?.Dispose();
                _run = null;
                _testRun = null;
                if (State == IdeState.Running) State = IdeState.Ready;
            }
        }
        catch (OperationCanceledException)
        {
            State = IdeState.Ready;
            StatusText = "Board tests stopped before completion";
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }
}
