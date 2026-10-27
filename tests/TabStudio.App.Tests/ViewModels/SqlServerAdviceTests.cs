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
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(project)!, "appsettings.json"), """
            { "ConnectionStrings": { "DefaultConnection": "Server=tcp:127.0.0.1,1;Database=Shop;Trusted_Connection=True;TrustServerCertificate=true" } }
            """);
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
            UiTestHelpers.SaveScreenshot(window, "compatibility-sql-server");
        }
        finally
        {
            window.Close();
        }

        // F5's Output says the same, once, before the app starts.
        var startup = workspace.Workspace!.Projects.Single(p => p.Name == "ShopWeb");
        var environment = await workspace.AddDatabaseEnvironmentAsync(startup, new Dictionary<string, string?>());
        Assert.Empty(environment);
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

        // The run uses the saved connection in place of appsettings, and the SQL Server message is not repeated.
        environment = await workspace.AddDatabaseEnvironmentAsync(startup, new Dictionary<string, string?>());
        Assert.Equal(Network, environment["ConnectionStrings__DefaultConnection"]);
        Assert.Contains("'DefaultConnection' comes from the Database Explorer connection 'ShopWeb: DefaultConnection'", workspace.Output.Text, StringComparison.Ordinal);
        Assert.Equal(1, Occurrences(workspace.Output.Text, "has no engine for ARM64 Linux"));

        // Use PostgreSQL: Compose gains a PostgreSQL service for Services to run, and the app its connection string.
        await workspace.SwitchDatabaseCommand.ExecuteAsync(row);
        var compose = await File.ReadAllTextAsync(Path.Combine(root, "compose.yaml"));
        Assert.Contains("image: postgres:17", compose, StringComparison.Ordinal);
        Assert.Contains("ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=shop;Username=app;Password=", compose, StringComparison.Ordinal);
        Assert.Contains(workspace.Services.Items, i => i.Name == "postgres");
        Assert.True(workspace.IsServicesSelected);
        Assert.Equal("Ready", workspace.Compatibility.Single(r => r.Project == "ShopWeb").Status);
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
