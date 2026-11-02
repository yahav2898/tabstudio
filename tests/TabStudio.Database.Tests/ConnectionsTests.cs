using TabStudio.Testing;

namespace TabStudio.Database.Tests;

public sealed class ConnectionsTests
{
    [Fact] public async Task Discovery_reads_nested_projects_configuration_and_secrets_without_leaking_in_labels()
    {
        using var temp = new TempDirectory();
        temp.Write("solution/src/App/App.csproj", "<Project><PropertyGroup><UserSecretsId>app-id</UserSecretsId></PropertyGroup></Project>");
        temp.Write("solution/src/App/appsettings.Development.json", """
            { // Configuration allows comments
              "ConnectionStrings": { "Local": "Data Source=local.db", "Remote": "Host=localhost;Database=app;Password=hidden" },
            }
            """);
        temp.Write("secrets/app-id/secrets.json", """{"ConnectionStrings:Production":"Server=host;Database=prod;Password=never-display"}""");
        temp.Write("solution/node_modules/dep/appsettings.json", """{"ConnectionStrings":{"Ignored":"Data Source=x"}}""");
        var found = await ConnectionDiscovery.FindAsync(temp.Resolve("solution"), temp.Resolve("secrets"));
        Assert.Equal(3, found.Count);
        Assert.All(found, c => { Assert.DoesNotContain("hidden", c.ToString(), StringComparison.Ordinal); Assert.DoesNotContain("never-display", c.ToString(), StringComparison.Ordinal); });
        Assert.Contains(temp.Resolve("solution/src/App/local.db"), found.Single(c => c.Name == "Local").Value, StringComparison.Ordinal);
        Assert.Equal(DatabaseProvider.PostgreSql, found.Single(c => c.Name == "Remote").Provider);
        Assert.Equal(DatabaseProvider.SqlServer, found.Single(c => c.Name == "Production").Provider);
    }
    [Theory]
    [InlineData("Server=127.0.0.1;Port=5432;Database=app;User Id=postgres;Password=p", DatabaseProvider.PostgreSql)]
    [InlineData("Server=localhost;Database=app;User Id=sa;Password=p;TrustServerCertificate=True", DatabaseProvider.SqlServer)]
    [InlineData("Server=localhost;Port=3306;Database=app;Uid=root;Pwd=p", null)]
    [InlineData("Data Source=app.db", DatabaseProvider.SQLite)]
    public void Detect_tells_npgsql_server_and_port_strings_from_sql_server(string value, DatabaseProvider? expected) =>
        Assert.Equal(expected, DatabaseConnections.Detect(value));
    [Fact] public async Task Saved_metadata_contains_no_connection_string_or_password_and_removal_forgets_secret()
    {
        using var temp = new TempDirectory();
        var memory = new MemoryDatabaseSecretStore();
        var store = new ConnectionStore(temp.Resolve("connections.json"), memory);
        var connection = new DatabaseConnection(Guid.NewGuid(), "Production", DatabaseProvider.PostgreSql, "Host=private-host;Password=do-not-write-me");
        await store.SaveAsync(connection);
        var text = await File.ReadAllTextAsync(temp.Resolve("connections.json"));
        Assert.DoesNotContain("private-host", text, StringComparison.Ordinal); Assert.DoesNotContain("do-not-write-me", text, StringComparison.Ordinal);
        Assert.Equal(connection, Assert.Single(await store.LoadAsync()));
        await store.RemoveAsync(connection.Id);
        Assert.Empty(await store.LoadAsync()); Assert.Null(await memory.GetAsync(connection.Id.ToString("N")));
    }
    [Theory]
    [InlineData(DatabaseProvider.SQLite, "a\"b", "\"a\"\"b\"")]
    [InlineData(DatabaseProvider.PostgreSql, "a\"b", "\"a\"\"b\"")]
    [InlineData(DatabaseProvider.SqlServer, "a]b", "[a]]b]")]
    public void Identifier_quoting_never_emits_an_unescaped_delimiter(DatabaseProvider provider, string name, string expected) => Assert.Equal(expected, DatabaseConnections.Quote(provider, name));

    [Fact] public void Completions_follow_aliases_and_do_not_crash_on_repeated_aliases()
    {
        var table = new DatabaseTable("public", "customers", false, [new("id", "integer", false, 1), new("email", "text", true)], [], []);
        var schema = new DatabaseSchema("test", ["public"], [table]);
        const string query = "SELECT * FROM customers c JOIN customers c ON c.";
        Assert.Contains(SqlCompletions.Get(schema, query, query.Length), c => c.Text == "email");
        Assert.Contains(SqlCompletions.Get(schema, "SEL", 3), c => c.Text == "SELECT");
        Assert.Empty(SqlCompletions.Get(schema, "SELECT 'no hints", 16));
        Assert.Empty(SqlCompletions.Get(schema, "-- no hints", 11));
        var hint = SqlCompletions.Get(schema, "SELECT c. FROM customers c", 9).Single(c => c.Text == "email");
        Assert.Equal("\"email\"", hint.InsertionText);
        var quoted = schema with { Tables = [table with { Name = "customer data" }] };
        Assert.Contains(SqlCompletions.Get(quoted, "SELECT c. FROM \"public\".\"customer data\" AS c", 9), c => c.Text == "email");
        Assert.Equal("[email]", SqlCompletions.Get(quoted, "SELECT c. FROM [public].[customer data] c", 9, DatabaseProvider.SqlServer).Single(c => c.Text == "email").InsertionText);
    }
}
