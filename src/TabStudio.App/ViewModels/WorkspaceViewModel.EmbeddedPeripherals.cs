using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using CommunityToolkit.Mvvm.Input;
using TabStudio.Core.Diagnostics;
using TabStudio.Compilation.Features;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.Toolchains.Embedded.Peripherals;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private SvdDevice? _embeddedSvd;
    private SvdMaskMacros? _embeddedMaskMacros;
    private readonly Dictionary<string, (string File, SvdMaskReplacement Replacement)> _embeddedMaskActions = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _peripheralOperation = new(1, 1);
    internal int EmbeddedRegisterMetadataRevision { get; private set; }

    internal QuickInfoResult? GetEmbeddedRegisterQuickInfo(string source, int position)
    {
        if (_embeddedSvd is not { } svd || SvdRegisterLookup.FindReference(svd, source, position) is not { } reference) return null;
        return new(reference.Start, reference.Length, SymbolGlyph.Field,
            [new(QuickInfoSectionKind.Documentation, [new(RegisterAccess.Describe(reference.Peripheral, reference.Register), TaggedTextKind.Text)])]);
    }

    [RelayCommand]
    private async Task LoadEmbeddedRegisterHeaderAsync()
    {
        var path = await Prompt.AskAsync("Load device register macros", "Path to the actual selected chip's C header. Only macros whose definitions equal the SVD field mask are offered as quick fixes:", "Load", "");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw new InvalidDataException("Device header exceeds 16 MiB.");
            _embeddedMaskMacros = SvdMaskMacros.ParseHeader(await File.ReadAllTextAsync(path));
            _embeddedMaskActions.Clear();
            EmbeddedRegisterMetadataRevision++;
            StatusText = "Device header loaded. Ctrl+. on a numeric register mask offers verified field macros.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            StatusText = "Register macros: " + error.Message;
        }
    }

    internal IReadOnlyList<CodeActionItem> GetEmbeddedMaskCodeActions(string file, string source, int position)
    {
        if (_embeddedSvd is not { } svd || _embeddedMaskMacros is not { } macros) return [];
        if (_embeddedMaskActions.Count > 128) _embeddedMaskActions.Clear();
        return macros.FindReplacements(svd, source, position).Select(replacement =>
        {
            var id = "embedded-mask:" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
            _embeddedMaskActions[id] = (file, replacement);
            return new CodeActionItem(id, "Use verified mask " + replacement.Macro, "Device register masks", CodeActionKind.Refactoring, null);
        }).ToArray();
    }

    internal DocumentEdit? GetEmbeddedMaskCodeActionEdit(string id, string file, string source)
    {
        if (!_embeddedMaskActions.Remove(id, out var pending) || pending.File != file) return null;
        var edit = pending.Replacement;
        if (edit.Start < 0 || edit.Start > source.Length - edit.Length || source.Substring(edit.Start, edit.Length) != edit.Original) return null;
        return new(file, DocumentEditKind.Modified, [new(edit.Start, edit.Length, edit.Macro)]);
    }

    [RelayCommand]
    private async Task CheckEmbeddedPinsAsync()
    {
        if (ActiveDocument is not { } document)
        {
            StatusText = "Open a CubeMX .ioc, devicetree .dts/.overlay or Arduino .ino file to check its explicit pin assignments.";
            return;
        }
        try
        {
            var path = document.FilePath;
            var text = document.Document.Text;
            var extension = Path.GetExtension(path).ToLowerInvariant();
            IReadOnlyList<PinAssignment> assignments = extension switch
            {
                ".ioc" => PinConflictChecker.ReadCubeIoc(text, path),
                ".dts" or ".dtsi" or ".overlay" => PinConflictChecker.ReadDevicetree(text, path),
                ".ino" => PinConflictChecker.ReadArduino(text, path),
                _ => throw new NotSupportedException("Pin checks support .ioc, .dts/.dtsi/.overlay and .ino files.")
            };
            IReadOnlySet<string>? pins = null;
            var mapFile = Workspace is { } workspace ? Path.Combine(workspace.RootDirectory, ".tabstudio", "pin-map.json") : null;
            if (mapFile is not null && File.Exists(mapFile))
            {
                if (new FileInfo(mapFile).Length > 256 * 1024) throw new InvalidDataException("Pin map exceeds 256 KiB.");
                using var map = JsonDocument.Parse(await File.ReadAllTextAsync(mapFile));
                var selectedDevice = map.RootElement.GetProperty("device").GetString();
                if (_embeddedSvd is { } svd && selectedDevice != svd.Name) throw new InvalidDataException("The pin map's device does not match the loaded SVD device.");
                pins = map.RootElement.GetProperty("pins").EnumerateArray().Select(p => p.GetString()
                    ?? throw new InvalidDataException("Pin names must be strings.")).ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
            var diagnostics = PinConflictChecker.Check(assignments, pins).Select(diagnostic => new BuildDiagnostic(
                diagnostic.Code, diagnostic.Code == "TSEMBPIN002" ? DiagnosticSeverityLevel.Error : DiagnosticSeverityLevel.Warning,
                diagnostic.Message, new DiagnosticLocation(diagnostic.File, 0, 0, diagnostic.Line, 1), "Pin assignments")).ToArray();
            ErrorList.SetRestoreDiagnostics(path + "#embedded-pins", diagnostics);
            SelectedPanel = BottomPanel.ErrorList;
            ShowOutputSource(Output);
            Output.WriteLine($"Pin check: {assignments.Count} explicit assignments, {diagnostics.Length} findings. Dynamic expressions, included files and board aliases require their actual resolved pin map.", OutputKind.Info);
            SelectedPanel = BottomPanel.ErrorList;
            StatusText = $"Pin check: {diagnostics.Length} findings";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException or JsonException or InvalidOperationException or KeyNotFoundException or RegexMatchTimeoutException)
        {
            StatusText = "Pin check: " + error.Message;
        }
    }

    [RelayCommand]
    private async Task LoadEmbeddedSvdAsync()
    {
        var path = await Prompt.AskAsync("Load device registers", "Path to the actual selected chip's CMSIS-SVD file (from its vendor device pack):", "Load",
            ActiveDocument?.FilePath.EndsWith(".svd", StringComparison.OrdinalIgnoreCase) == true ? ActiveDocument.FilePath : "");
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            var file = new FileInfo(path);
            if (file.Length > 16 * 1024 * 1024) throw new InvalidDataException("SVD file exceeds 16 MiB.");
            var xml = await File.ReadAllTextAsync(path);
            _embeddedSvd = await Task.Run(() => SvdReader.Parse(xml));
            _embeddedMaskMacros = null;
            _embeddedMaskActions.Clear();
            EmbeddedRegisterMetadataRevision++;
            ShowOutputSource(Output);
            Output.WriteLine($"{_embeddedSvd.Name}: {_embeddedSvd.Peripherals.Count} peripherals, {_embeddedSvd.Peripherals.Sum(p => p.Registers.Count)} registers. Choose Inspect device register to read a halted target.", OutputKind.Info);
            foreach (var peripheral in _embeddedSvd.Peripherals)
                Output.WriteLine($"  {peripheral.Name} @ 0x{peripheral.BaseAddress:X}: {peripheral.Description}", OutputKind.Info);
            StatusText = "Device register description loaded";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or FormatException or XmlException or OverflowException)
        {
            StatusText = "Device registers: " + error.Message;
        }
    }

    [RelayCommand]
    private async Task InspectEmbeddedRegisterAsync()
    {
        var selected = await ChooseEmbeddedRegisterAsync();
        if (selected is not { } entry) return;
        ShowOutputSource(Output);
        Output.WriteLine(RegisterAccess.Describe(entry.Peripheral, entry.Register), OutputKind.Info);
        if (_adapter is not { } adapter || !IsPaused)
        {
            Output.WriteLine("Pause the connected embedded debug adapter to read the actual register value.", OutputKind.Info);
            return;
        }
        if (entry.Register.Access is "write-only" or "writeOnce" || entry.Register.ReadAction is { Length: > 0 } || entry.Register.Fields.Any(f => f.ReadAction is { Length: > 0 }))
        {
            Output.WriteLine("This register is write-only or has read side effects. Automatic inspection is refused.", OutputKind.Info);
            return;
        }
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var value = await ReadPeripheralRegisterAsync(adapter, entry.Register, timeout.Token);
            Output.WriteLine($"{entry.Peripheral.Name}.{entry.Register.Name} = 0x{value:X}", OutputKind.Info);
            foreach (var field in entry.Register.Fields)
            {
                var decoded = field.Decode(value);
                var label = field.Values.FirstOrDefault(v => v.Value == decoded)?.Name;
                Output.WriteLine($"  {field.Name} = {decoded} (0x{decoded:X}){(label is null ? "" : " " + label)}", OutputKind.Info);
            }
        }
        catch (Exception error) when (error is DebugAdapterException or InvalidDataException or OperationCanceledException or InvalidOperationException)
        {
            StatusText = "Register read: " + error.Message;
        }
    }

    [RelayCommand]
    private async Task EditEmbeddedRegisterFieldAsync()
    {
        var selected = await ChooseEmbeddedRegisterAsync();
        if (selected is not { } entry || _embeddedSvd is not { } device || Workspace is not { } workspace || _adapter is not { } adapter || !IsPaused)
        {
            StatusText = "Load the selected device's SVD and pause its embedded debug adapter first.";
            return;
        }
        var fieldIndex = await Prompt.ChooseAsync("Edit register field", "Choose an ordinary writable field. Read/write side effects and configuration registers are refused.", "Select", entry.Register.Fields.Select(f => f.Name + " — " + f.Description).ToArray());
        if (fieldIndex is null || fieldIndex < 0 || fieldIndex >= entry.Register.Fields.Count) return;
        var field = entry.Register.Fields[fieldIndex.Value];
        await _peripheralOperation.WaitAsync();
        try
        {
            var mapFile = Path.Combine(workspace.RootDirectory, ".tabstudio", "peripheral-access.json");
            if (!File.Exists(mapFile)) throw new InvalidOperationException("Register writes require the reviewed device/register/address allowlist in .tabstudio/peripheral-access.json. SVD metadata never grants write permission.");
            if (new FileInfo(mapFile).Length > 256 * 1024) throw new InvalidDataException("Peripheral access map exceeds 256 KiB.");
            var policy = PeripheralAccessPolicy.Parse(await File.ReadAllTextAsync(mapFile));
            policy.AuthorizeOrdinaryWrite(device, entry.Peripheral, entry.Register);
            _ = RegisterAccess.CreateFieldWrite(entry.Register, field, 0, 0); // Validate side effects before any target read.
            var input = await Prompt.AskAsync("Set " + field.Name, "Unsigned decimal or 0x hexadecimal field value:", "Preview", "0");
            if (input is null) return;
            var requested = ParsePeripheralUnsigned(input);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            var current = await ReadPeripheralRegisterAsync(adapter, entry.Register, timeout.Token);
            var write = RegisterAccess.CreateFieldWrite(entry.Register, field, requested, current);
            var confirmation = $"WRITE {device.Name} {entry.Peripheral.Name}.{entry.Register.Name}.{field.Name} @ 0x{write.Address:X}: 0x{current:X} -> 0x{write.Value:X}";
            var typed = await Prompt.AskAsync("Confirm ordinary peripheral write", confirmation + "\nType this exact operation to apply it to the halted target:", "Write", "");
            if (typed != confirmation) return;
            EnsurePeripheralTargetPaused(adapter);
            if (await ReadPeripheralRegisterAsync(adapter, entry.Register, timeout.Token) != current)
                throw new InvalidOperationException("The register changed while confirmation was open. Inspect and review it again.");
            var bytes = new byte[write.SizeBits / 8];
            for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)(write.Value >> (8 * i));
            EnsurePeripheralTargetPaused(adapter);
            await adapter.WriteMemoryAsync(write.Address, bytes, timeout.Token);
            var verified = await ReadPeripheralRegisterAsync(adapter, entry.Register, timeout.Token);
            if (verified != write.Value) throw new InvalidDataException($"Register readback 0x{verified:X} did not match 0x{write.Value:X}. The write was sent; inspect before retrying.");
            ShowOutputSource(Output);
            Output.WriteLine(confirmation + " — readback verified", OutputKind.Info);
            StatusText = "Register field updated and verified";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException or JsonException or FormatException or OverflowException or DebugAdapterException or OperationCanceledException or KeyNotFoundException)
        {
            StatusText = "Register write: " + error.Message;
        }
        finally { _peripheralOperation.Release(); }
    }

    [RelayCommand]
    private async Task ShowEmbeddedRtosThreadsAsync()
    {
        if (_adapter is not { } adapter || !IsPaused)
        {
            StatusText = "Pause the embedded debug adapter to inspect RTOS tasks.";
            return;
        }
        var kind = await Prompt.ChooseAsync("RTOS tasks", "Read the firmware's own debug symbols. All cores must be halted. No target functions are called.", "Read", ["FreeRTOS", "Zephyr (CONFIG_THREAD_MONITOR)"]);
        if (kind is null) return;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var reader = new PeripheralRtosReader(this, adapter);
            var threads = kind == 0 ? await RtosThreadReader.ReadFreeRtosAsync(reader, cancellationToken: timeout.Token)
                : await RtosThreadReader.ReadZephyrAsync(reader, cancellationToken: timeout.Token);
            ShowOutputSource(Output);
            foreach (var thread in threads)
                Output.WriteLine($"{thread.Name} @ 0x{thread.Address:X}: {thread.State}, priority {thread.Priority}; stack {(thread.StackBytes is { } size ? size + " B" : "unavailable")}; minimum free stack unreported without verified initialization settings.", OutputKind.Info);
            StatusText = $"{threads.Count} RTOS tasks read";
        }
        catch (Exception error) when (error is DebugAdapterException or InvalidDataException or InvalidOperationException or NotSupportedException or OperationCanceledException or OverflowException)
        {
            StatusText = "RTOS task inspection: " + error.Message;
        }
    }

    private async Task<(SvdPeripheral Peripheral, SvdRegister Register)?> ChooseEmbeddedRegisterAsync()
    {
        if (_embeddedSvd is not { } device)
        {
            StatusText = "Load the selected chip's CMSIS-SVD file first.";
            return null;
        }
        var peripheralIndex = await Prompt.ChooseAsync("Device peripherals", device.Name, "Select", device.Peripherals.Select(p => p.Name + " — " + p.Description).ToArray());
        if (peripheralIndex is null || peripheralIndex < 0 || peripheralIndex >= device.Peripherals.Count) return null;
        var peripheral = device.Peripherals[peripheralIndex.Value];
        var registerIndex = await Prompt.ChooseAsync("Registers: " + peripheral.Name, peripheral.Description, "Select", peripheral.Registers.Select(r => r.Name + $" @ 0x{r.Address:X} — " + r.Description).ToArray());
        return registerIndex is { } index && index >= 0 && index < peripheral.Registers.Count ? (peripheral, peripheral.Registers[index]) : null;
    }

    private async Task<ulong> ReadPeripheralRegisterAsync(DebugAdapterSession adapter, SvdRegister register, CancellationToken cancellationToken)
    {
        EnsurePeripheralTargetPaused(adapter);
        if (register.SizeBits is not (8 or 16 or 32 or 64)) throw new InvalidDataException("Register debug access requires an 8/16/32/64-bit width.");
        if (_embeddedSvd?.ByteOrder != "little") throw new InvalidDataException("This register view requires an explicitly little-endian SVD CPU description. Big/selectable/unspecified byte order needs a target-specific adapter.");
        var bytes = await adapter.ReadMemoryAsync(register.Address, register.SizeBits / 8, cancellationToken);
        ulong value = 0;
        for (var i = 0; i < bytes.Length; i++) value |= (ulong)bytes[i] << (8 * i);
        return value;
    }

    private void EnsurePeripheralTargetPaused(DebugAdapterSession adapter)
    {
        if (!IsPaused || !ReferenceEquals(adapter, _adapter) || adapter.HasEnded)
            throw new InvalidOperationException("The selected target is no longer halted in this debug session.");
    }

    private static ulong ParsePeripheralUnsigned(string text)
    {
        var input = text.Trim();
        return input.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? ulong.Parse(input[2..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
            : ulong.Parse(input, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private sealed class PeripheralRtosReader(WorkspaceViewModel owner, DebugAdapterSession adapter) : IRtosDebugReader
    {
        public async Task<ulong> EvaluateUnsignedAsync(string expression, CancellationToken cancellationToken)
        {
            owner.EnsurePeripheralTargetPaused(adapter);
            try
            {
                var result = await adapter.EvaluateAsync(expression, null, DapEvaluateContext.Watch, cancellationToken);
                var hexadecimal = Regex.Match(result.Result, @"\b0x[0-9a-fA-F]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (hexadecimal.Success) return ParsePeripheralUnsigned(hexadecimal.Value);
                var number = Regex.Match(result.Result, @"^\s*(?:\([^)]*\)\s*)?(\d+)(?:\s|$)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
                if (number.Success) return ParsePeripheralUnsigned(number.Groups[1].Value);
                throw new InvalidDataException("The debugger did not return an unsigned scalar for " + expression);
            }
            catch (DebugAdapterException error) { throw new NotSupportedException("Matching RTOS type/symbol information is unavailable: " + error.Message, error); }
        }

        public Task<byte[]> ReadMemoryAsync(ulong address, int count, CancellationToken cancellationToken)
        {
            owner.EnsurePeripheralTargetPaused(adapter);
            return adapter.ReadMemoryAsync(address, count, cancellationToken);
        }
    }
}
