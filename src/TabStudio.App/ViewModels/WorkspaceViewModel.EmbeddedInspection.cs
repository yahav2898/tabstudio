using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Diagnostics;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Toolchains.Embedded.Debugging;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    /// <summary>Reads one bounded static memory snapshot from the same halted adapter epoch.</summary>
    [RelayCommand(CanExecute = nameof(IsPaused))]
    private async Task ReadEmbeddedMemoryAsync()
    {
        try
        {
            var (adapter, epoch) = BeginEmbeddedInspection();
            if (!adapter.Capabilities.ReadMemory)
                throw new NotSupportedException("This adapter does not advertise DAP readMemory. Attach with an embedded GDB/probe-rs adapter build that supports memory reads.");
            var input = await Prompt.AskAsync("Read target memory", "Unsigned decimal or 0x hexadecimal address. This reads once from the halted target; use a known readable memory region:", "Next", "");
            if (input is null) return;
            var address = ParsePeripheralUnsigned(input);
            var countText = await Prompt.AskAsync("Memory snapshot size", "Number of bytes, from 1 to 4096:", "Read", "256");
            if (countText is null) return;
            var count = EmbeddedInspectionCount(countText, 4096);
            EnsureEmbeddedInspectionPaused(adapter, epoch);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var bytes = await adapter.ReadMemoryAsync(address, count, timeout.Token);
            EnsureEmbeddedInspectionPaused(adapter, epoch);
            var rows = EmbeddedMemoryDump.Format(address, bytes);
            ShowOutputSource(Output);
            Output.WriteLine($"Memory snapshot · halt epoch {epoch} · {count} bytes at 0x{address:X}", OutputKind.Info);
            foreach (var row in rows) Output.WriteLine(row, OutputKind.Info);
            StatusText = $"Read {count} target bytes (static snapshot)";
        }
        catch (Exception error) when (IsEmbeddedError(error) || error is DebugAdapterException or OperationCanceledException)
        {
            StatusText = "Target memory: " + error.Message;
        }
    }

    /// <summary>Requests actual DAP disassembly once, retaining source links only for existing mapped native files.</summary>
    [RelayCommand(CanExecute = nameof(IsPaused))]
    private async Task DisassembleEmbeddedAsync()
    {
        try
        {
            var (adapter, epoch) = BeginEmbeddedInspection();
            if (!adapter.Capabilities.Disassemble)
                throw new NotSupportedException("This adapter does not advertise DAP disassemble. Attach with an embedded GDB/probe-rs adapter build that supports disassembly.");
            var input = await Prompt.AskAsync("Disassemble target code", "Unsigned decimal or 0x hexadecimal instruction address:", "Next", "");
            if (input is null) return;
            var address = ParsePeripheralUnsigned(input);
            var countText = await Prompt.AskAsync("Disassembly size", "Number of instructions, from 1 to 256:", "Read", "32");
            if (countText is null) return;
            var count = EmbeddedInspectionCount(countText, 256);
            EnsureEmbeddedInspectionPaused(adapter, epoch);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var instructions = await adapter.DisassembleAsync(address, count, timeout.Token);
            EnsureEmbeddedInspectionPaused(adapter, epoch);
            var diagnostics = new List<BuildDiagnostic>();
            var directory = Workspace?.RootDirectory ?? _paths.Root;
            ShowOutputSource(Output);
            Output.WriteLine($"Disassembly snapshot · halt epoch {epoch} · {count} instruction rows at 0x{address:X}", OutputKind.Info);
            foreach (var instruction in instructions)
            {
                var text = $"0x{instruction.Address:X16}  {instruction.Bytes ?? ""}  {instruction.Instruction}" +
                    (instruction.Symbol is { Length: > 0 } symbol ? "  <" + symbol + ">" : "") +
                    (instruction.IsInvalid ? " [unavailable instruction]" : "");
                var source = EmbeddedInstructionSource(instruction, directory);
                Output.WriteLine(source is null ? text : $"{source.FilePath}:{source.Line}:{source.Column}: {text}", OutputKind.Info);
                if (source is not null)
                    diagnostics.Add(new("TSEMBDISASM", DiagnosticSeverityLevel.Info, text, source, "Target disassembly"));
            }
            ErrorList.SetRestoreDiagnostics(Path.Combine(directory, ".tabstudio", "#disassembly"), diagnostics);
            StatusText = $"Read {count} instruction rows · {diagnostics.Count} source links in Output/Error List";
        }
        catch (Exception error) when (IsEmbeddedError(error) || error is DebugAdapterException or OperationCanceledException)
        {
            StatusText = "Target disassembly: " + error.Message;
        }
    }

    private (DebugAdapterSession Adapter, int Epoch) BeginEmbeddedInspection()
    {
        if (_adapter is not { } adapter || !IsPaused || adapter.HasEnded)
            throw new InvalidOperationException("Pause the connected on-chip debug adapter before inspecting target memory or instructions.");
        return (adapter, Volatile.Read(ref _adapterPauses));
    }

    private void EnsureEmbeddedInspectionPaused(DebugAdapterSession adapter, int epoch)
    {
        if (!IsPaused || !ReferenceEquals(adapter, _adapter) || adapter.HasEnded || Volatile.Read(ref _adapterPauses) != epoch)
            throw new InvalidOperationException("The target resumed, stopped again or disconnected during inspection. Stale/mixed snapshots were discarded; pause and read again.");
    }

    private static int EmbeddedInspectionCount(string input, int maximum)
    {
        if (!int.TryParse(input.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1 || count > maximum)
            throw new ArgumentOutOfRangeException(nameof(input), "Enter a whole count from 1 to " + maximum.ToString(CultureInfo.InvariantCulture) + ".");
        return count;
    }

    private DiagnosticLocation? EmbeddedInstructionSource(DapDisassembledInstruction instruction, string directory)
    {
        if (instruction.IsInvalid || instruction.SourcePath is not { Length: > 0 } source || instruction.Line is not { } line) return null;
        try
        {
            var path = _toolchains.Runtime.ToDevicePath(source, directory);
            return path is not null && File.Exists(path) ? new(path, 0, 0, line, instruction.Column ?? 1) : null;
        }
        catch (Exception error) when (error is ArgumentException or IOException or NotSupportedException) { return null; }
    }
}
