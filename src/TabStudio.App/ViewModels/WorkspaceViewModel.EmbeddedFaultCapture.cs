using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.Input;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Embedded.Projects;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private sealed record EmbeddedFaultSession(EmbeddedProject Project, EmbeddedDebugProfile Profile,
        string Firmware, string FirmwareSha256);

    private EmbeddedFaultSession? _embeddedFaultSession;

    private async Task BindEmbeddedFaultSessionAsync(EmbeddedProject project, EmbeddedDebugProfile profile,
        string firmware, DebugAdapterSession adapter, CancellationToken cancellationToken)
    {
        await using var file = File.OpenRead(firmware);
        var fingerprint = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, cancellationToken));
        if (ReferenceEquals(_adapter, adapter) && !adapter.HasEnded)
            _embeddedFaultSession = new(project, profile, firmware, fingerprint);
    }

    /// <summary>Reads latched Cortex-M fault status from an explicitly configured, unchanged halted debug session.</summary>
    [RelayCommand]
    private async Task CaptureEmbeddedFaultAsync()
    {
        try
        {
            if (_adapter is not { Adapter.Id: "embedded-gdb" } adapter || !IsPaused || _adapterThread is not int thread
                || _embeddedFaultSession is not { } session)
            {
                throw new InvalidOperationException("Pause the matching on-chip GDB session before capturing live fault status.");
            }
            var epoch = _adapterPauses;
            var profilePath = Path.Combine(session.Project.Directory, EmbeddedDebugProfile.FileName);
            if (new FileInfo(profilePath).Length > 65536) throw new InvalidDataException("Embedded debug profile exceeds 64 KiB.");
            var profile = EmbeddedDebugProfile.Parse(await File.ReadAllTextAsync(profilePath));
            if (!CortexFaultCapture.IsSupportedModel(profile.FaultModel))
                throw new NotSupportedException("Explicitly set FaultModel to cortex-m-v7 or cortex-m-v8-mainline for the actual core. Cortex-M0/M0+, baseline cores and unknown layouts are refused.");
            var firmware = profile.ResolveFirmware(session.Project.Directory);
            if (firmware != session.Firmware || profile.InterfaceConfig != session.Profile.InterfaceConfig
                || profile.TargetConfig != session.Profile.TargetConfig || profile.ProbeSelector != session.Profile.ProbeSelector)
            {
                throw new InvalidOperationException("The debug profile no longer matches the attached session. Restart on-chip debugging before capturing fault evidence.");
            }
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await using (var file = File.OpenRead(firmware))
            {
                var fingerprint = Convert.ToHexStringLower(await SHA256.HashDataAsync(file, timeout.Token));
                if (fingerprint != session.FirmwareSha256) throw new InvalidOperationException("The attached session's ELF has changed. Reattach with matching firmware and symbols before capturing.");
                file.Position = 0;
                var header = new byte[20];
                await file.ReadExactlyAsync(header, timeout.Token);
                if (header[4] != 1 || header[5] != 1 || profile.ElfMachine(session.Project.Directory) != 40)
                    throw new NotSupportedException("Cortex-M live fault capture requires the exact little-endian ELF32 ARM firmware.");
            }
            EnsureFaultCapturePaused(adapter, epoch);
            var frames = await adapter.GetStackTraceAsync(thread, timeout.Token);
            EnsureFaultCapturePaused(adapter, epoch);
            if (frames.Count == 0) throw new InvalidOperationException("The halted adapter supplied no top frame for live core-register evaluation.");
            var reader = new EmbeddedCortexFaultReader(this, adapter, epoch, frames[0].Id);
            var evidence = await CortexFaultCapture.CaptureAsync(reader, profile.FaultModel!, timeout.Token);
            EnsureFaultCapturePaused(adapter, epoch);
            var snapshot = evidence.Snapshot;
            var directory = Path.Combine(session.Project.Directory, ".tabstudio", "faults");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
                + "-" + Guid.NewGuid().ToString("N") + ".json");
            var temporary = path + ".partial";
            try
            {
                await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(output, new
                    {
                        snapshot.Cfsr, snapshot.Hfsr, snapshot.Mmfar, snapshot.Bfar, snapshot.StackedWords,
                        snapshot.StackPointer, snapshot.StackStart, snapshot.StackEnd,
                        Capture = new
                        {
                            Utc = DateTimeOffset.UtcNow, profile.FaultModel, evidence.CpuId, evidence.LinkRegister,
                            evidence.MainStackPointer, evidence.ProcessStackPointer, evidence.ProgramCounter,
                            evidence.Frame, Firmware = session.Firmware, session.FirmwareSha256,
                        },
                    }, EmbeddedAnalysisJson, timeout.Token);
                    await output.FlushAsync(timeout.Token);
                }
                timeout.Token.ThrowIfCancellationRequested();
                File.Move(temporary, path);
            }
            finally { File.Delete(temporary); }
            Output.WriteLine("Live fault evidence saved: " + path, OutputKind.Info);
            Output.WriteLine(evidence.Frame.Reason + " Fault flags are latched since they were last cleared; capture does not clear flags, write target memory or resume the CPU.", OutputKind.Info);
            await ReportEmbeddedCrashAsync(session.Project, CrashDecoder.DecodeCortexM(snapshot), session.Firmware, timeout.Token);
        }
        catch (Exception error) when (IsEmbeddedError(error) || error is DebugAdapterException or OperationCanceledException)
        {
            Output.WriteLine("Live fault capture: " + error.Message, OutputKind.Failure);
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
        }
    }

    private void EnsureFaultCapturePaused(DebugAdapterSession adapter, int epoch)
    {
        if (!IsPaused || Volatile.Read(ref _adapterPauses) != epoch || !ReferenceEquals(adapter, _adapter) || adapter.HasEnded)
            throw new InvalidOperationException("The target resumed, stopped again or disconnected during capture. Mixed halt epochs are not saved.");
    }

    private sealed class EmbeddedCortexFaultReader(WorkspaceViewModel owner, DebugAdapterSession adapter,
        int epoch, int frameId) : ICortexFaultReader
    {
        public async Task<byte[]> ReadMemoryAsync(uint address, int count, CancellationToken cancellationToken)
        {
            owner.EnsureFaultCapturePaused(adapter, epoch);
            var data = await adapter.ReadMemoryAsync(address, count, cancellationToken);
            owner.EnsureFaultCapturePaused(adapter, epoch);
            return data;
        }

        public async Task<uint> ReadRegisterAsync(CortexFaultRegister registerKind, CancellationToken cancellationToken)
        {
            owner.EnsureFaultCapturePaused(adapter, epoch);
            var name = registerKind switch
            {
                CortexFaultRegister.Link => "lr", CortexFaultRegister.MainStack => "msp",
                CortexFaultRegister.ProcessStack => "psp", CortexFaultRegister.ProgramCounter => "pc",
                _ => throw new ArgumentOutOfRangeException(nameof(registerKind)),
            };
            try
            {
                // A register cast is evaluated in the freshly fetched top frame, never an arbitrary selected caller.
                var result = await adapter.EvaluateAsync("(unsigned int)$" + name, frameId, DapEvaluateContext.Watch, cancellationToken);
                owner.EnsureFaultCapturePaused(adapter, epoch);
                var number = Regex.Match(result.Result, @"^\s*(?:\([^)]*\)\s*)?(0x[0-9a-fA-F]+|\d+)(?:\s|$)",
                    RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (!number.Success) throw new InvalidDataException("The debugger did not return a live unsigned 32-bit " + name + " register.");
                var value = number.Groups[1].Value;
                return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? uint.Parse(value.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture)
                    : uint.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
            }
            catch (DebugAdapterException error)
            {
                owner.EnsureFaultCapturePaused(adapter, epoch);
                throw new NotSupportedException("The adapter cannot expose live " + name + ": " + error.Message, error);
            }
        }
    }
}
