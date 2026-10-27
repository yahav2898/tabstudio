using System.Net;
using System.Net.Sockets;
using TabStudio.Core.Compatibility;
using TabStudio.Core.Workspaces;
using TabStudio.Testing;

namespace TabStudio.Core.Tests;

public sealed class SqlServerDetectorTests
{
    [Theory]
    [InlineData("Server=.;Database=SimplCommerce;Trusted_Connection=True;TrustServerCertificate=true;MultipleActiveResultSets=true", "127.0.0.1", 1433, SqlServerTransport.Tcp, "SimplCommerce", true)]
    [InlineData("Server=tcp:db.example.com,14330;Initial Catalog=Shop;User ID=dev;Password=secret;Encrypt=True", "db.example.com", 14330, SqlServerTransport.Tcp, "Shop", false)]
    [InlineData("Data Source=localhost\\SQLEXPRESS;Initial Catalog=Shop;Integrated Security=SSPI", "127.0.0.1", 1433, SqlServerTransport.Tcp, "Shop", true)]
    [InlineData("Server=(localdb)\\mssqllocaldb;Database=aspnet-53bc9b9d;Trusted_Connection=True;MultipleActiveResultSets=true", "(localdb)\\mssqllocaldb", 0, SqlServerTransport.LocalDb, "aspnet-53bc9b9d", true)]
    [InlineData("Server=np:\\\\.\\pipe\\sql\\query;Database=Shop;User ID=sa;Password=x", "np:\\\\.\\pipe\\sql\\query", 0, SqlServerTransport.WindowsOnly, "Shop", false)]
    [InlineData("Address='db.internal, 1500';Database=\"Orders\";User ID=sa;Password='a;b'", "db.internal", 1500, SqlServerTransport.Tcp, "Orders", false)]
    public void Reads_where_a_SQL_Server_connection_string_points(string value, string host, int port, SqlServerTransport transport, string database, bool windows)
    {
        var server = SqlServerConnectionString.TryParse(value, projectUsesSqlServer: true);

        Assert.NotNull(server);
        Assert.Equal(host, server.Host);
        Assert.Equal(port, server.Port);
        Assert.Equal(transport, server.Transport);
        Assert.Equal(database, server.Database);
        Assert.Equal(windows, server.UsesWindowsAuthentication);
    }

    [Theory]
    [InlineData("Data Source=app.db")]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=/data/shop.sqlite")]
    [InlineData("Host=localhost;Database=shop;Username=app;Password=x")]
    [InlineData("Server=localhost;Port=5432;Database=shop;User Id=app;Password=x")]
    [InlineData("Server=localhost;Database=shop;Uid=root;Pwd=x")]
    [InlineData("localhost:6379,abortConnect=false")]
    [InlineData("")]
    public void Other_databases_connection_strings_are_not_SQL_Servers(string value) =>
        Assert.Null(SqlServerConnectionString.TryParse(value, projectUsesSqlServer: true));

    [Fact]
    public void An_ambiguous_server_and_database_string_is_SQL_Servers_only_with_a_SQL_Server_client()
    {
        const string Value = "Server=db;Database=shop;User=app;Password=x";

        Assert.NotNull(SqlServerConnectionString.TryParse(Value, projectUsesSqlServer: true));
        Assert.Null(SqlServerConnectionString.TryParse(Value, projectUsesSqlServer: false));
        Assert.NotNull(SqlServerConnectionString.TryParse("Server=db;Initial Catalog=shop", projectUsesSqlServer: false));
    }

    [Fact]
    public void Detects_SimplCommerces_SQL_Server_through_its_packages_and_appsettings()
    {
        using var temp = new TempDirectory();
        temp.Write("src/WebHost/appsettings.json", """
            {
              // Comments and trailing commas, as ASP.NET Core allows.
              "ConnectionStrings": {
                "DefaultConnection": "Server=.;Database=SimplCommerce;Trusted_Connection=True;TrustServerCertificate=true;MultipleActiveResultSets=true",
                "Redis": "localhost:6379",
              },
            }
            """);
        var module = Project(temp, "src/Modules/Core/Core.csproj", "Microsoft.EntityFrameworkCore.SqlServer");
        var host = Project(temp, "src/WebHost/WebHost.csproj", "MediatR") with { ProjectReferences = ["../Modules/Core/Core.csproj"] };

        var usage = SqlServerDetector.Detect(host, [host, module]);

        Assert.NotNull(usage);
        Assert.Contains("Microsoft.EntityFrameworkCore.SqlServer", usage.Evidence);
        var connection = Assert.Single(usage.Connections);
        Assert.Equal("DefaultConnection", connection.Name);
        Assert.Equal("ConnectionStrings__DefaultConnection", connection.EnvironmentVariable);
        Assert.Equal("127.0.0.1:1433", connection.Server.Endpoint);
        Assert.False(usage.CanSwitch);
    }

