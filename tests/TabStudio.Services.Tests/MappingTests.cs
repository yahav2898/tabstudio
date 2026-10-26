using TabStudio.Services.Compose;
using TabStudio.Services.Native;
using TabStudio.Toolchains.Alpine;
using TabStudio.Testing;

namespace TabStudio.Services.Tests;

public sealed class MappingTests
{
    [Theory]
    [InlineData("postgres:18-alpine", NativeServiceKind.Postgres, "postgresql18")]
    [InlineData("docker.io/library/redis:7", NativeServiceKind.Redis, "redis")]
    [InlineData("valkey/valkey:8", NativeServiceKind.Valkey, "valkey")]
    [InlineData("mysql:8", NativeServiceKind.MariaDb, "mariadb")]
    [InlineData("mariadb:11", NativeServiceKind.MariaDb, "mariadb")]
    [InlineData("mongo:7", NativeServiceKind.MongoDb, "mongodb")]
    [InlineData("rabbitmq:4-management", NativeServiceKind.RabbitMq, "rabbitmq-server")]
    [InlineData("nginx:alpine", NativeServiceKind.Nginx, "nginx")]
    [InlineData("axllent/mailpit:latest", NativeServiceKind.Mailpit, "mailpit")]
    [InlineData("minio/minio:latest", NativeServiceKind.Minio, "minio")]
    [InlineData("quay.io/minio/minio:latest", NativeServiceKind.Minio, "minio")]
    public void Maps_only_known_upstream_images(string image, NativeServiceKind kind, string package)
    {
        var result = ServiceCatalog.Map(new("service", image));
        Assert.True(result.Supported);
        Assert.Equal(kind, result.Mapping!.Kind);
        Assert.Contains(package, result.Mapping.Packages);
    }
    [Theory]
    [InlineData("my-company/postgres:18")]
    [InlineData("registry.example/redis:7")]
    [InlineData("elasticsearch:9")]
    [InlineData("postgres@sha256:1234")]
    public void Unknown_images_and_digests_offer_remote_host(string image)
    {
        var mapping = ServiceCatalog.Map(new("custom", image));
        Assert.False(mapping.Supported);
        Assert.Contains("remote host", mapping.UnsupportedReason, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public void Missing_mongodb_package_is_unsupported_instead_of_downloading_an_arbitrary_binary()
    {
        Assert.False(ServiceCatalog.Map(new("db", "mongo:7"), new ApkIndex([])).Supported);
    }
    [Fact]
    public void Postgres_major_is_preserved_and_missing_versions_are_not_silently_replaced()
    {
        var index = new ApkIndex([new() { Name = "postgresql18", Version = "18.3-r0" }, new() { Name = "postgresql18-client", Version = "18.3-r0" }]);
        Assert.Equal("18.3-r0", ServiceCatalog.Map(new("db", "postgres:18"), index).Mapping!.Version);
        Assert.False(ServiceCatalog.Map(new("db", "postgres:16"), index).Supported);
    }
    [Fact]
    public void Redis_can_use_the_packaged_valkey_with_an_explicit_explanation()
    {
        var index = new ApkIndex([new() { Name = "valkey", Version = "8.1-r0" }]);
        var mapping = ServiceCatalog.Map(new("cache", "redis:7"), index);
        Assert.Equal(NativeServiceKind.Valkey, mapping.Mapping!.Kind);
        Assert.Contains("Valkey", mapping.Mapping.Note, StringComparison.Ordinal);
    }
    [Fact]
    public void Native_plan_keeps_postgres_password_out_of_argv_and_honors_init_mount()
    {
        using var temp = new TempDirectory();
        var seed = Path.Combine(temp.Path, "seed"); Directory.CreateDirectory(seed);
        File.WriteAllText(Path.Combine(seed, "01.sql"), "CREATE TABLE example(id int);");
        var definition = new ComposeService("db", "postgres:18")
        {
            Environment = new Dictionary<string, string?> { ["POSTGRES_PASSWORD"] = "secret;value'", ["POSTGRES_USER"] = "owner", ["POSTGRES_DB"] = "app" },
            Volumes = [new("pgdata", "/var/lib/postgresql/data", false, false), new(seed, "/docker-entrypoint-initdb.d", true, true)],
        };
        var storage = new ServiceStorage(temp.Path, seed);
        var plan = NativeServicePlan.Create(definition, ServiceCatalog.Map(definition).Mapping!, storage, [new(5432, 15432)]);
        Assert.Equal(70, plan.Start.UserId);
        Assert.DoesNotContain("secret;value'", plan.Start.CommandLine, StringComparison.Ordinal);
        Assert.Equal("/var/lib/postgresql/data", plan.DataDirectory);
        Assert.Equal("owner", plan.Start.Environment["POSTGRES_USER"]);
        var mount = plan.Start.Mounts.Single(m => m.Target == "/docker-entrypoint-initdb.d");
        Assert.NotEqual(seed, mount.Source);
        Assert.True(File.Exists(Path.Combine(mount.Source, "01.sql")));
        Assert.Contains("--auth-host=scram-sha-256", File.ReadAllText(Path.Combine(storage.ServiceDirectory("db"), "run.sh")), StringComparison.Ordinal);
    }
    [Fact]
    public void Volumes_persist_and_are_scoped_to_the_workspace_and_seed_never_changes()
    {
        using var temp = new TempDirectory();
        var seed = Path.Combine(temp.Path, "seed"); File.WriteAllText(seed, "original");
        var definition = new ComposeService("cache", "redis") { Volumes = [new(seed, "/data/config", false, true), new("data", "/data", false, false)] };
        var storage = new ServiceStorage(temp.Path, Path.Combine(temp.Path, "one"));
        var first = storage.Prepare(definition);
        var copy = first.Single(m => m.Target == "/data/config").Source;
        File.WriteAllText(copy, "service writes");
        Assert.Equal("service writes", File.ReadAllText(storage.Prepare(definition).Single(m => m.Target == "/data/config").Source));
        Assert.Equal("original", File.ReadAllText(seed));
        Assert.NotEqual(storage.Root, new ServiceStorage(temp.Path, Path.Combine(temp.Path, "two")).Root);
    }
}
