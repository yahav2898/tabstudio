using System.Text.Json;
using TabStudio.LanguageServers.Configuration;
using TabStudio.LanguageServers.DebugAdapters;
using TabStudio.LanguageServers.Hosting;
using TabStudio.Toolchains.Embedded.Debugging;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

/// <summary>Real production-guest DAP initialization; intentionally does not attach any physical target.</summary>
internal static class ProbeRsDapCheck
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { WriteIndented = true };

    public static async Task VerifyAsync(IToolchainRuntime runtime, string directory, string evidencePath, CancellationToken cancellationToken)
    {
        var adapter = new DebugAdapterDefinition
        {
            Id = "embedded-probe-rs-proof", Name = "Pinned probe-rs DAP", Command = "probe-rs", Arguments = ["dap-server"],
            Environment = ProbeRsDebugPlan.Server(directory, 1).Environment,
        };
        var launcher = new RuntimeLauncher(runtime);
        var executable = launcher.Resolve(adapter.Command) ?? throw new InvalidOperationException("The production runtime cannot resolve probe-rs.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var log = new List<string>();
        var gate = new Lock();
        void Log(string line) { lock (gate) { if (log.Count < 512) log.Add(line); } }
        await using (var session = await DebugAdapterSession.StartAsync(adapter, executable, directory, launcher,
            new DebugAdapterSessionOptions { StartTimeout = TimeSpan.FromSeconds(20), TraceMessages = true, StartupLog = Log }, timeout.Token))
        {
            if (session.HasEnded) throw new InvalidOperationException("probe-rs ended immediately after its actual DAP initialize response.");
            await File.WriteAllTextAsync(evidencePath, JsonSerializer.Serialize(new
            {
                adapter = "probe-rs 0.32.0", transport = "stdio", dapInitialized = true,
                configurationDone = session.Capabilities.ConfigurationDone, hardwareVerified = false,
                chipAttached = false, flashingRequested = false,
            }, EvidenceJson), cancellationToken);
            Console.WriteLine("PROBE_RS_DAP_INITIALIZE_PASSED (no target attached)");
        }
        string details;
        lock (gate) details = string.Join('\n', log);
        await File.WriteAllTextAsync(Path.ChangeExtension(evidencePath, ".log"), details, cancellationToken);
    }

    private sealed class RuntimeLauncher(IToolchainRuntime runtime) : IServerProcessLauncher
    {
        public string? Resolve(string command) => runtime.Resolve(command);
        public string? ToLocalPath(string programPath) => runtime.ToDevicePath(programPath, null);
        public IServerProcess Start(ServerStartInfo info) => new Running(runtime.Start(new ProcessStartSpec
        {
            Program = info.FileName, Arguments = info.Arguments, WorkingDirectory = info.WorkingDirectory, Environment = info.Environment,
        }));
    }

    private sealed class Running(ToolchainProcess process) : IServerProcess
    {
        public Stream StandardInput => process.Input;
        public Stream StandardOutput => process.Output;
        public Stream StandardError => process.Error ?? Stream.Null;
        public Task<int> Exited => process.Exited;
        public void Kill() => process.Stop();
        public void Dispose() => process.Dispose();
    }
}
