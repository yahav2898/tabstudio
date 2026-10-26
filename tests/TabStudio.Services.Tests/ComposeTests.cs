using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Services.Tests;

public sealed class ComposeTests : IDisposable
{
    private readonly TempDirectory _temp = new();
    private ComposeProject Read(string yaml)
    {
        File.WriteAllText(Path.Combine(_temp.Path, "compose.yaml"), yaml);
        return ComposeReader.Discover(_temp.Path, new Dictionary<string, string?> { ["PASSWORD"] = "p' @;secret", ["HOST_PORT"] = "15432" })!;
    }

    [Fact]
    public void Reads_short_and_long_forms_environment_commands_dependencies_health_and_volumes()
    {
        var project = Read("""
            services:
              db:
                image: postgres:18-alpine
                environment:
                  POSTGRES_USER: app
                  POSTGRES_PASSWORD: ${PASSWORD:?required}
                  POSTGRES_DB: appdb
                ports: ['127.0.0.1:${HOST_PORT}:5432']
                volumes: [db:/var/lib/postgresql/data, ./init:/docker-entrypoint-initdb.d:ro]
                healthcheck:
                  test: [CMD-SHELL, 'pg_isready -U $$POSTGRES_USER']
                  interval: 1m30s
                  timeout: 500ms
                  retries: 5
                  start_period: 5s
              cache:
                image: redis:7
                command: redis-server --appendonly 'yes'
                environment: [A=1, PASSWORD]
                ports:
                  - target: 6379
                    published: '16379'
                    host_ip: 127.0.0.1
                depends_on:
                  db: {condition: service_healthy}
            """);
        var db = project.Services["db"];
        Assert.Equal("p' @;secret", db.Environment["POSTGRES_PASSWORD"]);
        Assert.Equal(new ServicePort(5432, 15432, "tcp", "127.0.0.1"), Assert.Single(db.Ports));
        Assert.Equal("db", db.Volumes[0].Source);
        Assert.Equal(Path.Combine(_temp.Path, "init"), db.Volumes[1].Source);
        Assert.True(db.Volumes[1].ReadOnly);
        Assert.Equal("pg_isready -U $POSTGRES_USER", db.Healthcheck!.Test[1]);
        Assert.Equal(TimeSpan.FromSeconds(90), db.Healthcheck.Interval);
        Assert.Equal(TimeSpan.FromMilliseconds(500), db.Healthcheck.Timeout);
        Assert.Equal(5, db.Healthcheck.Retries);
        Assert.Equal(["redis-server", "--appendonly", "yes"], project.Services["cache"].Command);
        Assert.Equal("p' @;secret", project.Services["cache"].Environment["PASSWORD"]);
        Assert.Equal("service_healthy", project.Services["cache"].Dependencies[0].Condition);
    }

    [Fact]
    public void Devcontainer_jsonc_loads_ordered_overrides_relative_to_its_file()
    {
        Directory.CreateDirectory(Path.Combine(_temp.Path, ".devcontainer"));
        File.WriteAllText(Path.Combine(_temp.Path, "base.yml"), """
            services:
              app:
                build: .
                environment: {A: one, B: two}
                depends_on: [db]
              db:
                image: postgres:18
                volumes: [data:/var/lib/postgresql/data]
            """);
        File.WriteAllText(Path.Combine(_temp.Path, ".devcontainer", "override.yml"), """
            services:
              app:
                environment: {B: changed}
              db:
                volumes: [newdata:/var/lib/postgresql/data]
            """);
        File.WriteAllText(Path.Combine(_temp.Path, ".devcontainer", "devcontainer.json"), """
            {
              // developer configuration
              "dockerComposeFile": ["../base.yml", "override.yml"],
              "service": "app",
              "runServices": ["db"],
            }
            """);
        var project = ComposeReader.Discover(_temp.Path)!;
        Assert.Equal("app", project.DevelopmentService);
        Assert.Equal(["db"], project.RunServices);
        Assert.Equal("one", project.Services["app"].Environment["A"]);
        Assert.Equal("changed", project.Services["app"].Environment["B"]);
        Assert.Equal(_temp.Path, project.Services["app"].BuildContext);
        Assert.Equal("newdata", Assert.Single(project.Services["db"].Volumes).Source);
    }

    [Fact]
    public void Dotenv_and_env_files_do_not_execute_code_and_environment_wins()
    {
        File.WriteAllText(Path.Combine(_temp.Path, ".env"), "TAG=18\nVALUE='$(echo unsafe)'\n");
        File.WriteAllText(Path.Combine(_temp.Path, "db.env"), "POSTGRES_DB=fromfile\nPOSTGRES_PASSWORD=${PASSWORD}\n");
        var project = Read("""
            services:
              db:
                image: postgres:${TAG:-17}
                env_file: [db.env, {path: absent.env, required: false}]
                environment: {POSTGRES_DB: explicit, VALUE: '${VALUE}'}
            """);
        Assert.Equal("postgres:18", project.Services["db"].Image);
        Assert.Equal("explicit", project.Services["db"].Environment["POSTGRES_DB"]);
        Assert.Equal("p' @;secret", project.Services["db"].Environment["POSTGRES_PASSWORD"]);
        Assert.Equal("$(echo unsafe)", project.Services["db"].Environment["VALUE"]);
    }

    [Fact]
    public void Anchors_merge_and_escaped_dollars_are_expanded_once()
    {
        var project = Read("""
            x-common: &common
              environment: {VALUE: '$$PASSWORD'}
            services:
              one:
                <<: *common
                image: redis
              two:
                <<: *common
                image: valkey/valkey
            """);
        Assert.Equal("$PASSWORD", project.Services["one"].Environment["VALUE"]);
        Assert.Equal("$PASSWORD", project.Services["two"].Environment["VALUE"]);
    }

    [Theory]
    [InlineData("${A:-${B:-fallback}}", "fallback")]
    [InlineData("$$A", "$A")]
    [InlineData("${EMPTY-default}", "")]
    [InlineData("${EMPTY:-default}", "default")]
    [InlineData("${A+set}", "")]
    [InlineData("${EMPTY+set}", "set")]
    [InlineData("${EMPTY:+set}", "")]
    public void Interpolation_matches_compose_unset_and_empty_semantics(string input, string expected) =>
        Assert.Equal(expected, ComposeInterpolation.Expand(input, new Dictionary<string, string?> { ["EMPTY"] = "" }));

    [Fact]
    public void Required_variable_error_does_not_disclose_a_secret_default_message()
    {
        var error = Assert.Throws<ComposeException>(() => ComposeInterpolation.Expand("${MISSING:?secret-message}", new Dictionary<string, string?>()));
        Assert.Contains("MISSING", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-message", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ports: ['99999:5432']")]
    [InlineData("volumes: ['data:/../../outside']")]
    [InlineData("healthcheck: {interval: forever}")]
    public void Rejects_invalid_ports_volumes_and_health_settings(string setting) =>
        Assert.Throws<ComposeException>(() => Read("services:\n  db:\n    image: postgres\n    " + setting));

    public void Dispose() => _temp.Dispose();
}
