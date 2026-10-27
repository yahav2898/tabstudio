using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Catalog;
using TabStudio.Core.Diagnostics;
using TabStudio.Core.Progress;
using TabStudio.Toolchains.Embedded.Devices;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private string? _lastEmbeddedSerialDevice;
    private static readonly JsonSerializerOptions EmbeddedJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private async Task<EmbeddedProject?> SelectEmbeddedProjectAsync()
    {
        if (IsRemoteWorkspace || Workspace is not { } workspace) return null;
        var projects = await Task.Run(() => EmbeddedProjectDetector.Detect(workspace.RootDirectory));
        if (projects.Count == 0)
        {
            Output.WriteLine("No embedded project found. Open platformio.ini, an Arduino sketch, a vendor project, or a supported SDK project.", OutputKind.Info);
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            return null;
        }
        var active = CurrentToolchainProject()?.Project.Directory;
        if (projects.FirstOrDefault(p => string.Equals(p.Directory, active, StringComparison.Ordinal)) is { } selected) return selected;
        if (projects.Count == 1) return projects[0];
        var index = await Prompt.ChooseAsync("Embedded project", "Choose the project to work with.", "Select",
            projects.Select(p => p.Name + " · " + p.Kind).ToArray());
        return index is { } choice ? projects[choice] : null;
    }

    /// <summary>Edits explicit native board, environment and upload choices without changing the repository's project files.</summary>
    [RelayCommand]
    private async Task ConfigureEmbeddedAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is not { } project) return;
            var path = Path.Combine(project.Directory, EmbeddedBuildPlanner.OptionsFile);
            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new EmbeddedBuildOptions
                {
                    Environment = project.DefaultEnvironment,
                    Target = project.Target,
                }, EmbeddedJson) + "\n");
            }
            await OpenDocumentAsync(path);
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Builds firmware with the project's own build system.</summary>
    [RelayCommand(CanExecute = nameof(CanBuild))]
    private async Task BuildEmbeddedAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is { } project) await BuildEmbeddedCoreAsync(project);
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    private async Task<bool> BuildEmbeddedCoreAsync(EmbeddedProject project)
    {
        if (!await SaveAllCoreAsync()) return false;
        var plan = EmbeddedBuildPlanner.Plan(project);
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        foreach (var note in plan.Notes) Output.WriteLine(note, OutputKind.Info);
        if (plan.Build.Count == 0)
        {
            StatusText = "Choose an embedded build configuration";
            await ConfigureEmbeddedAsync();
            return false;
        }
        if (_toolchains.Catalog.Find(plan.ToolchainId) is not { } toolchain)
            throw new InvalidOperationException("Install the project's native SDK in the Terminal; no compatible Toolchain Store package is configured.");
        // The SDK version the project asks for: one clear line, and the pinned release that satisfies it, before CMake.
        var reason = project.Name + " builds with " + toolchain.Name + ".";
        if (EmbeddedSdkRequirements.Read(project) is { } requirement && requirement.ToolchainId == toolchain.Id)
        {
            var verdict = EmbeddedSdkRequirements.Check(requirement, _toolchains.EmbeddedSdkVersion(toolchain.Id), toolchain.PinnedVersion);
            if (!verdict.Satisfied && verdict.Message is { } message)
            {
                Output.WriteLine(message, OutputKind.Failure);
                if (verdict.InstallVersion is null)
                {
                    ErrorList.SetRunError(project.Directory, new BuildDiagnostic(EmbeddedSdkVersionId, DiagnosticSeverityLevel.Error, message, null, project.Name));
                    State = IdeState.Failed;
                    StatusText = $"{project.Name} needs {requirement.SdkName} {requirement.Display}";
                    return false;
                }

                reason = message;
            }
        }

        if (!await EnsureToolchainReadyAsync(toolchain, reason)) return false;
        using var cancellation = new CancellationTokenSource();
        _run = cancellation;
        State = IdeState.Running;
        StatusText = "Building " + project.Name;
        var programName = EmbeddedSdkRequirements.ProgramName(project);
        using var operation = new CompletingOperation(Operations.Begin("build:" + project.Directory, "Building " + programName, cancel: cancellation.Cancel));
        using var follower = FollowOutput(operation.Operation);
        try
        {
            if (_toolchains.Platform.UsesLinuxEnvironment
                && plan.Build.Any(command => command.Environment.GetValueOrDefault("TABSTUDIO_PLATFORMIO_BOX64") == "1"))
            {
                await _toolchains.PrepareEmbeddedBox64Async(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
            }
            ErrorList.SetRunError(project.Directory, null);
            foreach (var step in plan.Preparation.Concat(plan.Build))
            {
                operation.Operation.Report(OutputProgressParser.StageOf(step.Program, step.Arguments, programName));
                Output.WriteLine("> " + step.CommandLine, OutputKind.Info);
                var result = project.Kind == EmbeddedProjectKind.Rust
                    ? await BuildEmbeddedCargoAsync(step, cancellation.Token)
                    : await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, cancellation.Token);
                if (!result.Succeeded)
                {
                    State = IdeState.Failed;
                    StatusText = "Firmware build failed";
                    ReportToolchainRun(new ToolchainProject(toolchain, project.Directory, project.Marker), EmbeddedSdkRequirements.ProgramName(project), result);
                    ReportEmbeddedSdkVersionError(project, toolchain, result);
                    return false;
                }
            }
            operation.Outcome = OperationOutcome.Succeeded;
            State = IdeState.Ready;
            StatusText = "Firmware built";
            await ReportEmbeddedMemoryAsync(project.Directory, cancellation.Token);
            await ReportEmbeddedStackAsync(project, afterBuild: true);
            return true;
        }
        catch (OperationCanceledException) { operation.Outcome = OperationOutcome.Cancelled; State = IdeState.Ready; StatusText = "Firmware build stopped"; return false; }
        finally { _run = null; }
    }

    /// <summary>The Error List id of a firmware project whose SDK is too old.</summary>
    private const string EmbeddedSdkVersionId = "TS4201";

    /// <summary>
    /// When CMake stopped because the SDK is older than the project needs, says so in one line, with the release to
    /// install, in place of CMake's fatal error.
    /// </summary>
    private void ReportEmbeddedSdkVersionError(EmbeddedProject project, ToolchainDefinition toolchain, OutputProcessResult result)
    {
        if (EmbeddedSdkRequirements.FromCMakeOutput(string.Join('\n', result.Lines)) is not { } stated)
        {
            return;
        }

        var requirement = new EmbeddedSdkRequirement(toolchain.Id, "Pico SDK", stated.Required, null, "CMakeLists.txt");
        var message = EmbeddedSdkRequirements.Check(requirement, stated.Installed, toolchain.PinnedVersion).Message;
        if (message is null)
        {
            return;
        }

        Output.WriteLine(message, OutputKind.Failure);
        ErrorList.SetRunError(project.Directory, new BuildDiagnostic(EmbeddedSdkVersionId, DiagnosticSeverityLevel.Error, message, null, project.Name));
        StatusText = $"{project.Name} needs Pico SDK ≥ {stated.Required}";
    }

    private async Task<OutputProcessResult> BuildEmbeddedCargoAsync(ProcessStartSpec command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await CommandRunner.RunAsync(_toolchains.Runtime, command, line =>
            {
                if (CargoBuildOutput.Display(line) is { Length: > 0 } display)
                {
                    Output.WriteLine(display, OutputKind.Program);
                }
            }, cancellationToken);
            var diagnostics = CargoBuildOutput.Diagnostics(result.Output)
                .Concat(result.Output.Split('\n').Select(CargoBuildOutput.Display).OfType<string>().SelectMany(line => line.Split('\n')))
                .ToArray();
            return new OutputProcessResult(result.Exit, diagnostics);
        }
        catch (ToolchainRuntimeException error)
        {
            Output.WriteLine(error.Message, OutputKind.Failure);
            return new OutputProcessResult(null, [], error.Message);
        }
    }

    /// <summary>Programs only an explicitly mapped application image through one selected permissioned USB device.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task UploadEmbeddedAsync()
    {
        try
        {
            if (_adapter is not null || IsDebugging || _embeddedDebugUsb is not null)
                throw new InvalidOperationException("Stop the active debugger before uploading; one process owns each USB probe.");
            await _embeddedDebugCleanup;
            if (await SelectEmbeddedProjectAsync() is not { } project || !await BuildEmbeddedCoreAsync(project)) return;
            var profilePath = Path.Combine(project.Directory, ".tabstudio", "embedded-flash.json");
            if (!File.Exists(profilePath)) await TryCreateEspMetadataProfileAsync(project, profilePath);
            if (!File.Exists(profilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
                await File.WriteAllTextAsync(profilePath, JsonSerializer.Serialize(new EmbeddedFlashRequest
                {
                    Protocol = EmbeddedFlashProtocol.OpenOcd, Target = "SELECT_ACTUAL_CHIP",
                    FirmwarePath = "firmware.elf", WorkingDirectory = project.Directory,
                    SerialPort = "/dev/ttyUSB0",
                }, EmbeddedJson) + "\n");
                await OpenDocumentAsync(profilePath);
                Output.WriteLine("Set the actual firmware, probe/bootloader, target and application-flash ranges in this profile. Empty ranges refuse every write before erase.", OutputKind.Info);
                return;
            }
            if (new FileInfo(profilePath).Length > 65536) throw new InvalidDataException("Flash profile exceeds 64 KiB.");
            var request = JsonSerializer.Deserialize<EmbeddedFlashRequest>(await File.ReadAllTextAsync(profilePath), EmbeddedJson)
                ?? throw new InvalidDataException("Flash profile is empty.");
            request = request with
            {
                WorkingDirectory = project.Directory,
                FirmwarePath = Path.GetFullPath(request.FirmwarePath, project.Directory),
                EspFlasherArguments = request.EspFlasherArguments is { } metadata ? Path.GetFullPath(metadata, project.Directory) : null,
                SerialPort = _toolchains.Environment is null ? request.SerialPort
                    : Path.Combine(project.Directory, ".tabstudio", "dev", "ttyUSB0"),
            };
            if (request.Protocol == EmbeddedFlashProtocol.ProbeRs && _toolchains.Catalog.Find("probe-rs") is { } probeTools
                && !await EnsureToolchainReadyAsync(probeTools, "probe-rs uses the embedded glibc environment and the selected device's permissioned USBFS inventory."))
            {
                return;
            }
            UsbDeviceInfo? selectedDevice = null;
            if (_toolchains.Environment is not null)
            {
                var devices = _toolchains.Usb.Devices;
                if (devices.Count == 0) throw new IOException("Connect the board or debug probe using USB-OTG.");
                var choice = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Upload device", "Choose the board or probe to program.", "Select", devices.Select(d => d.ToString()).ToArray());
                if (choice is null) return;
                selectedDevice = devices[choice.Value];
                if (request.Protocol == EmbeddedFlashProtocol.ProbeRs)
                    request = request with { ProbeSelector = ProbeRsProductionLease.Selector(selectedDevice, request.ProbeSelector) };
            }
            if (request.Protocol == EmbeddedFlashProtocol.WchLink)
            {
                if (selectedDevice is null) throw new NotSupportedException("The guarded WCH-Link route requires Android's permissioned USB connection.");
                await UploadGuardedWchLinkAsync(request, selectedDevice);
                return;
            }
            var plan = await Task.Run(() => EmbeddedFlashPlanner.Plan(request));
            if (plan.TargetChip is { } resolvedChip) request = request with { Target = resolvedChip };
            using var deviceReservation = _toolchains.Environment is null ? null : SerialMonitor.ReserveDeviceOperation();
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "Uploading firmware";
            IUsbConnection? connection = null;
            UsbfsInventory? inventory = null;
            try
            {
                Func<ProcessStartSpec, ProcessStartSpec>? bridge = null;
                Func<CancellationToken, Task>? prepareBootloader = null;
                if (_toolchains.Environment is not null)
                {
                    await SerialMonitor.ReleaseAsync();
                    await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
                    await PrepareEmbeddedProtocolAsync(request, cancellation.Token);
                    connection = await _toolchains.Usb.OpenAsync(selectedDevice!, cancellation.Token);
                    if (request.Protocol == EmbeddedFlashProtocol.ProbeRs)
                    {
                        var granted = connection;
                        inventory = await Task.Run(() => UsbfsDeviceInventory.Create(granted, Path.Combine(_paths.Root, "embedded-usb")), cancellation.Token);
                    }
                    bridge = step => EmbeddedFlashPlanner.RequiresSerial(request)
                        ? UsbBridgeProcess.CreateSerial(connection ?? throw new IOException("USB device disconnected."), step, request.SerialPort!, request.BaudRate)
                        : inventory is not null
                            ? UsbBridgeProcess.CreateUsbfs(connection ?? throw new IOException("USB device disconnected."), inventory, step)
                            : UsbBridgeProcess.CreateLibUsb(connection ?? throw new IOException("USB device disconnected."), step);
                    prepareBootloader = async token =>
                    {
                        var before = _toolchains.Usb.Devices.Select(device => device.Id).ToHashSet(StringComparer.Ordinal);
                        var touched = await OutputProcess.RunAsync(_toolchains.Runtime, UsbBridgeProcess.CreateTouch1200(connection), Output, null, token);
                        if (!touched.Succeeded) throw new IOException("The 1200-baud bootloader touch failed.");
                        connection.Dispose();
                        connection = null;
                        var deadline = DateTime.UtcNow.AddSeconds(15);
                        while (DateTime.UtcNow < deadline)
                        {
                            var bootloaders = _toolchains.Usb.Devices.Where(device => !before.Contains(device.Id)).ToArray();
                            if (bootloaders.Length > 0)
                            {
                                var index = bootloaders.Length == 1 ? 0 : await Prompt.ChooseAsync("Bootloader device", "Choose the board that reconnected after the 1200-baud touch.", "Select", bootloaders.Select(d => d.ToString()).ToArray());
                                if (index is null) throw new OperationCanceledException(token);
                                connection = await _toolchains.Usb.OpenAsync(bootloaders[index.Value], token);
                                return;
                            }
                            await Task.Delay(100, token);
                        }
                        throw new IOException("No new USB bootloader appeared after the 1200-baud touch. Reconnect in bootloader mode and select its actual USB identity.");
                    };
                }
                var result = await EmbeddedFlashExecutor.ExecuteAsync(_toolchains.Runtime, plan, bridge,
                    new Progress<string>(line => Output.WriteLine(line.TrimEnd(), OutputKind.Info)), prepareBootloader, cancellation.Token);
                Output.WriteLine((result.Verified ? "Verified firmware SHA-256: " : "Transferred firmware SHA-256: ") + result.FirmwareSha256, OutputKind.Info);
                Output.WriteLine(plan.Verification, OutputKind.Info);
                if (connection is not null) await RememberEmbeddedDeviceAsync(connection.Device, project.Name, request);
                _lastEmbeddedSerialDevice = EmbeddedFlashPlanner.RequiresSerial(request)
                    || connection?.Device.Interfaces.Any(item => item.Class == 2 && item.Subclass == 2) == true
                    ? connection?.Device.Id : null;
                State = IdeState.Ready;
                StatusText = result.Verified ? "Firmware upload verified" : "Firmware transferred; readback unavailable";
            }
            finally { inventory?.Dispose(); connection?.Dispose(); _run = null; if (State == IdeState.Running) State = IdeState.Ready; }
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Firmware upload stopped"; }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Lists attached USB identities and conservative board/probe suggestions without requesting permission.</summary>
    [RelayCommand]
    private async Task ShowEmbeddedDevicesAsync()
    {
        try
        {
            var database = await EmbeddedDeviceDatabase.LoadAsync(Path.Combine(_paths.Root, "embedded-devices.json"));
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            foreach (var device in _toolchains.Usb.Devices)
            {
                Output.WriteLine(device.ToString(), OutputKind.Info);
                foreach (var candidate in database.Identify(device)) Output.WriteLine("  " + candidate.Name + " · " + candidate.Route, OutputKind.Info);
            }
            if (_toolchains.Usb.Devices.Count == 0) Output.WriteLine("No USB board or debug probe attached.", OutputKind.Info);
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Converts an active vendor project to an independent GCC/CMake project with structured port notes.</summary>
    [RelayCommand]
    private async Task ImportEmbeddedProjectAsync()
    {
        try
        {
            if (ActiveDocument?.FilePath is not { } path || Path.GetExtension(path).ToLowerInvariant() is not (".uvprojx" or ".ewp" or ".cproj"))
                throw new InvalidOperationException("Open a Keil .uvprojx, IAR .ewp or Atmel/Microchip .cproj file first.");
            var destination = await Prompt.AskAsync("Import embedded project", "Create a separate GCC/CMake project. The original is preserved.", "Import", Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "-gcc"));
            if (destination is null) return;
            await Task.Run(() => VendorProjectImporter.Import(path, destination));
            await OpenDocumentAsync(Path.Combine(destination, "CMakeLists.txt"));
            StatusText = "GCC/CMake import created; review port notes before building";
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    private static bool IsEmbeddedError(Exception error) => error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException
        or ArgumentException or InvalidOperationException or NotSupportedException or System.Xml.XmlException or OverflowException or FormatException
        or ToolchainRuntimeException or TabStudio.Toolchains.Installation.ToolchainInstallException;

    private async Task RememberEmbeddedDeviceAsync(UsbDeviceInfo device, string board, EmbeddedFlashRequest request)
    {
        try
        {
            var path = Path.Combine(_paths.Root, "embedded-devices.json");
            var database = await EmbeddedDeviceDatabase.LoadAsync(path);
            database.Remember(new(device.VendorId, device.ProductId, null, board, request.Protocol.ToString(), request.Target));
            await database.SaveAsync(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            Output.WriteLine("Firmware verified; could not save the device association: " + error.Message, OutputKind.Info);
        }
    }

    private void EmbeddedFailure(Exception error)
    {
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
        Output.WriteLine(error.Message, OutputKind.Failure);
        State = IdeState.Failed;
        StatusText = "Embedded operation failed; see Output";
    }
}
