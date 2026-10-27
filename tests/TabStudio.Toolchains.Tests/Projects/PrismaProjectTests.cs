using TabStudio.Testing;
using TabStudio.Toolchains.Catalog;
using TabStudio.Toolchains.Linux;
using TabStudio.Toolchains.Projects;

namespace TabStudio.Toolchains.Tests.Projects;

/// <summary>
/// Prisma in a Node.js project, as gothinkster/node-express-realworld-example-app (30b68e1) uses it: the schema
/// <c>package.json</c> names, its PostgreSQL datasource on <c>DATABASE_URL</c>, <c>prisma generate</c> with the
/// tablet's engine after <c>npm install</c>, and the URL filled into <c>.env</c>.
/// </summary>
public sealed class PrismaProjectTests : IDisposable
{
    private const string RealWorldPackage = """
        {
          "name": "@api/source",
          "scripts": { "start": "nx serve" },
          "prisma": {
            "seed": "ts-node --transpile-only src/prisma/seed.ts",
            "schema": "src/prisma/schema.prisma"
          },
          "dependencies": { "@prisma/client": "^4.16.1", "express": "~4.18.1" },
          "devDependencies": { "prisma": "^4.16.1", "nx": "17.2.6" }
        }
        """;

    private const string RealWorldSchema = """
        datasource db {
          provider = "postgresql"
          url      = env("DATABASE_URL")
        }

        generator client {
          provider        = "prisma-client-js"
          previewFeatures = []
        }

        model Tag {
          id   Int    @id @default(autoincrement())
          name String @unique
        }
        """;

    private readonly TempDirectory _workspace = new();

    private static ToolchainDefinition Node => ToolchainCatalog.Default.Find("node")!;

    private static ToolchainPlatform Tablet => new() { NativeLibraryDirectory = "/data/app/lib/arm64", Architecture = "aarch64" };

    [Fact]
    public void The_realworld_app_s_schema_database_and_migrations_are_found()
    {
        WriteRealWorld();

        var prisma = Assert.IsType<PrismaProject>(PrismaProject.Detect(_workspace.Path));

        Assert.Equal("src/prisma/schema.prisma", prisma.Schema);
        Assert.Equal("postgresql", prisma.Provider);
        Assert.Equal("PostgreSQL", prisma.DatabaseName);
        Assert.Equal("postgres:16", prisma.DatabaseImage);
        Assert.Equal("DATABASE_URL", prisma.UrlVariable);
        Assert.True(prisma.HasMigrations);
    }

    [Theory]
    [InlineData("""{ "dependencies": { "express": "4" } }""", "prisma/schema.prisma")]
    [InlineData("""{ "dependencies": { "@prisma/client": "5" } }""", "db/schema.prisma")]
    [InlineData("""{ "devDependencies": { "prisma": "5" }, "prisma": { "schema": "../outside.prisma" } }""", "outside.prisma")]
    public void Without_Prisma_or_its_schema_there_is_nothing_to_do(string package, string schema)
    {
        _workspace.Write("app/package.json", package);
        _workspace.Write(schema.StartsWith("outside", StringComparison.Ordinal) ? schema : "app/" + schema, RealWorldSchema);

        Assert.Null(PrismaProject.Detect(_workspace.Resolve("app")));
    }

    [Fact]
    public void The_default_schema_location_and_a_SQLite_datasource_are_read()
    {
        _workspace.Write("package.json", """{ "dependencies": { "@prisma/client": "5.22.0" } }""");
        _workspace.Write("prisma/schema.prisma", "datasource db {\n  provider = \"sqlite\"\n  url = \"file:./dev.db\"\n}\n");

        var prisma = Assert.IsType<PrismaProject>(PrismaProject.Detect(_workspace.Path));

        Assert.Equal("prisma/schema.prisma", prisma.Schema);
        Assert.Equal("sqlite", prisma.Provider);
        Assert.Null(prisma.UrlVariable);
        Assert.Null(prisma.DatabaseImage);
        Assert.False(prisma.HasMigrations);
    }