    [Fact]
    public void Knows_when_a_project_also_supports_PostgreSQL_or_SQLite_and_a_provider_setting()
    {
        using var temp = new TempDirectory();
        temp.Write("App/appsettings.Development.json", """
            { "DatabaseProvider": "SqlServer", "ConnectionStrings": { "Shop": "Server=(localdb)\\MSSQLLocalDB;Database=Shop;Trusted_Connection=True" } }
            """);
        var project = Project(temp, "App/App.csproj", "Microsoft.EntityFrameworkCore.SqlServer", "Npgsql.EntityFrameworkCore.PostgreSQL", "Microsoft.EntityFrameworkCore.Sqlite");

        var usage = SqlServerDetector.Detect(project, [project]);

        Assert.NotNull(usage);
        Assert.True(usage.SupportsPostgreSql);
        Assert.True(usage.SupportsSqlite);
        Assert.Equal(new KeyValuePair<string, string>("DatabaseProvider", "SqlServer"), usage.ProviderSetting);
        Assert.Equal(SqlServerTransport.LocalDb, Assert.Single(usage.Connections).Server.Transport);
    }

    [Fact]
    public void Projects_without_SQL_Server_are_not_reported()
    {
        using var temp = new TempDirectory();
        temp.Write("App/appsettings.json", """{ "ConnectionStrings": { "Db": "Host=localhost;Database=shop" } }""");
        var project = Project(temp, "App/App.csproj", "Npgsql.EntityFrameworkCore.PostgreSQL");

        Assert.Null(SqlServerDetector.Detect(project, [project]));
    }

    [Fact]
    public void The_message_names_the_engine_the_endpoint_and_the_choices()
    {
        var server = SqlServerConnectionString.TryParse("Server=.;Database=Shop;Trusted_Connection=True", projectUsesSqlServer: true)!;
        var connection = new SqlServerConnection("DefaultConnection", "Server=.;Database=Shop;Trusted_Connection=True", "/src/App/appsettings.json", server);
        var sqlServerOnly = new SqlServerUsage("Shop", ["Microsoft.EntityFrameworkCore.SqlServer"], [connection], SupportsPostgreSql: false, SupportsSqlite: false);

        var message = SqlServerDetector.Explain(sqlServerOnly, connection);

        Assert.Equal(
            "Shop uses SQL Server, which has no engine for ARM64 Linux, so it cannot run on this tablet, and nothing answers at 127.0.0.1:1433 "
            + "('DefaultConnection' in appsettings.json). Connect to a SQL Server on your network or in the cloud with a SQL login (Windows sign-in is not available here).",
            message);
        Assert.EndsWith(", or switch to PostgreSQL, which runs here in Services.", SqlServerDetector.Explain(sqlServerOnly with { SupportsPostgreSql = true }, connection), StringComparison.Ordinal);
        Assert.EndsWith(", or switch to SQLite, which runs here.", SqlServerDetector.Explain(sqlServerOnly with { SupportsSqlite = true }, connection), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_probe_finds_a_listening_server_and_nothing_where_none_listens()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var listening = SqlServerConnectionString.TryParse($"Server=tcp:127.0.0.1,{port};Database=x;User ID=sa;Password=x", projectUsesSqlServer: true)!;
        var localDb = SqlServerConnectionString.TryParse("Server=(localdb)\\MSSQLLocalDB;Database=x", projectUsesSqlServer: true)!;

        Assert.True(await SqlServerDetector.IsReachableAsync(listening, TimeSpan.FromSeconds(5)));
        listener.Stop();
        Assert.False(await SqlServerDetector.IsReachableAsync(listening, TimeSpan.FromSeconds(2)));
        Assert.False(await SqlServerDetector.IsReachableAsync(localDb, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void SqlClient_failures_are_recognized_in_the_exception_chain()
    {
        Assert.True(SqlServerDetector.IsSqlServerFailure(new InvalidOperationException("startup", new PlatformNotSupportedException("Microsoft.Data.SqlClient is not supported on this platform."))));
        Assert.True(SqlServerDetector.IsSqlServerFailure(new AggregateException(new PlatformNotSupportedException("System.Data.SqlClient is not supported on this platform."))));
        Assert.False(SqlServerDetector.IsSqlServerFailure(new PlatformNotSupportedException("Windows only")));
        Assert.False(SqlServerDetector.IsSqlServerFailure(null));
    }

    private static ProjectInfo Project(TempDirectory temp, string relativePath, params string[] packages)
    {
        var path = temp.Write(relativePath, "<Project Sdk=\"Microsoft.NET.Sdk.Web\" />");
        return new ProjectInfo
        {
            Name = Path.GetFileNameWithoutExtension(path),
            FilePath = path,
            Language = ProjectLanguage.CSharp,
            PackageReferences = [.. packages.Select(p => new PackageReferenceInfo(p, "8.0.0"))],
        };
    }
}
