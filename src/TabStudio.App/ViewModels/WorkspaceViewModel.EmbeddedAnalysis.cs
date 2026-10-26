using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using TabStudio.App.Services;
using TabStudio.Core.Diagnostics;
using TabStudio.Toolchains.Embedded.Analysis;
using TabStudio.Toolchains.Embedded.Projects;
using TabStudio.Toolchains.Processes;
using TabStudio.Web.Serving;

namespace TabStudio.App.ViewModels;

public sealed partial class WorkspaceViewModel
{
    private static readonly JsonSerializerOptions EmbeddedAnalysisJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private readonly SemaphoreSlim _embeddedMemoryGate = new(1, 1);
    private StaticSiteServer? _embeddedMemoryServer;

    private sealed record EmbeddedAnalysisOptions
    {
        public string? Firmware { get; init; }
        public string? Addr2Line { get; init; }
        public string? MapFile { get; init; }
        public IReadOnlyList<FirmwareMemoryRegion>? MemoryRegions { get; init; }
        public IReadOnlyList<string> StackUsageFiles { get; init; } = [];
        public IReadOnlyList<StackCall> StackCalls { get; init; } = [];
        public IReadOnlyList<TaskStackBudget> TaskBudgets { get; init; } = [];
        public bool CallGraphComplete { get; init; }
    }

    [RelayCommand]
    private async Task AnalyzeEmbeddedMemoryAsync()
    {
        if (await SelectEmbeddedProjectAsync() is not { } project) return;
        if (!await ReportEmbeddedMemoryAsync(project.Directory, CancellationToken.None)) return;
        var folder = Path.Combine(project.Directory, ".tabstudio", "memory-report");
        if (!File.Exists(Path.Combine(folder, "index.html"))) return;
        try
        {
            if (_embeddedMemoryServer is not null) await _embeddedMemoryServer.DisposeAsync();
            _embeddedMemoryServer = StaticSiteServer.Start(folder);
            WebPreview.Show("Firmware memory", _embeddedMemoryServer.Address, null);
            SelectSideBarPanel(SideBarPanel.WebPreview);
        }
        catch (Exception ex) when (ex is IOException or System.Net.HttpListenerException)
        {
            Output.WriteLine("Memory report: " + ex.Message, OutputKind.Failure);
        }
    }

