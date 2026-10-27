using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Services.Tests;

/// <summary>F5 starts only the services a run's configuration points at; the others are offered, not started.</summary>
public sealed class ServiceTargetsTests : IDisposable
{
    /// <summary>spring-petclinic's docker-compose.yml.</summary>
    private const string PetClinicCompose = """
        services:
          mysql:
            image: mysql:9.7
            ports:
              - "3306:3306"
            environment:
              - MYSQL_ROOT_PASSWORD=
              - MYSQL_ALLOW_EMPTY_PASSWORD=true
              - MYSQL_USER=petclinic
              - MYSQL_PASSWORD=petclinic
              - MYSQL_DATABASE=petclinic
            volumes:
              - "./conf.d:/etc/mysql/conf.d:ro"
          postgres:
            image: postgres:18.4
            ports:
              - "5432:5432"
            environment:
              - POSTGRES_PASSWORD=petclinic
              - POSTGRES_USER=petclinic
              - POSTGRES_DB=petclinic
        """;

    private readonly TempDirectory _repo = new();

    public ServiceTargetsTests()
    {
        _repo.Write("docker-compose.yml", PetClinicCompose);
        _repo.Write("src/main/resources/application.properties", "database=h2\nspring.sql.init.schema-locations=classpath*:db/${database}/schema.sql\nspring.thymeleaf.mode=HTML\n");
        _repo.Write("src/main/resources/application-mysql.properties", "database=mysql\nspring.datasource.url=${MYSQL_URL:jdbc:mysql://localhost/petclinic}\n");
        _repo.Write("src/main/resources/application-postgres.properties", "database=postgres\nspring.datasource.url=${POSTGRES_URL:jdbc:postgresql://localhost/petclinic}\n");
    }

    private ComposeProject Project => ComposeReader.Discover(_repo.Path)!;

    [Fact]
    public void PetClinics_default_profile_runs_on_H2_and_points_at_no_service()
    {
        Assert.Empty(ServiceTargets.Referenced(Project, _repo.Path));
    }

    [Theory]
    [InlineData("postgres", "postgres")]
    [InlineData("mysql", "mysql")]
    [InlineData("mysql,postgres", "postgres")]
    [InlineData("dev", "")]
    public void An_active_Spring_profile_points_at_the_database_its_settings_use(string profiles, string expected)
    {
        // As in Spring, a later profile's setting replaces an earlier one's: mysql,postgres uses PostgreSQL.
        var targets = ServiceTargets.Referenced(Project, _repo.Path, new Dictionary<string, string?> { ["SPRING_PROFILES_ACTIVE"] = profiles });

        Assert.Equal(expected, string.Join(",", targets));
    }

    [Fact]
    public void A_profile_set_in_application_properties_and_an_overridden_URL_are_followed()
    {
        _repo.Write("src/main/resources/application.properties", "spring.profiles.active=postgres\n");
        Assert.Equal(["postgres"], ServiceTargets.Referenced(Project, _repo.Path));

        // The run's own URL wins over the profile's default, here pointing at no Compose service.
        Assert.Empty(ServiceTargets.Referenced(Project, _repo.Path, new Dictionary<string, string?> { ["POSTGRES_URL"] = "jdbc:postgresql://db.example.com/petclinic" }));
    }

    [Fact]
    public void Spring_YAML_settings_are_read_too()
    {
        _repo.Write("src/main/resources/application.properties", "database=h2\n");
        _repo.Write("src/main/resources/application.yml", "spring:\n  profiles:\n    active: cache\n");
        _repo.Write("src/main/resources/application-cache.yml", "spring:\n  data:\n    redis:\n      url: redis://cache:6379\n");
        _repo.Write("docker-compose.yml", PetClinicCompose + "\n  cache:\n    image: redis:7\n");

        Assert.Equal(["cache"], ServiceTargets.Referenced(Project, _repo.Path));
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=app;Username=app", "postgres")]
    [InlineData("Server=mysql;Database=app;User=app", "mysql")]
    [InlineData("postgresql://app:secret@127.0.0.1:5432/app", "postgres")]
    [InlineData("mysql+pymysql://app@localhost/app", "mysql")]
    [InlineData("postgres:5432", "postgres")]
    [InlineData("Host=localhost;Port=6543", "")]
    [InlineData("https://localhost:5432/not-a-database-url", "postgres")]
    [InlineData("sqlite:///app.db", "")]
    [InlineData("12:30", "")]
    public void A_connection_string_or_URL_points_at_a_service_by_name_or_by_its_published_port(string value, string expected) =>
        Assert.Equal(expected, string.Join(",", ServiceTargets.Targets(value, Project)));

    [Fact]
    public void Dot_env_files_and_appsettings_point_at_services()
    {
        _repo.Write(".env", "# local\nexport DATABASE_URL=postgresql://petclinic:petclinic@localhost:5432/petclinic\n");
        Assert.Equal(["postgres"], ServiceTargets.Referenced(Project, _repo.Path));

        File.Delete(_repo.Resolve(".env"));
        _repo.Write("appsettings.Development.json", """{ "ConnectionStrings": { "Shop": "Server=mysql;Port=3306;Database=shop" } }""");
        Assert.Equal(["mysql"], ServiceTargets.Referenced(Project, _repo.Path));
        Assert.Empty(ServiceTargets.Referenced(Project, _repo.Path, environmentName: "Production"));
    }

    public void Dispose() => _repo.Dispose();
}
