using TabStudio.Containers.Registry;
using TabStudio.Containers.Runtime;
using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Containers.Tests;

public sealed class ComposeTests
{
    [Fact]
    public void Backend_discovers_parent_compose_and_override_but_stops_at_nested_repository_boundary()
    {
        using var temp = new TempDirectory();
        var backend = Path.Combine(temp.Path, "backend");
        Directory.CreateDirectory(backend);
        File.WriteAllText(Path.Combine(temp.Path, "compose.yml"), "services:\n  db: {image: 'postgres:18'}\n  backend: {build: ., depends_on: [db]}\n");
        File.WriteAllText(Path.Combine(temp.Path, "compose.override.yml"), "services:\n  backend:\n    environment: {DATABASE_URL: 'postgresql://postgres@db:5432/app'}\n");
        var project = ComposeReader.Discover(backend)!;
        Assert.Equal(temp.Path, project.Directory);
        Assert.Equal(2, project.Files.Count);
        var environment = TabStudio.Services.ConnectionEnvironment.ForProject(project, backend,
            new Dictionary<string, IReadOnlyList<ServicePort>> { ["db"] = [new(5432, 25432)] });
        Assert.Equal("postgresql://postgres@127.0.0.1:25432/app", environment["DATABASE_URL"]);
        File.WriteAllText(Path.Combine(backend, ".git"), "gitdir: elsewhere\n");
        Assert.Null(ComposeReader.Discover(backend));
    }

    [Fact]
    public void A_completed_image_without_ports_does_not_need_a_connection_endpoint()
    {
        using var temp = new TempDirectory();
        var project = new ComposeProject(temp.Path, [], new Dictionary<string, ComposeService>
        { ["migrate"] = new("migrate", "example/migrate:1") });
        var environment = TabStudio.Services.ConnectionEnvironment.ForProject(project, temp.Path,
            new Dictionary<string, IReadOnlyList<ServicePort>> { ["migrate"] = [] });
        Assert.Empty(environment);
    }

