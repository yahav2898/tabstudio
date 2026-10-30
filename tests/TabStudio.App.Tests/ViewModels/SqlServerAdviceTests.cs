using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;
using TabStudio.App.Views;
using TabStudio.Database;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// A web app whose appsettings point at a SQL Server that does not answer: the Compatibility Doctor and the run's Output
/// say once that SQL Server cannot run on the tablet, and offer a SQL Server on the network (saved in the Database
/// Explorer, then used by the run) or PostgreSQL in Services, which the project also supports.
/// </summary>
public sealed class SqlServerAdviceTests
{
    private const string Configured = "Server=tcp:127.0.0.1,1;Database=Shop;Trusted_Connection=True;TrustServerCertificate=true";

    [AvaloniaFact]
    public async Task An_unreachable_SQL_Server_gets_one_clear_message_with_the_network_and_PostgreSQL_choices()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.WebId, "ShopWeb");
        var root = Path.GetDirectoryName(solution)!;
        var project = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories).Single();
        var csproj = await File.ReadAllTextAsync(project);
        await File.WriteAllTextAsync(project, csproj.Replace(
            "</Project>",
            """
              <ItemGroup>
                <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="9.0.0" />
                <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />
              </ItemGroup>
            </Project>
            """,
            StringComparison.Ordinal));

        // Port 1 never has a SQL Server, so the probe finds nothing, as on a tablet where "Server=." has none.
        var directory = Path.GetDirectoryName(project)!;
        await File.WriteAllTextAsync(Path.Combine(directory, "appsettings.json"), $$"""
            { "ConnectionStrings": { "DefaultConnection": "{{Configured}}" } }
            """);
        var program = Path.Combine(directory, "Program.cs");
        var code = await File.ReadAllTextAsync(program);
        await File.WriteAllTextAsync(program, code.Replace(
            "app.Run();",
            "app.MapGet(\"/connection\", (IConfiguration configuration) => configuration.GetConnectionString(\"DefaultConnection\") ?? \"none\");\napp.Run();",
            StringComparison.Ordinal));
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);
        await workspace.DatabaseCheck;

        var row = workspace.Compatibility.Single(r => r.Project == "ShopWeb");
        Assert.Equal("Needs a database", row.Status);
        Assert.Equal(
            "ShopWeb uses SQL Server, which has no engine for ARM64 Linux, so it cannot run on this tablet, and nothing answers at 127.0.0.1:1 "
            + "('DefaultConnection' in appsettings.json). Connect to a SQL Server on your network or in the cloud with a SQL login "
            + "(Windows sign-in is not available here), or switch to PostgreSQL, which runs here in Services.",
            row.Explanation);
        Assert.True(row.CanConnectSqlServer);
        Assert.Equal("Use PostgreSQL", row.SwitchDatabaseText);

        var window = new Window { Width = 1280, Height = 800, Content = new WorkspaceView { DataContext = workspace } };
        window.Show();
        try
        {
            workspace.SelectPanelCommand.Execute(BottomPanel.Compatibility);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            UiTestHelpers.SaveScreenshot(window, "compatibility-sql-server");

            // The project's name sits at the top of its row, beside the start of the explanation, not centered
            // against all of it below the panel's fold.
            var cell = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<TextBlock>()
                .Single(t => t.Name == "ProjectCell" && t.Text == "ShopWeb");
            Assert.True(cell.Bounds.Y < 4, $"The project's name is {cell.Bounds.Y} pixels down its row.");
        }
        finally
        {
            window.Close();
        }

        // F5's Output says the same, once, and the app still starts with its appsettings value.
        Assert.Equal(Configured, await RunAndReadConnectionAsync(workspace));
        Assert.Equal(1, Occurrences(workspace.Output.Text, "has no engine for ARM64 Linux"));
        Assert.Contains("Both choices are in the Compatibility Doctor", workspace.Output.Text, StringComparison.Ordinal);

        // Connect to SQL Server: the Database Explorer's form, filled in from appsettings with a SQL login to complete.
        await workspace.ConnectSqlServerCommand.ExecuteAsync(row);
        var explorer = workspace.DatabaseExplorer;
        Assert.True(workspace.IsDatabaseSelected);
        Assert.True(explorer.IsAddingConnection);
        Assert.Equal("ShopWeb: DefaultConnection", explorer.ConnectionName);
        Assert.Equal(DatabaseProvider.SqlServer, explorer.Provider);
        Assert.Equal("Server=tcp:127.0.0.1,1;Database=Shop;TrustServerCertificate=true;User ID=;Password=", explorer.ConnectionString);

        const string Network = "Server=tcp:sql.example.com,1433;Database=Shop;User ID=dev;Password=secret;Encrypt=True";
        explorer.ConnectionString = Network;
        await explorer.SaveConnectionCommand.ExecuteAsync(null);
        Assert.Contains(await explorer.LoadSavedConnectionsAsync(), c => c.Name == "ShopWeb: DefaultConnection" && c.ConnectionString == Network);

        // The next run uses the saved connection in place of appsettings, and has nothing to warn about.
        Assert.Equal(Network, await RunAndReadConnectionAsync(workspace));
        Assert.Contains("'DefaultConnection' comes from the Database Explorer connection 'ShopWeb: DefaultConnection'", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(0, Occurrences(workspace.Output.Text, "has no engine for ARM64 Linux"));
        Assert.Null(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection"));

        // Use PostgreSQL: Compose gains a PostgreSQL service for Services to run, and the app its connection string.
        await workspace.SwitchDatabaseCommand.ExecuteAsync(row);
        var compose = await File.ReadAllTextAsync(Path.Combine(root, "compose.yaml"));
        Assert.Contains("image: postgres:17", compose, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=shop;Username=app;Password=", compose, StringComparison.Ordinal);
        Assert.Contains(workspace.Services.Items, i => i.Name == "postgres");
        Assert.True(workspace.IsServicesSelected);
        Assert.Equal("Ready", workspace.Compatibility.Single(r => r.Project == "ShopWeb").Status);
    }

    /// <summary>F5, then the app's own view of its connection string, then Stop.</summary>
    private static async Task<string> RunAndReadConnectionAsync(WorkspaceViewModel workspace)
    {
        var run = workspace.RunCommand.ExecuteAsync(null);
        try
        {
            await UiTestHelpers.WaitUntilAsync(() => workspace.WebPreview.IsRunning || run.IsCompleted, TimeSpan.FromMinutes(4), "the web app to start");
            Assert.True(workspace.WebPreview.IsRunning, workspace.Output.Text);
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            return await http.GetStringAsync(new Uri(workspace.WebPreview.AppAddress!, "connection"));
        }
        finally
        {
            if (workspace.StopCommand.CanExecute(null))
            {
                workspace.StopCommand.Execute(null);
            }

            await run.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
