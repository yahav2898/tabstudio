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
    [InlineData("Password=db;Host=elsewhere;Port=5432", "Password=db;Host=elsewhere;Port=5432")]
    public void Only_rewrites_host_fields_and_their_ports(string input, string expected) => Assert.Equal(expected, ConnectionEnvironment.Rewrite(input, Endpoints));

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
