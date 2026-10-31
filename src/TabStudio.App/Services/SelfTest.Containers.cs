using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using TabStudio.App.Platform;
using TabStudio.App.Swarm;
using TabStudio.App.ViewModels;
using TabStudio.Toolchains.Processes;

namespace TabStudio.App.Services;

/// <summary>Real OCI images under the APK's PRoot and Android app UID, including a rendered nginx preview.</summary>
public static partial class SelfTest
{
    private static async Task<SelfTestResult> RunContainersAsync(MainViewModel main, ProjectTemplates templates,
        string? screenshots, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        var solution = templates.Create(ProjectTemplates.ConsoleId, "SelfTestContainers");
        var root = Path.GetDirectoryName(solution)!;
        await File.WriteAllTextAsync(Path.Combine(root, "compose.yaml"), """
            services:
              db:
                image: postgres:17-alpine
                environment: { POSTGRES_USER: tablet, POSTGRES_PASSWORD: self-test, POSTGRES_DB: tablet }
                ports: ['0:5432']
                healthcheck: { test: ['CMD', 'pg_isready', '-U', 'tablet'], interval: 1s, retries: 60 }
              cache:
                image: redis:7-alpine
                ports: ['0:6379']
              web:
                image: nginx:alpine
                ports: ['0:80']
              app:
                build: .
                depends_on: [db, cache, web]
            """, cancellationToken);
        await main.OpenWorkspaceAsync(solution);
        var workspace = main.Workspace;
        if (workspace?.Services.Manager is not { } manager) return new(false, "Containers: Compose did not load", clock.Elapsed);
        var engine = workspace.Containers.Engine;
        try
        {
            // The exact F5 path: select application dependencies, start real images, rewrite host endpoints.
            var environment = await manager.StartForProjectAsync(root, cancellationToken: cancellationToken);
            var db = engine.Containers.Single(c => c.Label("com.docker.compose.service") == "db");
            var sql = await engine.ExecCaptureAsync(db.Id, ["psql", "-U", "tablet", "-d", "tablet", "-Atqc", "SELECT 21*2"], TimeSpan.FromSeconds(15), cancellationToken);
            if (sql.ExitCode != 0 || !sql.Output.Contains("42", StringComparison.Ordinal)) throw new IOException("PostgreSQL query failed: " + sql.Output);
            using (var postgres = new TcpClient())
                await postgres.ConnectAsync("127.0.0.1", manager.Endpoints["db"][0].Published, cancellationToken);
            using (var redis = new TcpClient())
            {
                await redis.ConnectAsync("127.0.0.1", manager.Endpoints["cache"][0].Published, cancellationToken);
                await redis.GetStream().WriteAsync(Encoding.ASCII.GetBytes("*1\r\n$4\r\nPING\r\n"), cancellationToken);
                var answer = new byte[7]; await redis.GetStream().ReadExactlyAsync(answer, cancellationToken);
                if (Encoding.ASCII.GetString(answer) != "+PONG\r\n") throw new IOException("Redis did not answer PING.");
            }
            var address = new Uri($"http://127.0.0.1:{manager.Endpoints["web"][0].Published}/");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            var page = await http.GetStringAsync(address, cancellationToken);
            if (!page.Contains("Welcome to nginx", StringComparison.Ordinal)) throw new IOException("nginx did not serve its page.");
            workspace.WebPreview.Show("nginx container", address, null);
            workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.WebPreview);
            var previewClock = Stopwatch.StartNew();
            var rendered = false;
            while (previewClock.Elapsed < TimeSpan.FromSeconds(30))
            {
                // A container preview has no IDE-run web project, so it is not registered as a Swarm screen.
                if (workspace.WebPreview.WebView is IScriptableWebView browser)
                {
                    var text = await browser.EvaluateAsync("document.body && document.body.textContent", cancellationToken);
                    if (text?.Contains("Welcome to nginx", StringComparison.Ordinal) == true) { rendered = true; break; }
                }
                await Task.Delay(200, cancellationToken);
            }
            if (!rendered) throw new IOException("The web preview did not render nginx's page.");
            if (screenshots is not null)
            {
                if (workspace.WebPreview.WebView is not IScriptableWebView preview
                    || await preview.CaptureAsync(ScreenCapture.MaxPixels, cancellationToken) is not { Length: > 0 } picture)
                    throw new IOException("The nginx preview could not be captured.");
                Directory.CreateDirectory(screenshots);
                await File.WriteAllBytesAsync(Path.Combine(screenshots, "containers-nginx.png"), picture, cancellationToken);
            }
            workspace.Containers.PrepareTerminalCommand();
            var cli = await CommandRunner.RunAsync(workspace.Toolchains.Runtime, new()
            { Program = "/bin/sh", Arguments = ["-c", "docker ps"], WorkingDirectory = root }, null, cancellationToken);
            if (!cli.Succeeded || !cli.Output.Contains("nginx:alpine", StringComparison.Ordinal)) throw new IOException("docker terminal command failed: " + cli.Output);
            if (engine.Containers.Count(c => c.IsRunning) != 3) throw new IOException("An image fell back to a native service.");
            if (engine.Containers.Select(c => c.Address).Distinct().Count() != 3) throw new IOException("Containers shared a loopback address.");
            var images = await engine.Store.ListAsync(cancellationToken);
            return new(true, $"Containers: PostgreSQL query=42 and TCP connected; Redis PING=PONG; nginx rendered in web preview; docker ps; {images.Count} verified images; {clock.Elapsed.TotalSeconds:0.0} s; {string.Join(", ", images.Select(i => i.DisplayName + "@" + i.ManifestDigest))}; {environment.Count} run variables", clock.Elapsed);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return new(false, "Containers: " + ex.Message + " | " + string.Join(" | ", manager.Statuses.SelectMany(s => manager.Logs(s.Name).TakeLast(5)).Select(l => l.Service + ": " + l.Text)), clock.Elapsed);
        }
        finally { await manager.StopAsync(cancellationToken: CancellationToken.None); }
    }
}