    [Fact]
    public void Npm_install_is_followed_by_prisma_generate_with_the_tablet_s_engine()
    {
        WriteRealWorld();
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");
        var dependency = RunPlanner.DependenciesOf(project)!;

        var tablet = RunPlanner.PlanDependencies(project, dependency, Tablet);
        var computer = RunPlanner.PlanDependencies(project, dependency);

        Assert.Equal(["npm install --no-fund --no-audit", "npx --no-install prisma generate --schema src/prisma/schema.prisma"], tablet.Select(s => s.CommandLine));
        Assert.Equal("linux-musl-arm64-openssl-3.0.x", tablet[1].Environment["PRISMA_CLI_BINARY_TARGETS"]);
        Assert.Equal("1", tablet[1].Environment["PRISMA_HIDE_UPDATE_MESSAGE"]);
        Assert.False(computer[1].Environment.ContainsKey("PRISMA_CLI_BINARY_TARGETS"));
        Assert.Equal(_workspace.Path, tablet[1].WorkingDirectory);
    }

    [Fact]
    public void Packages_without_a_generated_client_are_still_pending()
    {
        WriteRealWorld();
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");
        var dependency = RunPlanner.DependenciesOf(project)!;
        _workspace.Write("node_modules/@prisma/client/package.json", "{}");

        Assert.True(RunPlanner.IsPending(project, dependency));

        _workspace.Write("node_modules/.prisma/client/index.js", "module.exports = {};");
        Assert.False(RunPlanner.IsPending(project, dependency));
    }

    [Theory]
    [InlineData("aarch64", "linux-musl-arm64-openssl-3.0.x")]
    [InlineData("x86_64", "linux-musl-openssl-3.0.x")]
    [InlineData("armv7", null)]
    public void The_engine_matches_the_Linux_environment_s_architecture(string architecture, string? target)
    {
        Assert.Equal(target, PrismaProject.EngineTarget(new ToolchainPlatform { NativeLibraryDirectory = "/lib", Architecture = architecture }));
        Assert.Null(PrismaProject.EngineTarget(ToolchainPlatform.Desktop));
    }

    [Fact]
    public void The_migration_runs_in_the_project_with_the_engine()
    {
        WriteRealWorld();
        var project = new ToolchainProject(Node, _workspace.Path, "package.json");
        var prisma = PrismaProject.Detect(_workspace.Path)!;

        var migrate = RunPlanner.PlanPrismaMigrate(project, prisma, Tablet);

        Assert.Equal("npx --no-install prisma migrate deploy --schema src/prisma/schema.prisma", migrate.CommandLine);
        Assert.Equal("linux-musl-arm64-openssl-3.0.x", migrate.Environment["PRISMA_CLI_BINARY_TARGETS"]);
    }

    [Fact]
    public void The_URL_goes_into_env_only_where_the_project_has_none()
    {
        WriteRealWorld();
        var prisma = PrismaProject.Detect(_workspace.Path)!;
        var url = prisma.ConnectionUrl(15432, "tabstudio", "p@ss", "realworld");
        Assert.Equal("postgresql://tabstudio:p%40ss@127.0.0.1:15432/realworld?schema=public", url);

        Assert.True(prisma.FillEnvironmentFile(url));
        Assert.Equal(url, prisma.EnvironmentFileUrl());

        _workspace.Write(".env", "JWT_SECRET=secret\nDATABASE_URL=\n");
        Assert.True(prisma.FillEnvironmentFile(url));
        Assert.Equal($"JWT_SECRET=secret\nDATABASE_URL=\"{url}\"\n", File.ReadAllText(_workspace.Resolve(".env")).ReplaceLineEndings("\n"));

        var moved = prisma.ConnectionUrl(15433, "tabstudio", "p@ss", "realworld");
        Assert.False(prisma.FillEnvironmentFile(moved));
        Assert.True(prisma.FillEnvironmentFile(moved, value => value == url));
        Assert.Equal(moved, prisma.EnvironmentFileUrl());

        _workspace.Write(".env", "DATABASE_URL=postgresql://me@example.com/prod\n");
        Assert.False(prisma.FillEnvironmentFile(url, _ => false));
        Assert.Equal("postgresql://me@example.com/prod", prisma.EnvironmentFileUrl());
    }

    private void WriteRealWorld()
    {
        _workspace.Write("package.json", RealWorldPackage);
        _workspace.Write("src/prisma/schema.prisma", RealWorldSchema);
        _workspace.Write("src/prisma/migrations/migration_lock.toml", "provider = \"postgresql\"\n");
        _workspace.Write("src/prisma/migrations/20210924222830_initial/migration.sql", "CREATE TABLE \"Tag\" (\"id\" SERIAL NOT NULL);\n");
    }

    public void Dispose() => _workspace.Dispose();
}
