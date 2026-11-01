using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Services.Tests;

public sealed class ConnectionTests
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<ServicePort>> Endpoints = new Dictionary<string, IReadOnlyList<ServicePort>> { ["db"] = [new(5432, 15432)], ["cache"] = [new(6379, 16379)] };
    [Theory]
    [InlineData("Host=db;Port=5432;Password=db;Database=app", "Host=127.0.0.1;Port=15432;Password=db;Database=app")]
    [InlineData("Server=db;Database=test", "Server=127.0.0.1;Database=test;Port=15432")]
    [InlineData("postgres://owner:db@db:5432/app", "postgres://owner:db@127.0.0.1:15432/app")]
    [InlineData("cache:6379,password=db", "127.0.0.1:16379,password=db")]
    [InlineData("db.example.com", "db.example.com")]
    [InlineData("Password=\";Host=db;Port=5432;\";Host=elsewhere;Port=5432", "Password=\";Host=db;Port=5432;\";Host=elsewhere;Port=5432")]
    [InlineData("Password=db;Host=elsewhere;Port=5432", "Password=db;Host=elsewhere;Port=5432")]
    public void Only_rewrites_host_fields_and_their_ports(string input, string expected) => Assert.Equal(expected, ConnectionEnvironment.Rewrite(input, Endpoints));

    [Theory]
    [InlineData("POSTGRES_USER", "db", "db")]
    [InlineData("POSTGRES_PASSWORD", "db", "db")]
    [InlineData("POSTGRES_DB", "db", "db")]
    [InlineData("POSTGRES_SERVER", "db", "127.0.0.1")]
    [InlineData("DB_HOST", "db", "127.0.0.1")]
    [InlineData("Redis:Configuration", "cache", "127.0.0.1")]
    [InlineData("REDIS_PASSWORD", "cache", "cache")]
    [InlineData("KAFKA_BROKERS", "cache:6379", "127.0.0.1:16379")]
    [InlineData(null, "db", "db")]
    public void A_bare_service_name_is_a_host_only_in_a_host_setting(string? key, string value, string expected) =>
        // A service named postgres with POSTGRES_USER, POSTGRES_PASSWORD and POSTGRES_DB set to postgres, the usual
        // tutorial layout, had all three turned into 127.0.0.1, in the app and in the database server itself.
        Assert.Equal(expected, ConnectionEnvironment.Rewrite(value, Endpoints, key));

    [Fact]
    public void A_native_database_keeps_credentials_that_share_its_service_name()
    {
        using var temp = new TempDirectory();
        var service = new ComposeService("postgres", "postgres:17-alpine")
        {
            Environment = new Dictionary<string, string?> { ["POSTGRES_USER"] = "postgres", ["POSTGRES_PASSWORD"] = "postgres", ["POSTGRES_DB"] = "postgres" },
        };
        var endpoints = new Dictionary<string, IReadOnlyList<ServicePort>> { ["postgres"] = [new(5432, 15432)] };
        var plan = Native.NativeServicePlan.Create(service, Native.ServiceCatalog.Map(service).Mapping!, new Native.ServiceStorage(temp.Path, temp.Path), [new(5432, 15432)],
            value => ConnectionEnvironment.Rewrite(value, endpoints), (key, value) => ConnectionEnvironment.Rewrite(value, endpoints, key));

        Assert.Equal("postgres", plan.Start.Environment["POSTGRES_USER"]);
        Assert.Equal("postgres", plan.Start.Environment["POSTGRES_PASSWORD"]);
        Assert.Equal("postgres", plan.Start.Environment["POSTGRES_DB"]);
    }

    [Fact]
    public void Compose_application_environment_wins_and_appsettings_are_overridden_without_editing_files()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "appsettings.json");
        const string content = """{"ConnectionStrings":{"Default":"Host=db;Port=5432;Database=app"},"Redis":"cache:6379"}""";
        File.WriteAllText(path, content);
        var app = new ComposeService("api", "custom") { BuildContext = temp.Path, Environment = new Dictionary<string, string?> { ["ConnectionStrings__Default"] = "Host=db;Port=5432;Database=compose" } };
        var project = new ComposeProject(temp.Path, [], new Dictionary<string, ComposeService> { ["api"] = app, ["db"] = new("db", "postgres:18"), ["cache"] = new("cache", "redis:7") });
        var env = ConnectionEnvironment.ForProject(project, temp.Path, Endpoints);
        Assert.Equal("Host=127.0.0.1;Port=15432;Database=compose", env["ConnectionStrings__Default"]);
        Assert.Equal("127.0.0.1:16379", env["Redis"]);
        Assert.Equal("127.0.0.1", env["DB_HOST"]);
        Assert.Equal(content, File.ReadAllText(path));
    }
}
