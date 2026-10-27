using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Services.Tests;

/// <summary>
/// The database a Prisma schema names, offered in Services for a repository without a Compose file, as
/// gothinkster/node-express-realworld-example-app: F5 starts it and the run gets <c>DATABASE_URL</c> at its port.
/// </summary>
public sealed class PrismaServicesTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    [Fact]
    public void A_PostgreSQL_schema_without_Compose_offers_PostgreSQL_and_points_the_run_at_it()
    {
        var repository = WriteRealWorld("node-express-realworld-example-app");

        var project = Assert.IsType<ComposeProject>(PrismaServices.Discover(repository));

        var db = Assert.Single(project.Services).Value;
        Assert.Equal("db", db.Name);
        Assert.Equal("postgres:16", db.Image);
        Assert.Equal("node_express_realworld_example_app", db.Environment["POSTGRES_DB"]);
        Assert.Equal(["db"], project.RunServices);
        Assert.Equal("src/prisma/schema.prisma", project.Origin);
        // No Compose application builds the project, so F5 starts the run services: the database.
        Assert.Null(ConnectionEnvironment.FindApplication(project, repository));

        var endpoints = new Dictionary<string, IReadOnlyList<ServicePort>> { ["db"] = [new(5432, 15432)] };
        var environment = ConnectionEnvironment.ForProject(project, repository, endpoints);
        Assert.Equal(
            "postgresql://tabstudio:tabstudio@127.0.0.1:15432/node_express_realworld_example_app?schema=public",
            environment["DATABASE_URL"]);
        Assert.Equal("15432", environment["DB_PORT"]);
    }

    [Fact]
    public void A_database_the_project_names_in_env_is_left_alone_and_TabStudio_s_own_URL_is_not()
    {
        var repository = WriteRealWorld("app");
        _temp.Write("app/.env", "DATABASE_URL=postgresql://me:secret@db.example.com:5432/prod\n");
        Assert.Null(PrismaServices.Discover(repository));

        _temp.Write("app/.env", "DATABASE_URL=\"postgresql://me:secret@localhost:5432/dev\"\n");
        Assert.Null(PrismaServices.Discover(repository));

        _temp.Write("app/.env", "JWT_SECRET=x\nDATABASE_URL=\"postgresql://tabstudio:tabstudio@127.0.0.1:15432/app?schema=public\"\n");
        Assert.NotNull(PrismaServices.Discover(repository));
        Assert.True(PrismaServices.IsOwn("postgresql://tabstudio:tabstudio@127.0.0.1:15433/app?schema=public"));
        Assert.False(PrismaServices.IsOwn("postgresql://tabstudio:tabstudio@db.example.com/app"));
        Assert.True(PrismaServices.IsLocal("postgresql://me@127.0.0.1/dev"));
        Assert.False(PrismaServices.IsLocal("not a url"));
    }

    [Fact]
    public void A_project_one_folder_down_is_found_and_SQLite_needs_no_service()
    {
        _temp.Write("api/package.json", """{ "dependencies": { "@prisma/client": "5.22.0" } }""");
        _temp.Write("api/prisma/schema.prisma", "datasource db {\n  provider = \"mysql\"\n  url = env(\"DB_URL\")\n}\n");

        var project = Assert.IsType<ComposeProject>(PrismaServices.Discover(_temp.Path));
        Assert.Equal("mysql:8", project.Services["db"].Image);
        Assert.Equal("api/prisma/schema.prisma", project.Origin);
        Assert.StartsWith("mysql://root:tabstudio@db:3306/api", project.RunEnvironment["DB_URL"], StringComparison.Ordinal);

        _temp.Write("api/prisma/schema.prisma", "datasource db {\n  provider = \"sqlite\"\n  url = \"file:./dev.db\"\n}\n");
        Assert.Null(PrismaServices.Discover(_temp.Path));
    }

    private string WriteRealWorld(string folder)
    {
        _temp.Write(folder + "/package.json", """
            {
              "name": "@api/source",
              "prisma": { "schema": "src/prisma/schema.prisma" },
              "dependencies": { "@prisma/client": "^4.16.1" },
              "devDependencies": { "prisma": "^4.16.1" }
            }
            """);
        _temp.Write(folder + "/src/prisma/schema.prisma", """
            datasource db {
              provider = "postgresql"
              url      = env("DATABASE_URL")
            }
            """);
        return _temp.Resolve(folder);
    }

    public void Dispose() => _temp.Dispose();
}
