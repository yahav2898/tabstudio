using TabStudio.Containers.Runtime;
using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Containers.Tests;

public sealed class ComposeTests
{
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
        Assert.Throws<ComposeException>(() => ComposeImages.ToSpec(project, project.Services["app"], []));
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
