using Avalonia.Headless.XUnit;
using TabStudio.App.Services;
using TabStudio.App.ViewModels;

namespace TabStudio.App.Tests.ViewModels;

/// <summary>
/// Packages whose <c>lib/</c> assembly is only a "not supported on this platform" stub, or that need a native library,
/// in a console app built and run in the IDE against the real nuget.org: Microsoft.Data.SqlClient must run with its
/// <c>runtimes/unix/lib/</c> assembly, and Microsoft.Data.Sqlite must find <c>e_sqlite3</c> in
/// <c>runtimes/{rid}/native/</c>.
/// </summary>
public sealed class NativePackageTests
{
    [AvaloniaFact]
    public async Task A_console_app_uses_SqlClients_unix_assembly_and_SQLites_native_library()
    {
        using var fixture = new WorkspaceFixture();
        var solution = new ProjectTemplates(fixture.Paths).Create(ProjectTemplates.ConsoleId, "DataApp");
        var project = Directory.EnumerateFiles(Path.GetDirectoryName(solution)!, "*.csproj", SearchOption.AllDirectories).Single();
        var csproj = await File.ReadAllTextAsync(project);
        await File.WriteAllTextAsync(project, csproj.Replace(
            "</Project>",
            """
              <ItemGroup>
                <PackageReference Include="Microsoft.Data.SqlClient" Version="6.0.2" />
                <PackageReference Include="Microsoft.Data.Sqlite" Version="10.0.0" />
              </ItemGroup>
            </Project>
            """,
            StringComparison.Ordinal));
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(project)!, "Program.cs"), """
            using Microsoft.Data.SqlClient;
            using Microsoft.Data.Sqlite;

            var server = new SqlConnectionStringBuilder("Server=tcp:db.example.com,1433;Initial Catalog=Shop;User ID=dev;Password=secret;Encrypt=True");
            Console.WriteLine($"SqlClient: {server.DataSource} / {server.InitialCatalog}");
            Console.WriteLine($"SqlClient from: {typeof(SqlConnection).Assembly.Location.Replace('\\', '/')}");

            using var connection = new SqliteConnection("Data Source=:memory:");
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "select 40 + 2";
            Console.WriteLine($"SQLite says {command.ExecuteScalar()}");
            """);
        var workspace = fixture.Workspace;
        await workspace.LoadAsync(solution);

        await workspace.RunCommand.ExecuteAsync(null);
        var output = workspace.Output.Text;

        Assert.Contains("========== Build: 1 succeeded, 0 failed ==========", output, StringComparison.Ordinal);
        Assert.DoesNotContain("PlatformNotSupportedException", output, StringComparison.Ordinal);
        Assert.Contains("SqlClient: tcp:db.example.com,1433 / Shop", output, StringComparison.Ordinal);
        Assert.Contains("/runtimes/unix/lib/", output, StringComparison.Ordinal);
        Assert.Contains("SQLite says 42", output, StringComparison.Ordinal);
        Assert.Contains("has exited with code 0", output, StringComparison.Ordinal);
        Assert.Equal(IdeState.Ready, workspace.State);
    }
}
