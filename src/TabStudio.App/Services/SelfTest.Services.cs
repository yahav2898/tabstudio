using System.Diagnostics;
using System.Globalization;
using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Services;

/// <summary>Starts PostgreSQL in the tablet's real PRoot environment, initializes a database and queries it.</summary>
public static partial class SelfTest
{
    public const string ServicesAppName = "SelfTestServices";

    internal static async Task<SelfTestResult> RunServicesAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var previous = main.Workspace?.Workspace;
        var previousPath = previous?.Solution?.FilePath ?? previous?.RootDirectory;
        try { return await RunServicesCoreAsync(main, templates, cancellationToken); }
        finally
        {
            // The next self-test step continues in the Python workspace left by RunToolchainsAsync.
            if (previousPath is not null) await main.OpenWorkspaceAsync(previousPath);
        }
    }

    private static async Task<SelfTestResult> RunServicesCoreAsync(MainViewModel main, ProjectTemplates templates, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var solution = templates.Create(ProjectTemplates.ConsoleId, ServicesAppName);
        var root = Path.GetDirectoryName(solution)!;
        var init = Path.Combine(root, "init"); Directory.CreateDirectory(init);
        File.WriteAllText(Path.Combine(init, "01-proof.sql"), "CREATE TABLE tablet_proof(value text); INSERT INTO tablet_proof VALUES ('postgres-on-tablet');");
        File.WriteAllText(Path.Combine(root, "compose.yaml"), """
            services:
              db:
                image: postgres:18
                environment:
                  POSTGRES_USER: tablet
                  POSTGRES_PASSWORD: self-test-only
                  POSTGRES_DB: tablet
                ports: ['0:5432']
                volumes: [data:/var/lib/postgresql/data, ./init:/docker-entrypoint-initdb.d:ro]
            """);
        await main.OpenWorkspaceAsync(solution);
        if (main.Workspace is not { } workspace) return new(false, "Services: the project did not open", clock.Elapsed);
        if (workspace.Toolchains.Runtime.Kind != RuntimeKind.Linux) return new(true, "Services: native Alpine integration runs in CI; no PRoot in this desktop head", clock.Elapsed);
        var project = TabStudio.Services.Compose.ComposeReader.Discover(root);
        if (project is null) return new(false, "Services: compose.yaml was not read", clock.Elapsed);
        await using var manager = new TabStudio.Services.ServiceManager(project, workspace.Toolchains.Runtime,
            new Toolchains.ServicePackages(workspace.Toolchains), new TabStudio.Services.Native.ServiceStorage(root, root));
        try
        {
            await manager.StartAllAsync(cancellationToken);
            var port = manager.Endpoints["db"][0].Published;
            var result = await CommandRunner.RunAsync(workspace.Toolchains.Runtime, new()
            {
                Program = "/bin/sh", Arguments = ["-c", "exec psql -X -w -Atqc 'SELECT value FROM tablet_proof'"], WorkingDirectory = root,
                Environment = new Dictionary<string, string?> { ["PGHOST"] = "127.0.0.1", ["PGPORT"] = port.ToString(CultureInfo.InvariantCulture), ["PGUSER"] = "tablet", ["PGDATABASE"] = "tablet", ["PGPASSWORD"] = "self-test-only" },
            }, null, cancellationToken);
            var success = result.Succeeded && result.Output.Contains("postgres-on-tablet", StringComparison.Ordinal);
            return new(success, success ? $"Services: PostgreSQL in PRoot on port {port}, init script and SELECT verified in {clock.Elapsed.TotalSeconds:0.0} s" : $"Services: query failed: {result.Tail(4)}", clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(false, "Services: " + ex.Message + " | " + string.Join(" | ", manager.Logs("db").TakeLast(8).Select(l => l.Text)), clock.Elapsed);
        }
        finally { await manager.StopAsync(cancellationToken: CancellationToken.None); }
    }
}

