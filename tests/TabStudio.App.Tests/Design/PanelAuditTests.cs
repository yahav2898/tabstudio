using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using TabStudio.App.Services;
using TabStudio.App.Services.Toolchains;
using TabStudio.App.Tests.Theming;
using TabStudio.App.Tests.ViewModels;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.App.Views.Database;
using TabStudio.App.Views.Toolchains;
using TabStudio.Database;
using TabStudio.Toolchains.Tests.Fakes;

namespace TabStudio.App.Tests.Design;

/// <summary>
/// Every panel 0.15 and 0.16 added, in the real IDE on a landscape tablet, a portrait tablet and a phone, in both
/// themes, checked against the design spec by <see cref="PanelAudit"/>. The screenshots are those of
/// docs/design/audit-0.16.md, in <c>screenshots/audit/</c> of the IDE job.
/// </summary>
[Collection(AppearanceTestGroup.Name)]
public sealed class PanelAuditTests
{
    [AvaloniaFact]
    public async Task Database_Explorer_and_its_query_tab()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            await workspace.ShowDatabaseCommand.ExecuteAsync(null);
            var explorer = workspace.DatabaseExplorer;
            await PanelAudit.RunAsync(window, "audit-database-new-connection", () => Find<DatabaseExplorerView>(window), _ => OpenSideBar(workspace));

            var connection = new DatabaseConnection(Guid.NewGuid(), "Shop · SQLite", DatabaseProvider.SQLite, $"Data Source={Path.Combine(fixture.Paths.Root, "shop.db")};Pooling=False");
            await new SqlExecutor().ExecuteAsync(connection, """
                CREATE TABLE customers(id INTEGER PRIMARY KEY, name TEXT NOT NULL, city TEXT, orders INTEGER);
                CREATE INDEX customers_city ON customers(city);
                CREATE VIEW active_customers AS SELECT * FROM customers WHERE orders > 0;
                INSERT INTO customers VALUES(1,'Dana Cohen','Tel Aviv',12),(2,'Noam Levi','Haifa',8),(3,'Maya Bar','Jerusalem',23),(4,'Amit Shalev','Netanya',5);
                """);
            explorer.ConnectionName = connection.Name;
            explorer.ConnectionString = connection.ConnectionString;
            explorer.Provider = connection.Provider;
            await explorer.SaveConnectionCommand.ExecuteAsync(null);
            await explorer.ConnectCommand.ExecuteAsync(null);
            var database = Assert.Single(Assert.Single(explorer.Nodes).Children);
            var table = database.Children.SelectMany(s => s.Children).SelectMany(g => g.Children).Single(t => t.Table?.Name == "customers");
            table.IsExpanded = true;
            table.Children[0].IsExpanded = true;
            explorer.SelectedNode = table;
            await PanelAudit.RunAsync(window, "audit-database-explorer", () => Find<DatabaseExplorerView>(window), _ => OpenSideBar(workspace));