    [Fact]
    public void Real_image_configuration_preserves_overrides_environment_health_and_workspace_volumes()
    {
        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, "compose.yaml");
        File.WriteAllText(Path.Combine(temp.Path, ".env"), "DB_PASSWORD=development-only\n");
        File.WriteAllText(file, """
            services:
              db:
                image: postgres:17-alpine
                environment: { POSTGRES_PASSWORD: '${DB_PASSWORD}' }
                ports: ['0:5432']
                volumes: [data:/var/lib/postgresql/data]
                entrypoint: ['/usr/local/bin/docker-entrypoint.sh']
                command: ['postgres', '-c', 'max_connections=20']
                working_dir: /var/lib/postgresql
                user: '70:70'
                platform: linux/arm64
                restart: unless-stopped
                healthcheck: { test: ['CMD-SHELL', 'pg_isready -U postgres'], interval: 1s }
              app:
                build: .
                depends_on: { db: { condition: service_healthy } }
            """);
        var project = ComposeReader.Read([file]);
        var service = project.Services["db"];
        var spec = ComposeImages.ToSpec(project, service, [new(5432, 25432)]);
        Assert.Equal("development-only", spec.Environment["POSTGRES_PASSWORD"]);
        Assert.Equal(service.Entrypoint, spec.Entrypoint);
        Assert.Equal(service.Command, spec.Command);
        Assert.Equal("/var/lib/postgresql", spec.WorkingDirectory);
        Assert.Equal("70:70", spec.User);
        Assert.Equal("linux/arm64", spec.Platform);
        Assert.Equal("unless-stopped", spec.Restart.Name);
        Assert.Equal(25432, Assert.Single(spec.Ports).HostPort);
        Assert.EndsWith("-data", Assert.Single(spec.Mounts).Source, StringComparison.Ordinal);
        Assert.Equal("db", Assert.Single(spec.Aliases));
        Assert.Equal(TimeSpan.FromSeconds(1), spec.Healthcheck!.Interval);
        Assert.Equal(ComposeImages.BuiltImageName(project, project.Services["app"]), ComposeImages.ToSpec(project, project.Services["app"], []).Image);
    }

    [Fact]
    public async Task A_service_with_only_a_build_is_built_from_its_Dockerfile_and_created_like_an_image_service()
    {
        // A service with build: and no image: could not run at all: Services refused it and ToSpec threw.
        using var registry = new FakeRegistry();
        registry.PublishImage("team/base", "1.0", "amd64", "arm64");
        using var temp = new TempDirectory();
        // Creating a container only needs PRoot to exist; nothing runs in this test.
        var proot = temp.Write("tools/proot");
        temp.Write("app/Dockerfile.dev", """
            ARG BASE
            FROM ${BASE}
            ENV GREETING=hello
            WORKDIR /srv/app
            COPY site/ ./
            COPY --chmod=755 run.sh /usr/local/bin/run
            EXPOSE 8080
            CMD ["/usr/local/bin/run"]
            """);
        temp.Write("app/site/index.html", "<h1>hi</h1>");
        temp.Write("app/site/secret.txt", "left out");
        temp.Write("app/.dockerignore", "site/secret.txt\n");
        temp.Write("app/run.sh", "#!/bin/sh\necho \"$GREETING\"\n");
        var file = temp.Write("compose.yaml", $$"""
            services:
              web:
                build:
                  context: ./app
                  dockerfile: Dockerfile.dev
                  args:
                    BASE: {{registry.Host}}/team/base:1.0
            """);
        var project = ComposeReader.Read([file]);
        var service = project.Services["web"];
        Assert.Null(service.Image);
        Assert.Equal(Path.Combine(temp.Path, "app", "Dockerfile.dev"), service.BuildDockerfile);
        Assert.Equal($"{registry.Host}/team/base:1.0", service.BuildArgs["BASE"]);
        using var http = new HttpClient();
        await using var engine = new ContainerEngine(new() { StoreRoot = Path.Combine(temp.Path, "store"), ProotPath = proot },
            new RegistryClient(http, new SessionRegistryCredentialStore()));
        var images = new ComposeImages(engine);
        Assert.True(images.CanRun(service));

        var image = await images.BuildAsync(project, service, _ => { }, CancellationToken.None);
        var spec = ComposeImages.ToSpec(project, service, []);
        var container = await engine.CreateAsync(spec);

        Assert.Equal(ComposeImages.BuiltImageName(project, service), spec.Image);
        Assert.Equal(image.Id, container.ImageId);
        var rootfs = engine.RootfsOf(container.Id);
        Assert.True(File.Exists(Path.Combine(rootfs, "bin/hello")));
        Assert.Equal("<h1>hi</h1>", File.ReadAllText(Path.Combine(rootfs, "srv/app/index.html")));
        Assert.False(File.Exists(Path.Combine(rootfs, "srv/app/secret.txt")));
        Assert.True(File.Exists(Path.Combine(rootfs, "usr/local/bin/run")));
        if (!OperatingSystem.IsWindows()) Assert.Equal((UnixFileMode)0b111_101_101, File.GetUnixFileMode(Path.Combine(rootfs, "usr/local/bin/run")));
        Assert.Equal(["/usr/local/bin/run"], image.Config.Config.Cmd!);
        Assert.Contains("GREETING=hello", image.Config.Config.Env!);
        Assert.Equal("/srv/app", image.Config.Config.WorkingDir);
        Assert.Contains("8080/tcp", image.Config.Config.ExposedPorts!.Keys);

        // Nothing it is built from changed, so the next start uses the same image instead of building it again.
        Assert.Equal(image.Id, (await images.BuildAsync(project, service, _ => { }, CancellationToken.None)).Id);
    }

    [Fact]
    public void Privileged_services_are_rejected_and_missing_bind_rules_are_honored()
    {
        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, "compose.yaml");
        File.WriteAllText(file, """
            services:
              forbidden: {image: nginx:alpine, privileged: true}
              bind:
                image: nginx:alpine
                volumes:
                  - type: bind
                    source: ./absent
                    target: /data
                    bind: {create_host_path: false}
            """);
        var project = ComposeReader.Read([file]);
        Assert.Throws<ComposeException>(() => ComposeImages.ToSpec(project, project.Services["forbidden"], []));
        Assert.Throws<ComposeException>(() => ComposeImages.ToSpec(project, project.Services["bind"], []));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "absent")));
    }

    [Fact]
    public void Docker_run_arguments_remain_literal_and_unknown_options_fail()
    {
        var (spec, detached) = DockerCommand.ParseRun(["-d", "--name", "test", "-p", "8080:80", "-e", "VALUE=$(touch /tmp/never)", "--entrypoint", "/bin/sh", "nginx:alpine", "-c", "echo hello"], "/tmp");
        Assert.True(detached);
        Assert.Equal("$(touch /tmp/never)", spec.Environment["VALUE"]);
        Assert.Equal(["-c", "echo hello"], spec.Command);
        Assert.Equal(["/bin/sh"], spec.Entrypoint);
        Assert.Throws<ContainerException>(() => DockerCommand.ParseRun(["--privileged", "nginx"], "/tmp"));
    }

    [Fact]
    public void Network_addresses_are_distinct_and_explicit_port_collisions_fail_before_launch()
    {
        using var temp = new TempDirectory();
        var next = 30000;
        var network = new ContainerNetwork(temp.Path, _ => true, () => next++);
        var a = network.Allocate("a", null, ["db"], [new(5432, HostPort: 15432)]);
        var b = network.Allocate("b", null, ["cache"], [new(6379, HostPort: 0)]);
        Assert.NotEqual(a.Address, b.Address);
        Assert.Throws<ContainerException>(() => network.Allocate("c", null, [], [new(5432, HostPort: 15432)]));
        Assert.Contains(b.Address, network.Hosts(a, []), StringComparison.Ordinal);
        Assert.Contains("15432", File.ReadAllText(network.PeersFile), StringComparison.Ordinal);
    }
}
