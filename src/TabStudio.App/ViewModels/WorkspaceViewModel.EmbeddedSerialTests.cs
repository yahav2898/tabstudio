using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded.Testing;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Captures one Unity run from an explicitly selected already-programmed USB serial board, without uploading or requesting reset.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunSerialEmbeddedTestsAsync()
    {
        try
        {
            if (_adapter is not null || IsDebugging || _embeddedDebugUsb is not null)
            {
                throw new InvalidOperationException("Stop the active debugger before serial board-test capture; its USB connection must be released first.");
            }
            if (IsRemoteWorkspace || await SelectEmbeddedProjectAsync() is not { } project || !await SaveAllCoreAsync())
            {
                return;
            }
            await _embeddedDebugCleanup;
            var optionsPath = Path.Combine(project.Directory, EmbeddedSerialTestOptions.FileName);
            if (!File.Exists(optionsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(optionsPath)!);
                await File.WriteAllTextAsync(optionsPath, JsonSerializer.Serialize(new EmbeddedSerialTestOptions(), EmbeddedJson) + "\n");
                await OpenDocumentAsync(optionsPath);
                ShowOutputSource(Output);
                Output.WriteLine("Program the board with your Unity test firmware first, then set its BaudRate and optional InterfaceNumber in embedded-serial-tests.json. This command receives one complete test run; it does not build, upload, send commands or request a reset. Start the harness yourself after capture begins. BoardLabel is only a label; firmware/chip identity is unverified.", OutputKind.Info);
                return;
            }
            if (new FileInfo(optionsPath).Length > 65536)
            {
                throw new InvalidDataException("Serial test configuration exceeds 64 KiB.");
            }
            var options = JsonSerializer.Deserialize<EmbeddedSerialTestOptions>(await File.ReadAllTextAsync(optionsPath), EmbeddedJson)
                ?? throw new InvalidDataException("Serial test configuration is empty.");
            options.Validate();
            if (_toolchains.Environment is null)
            {
                throw new NotSupportedException("Select a USB-OTG serial device on Android to capture board tests. Desktop test logs can be imported into Test Explorer.");
            }
            using var deviceReservation = SerialMonitor.ReserveDeviceOperation();
            await SerialMonitor.ReleaseAsync();
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            _testRun = cancellation;
            State = IdeState.Running;
            ShowOutputSource(Output);
            try
            {
                await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
                var devices = _toolchains.Usb.Devices;
                if (devices.Count == 0)
                {
                    throw new IOException("Connect the already-programmed Unity board's USB serial interface over USB-OTG.");
                }
                var selected = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Serial test board",
                    "Choose the already-programmed board. Capture never uploads firmware or requests reset; start its Unity harness after capture begins.",
                    "Capture tests", devices.Select(device => device.ToString()).ToArray());
                if (selected is null)
                {
                    return;
                }
                using var connection = await _toolchains.Usb.OpenAsync(devices[selected.Value], cancellation.Token);
                var identity = options.BoardIdentity(connection.Device);
                var alias = Path.Combine(project.Directory, ".tabstudio", "dev", "unity-" + Guid.NewGuid().ToString("N"));
                StatusText = "Capturing serial Unity tests: " + identity;
                Output.WriteLine($"Capturing existing Unity firmware at {options.BaudRate} baud from {identity}. Firmware/chip identity is unverified. No data, modem-output changes or reset requests are sent intentionally; adapter line-coding initialization still occurs. Wait for 'Serial Unity capture ready' before starting the board's harness. Unity's matching summary and final OK/FAIL line are required.", OutputKind.Info);
                var started = Stopwatch.GetTimestamp();
                var result = await EmbeddedSerialTestRunner.RunAsync(_toolchains.Runtime, connection, options, project.Directory, alias,
                    (line, error) => Output.WriteLine(line, line.StartsWith("Serial Unity capture ready", StringComparison.Ordinal) ? OutputKind.Info
                        : error ? OutputKind.Error : OutputKind.Program), cancellation.Token);
                ShowEmbeddedTestResults(project, result.Results, Stopwatch.GetElapsedTime(started));
                var failures = result.Results.Count(test => test.Outcome is EmbeddedTestOutcome.Failed or EmbeddedTestOutcome.Errored);
                State = failures > 0 || !result.HarnessCompleted ? IdeState.Failed : IdeState.Ready;
                var completion = result.HarnessCompleted ? "" : "incomplete; ";
                StatusText = $"Serial {identity}: {completion}{result.Results.Count(test => test.Outcome == EmbeddedTestOutcome.Passed)} passed, {failures} failed";
            }
            finally
            {
                _run = null;
                _testRun = null;
                if (State == IdeState.Running)
                {
                    State = IdeState.Ready;
                }
            }
        }
        catch (OperationCanceledException)
        {
            State = IdeState.Ready;
            StatusText = "Serial board-test capture stopped before completion";
        }
        catch (Exception error) when (IsEmbeddedError(error))
        {
            EmbeddedFailure(error);
        }
    }
}
