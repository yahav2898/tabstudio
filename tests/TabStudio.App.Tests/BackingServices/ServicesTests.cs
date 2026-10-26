using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using TabStudio.App.Platform;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Services.Tests;
using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.App.Tests.BackingServices;

[Xunit.Sdk.XunitTestCaseDiscoverer("Avalonia.Headless.XUnit.AvaloniaUIFactDiscoverer", "Avalonia.Headless.XUnit")]
public sealed class AlpineAvaloniaFactAttribute : FactAttribute
{
    public AlpineAvaloniaFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("TABSTUDIO_SERVICE_ROOTFS"))) Skip = "Native Alpine service harness is not configured.";
    }
}

public sealed class ServicesTests
{
    [AvaloniaFact]
    public async Task Services_window_lists_mappings_and_unsupported_images_with_logs_and_controls()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var solution = new ProjectTemplates(paths).Create(ProjectTemplates.ConsoleId, "ServiceApp");
        var root = Path.GetDirectoryName(solution)!;
        File.WriteAllText(Path.Combine(root, "compose.yaml"), """
            services:
              db:
                image: postgres:18
                environment: {POSTGRES_USER: app, POSTGRES_PASSWORD: development, POSTGRES_DB: app}
                ports: ['15432:5432']
              cache:
                image: redis:7
              search:
                image: example/custom-search:latest
            """);
        var compiler = new CompilerServices(paths);
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, false));
        using var workspace = new WorkspaceViewModel(compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(paths), paths);
        await workspace.LoadAsync(solution);
        workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Services);
        Assert.Equal(3, workspace.Services.Items.Count);
        var unsupported = workspace.Services.Items.Single(i => i.Name == "search");
        Assert.True(unsupported.IsUnsupported);
        Assert.Contains("remote host", unsupported.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(workspace.Activity.Items, i => i.Title == "Services");
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => window.IsVisible);
            UiTestHelpers.SaveScreenshot(window, "60-services");
            Assert.True(workspace.IsServicesSelected);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task Services_self_test_restores_the_workspace_needed_by_later_steps()
    {
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        using var provider = AppServices.Create(paths);
        var main = provider.GetRequiredService<MainViewModel>();
        var templates = new ProjectTemplates(paths);
        var python = templates.Create(ProjectTemplates.PythonId, "BeforeServices");
        await main.OpenWorkspaceAsync(python);
        var directory = main.Workspace!.Workspace!.RootDirectory;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        var result = await SelfTest.RunServicesAsync(main, templates, timeout.Token);
        Assert.True(result.Success, result.Message);
        Assert.Equal(directory, main.Workspace!.Workspace!.RootDirectory);
        Assert.Contains(main.Workspace.ToolchainProjects, p => p.Toolchain.Id == "python");
    }

    [AlpineAvaloniaFact]
    public async Task F5_of_an_AspNet_template_connects_with_Npgsql_using_rewritten_compose_environment()
    {
        using var alpine = new AlpineServiceFixture();
        using var temp = new TempDirectory();
        var paths = new AppPaths(temp.Path);
        var solution = new ProjectTemplates(paths).Create(ProjectTemplates.WebId, "DatabaseWeb");
        var root = Path.GetDirectoryName(solution)!;
        var project = System.IO.Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Single();
        var projectDirectory = Path.GetDirectoryName(project)!;
        var csproj = File.ReadAllText(project).Replace("</Project>", "<ItemGroup><PackageReference Include=\"Npgsql\" Version=\"10.0.0\" /></ItemGroup></Project>", StringComparison.Ordinal);
        File.WriteAllText(project, csproj);
        File.WriteAllText(Path.Combine(projectDirectory, "Program.cs"), """
            using Npgsql;
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/proof", async () => {
                await using var db = new NpgsqlConnection(builder.Configuration["ConnectionStrings:App"]);
                await db.OpenAsync();
                await using var command = new NpgsqlCommand("SELECT 'tablet-postgres'::text", db);
                return (string)(await command.ExecuteScalarAsync())!;
            });
            app.Run();
            """);
        File.WriteAllText(Path.Combine(root, "compose.yaml"), """
            services:
              web:
                build: .
                depends_on: [db]
                environment:
                  ConnectionStrings__App: Host=db;Port=5432;Username=app;Password=test-password;Database=app
              db:
                image: postgres:18
                environment: {POSTGRES_USER: app, POSTGRES_PASSWORD: test-password, POSTGRES_DB: app}
                ports: ['0:5432']
            """);
        using var toolchains = new ToolchainServices(new ToolchainManager(ToolchainCatalog.Default, new HostToolchainInstaller(alpine.Runtime)), ToolchainPlatform.Desktop);
        var compiler = new CompilerServices(paths);
        var device = new DefaultDeviceStatus(new DeviceInfo(8L * 1024 * 1024 * 1024, 8, false));
        using var workspace = new WorkspaceViewModel(compiler, device, DeviceProfile.For(device.Device), new AvaloniaUiDispatcher(), new SessionStore(paths), paths, toolchains: toolchains);
        await workspace.LoadAsync(solution);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, timeout: TimeSpan.FromMinutes(3), because: "native PostgreSQL and the web app");
            Assert.True(workspace.WebPreview.IsRunning, workspace.Services.Error + "\n" + workspace.Output.Text);
            using var http = new HttpClient();
            var proof = await http.GetStringAsync(new Uri(workspace.WebPreview.AppAddress!, "/proof"), timeout.Token);
            Assert.Equal("tablet-postgres", proof);
            workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Services);
            workspace.Services.SelectedItem = workspace.Services.Items.Single(i => i.Name == "db");
            var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
            window.Show();
            try { UiTestHelpers.SaveScreenshot(window, "61-services-postgres-running"); }
            finally { window.Close(); }
        }
        finally
        {
            if (workspace.StopCommand.CanExecute(null)) workspace.StopCommand.Execute(null);
            await run.WaitAsync(timeout.Token);
            if (workspace.Services.Manager is { } manager) await manager.StopAsync();
        }
        Assert.Null(Environment.GetEnvironmentVariable("ConnectionStrings__App"));
    }
}
