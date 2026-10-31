using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Toolchains.Processes;
using TabStudio.Toolchains.Runtimes;

namespace TabStudio.App.Tests.BackingServices;

public sealed class ContainerRepositoryTests
{
    [AvaloniaFact]
    public async Task Pinned_realworld_repository_fails_without_its_database_then_F5_serves_its_first_route_from_compose()
    {
        var folder = Environment.GetEnvironmentVariable("TABSTUDIO_CONTAINER_TRIAL_REPO");
        if (string.IsNullOrEmpty(folder)) return;
        var root = FindRoot();
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "build/ci/trial-sweep.json")))!;
        var repository = manifest["repositories"]![0]!;
        File.Copy(Path.Combine(root, repository["compose"]!.GetValue<string>()), Path.Combine(folder, "compose.yaml"), overwrite: true);
        // A fixture Compose file is intentional: this pinned upstream predates one.
        File.WriteAllText(Path.Combine(folder, ".env"), "DATABASE_URL=postgresql://tablet:development@db:5432/realworld?schema=public\nJWT_SECRET=container-trial-only\n");
        using var fixture = new WorkspaceFixture();
        var workspace = fixture.Workspace;
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        Task? run = null;
        try
        {
            await workspace.LoadAsync(folder);
            await UiTestHelpers.WaitUntilAsync(() => workspace.State != IdeState.Busy && workspace.ProjectSetupTask.IsCompleted && !workspace.IsSettingUpProjects,
                TimeSpan.FromMinutes(10), "the real repository's npm and Prisma setup");
            var baseline = await CommandRunner.RunAsync(new HostRuntime(), new()
            {
                Program = "node", WorkingDirectory = folder,
                Arguments = ["-e", "const {PrismaClient}=require('@prisma/client');const p=new PrismaClient();p.tag.findMany().then(()=>process.exit(0)).catch(e=>{console.error(JSON.stringify({name:e.name,code:e.code,errorCode:e.errorCode,message:e.message}));process.exit(1)})"],
                Environment = new Dictionary<string, string?> { ["DATABASE_URL"] = "postgresql://tablet:development@127.0.0.1:1/realworld?connect_timeout=2" },
            }, null, CancellationToken.None);
            Assert.False(baseline.Succeeded);
            Assert.Contains("P1001", baseline.Output, StringComparison.Ordinal);
            run = workspace.RunCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(() => workspace.ServerNotice is not null || run.IsCompleted, TimeSpan.FromMinutes(6), "F5 to start the Compose image, migrate and serve");
            Assert.True(workspace.ServerNotice is not null, workspace.Output.Text);
            workspace.ServerNotice!.OpenPreview.Execute(null);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            var route = new Uri(workspace.WebPreview.AppAddress!, repository["route"]!.GetValue<string>());
            using var response = await http.GetAsync(route);
            response.EnsureSuccessStatusCode();
            Assert.Contains(repository["expect"]!.GetValue<string>(), await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            Assert.Single(workspace.Containers.Engine.Containers.Where(c => c.IsRunning));
            Assert.Equal("db", Assert.Single(workspace.Services.LastNeeded));
            UiTestHelpers.SaveScreenshot(window, "62-containers-realworld-first-route");
            Directory.CreateDirectory(Path.Combine(root, "ci-out"));
            File.WriteAllText(Path.Combine(root, "ci-out/container-repository.txt"), $"{repository["url"]}@{repository["sha"]}\nBaseline: P1001 (database unavailable)\nCompose: real OCI postgres:17-alpine\nF5 route: {route.AbsolutePath} HTTP {(int)response.StatusCode}\n" + workspace.Output.Text);
        }
        finally
        {
            workspace.StopCommand.Execute(null);
            if (run is not null) await run.WaitAsync(TimeSpan.FromSeconds(30));
            if (workspace.Services.Manager is { } manager) await manager.StopAsync();
            window.Close();
        }
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TabStudio.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
