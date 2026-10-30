using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Services.Tests;

/// <summary>
/// The database a Laravel app's <c>.env</c> names, offered in Services as gothinkster/laravel-realworld-example-app
/// needs it: F5 starts it, and the app gets <c>DB_HOST</c> and <c>DB_PORT</c> for it, its <c>.env</c> unchanged.
/// </summary>
public sealed class LaravelServicesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_MySQL_app_gets_MariaDB_with_its_own_user_and_the_run_points_at_it()
    {
        var app = WriteLaravel("DB_CONNECTION=mysql\nDB_HOST=127.0.0.1\nDB_PORT=9\nDB_DATABASE=homestead\nDB_USERNAME=homestead\nDB_PASSWORD=secret\n");

        var project = Assert.IsType<ComposeProject>(LaravelServices.Discover(app));

        var db = Assert.Single(project.Services).Value;
        Assert.Equal(("db", "mariadb:11"), (db.Name, db.Image));
        Assert.Equal("homestead", db.Environment["MYSQL_USER"]);
        Assert.Equal("secret", db.Environment["MYSQL_PASSWORD"]);
        Assert.Equal("homestead", db.Environment["MYSQL_DATABASE"]);
        Assert.Equal(["db"], project.RunServices);
        Assert.Equal(".env", project.Origin);

        var endpoints = new Dictionary<string, IReadOnlyList<ServicePort>> { ["db"] = [new(3306, 13306)] };
        var environment = ConnectionEnvironment.ForProject(project, app, endpoints);
        Assert.Equal("127.0.0.1", environment["DB_HOST"]);
        Assert.Equal("13306", environment["DB_PORT"]);
        Assert.Equal("homestead", environment["DB_USERNAME"]);
        Assert.Equal("secret", environment["DB_PASSWORD"]);
    }

    [Fact]
    public void Root_with_an_empty_password_is_allowed_explicitly_and_PostgreSQL_gets_a_password()
    {
        var app = WriteLaravel("DB_CONNECTION=mysql\nDB_PORT=9\nDB_DATABASE=laravel\nDB_USERNAME=root\nDB_PASSWORD=\n");
        var mysql = Assert.Single(LaravelServices.Discover(app)!.Services).Value;
        Assert.Equal("yes", mysql.Environment["MYSQL_ALLOW_EMPTY_PASSWORD"]);

        _temp.Write("app/.env", "DB_CONNECTION=pgsql\nDB_HOST=localhost\nDB_PORT=9\nDB_DATABASE=shop\nDB_USERNAME=shop\n");
        var project = LaravelServices.Discover(app)!;
        var postgres = Assert.Single(project.Services).Value;
        Assert.Equal("postgres:17", postgres.Image);
        Assert.Equal(LaravelServices.Credential, postgres.Environment["POSTGRES_PASSWORD"]);
        Assert.Equal(LaravelServices.Credential, project.RunEnvironment["DB_PASSWORD"]);
    }

    [Fact]
    public void A_database_already_answering_where_env_points_is_the_apps_own()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;

        Assert.Null(LaravelServices.Discover(WriteLaravel($"DB_CONNECTION=mysql\nDB_HOST=127.0.0.1\nDB_PORT={port}\nDB_USERNAME=root\n")));
    }

    [Theory]
    [InlineData("DB_CONNECTION=sqlite\n")]
    [InlineData("DB_CONNECTION=mysql\nDB_HOST=db.example.com\n")]
    public void SQLite_and_a_database_on_the_network_are_the_apps_own(string settings)
    {
        Assert.Null(LaravelServices.Discover(WriteLaravel(settings)));
    }

    [Fact]
    public void Reads_env_example_until_setup_creates_env_and_ignores_other_PHP_apps()
    {
        var app = WriteLaravel(null);
        _temp.Write("app/.env.example", "DB_CONNECTION=mariadb\nDB_PORT=9\nDB_USERNAME=app\nDB_PASSWORD=app\n");
        Assert.Equal(".env.example", LaravelServices.Discover(app)!.Origin);

        _temp.Write("slim/composer.json", """{ "require": { "slim/slim": "^4" } }""");
        _temp.Write("slim/.env", "DB_CONNECTION=mysql\n");
        Assert.Null(LaravelServices.Discover(_temp.Resolve("slim")));
    }

    [Fact]
    public void A_Sail_Compose_file_runs_only_the_database_the_env_names_and_points_the_app_at_it()
    {
        var app = WriteLaravel("DB_CONNECTION=pgsql\nDB_HOST=pgsql\nDB_PORT=5432\nDB_DATABASE=realworld\nDB_USERNAME=sail\nDB_PASSWORD=password\n");
        _temp.Write("app/docker-compose.yml", """
            services:
                laravel.test:
                    build:
                        context: ./vendor/laravel/sail/runtimes/8.1
                    ports:
                        - '80:80'
                    depends_on:
                        - pgsql
                pgsql:
                    image: 'postgres:14-alpine'
                    ports:
                        - '${FORWARD_DB_PORT:-5432}:5432'
                    environment:
                        POSTGRES_DB: '${DB_DATABASE}'
                        POSTGRES_USER: '${DB_USERNAME}'
                        POSTGRES_PASSWORD: '${DB_PASSWORD:-secret}'
            """);

        var project = LaravelServices.WithCompose(ComposeReader.Discover(app), app)!;

        Assert.Equal(["pgsql"], project.RunServices);
        Assert.Equal("sail", project.Services["pgsql"].Environment["POSTGRES_USER"]);
        var endpoints = new Dictionary<string, IReadOnlyList<ServicePort>> { ["pgsql"] = [new(5432, 15432)] };
        var environment = ConnectionEnvironment.ForProject(project, app, endpoints);
        Assert.Equal("127.0.0.1", environment["DB_HOST"]);
        Assert.Equal("15432", environment["DB_PORT"]);

        _temp.Write("other/docker-compose.yml", "services:\n  db:\n    image: postgres:16\n");
        var other = ComposeReader.Discover(_temp.Resolve("other"));
        Assert.Same(other, LaravelServices.WithCompose(other, _temp.Resolve("other")));
    }

    private string WriteLaravel(string? env)
    {
        _temp.Write("app/composer.json", """{ "require": { "laravel/framework": "^13.0" } }""");
        _temp.Write("app/artisan", "<?php");
        if (env is not null)
        {
            _temp.Write("app/.env", env);
        }

        return _temp.Resolve("app");
    }

    public void Dispose() => _temp.Dispose();
}
