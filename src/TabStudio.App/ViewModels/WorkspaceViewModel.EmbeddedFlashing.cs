using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Toolchains.Embedded;
using TabStudio.Toolchains.Embedded.Flashing;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Microchip;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Uploads an application BIN to firmware already running ArduinoOTA over Wi-Fi.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task UploadEmbeddedOtaAsync()
    {
        try
        {
            if (await SelectEmbeddedProjectAsync() is not { } project || !await BuildEmbeddedCoreAsync(project))
            {
                return;
            }
            var request = await LoadEmbeddedFlashRequestAsync(project);
            var firmware = request.EspFlasherArguments is { } metadata
                ? (await Task.Run(() => EspFlashManifestReader.Read(metadata))).ApplicationPath : request.FirmwarePath;
            var address = await Prompt.AskAsync("Wi-Fi firmware upload", "Board IP address, optionally with :port (use [IPv6]:port). Default port: 3232. Its running firmware must support ArduinoOTA and OTA partitions.", "Next");
            if (address is null)
            {
                return;
            }
            IPEndPoint target;
            if (IPAddress.TryParse(address, out var ip))
            {
                target = new(ip, 3232);
            }
            else if (IPEndPoint.TryParse(address, out var endpoint) && endpoint.Port > 0)
            {
                target = endpoint;
            }
            else
            {
                throw new ArgumentException("Enter the board's IPv4/IPv6 address, optionally with its OTA port.");
            }
            var password = await Prompt.AskSecretAsync("ArduinoOTA password", "Password for this upload only. Leave empty if the running firmware has no OTA password.", "Upload");
            if (password is null)
            {
                return;
            }
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            State = IdeState.Running;
            ShowEmbeddedFlashOutput();
            try
            {
                var result = await ArduinoOtaUploader.UploadAsync(new(firmware, target, password),
                    new Progress<double>(value => StatusText = $"Wi-Fi firmware upload: {value:P0}"), cancellationToken: cancellation.Token);
                Output.WriteLine("ArduinoOTA target accepted firmware SHA-256: " + result.FirmwareSha256 + ". Physical readback is not part of ArduinoOTA.", OutputKind.Info);
                StatusText = "Wi-Fi firmware accepted by target";
                State = IdeState.Ready;
            }
            finally
            {
                _run = null;
                if (State == IdeState.Running)
                {
                    State = IdeState.Ready;
                }
            }
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Wi-Fi firmware upload stopped or timed out"; }
        catch (Exception error) when (IsEmbeddedError(error) || error is SocketException) { EmbeddedFailure(error); }
    }

    /// <summary>Compares two local raw firmware dumps and reports exact differing-byte counts and bounded ranges.</summary>
    [RelayCommand]
    private async Task CompareFirmwareDumpsAsync()
    {
        try
        {
            var first = await Prompt.AskAsync("Compare firmware dumps", "Absolute path to the first raw flash dump:", "Next", ActiveDocument?.FilePath ?? string.Empty);
            if (first is null)
            {
                return;
            }
            var second = await Prompt.AskAsync("Compare firmware dumps", "Absolute path to the second raw flash dump:", "Compare");
            if (second is null)
            {
                return;
            }
            if (!Path.IsPathRooted(first) || !Path.IsPathRooted(second))
            {
                throw new ArgumentException("Use absolute paths for both firmware dumps.");
            }
            var result = await FirmwareBackup.CompareAsync(Path.GetFullPath(first), Path.GetFullPath(second));
            ShowEmbeddedFlashOutput();
            Output.WriteLine($"Firmware dumps: {(result.Identical ? "identical" : result.DifferentBytes + " differing bytes")}", OutputKind.Info);
            Output.WriteLine("First SHA-256:  " + result.LeftSha256 + "\nSecond SHA-256: " + result.RightSha256, OutputKind.Info);
            foreach (var range in result.Ranges)
            {
                Output.WriteLine($"  0x{range.Offset:X}..0x{range.Offset + range.Length:X} ({range.Length} bytes)", OutputKind.Info);
            }
            if (result.Truncated)
            {
                Output.WriteLine("Additional difference ranges omitted; the differing-byte count covers the complete dumps.", OutputKind.Info);
            }
            StatusText = result.Identical ? "Firmware dumps match" : "Firmware dump differences shown in Output";
        }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Reads one explicitly mapped flash interval twice and saves only an identical, verified backup.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task BackupEmbeddedFirmwareAsync()
    {
        try
        {
            if (_adapter is not null || IsDebugging || _embeddedDebugUsb is not null)
                throw new InvalidOperationException("Stop the active debugger before backing up flash; one process owns each USB probe.");
            await _embeddedDebugCleanup;
            if (await SelectEmbeddedProjectAsync() is not { } project)
            {
                return;
            }
            var request = await LoadEmbeddedFlashRequestAsync(project);
            var path = await Prompt.AskAsync("Back up application flash", "New backup BIN file. The selected application interval is read twice; protection is never disabled.", "Read flash",
                Path.Combine(project.Directory, "firmware-backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".bin"));
            if (path is null)
            {
                return;
            }
            path = Path.GetFullPath(path, project.Directory);
            var capture = Path.Combine(project.Directory, ".tabstudio", "embedded", Guid.NewGuid().ToString("N") + ".dump.bin");
            Directory.CreateDirectory(Path.GetDirectoryName(capture)!);
            using var deviceReservation = _toolchains.Environment is null ? null : SerialMonitor.ReserveDeviceOperation();
            using var cancellation = new CancellationTokenSource();
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "Reading and verifying flash backup";
            ShowEmbeddedFlashOutput();
            IUsbConnection? connection = null;
            UsbfsInventory? inventory = null;
            try
            {
                await PrepareEmbeddedProtocolAsync(request, cancellation.Token);
                if (request.Protocol == EmbeddedFlashProtocol.WchLink)
                {
                    var wchDigest = await BackupGuardedWchLinkAsync(request, path, cancellation.Token);
                    Output.WriteLine("Verified backup saved: " + path + "\nSHA-256: " + wchDigest, OutputKind.Info);
                    StatusText = "Flash backup verified";
                    State = IdeState.Ready;
                    return;
                }
                connection = await OpenEmbeddedFlashConnectionAsync(cancellation.Token);
                if (request.Protocol == EmbeddedFlashProtocol.ProbeRs && connection is not null)
                {
                    request = request with { ProbeSelector = ProbeRsProductionLease.Selector(connection.Device, request.ProbeSelector) };
                    inventory = UsbfsDeviceInventory.Create(connection, Path.Combine(_paths.Root, "embedded-usb"));
                }
                var read = EmbeddedReadbackPlanner.Plan(request, capture);
                var step = inventory is not null
                    ? UsbBridgeProcess.CreateUsbfs(connection!, inventory, read)
                    : WrapEmbeddedFlashCommand(request, connection, read);
                var digest = await FirmwareBackup.CreateAsync(path, async (destination, token) =>
                {
                    var result = await OutputProcess.RunAsync(_toolchains.Runtime, step, Output, null, token);
                    token.ThrowIfCancellationRequested();
                    if (!result.Succeeded)
                    {
                        throw new IOException("Flash read failed. Protected devices remain protected; no backup was saved.");
                    }
                    await using (var source = File.OpenRead(capture))
                    {
                        if ((ulong)source.Length != request.ApplicationFlash[0].Length)
                        {
                            throw new InvalidDataException("Device readback length does not match the selected flash interval.");
                        }
                        await source.CopyToAsync(destination, token);
                    }
                    File.Delete(capture);
                }, cancellation.Token);
                Output.WriteLine("Verified backup saved: " + path + "\nSHA-256: " + digest, OutputKind.Info);
                StatusText = "Flash backup verified";
                State = IdeState.Ready;
            }
            finally
            {
                inventory?.Dispose();
                connection?.Dispose();
                File.Delete(capture);
                _run = null;
                if (State == IdeState.Running)
                {
                    State = IdeState.Ready;
                }
            }
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Flash backup stopped"; }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
    }

    /// <summary>Programs an explicitly selected batch of identical USB transports, with independent connections and verification.</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ProductionFlashEmbeddedAsync()
    {
        var connections = new List<IUsbConnection>();
        var probeLeases = new Dictionary<string, ProbeRsProductionLease>(StringComparer.Ordinal);
        IDisposable? deviceReservation = null;
        try
        {
            if (_adapter is not null || IsDebugging || _embeddedDebugUsb is not null)
                throw new InvalidOperationException("Stop the active debugger before production flashing; one process owns each USB probe.");
            await _embeddedDebugCleanup;
            if (await SelectEmbeddedProjectAsync() is not { } project || !await BuildEmbeddedCoreAsync(project))
            {
                return;
            }
            if (_toolchains.Environment is null)
            {
                throw new NotSupportedException("USB-hub production flashing uses Android's permissioned USB Host API.");
            }
            var request = await LoadEmbeddedFlashRequestAsync(project);
            if (request.Touch1200Baud)
            {
                throw new NotSupportedException("Production batches require a stable USB identity. Touch/re-enumerating bootloaders need individual uploads.");
            }
            var devices = _toolchains.Usb.Devices;
            if (devices.Count == 0)
            {
                throw new IOException("Attach identical boards/probes using a USB hub.");
            }
            var choice = await Prompt.ChooseAsync("Production board type", "Choose one board/probe. The batch includes attached devices with the same VID/PID; confirm they all have the exact configured MCU and flash layout.", "Select", devices.Select(x => x.ToString()).ToArray());
            if (choice is not { } selected)
            {
                return;
            }
            var reference = devices[selected];
            if (request.Protocol == EmbeddedFlashProtocol.ProbeRs)
                request = request with { ProbeSelector = ProbeRsProductionLease.Selector(reference, request.ProbeSelector) };
            var plan = await Task.Run(() => EmbeddedFlashPlanner.Plan(request));
            if (!plan.Verified)
                throw new NotSupportedException("This loader reports transfer completion only. Production batches require target verification/readback.");
            var batch = devices.Where(x => x.VendorId == reference.VendorId && x.ProductId == reference.ProductId).ToArray();
            if (!await Prompt.ConfirmAsync("Flash " + batch.Length + " boards", "All listed boards must match target " + (plan.TargetChip ?? request.Target) + ". Each gets the same validated application image and independent verification.\n" + string.Join("\n", batch.Select(x => x.Id)), "Flash batch"))
            {
                return;
            }
            using var cancellation = new CancellationTokenSource();
            deviceReservation = SerialMonitor.ReserveDeviceOperation();
            _run = cancellation;
            State = IdeState.Running;
            StatusText = "Preparing production batch";
            ShowEmbeddedFlashOutput();
            try
            {
                await SerialMonitor.ReleaseAsync();
                await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellation.Token);
                await PrepareEmbeddedProtocolAsync(request, cancellation.Token);
                // Serialize permission requests; Android presents one permission dialog at a time.
                foreach (var device in batch)
                {
                    connections.Add(await _toolchains.Usb.OpenAsync(device, cancellation.Token));
                }
                if (request.Protocol == EmbeddedFlashProtocol.ProbeRs)
                {
                    foreach (var connection in connections)
                    {
                        cancellation.Token.ThrowIfCancellationRequested();
                        probeLeases.Add(connection.Device.Id, ProbeRsProductionLease.Create(request, plan, connection,
                            Path.Combine(_paths.Root, "embedded-usb")));
                    }
                }
                var jobs = connections.Select(connection =>
                {
                    var probeLease = probeLeases.GetValueOrDefault(connection.Device.Id);
                    var boardPlan = probeLease?.Plan ?? plan;
                    if (plan.ReadbackPath is { } sharedReadback)
                    {
                        var privateReadback = Path.Combine(Path.GetDirectoryName(sharedReadback)!, Guid.NewGuid().ToString("N") + ".readback.bin");
                        boardPlan = plan with
                        {
                            ReadbackPath = privateReadback,
                            Steps = plan.Steps.Select(step => step with
                            {
                                Arguments = step.Arguments.Select(argument => argument == sharedReadback ? privateReadback : argument).ToArray(),
                            }).ToArray(),
                        };
                    }
                    return new ProductionFlashJob(connection.Device.Id, token =>
                        EmbeddedFlashExecutor.ExecuteAsync(_toolchains.Runtime, boardPlan,
                            step => probeLease is not null ? probeLease.Wrap(step) : WrapEmbeddedFlashCommand(request, connection, step), cancellationToken: token), ProvidesVerification: boardPlan.Verified);
                }).ToArray();
                var log = Path.Combine(project.Directory, ".tabstudio", "production", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".jsonl");
                var results = await ProductionFlasher.RunAsync(jobs, log, parallelism: 2,
                    progress: new Progress<ProductionFlashResult>(result => Output.WriteLine(result.DeviceIdentity + ": " + (result.Verified ? "verified " + result.FirmwareSha256 : result.Error), result.Verified ? OutputKind.Info : OutputKind.Failure)), cancellationToken: cancellation.Token);
                var verified = results.Count(x => x.Verified);
                Output.WriteLine("Production log: " + log, OutputKind.Info);
                StatusText = $"Production batch: {verified}/{results.Count} verified";
                State = verified == results.Count ? IdeState.Ready : IdeState.Failed;
            }
            finally
            {
                _run = null;
                if (State == IdeState.Running)
                {
                    State = IdeState.Ready;
                }
            }
        }
        catch (OperationCanceledException) { State = IdeState.Ready; StatusText = "Production flashing stopped"; }
        catch (Exception error) when (IsEmbeddedError(error)) { EmbeddedFailure(error); }
        finally
        {
            try
            {
                foreach (var lease in probeLeases.Values) lease.Dispose();
                foreach (var connection in connections) connection.Dispose();
            }
            finally { deviceReservation?.Dispose(); }
        }
    }

    private static async Task<EmbeddedFlashRequest> LoadEmbeddedFlashRequestAsync(EmbeddedProject project)
    {
        var path = Path.Combine(project.Directory, ".tabstudio", "embedded-flash.json");
        if (!File.Exists(path))
        {
            throw new InvalidOperationException("Configure an embedded flash profile with the exact target, firmware and application ranges first.");
        }
        if (new FileInfo(path).Length > 65536)
        {
            throw new InvalidDataException("Flash profile exceeds 64 KiB.");
        }
        var request = JsonSerializer.Deserialize<EmbeddedFlashRequest>(await File.ReadAllTextAsync(path), EmbeddedJson)
            ?? throw new InvalidDataException("Flash profile is empty.");
        return request with
        {
            WorkingDirectory = project.Directory, FirmwarePath = Path.GetFullPath(request.FirmwarePath, project.Directory),
            EspFlasherArguments = request.EspFlasherArguments is { } metadata ? Path.GetFullPath(metadata, project.Directory) : null,
        };
    }

    private async Task<IUsbConnection?> OpenEmbeddedFlashConnectionAsync(CancellationToken cancellationToken)
    {
        if (_toolchains.Environment is null)
        {
            return null;
        }
        await SerialMonitor.ReleaseAsync();
        await _toolchains.PrepareEmbeddedSupportAsync(line => Output.WriteLine(line, OutputKind.Info), cancellationToken);
        var devices = _toolchains.Usb.Devices;
        if (devices.Count == 0)
        {
            throw new IOException("Attach the selected board/probe using USB-OTG.");
        }
        var choice = devices.Count == 1 ? 0 : await Prompt.ChooseAsync("Flash read device", "Choose the exact board/probe whose flash should be read.", "Select", devices.Select(x => x.ToString()).ToArray());
        if (choice is not { } selected)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        return await _toolchains.Usb.OpenAsync(devices[selected], cancellationToken);
    }

    private static ProcessStartSpec WrapEmbeddedFlashCommand(EmbeddedFlashRequest request, IUsbConnection? connection, ProcessStartSpec command)
    {
        if (connection is null)
        {
            return command;
        }
        if (!EmbeddedFlashPlanner.RequiresSerial(request))
        {
            return UsbBridgeProcess.CreateLibUsb(connection, command);
        }
        var originalPort = request.SerialPort ?? "/dev/ttyUSB0";
        var privatePort = "/tmp/tabstudio-serial-" + Guid.NewGuid().ToString("N");
        var privateCommand = command with
        {
            Arguments = command.Arguments.Select(argument => argument == originalPort ? privatePort :
                argument == "--port=" + originalPort ? "--port=" + privatePort : argument).ToArray(),
        };
        return UsbBridgeProcess.CreateSerial(connection, privateCommand, privatePort, request.BaudRate);
    }

    private async Task PrepareEmbeddedProtocolAsync(EmbeddedFlashRequest request, CancellationToken cancellationToken)
    {
        if (TabStudio.Toolchains.Embedded.Installation.EmbeddedFlashToolInstallation.ToolchainId(request.Protocol) is { } id)
        {
            var tool = _toolchains.Catalog.Find(id) ?? throw new InvalidOperationException("The flashing tool is missing from the Store catalog: " + id);
            if (!await EnsureToolchainReadyAsync(tool, "This firmware route uses " + tool.Name + "."))
            {
                throw new OperationCanceledException(cancellationToken);
            }
        }
        if (request.Protocol == EmbeddedFlashProtocol.NrfDfu && !_toolchains.Runtime.HasCommand("nrfutil"))
        {
            throw new NotSupportedException("The verified Nordic legacy DFU installation did not expose nrfutil. Finish installing its unchanged vendor bundle in the embedded glibc environment; ARM64 also needs Box64 and the matching x64 serial shim. No USB programming was attempted.");
        }
        if (request.Protocol != EmbeddedFlashProtocol.PymcuprogUpdi || _toolchains.Environment is null)
        {
            return;
        }
        // Reuse PIC's existing SHA-256-pinned MIT Python wheels, without installing or licensing XC compilers.
        await _toolchains.PreparePicSupportAsync(false, line => Output.WriteLine(line, OutputKind.Info), cancellationToken);
        var install = new ProcessStartSpec
        {
            Program = "python3",
            Arguments = [MicrochipInstallation.ToolsDirectory + "/programmers.py", "pymcuprog"],
            WorkingDirectory = request.WorkingDirectory,
        };
        var result = await OutputProcess.RunAsync(_toolchains.Runtime, install, Output, null, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (!result.Succeeded)
        {
            throw new IOException("The existing verified pymcuprog support packages could not be prepared; no programming was attempted.");
        }
    }

    private void ShowEmbeddedFlashOutput()
    {
        SelectedPanel = BottomPanel.Output;
        IsBottomPanelVisible = true;
    }
}