    /// <summary>Reports a real linked ELF after build; a missing or ambiguous artifact is never guessed.</summary>
    internal async Task<bool> ReportEmbeddedMemoryAsync(string directory, CancellationToken cancellationToken)
    {
        await _embeddedMemoryGate.WaitAsync(cancellationToken);
        try
        {
            var options = ReadEmbeddedAnalysisOptions(directory);
            var elf = FindEmbeddedElf(directory, options);
            if (elf is null)
            {
                Output.WriteLine("Memory analysis needs one linked ELF. Set Firmware in .tabstudio/embedded-analysis.json or .tabstudio/embedded.json when the build has multiple artifacts.", OutputKind.Info);
                return false;
            }
            var report = await Task.Run(async () =>
            {
                if (new FileInfo(elf).Length > 128 * 1024 * 1024) throw new InvalidDataException("ELF exceeds the 128 MB analysis limit.");
                var image = await File.ReadAllBytesAsync(elf, cancellationToken).ConfigureAwait(false);
                return MemoryAnalyzer.AnalyzeElf(image, options.MemoryRegions);
            }, cancellationToken);
            var path = Path.Combine(directory, ".tabstudio", "embedded-memory.json");
            FirmwareMemoryReport? previous = null;
            if (File.Exists(path))
            {
                try { previous = JsonSerializer.Deserialize<FirmwareMemoryReport>(await File.ReadAllTextAsync(path, cancellationToken), EmbeddedAnalysisJson); }
                catch (JsonException) { /* A corrupt previous report cannot invalidate the current ELF. */ }
            }
            var delta = previous is null ? null : report.Compare(previous);
            var message = FormattableString.Invariant($"Flash {report.FlashBytes:N0} B · RAM {report.RamBytes:N0} B");
            if (delta is not null) message += FormattableString.Invariant($" · Δ flash {delta.FlashBytes:+#,0;-#,0;0} B, RAM {delta.RamBytes:+#,0;-#,0;0} B");
            Output.WriteLine(message + (report.PlacementInferred ? " (RAM placement partly inferred from ELF flags)" : ""), OutputKind.Info);
            StatusText = message;
            var reportFolder = Path.Combine(directory, ".tabstudio", "memory-report");
            Directory.CreateDirectory(reportFolder);
            IReadOnlyList<FirmwareObjectContribution> objects = [];
            var maps = options.MapFile is { Length: > 0 } configuredMap
                ? new[] { Path.GetFullPath(configuredMap, directory) }
                : FindEmbeddedBuildFiles(directory, ".map").Take(2).ToArray();
            if (maps.Length == 1)
            {
                if (new FileInfo(maps[0]).Length > 16 * 1024 * 1024) throw new InvalidDataException("Linker map exceeds the 16 MB analysis limit.");
                objects = await Task.Run(async () => MemoryAnalyzer.ParseGnuMap(await File.ReadAllTextAsync(maps[0], cancellationToken)), cancellationToken);
            }
            await File.WriteAllTextAsync(Path.Combine(reportFolder, "index.html"), EmbeddedMemoryReport.Create(report, delta, objects), cancellationToken);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(report, EmbeddedAnalysisJson), cancellationToken);
            _embeddedMemoryServer?.NotifyChanged(Path.Combine(reportFolder, "index.html"));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or OverflowException or ArgumentException)
        {
            Output.WriteLine("Memory analysis: " + ex.Message, OutputKind.Failure);
            return false;
        }
        finally { _embeddedMemoryGate.Release(); }
    }

    [RelayCommand]
    private async Task DecodeEmbeddedCrashAsync()
    {
        var text = ActiveDocument?.Document.Text ?? Output.Text;
        if (await SelectEmbeddedProjectAsync() is not { } project) return;
        try
        {
            var report = text.TrimStart().StartsWith('{')
                ? CrashDecoder.DecodeCortexM(JsonSerializer.Deserialize<CortexFaultSnapshot>(text, EmbeddedAnalysisJson)
                    ?? throw new InvalidDataException("Expected a CortexFaultSnapshot JSON object with numeric register values."))
                : CrashDecoder.DecodeEspPanic(text);
            var options = ReadEmbeddedAnalysisOptions(project.Directory);
            var elf = FindEmbeddedElf(project.Directory, options);
            IReadOnlyList<FirmwareSourceFrame> frames = [];
            if (options.Addr2Line is { Length: > 0 } addr2line && elf is not null && report.ProgramCounters.Count > 0)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var arguments = new[] { "-a", "-f", "-C", "-e", elf }
                    .Concat(report.ProgramCounters.Take(64).Select(pc => "0x" + pc.ToString("x", CultureInfo.InvariantCulture))).ToArray();
                var result = await CommandRunner.RunAsync(_toolchains.Runtime,
                    new ProcessStartSpec { Program = addr2line, Arguments = arguments, WorkingDirectory = project.Directory }, null, timeout.Token);
                if (result.Succeeded) frames = Addr2LineParser.Parse(result.Output);
                else Output.WriteLine("addr2line did not resolve this firmware: " + result.Tail(), OutputKind.Failure);
            }
            var primary = frames.Count > 0 ? frames[0] : null;
            DiagnosticLocation? location = null;
            if (primary is not null && _toolchains.Runtime.ToDevicePath(primary.File, project.Directory) is { } source && File.Exists(source))
                location = new(source, 0, 0, primary.Line, 1);
            var diagnostics = report.Findings.Select(finding => new BuildDiagnostic(finding.Code, DiagnosticSeverityLevel.Error,
                finding.Message + (finding.Address is ulong address ? $" [0x{address:X}]" : ""), location, project.Name)).ToArray();
            ErrorList.SetRestoreDiagnostics(project.MarkerPath + "#embedded-crash", diagnostics);
            foreach (var finding in diagnostics) Output.WriteLine(finding.ToString(), OutputKind.Error);
            foreach (var frame in frames) Output.WriteLine($"  {frame.Function} at {frame.File}:{frame.Line} [0x{frame.Address:X}]", OutputKind.Info);
            if (frames.Count == 0 && report.ProgramCounters.Count > 0)
                Output.WriteLine("Captured PCs: " + string.Join(", ", report.ProgramCounters.Select(pc => $"0x{pc:X}")) + ". Configure Addr2Line and the matching Firmware ELF in .tabstudio/embedded-analysis.json for source locations.", OutputKind.Info);
            SelectedPanel = BottomPanel.ErrorList;
            IsBottomPanelVisible = true;
            StatusText = $"Decoded {report.Findings.Count} crash findings; {frames.Count} source frames";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or OperationCanceledException or ToolchainRuntimeException)
        {
            Output.WriteLine("Crash decoder: " + ex.Message, OutputKind.Failure);
        }
    }

    [RelayCommand]
    private async Task AnalyzeEmbeddedStackAsync()
    {
        if (await SelectEmbeddedProjectAsync() is not { } project) return;
        try
        {
            var options = ReadEmbeddedAnalysisOptions(project.Directory);
            var files = options.StackUsageFiles.Count > 0
                ? options.StackUsageFiles.Select(file => Path.GetFullPath(file, project.Directory)).ToArray()
                : FindEmbeddedBuildFiles(project.Directory, ".su").ToArray();
            if (files.Length == 0)
            {
                Output.WriteLine("No GCC .su files were found. Build with -fstack-usage, then configure StackUsageFiles, StackCalls and TaskBudgets in .tabstudio/embedded-analysis.json. CallGraphComplete defaults false; missing call evidence never passes a stack budget.", OutputKind.Info);
                return;
            }
            var analysis = await Task.Run(() =>
            {
                var entries = files.SelectMany(file => StackAnalyzer.ParseGccStackUsage(File.ReadAllText(file))).ToArray();
                return StackAnalyzer.Analyze(entries, options.StackCalls, options.CallGraphComplete);
            });
            foreach (var function in analysis.OrderByDescending(f => f.KnownBytes))
            {
                Output.WriteLine(function.WorstCaseBytes is ulong bound
                    ? $"{function.Function}: {bound} B including callees"
                    : $"{function.Function}: local/known frames {function.KnownBytes} B; worst case unknown. " + string.Join(" ", function.Reasons), OutputKind.Info);
            }
            var budgets = StackAnalyzer.CheckTaskBudgets(analysis, options.TaskBudgets);
            ErrorList.SetRestoreDiagnostics(project.MarkerPath + "#embedded-stack", budgets.Select(b => new BuildDiagnostic(
                b.ExceedsBudget ? "EMBSTACK001" : "EMBSTACK002", b.ExceedsBudget || b.RequiredBytes is null ? DiagnosticSeverityLevel.Warning : DiagnosticSeverityLevel.Info,
                b.Task + ": " + b.Message, null, project.Name)).ToArray());
            foreach (var budget in budgets) Output.WriteLine(budget.Task + ": " + budget.Message, budget.ExceedsBudget ? OutputKind.Failure : OutputKind.Info);
            SelectedPanel = BottomPanel.Output;
            IsBottomPanelVisible = true;
            StatusText = $"Stack analysis: {analysis.Count} functions, {analysis.Count(f => f.WorstCaseBytes is null)} unknown bounds";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or OverflowException or ArgumentException)
        {
            Output.WriteLine("Stack analysis: " + ex.Message, OutputKind.Failure);
        }
    }

    private static EmbeddedAnalysisOptions ReadEmbeddedAnalysisOptions(string directory)
    {
        var path = Path.Combine(directory, ".tabstudio", "embedded-analysis.json");
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Embedded analysis configuration exceeds 1 MB.");
        var options = JsonSerializer.Deserialize<EmbeddedAnalysisOptions>(File.ReadAllText(path), EmbeddedAnalysisJson)
            ?? throw new InvalidDataException("Embedded analysis configuration must be a JSON object.");
        if (options.StackUsageFiles is null || options.StackCalls is null || options.TaskBudgets is null
            || options.StackUsageFiles.Any(string.IsNullOrWhiteSpace) || options.StackCalls.Any(call => call is null)
            || options.TaskBudgets.Any(task => task is null) || options.MemoryRegions?.Any(region => region is null) == true)
        {
            throw new InvalidDataException("Analysis collections and their entries cannot be null or blank.");
        }
        return options;
    }

    private static string? FindEmbeddedElf(string directory, EmbeddedAnalysisOptions options)
    {
        var explicitFirmware = options.Firmware ?? EmbeddedBuildPlanner.LoadOptions(directory).Firmware;
        if (explicitFirmware is { Length: > 0 })
        {
            var path = Path.GetFullPath(explicitFirmware, directory);
            if (!File.Exists(path)) throw new FileNotFoundException("Configured firmware does not exist.", path);
            return path;
        }
        var files = FindEmbeddedBuildFiles(directory, ".elf").Concat(FindEmbeddedBuildFiles(directory, ".axf")).Distinct(StringComparer.Ordinal).Take(2).ToArray();
        return files.Length == 1 ? files[0] : null;
    }

    private static IEnumerable<string> FindEmbeddedBuildFiles(string directory, string extension)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            if (Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)) yield return path;
        string[] roots = ["build", "Debug", "Release", ".pio/build", "target", ".tabstudio/embedded/build"];
        var count = 0;
        foreach (var relative in roots)
        {
            var root = Path.Combine(directory, relative);
            if (!Directory.Exists(root)) continue;
            var enumeration = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 20 };
            foreach (var path in Directory.EnumerateFiles(root, "*", enumeration))
            {
                if (++count > 100_000) throw new IOException("Build tree exceeds the scan limit; configure explicit analysis files.");
                if (Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase)) yield return path;
            }
        }
    }

    private void DisposeEmbeddedAnalysis()
    {
        var server = _embeddedMemoryServer;
        _embeddedMemoryServer = null;
        if (server is not null) _ = server.DisposeAsync().AsTask();
    }
}