            explorer.NewQueryCommand.Execute(null);
            var document = workspace.ActiveDocument!;
            var query = Assert.IsType<TabStudio.App.ViewModels.Database.DatabaseQueryViewModel>(document.DatabaseQuery);
            document.Document.Text = "SELECT id, name, city, orders FROM customers ORDER BY orders DESC;\nSELECT count(*) AS total_customers FROM customers;";
            await query.ExecuteAsync(document.Document.Text);
            await UiTestHelpers.WaitUntilAsync(() => !query.IsBusy && query.Rows.Count > 0, because: "the query's results");
            workspace.IsSideBarVisible = false;
            await PanelAudit.RunAsync(window, "audit-database-query", () => Find<DatabaseQueryView>(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Services_with_a_Compose_file_and_without_one()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.ConsoleId, "ServiceApp");
        await fixture.Workspace.LoadAsync(solution);
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            workspace.SelectSideBarPanelCommand.Execute(SideBarPanel.Services);
            await PanelAudit.RunAsync(window, "audit-services-empty", () => Find<ServicesView>(window), _ => OpenSideBar(workspace));

            File.WriteAllText(Path.Combine(Path.GetDirectoryName(solution)!, "compose.yaml"), """
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
            await workspace.Services.ReloadCommand.ExecuteAsync(null);
            Assert.Equal(3, workspace.Services.Items.Count);
            await PanelAudit.RunAsync(window, "audit-services", () => Find<ServicesView>(window), _ => OpenSideBar(workspace));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Remote_Explorer_and_Publish_to_server()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WebId, "HelloWeb");
        await fixture.Workspace.LoadAsync(solution);
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            var remote = workspace.RemoteExplorer;
            remote.Alias = "Development server";
            remote.HostName = "dev.example.com";
            remote.User = "deploy";
            remote.SaveHostCommand.Execute(null);
            remote.RemoteFolder = "/srv/projects/HelloWeb";
            workspace.ShowRemoteExplorerCommand.Execute(null);
            await PanelAudit.RunAsync(window, "audit-remote-explorer", () => Find<RemoteExplorerView>(window), _ => OpenSideBar(workspace));

            var publish = workspace.PublishToServerCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "Publish to server's first question");
            await PanelAudit.RunAsync(window, "audit-publish-to-server", () => PromptDialog(window));
            workspace.Prompt.CancelCommand.Execute(null);
            await publish;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Microchip_PIC_configuration_and_the_Serial_Monitor()
    {
        using var fixture = new WorkspaceFixture();
        var folder = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.Pic18Id, "Blink");
        await fixture.Workspace.LoadAsync(folder);
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            workspace.ShowSerialMonitorCommand.Execute(null);
            await PanelAudit.RunAsync(window, "audit-serial-monitor", () => Find<SerialMonitorView>(window));

            Assert.True(workspace.IsPicProject, "The Blink template is an MPLAB X project.");
            var choose = workspace.SelectPicConfigurationCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the MPLAB configuration question");
            await PanelAudit.RunAsync(window, "audit-pic-configuration", () => PromptDialog(window));
            workspace.Prompt.CancelCommand.Execute(null);
            await choose;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Agent_settings_and_the_local_AI_model_manager()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            workspace.ShowAgentCommand.Execute(null);
            var agent = workspace.Agent;
            agent.OpenSettingsCommand.Execute(null);
            await PanelAudit.RunAsync(window, "audit-agent-settings", () => Find<AgentPanelView>(window), _ => OpenSideBar(workspace));

            agent.UseLocalMode = true;
            await PanelAudit.RunAsync(window, "audit-local-ai", () => Find<AgentPanelView>(window), _ => OpenSideBar(workspace));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_time_travel_timeline()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            workspace.ActiveDocument!.Document.Text = RecordingTests.Loop;
            await workspace.Watch.AddAsync("total");
            await workspace.StartWithRecordingCommand.ExecuteAsync(null);
            await DebuggingTests.WaitForExitAsync(workspace);
            await workspace.StepBackCommand.ExecuteAsync(null);
            await workspace.JumpToLastWriteCommand.ExecuteAsync("total");
            Assert.True(workspace.IsHistorical);
            await PanelAudit.RunAsync(window, "audit-timeline", () => Shell(window).FindControl<Border>("RecordingTimeline")!);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Port_to_Avalonia_summary()
    {
        using var fixture = new WorkspaceFixture();
        var input = Path.Combine(AppContext.BaseDirectory, "PortingSamples", "WpfContacts", "Input");
        var sourceFolder = Directory.GetDirectories(input).Single();
        var sourceName = Path.GetFileName(sourceFolder);
        var root = Path.Combine(fixture.Paths.Projects, "Porting");
        CopyTree(sourceFolder, Path.Combine(root, sourceName));
        var solution = Path.Combine(root, "Porting.slnx");
        File.WriteAllText(solution, $"<Solution><Project Path=\"{sourceName}/{sourceName}.csproj\" /></Solution>");
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        var summary = await workspace.PortProjectAsync(workspace.Workspace!.Projects.Single(), sourceName + ".Avalonia");
        var window = Show(workspace);
        try
        {
            workspace.ActiveDocument = workspace.Documents.Single(d => d.PortSummary == summary);
            workspace.IsSideBarVisible = false;
            await PanelAudit.RunAsync(window, "audit-port-summary", () => Find<PortSummaryView>(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task Logcat_and_the_iOS_workflow_question()
    {
        using var fixture = new WorkspaceFixture();
        var folder = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.CapacitorId, "PocketApp");
        await fixture.Workspace.LoadAsync(folder);
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            workspace.Logcat.WriteLine("Logcat for com.example.pocketapp (debug app process)", OutputKind.Info);
            workspace.Logcat.WriteLine("10-26 14:02:11.482  4120  4120 I Capacitor: Starting BridgeActivity");
            workspace.Logcat.WriteLine("10-26 14:02:11.913  4120  4141 D Capacitor: Loading app at https://localhost");
            workspace.Logcat.WriteLine("10-26 14:02:12.204  4120  4120 W Capacitor/Console: File: https://localhost/ - Line 1 - Msg: Service worker is not available", OutputKind.Failure);
            workspace.SelectPanelCommand.Execute(BottomPanel.Logcat);
            await PanelAudit.RunAsync(window, "audit-logcat", () => window.GetVisualDescendants().OfType<OutputView>().Single(v => ReferenceEquals(v.DataContext, workspace.Logcat)));

            await workspace.BuildForIosCommand.ExecuteAsync(null);
            var again = workspace.BuildForIosCommand.ExecuteAsync(null);
            await UiTestHelpers.WaitUntilAsync(() => workspace.Prompt.IsOpen, because: "the question about the existing workflow");
            await PanelAudit.RunAsync(window, "audit-ios-replace-workflow", () => PromptDialog(window));
            workspace.Prompt.CancelCommand.Execute(null);
            await again;
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task The_Toolchain_Store_with_the_Android_Flutter_and_Microchip_entries()
    {
        using var device = FakeDevice.WithCatalogToolchains("python", "android", "flutter", "xc8", "xc32");
        using var fixture = new WorkspaceFixture(toolchains: new ToolchainServices(device.Manager, device.Platform, device.Environment));
        await fixture.OpenSampleAsync();
        var workspace = fixture.Workspace;
        var window = Show(workspace);
        try
        {
            workspace.ShowToolchainStoreCommand.Execute(null);
            Assert.True(workspace.ToolchainStore.IsOpen);
            await UiTestHelpers.WaitUntilAsync(() => !workspace.ToolchainStore.IsChecking, because: "the Store's statuses");
            await PanelAudit.RunAsync(window, "audit-toolchain-store", () => Find<ToolchainOverlaysView>(window));
        }
        finally
        {
            window.Close();
        }
    }

    private static Window Show(WorkspaceViewModel workspace)
    {
        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        return window;
    }

    private static Task OpenSideBar(WorkspaceViewModel workspace)
    {
        workspace.IsSideBarVisible = true;
        return Task.CompletedTask;
    }

    private static WorkspaceView Shell(Window window) => window.GetVisualDescendants().OfType<WorkspaceView>().Single();

    private static T Find<T>(Window window)
        where T : Control =>
        window.GetVisualDescendants().OfType<T>().First(control => control.IsEffectivelyVisible);

    /// <summary>The in-app dialog that asks a question: the box around its title, message and buttons.</summary>
    private static Border PromptDialog(Window window) =>
        Shell(window).FindControl<TextBox>("PromptBox")!.GetVisualAncestors().OfType<Border>().First();

    private static void CopyTree(string source, string destination)
    {
        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
