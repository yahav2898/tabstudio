using System.Text.Json;
using TabStudio.Containers.Images;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Runtime;
using TabStudio.Services;
using TabStudio.Services.Compose;
using TabStudio.Testing;

namespace TabStudio.Containers.Tests;

/// <summary>The engine's store, recovery, cleanup and Compose fixes of 0.21.3, without a real PRoot.</summary>
public sealed class HardeningTests
{
    private static ContainerEngine Engine(TempDirectory temp, HttpClient http) => new(new()
    {
        StoreRoot = Path.Combine(temp.Path, "store"),
        // Any existing file: these tests never start a process, but the engine must think it could.
        ProotPath = temp.Write("proot", "#!/bin/sh\n"),
    }, new RegistryClient(http, new SessionRegistryCredentialStore()));

    [Fact]
    public async Task A_second_engine_on_the_same_store_cannot_change_it_until_the_first_is_gone()
    {
        using var temp = new TempDirectory();
        using var http = new HttpClient();
        var first = Engine(temp, http);
        Assert.Null(first.Problem);
        first.CreateVolume("data");

        await using (var second = Engine(temp, http))
        {
            Assert.Contains("Another TabStudio", second.Problem, StringComparison.Ordinal);
            Assert.Throws<ContainerException>(() => second.CreateVolume("other"));
            await Assert.ThrowsAsync<ContainerException>(() => second.PullAsync("postgres:17-alpine"));
            Assert.False(Directory.Exists(Path.Combine(temp.Path, "store", "volumes", "other")));

            await first.DisposeAsync();
            Assert.Null(second.Problem);
            second.CreateVolume("other");
            Assert.True(Directory.Exists(Path.Combine(temp.Path, "store", "volumes", "other")));
        }
    }

    [Fact]
    public async Task What_an_engine_ended_mid_create_left_behind_is_removed_and_records_stay()
    {
        using var temp = new TempDirectory();
        var store = Path.Combine(temp.Path, "store");
        // A container whose copy Android cut short: its folder and part of its files, but no record.
        var partial = temp.Write("store/containers/" + new string('a', 64) + "/rootfs/usr/lib/libbig.so", "half");
        var unpacking = temp.Write("store/tmp/0123456789abcdef/etc/passwd", "root:x:0:0");
        var blob = temp.Write("store/blobs/sha256/" + new string('b', 64) + ".1234abcd", "{}");
        var download = temp.Write("store/blobs/sha256/" + new string('c', 64) + ".partial", "resumes");
        var index = temp.Write("store/images.json.partial", "[");
        var kept = new ContainerInfo
        {
            Id = new string('d', 64), Name = "kept", ImageId = "sha256:" + new string('e', 64), ImageName = "redis:7-alpine",
            Spec = new ContainerSpec { Image = "redis:7-alpine" }, Status = ContainerStatus.Exited,
        };
        temp.Write("store/containers/" + kept.Id + "/container.json", JsonSerializer.Serialize(kept));
        temp.Write("store/containers/" + kept.Id + "/rootfs/data", "stays");

        using var http = new HttpClient();
        await using var engine = Engine(temp, http);
        await engine.Recovery.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.False(Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(partial))))));
        Assert.False(File.Exists(unpacking));
        Assert.False(File.Exists(blob));
        Assert.False(File.Exists(index));
        Assert.True(File.Exists(download), "A download that resumes must be kept.");
        Assert.True(File.Exists(Path.Combine(store, "containers", kept.Id, "rootfs", "data")));
        Assert.Equal("kept", Assert.Single(engine.Containers).Name);
    }

    [Fact]
    public async Task Prune_removes_volumes_only_a_removed_container_could_use()
    {
        using var temp = new TempDirectory();
        using var http = new HttpClient();
        await using var engine = Engine(temp, http);
        engine.CreateVolume("0123456789ab-image-0");
        engine.CreateVolume("0123456789ab-anonymous-1");
        engine.CreateVolume("data");
        engine.CreateVolume("compose-0123456789ab-db-1a2b3c4d");

        Assert.Equal("0123456789ab", ContainerEngine.PrivateVolumeOwner("0123456789ab-image-0"));
        Assert.Null(ContainerEngine.PrivateVolumeOwner("compose-0123456789ab-db-1a2b3c4d"));
        Assert.Equal(2, engine.OrphanedPrivateVolumes().Count);

        await engine.PruneAsync(allImages: false);

        Assert.Equal(["compose-0123456789ab-db-1a2b3c4d", "data"], engine.Volumes.Select(v => v.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Only_containers_set_to_restart_and_not_from_compose_start_again_with_tabstudio()
    {
        using var temp = new TempDirectory();
        void Record(string name, string restart, bool stoppedByUser, bool compose = false)
        {
            var info = new ContainerInfo
            {
                Id = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(name))),
                Name = name, ImageId = "sha256:" + new string('e', 64), ImageName = "redis:7-alpine", Status = ContainerStatus.Running,
                StoppedByUser = stoppedByUser,
                Spec = new ContainerSpec
                {
                    Image = "redis:7-alpine", Restart = RestartPolicy.Parse(restart),
                    Labels = compose ? new Dictionary<string, string> { ["com.docker.compose.project"] = "compose-x" } : new Dictionary<string, string>(),
                },
            };
            temp.Write("store/containers/" + info.Id + "/container.json", JsonSerializer.Serialize(info));
        }

        Record("none", "no", stoppedByUser: false);
        Record("compose", "always", stoppedByUser: false, compose: true);
        Record("stopped", "unless-stopped", stoppedByUser: true);
        using var http = new HttpClient();
        await using (var engine = Engine(temp, http))
        {
            Assert.All(engine.Containers, c => Assert.Equal(ContainerStatus.Exited, c.Status));
            Assert.False(engine.HasContainersToResume);
        }

        Record("always", "always", stoppedByUser: true);
        await using (var engine = Engine(temp, http))
        {
            Assert.True(engine.HasContainersToResume);
        }
    }

    [Fact]
    public void A_program_for_another_processor_or_systemd_is_named_before_it_runs()
    {
        using var temp = new TempDirectory();
        var rootfs = temp.Resolve("rootfs");
        var elf = new byte[64];
        elf[0] = 0x7f; elf[1] = (byte)'E'; elf[2] = (byte)'L'; elf[3] = (byte)'F'; elf[4] = 2; elf[5] = 1; elf[18] = 62;
        Directory.CreateDirectory(Path.Combine(rootfs, "usr/bin"));
        File.WriteAllBytes(Path.Combine(rootfs, "usr/bin/server"), elf);
        temp.Write("rootfs/usr/local/bin/entrypoint.sh", "#!/bin/sh\nexec server\n");
        temp.Write("rootfs/lib/systemd/systemd", "");
        var environment = new Dictionary<string, string> { ["PATH"] = "/usr/local/bin:/usr/bin" };
        var arm = new OciPlatform { Os = "linux", Architecture = "arm64" };
        var amd = new OciPlatform { Os = "linux", Architecture = "amd64" };

        Assert.Contains("built for amd64, but the image says it is for arm64", ContainerEngine.Unrunnable(rootfs, "server", environment, arm), StringComparison.Ordinal);
        Assert.Null(ContainerEngine.Unrunnable(rootfs, "server", environment, amd));
        Assert.Null(ContainerEngine.Unrunnable(rootfs, "server", environment, null));
        Assert.Null(ContainerEngine.Unrunnable(rootfs, "entrypoint.sh", environment, arm));
        Assert.Contains("systemd", ContainerEngine.Unrunnable(rootfs, "/lib/systemd/systemd", environment, arm), StringComparison.Ordinal);
        Assert.Null(ContainerEngine.Unrunnable(rootfs, "missing", environment, arm));
    }

    [Fact]
    public void Compose_container_name_aliases_hostname_and_expose_reach_the_container()
    {
        using var temp = new TempDirectory();
        var file = temp.Write("compose.yaml", """
            services:
              db:
                image: postgres:17-alpine
                container_name: shop-db
                hostname: database
                expose: ['5432', '9187/tcp']
                networks:
                  backend: { aliases: [postgres, primary] }
                  frontend:
              app:
                build: .
                image: example/shop:dev
                networks: [backend]
            networks: { backend: {}, frontend: {} }
            """);
        var project = ComposeReader.Read([file]);
        var db = project.Services["db"];
        Assert.Equal("shop-db", db.ContainerName);
        Assert.Equal(["postgres", "primary"], db.Aliases);
        Assert.Equal([5432, 9187], db.Expose);
        Assert.Empty(project.Services["app"].Aliases);

        var spec = ComposeImages.ToSpec(project, db, []);
        Assert.Equal("shop-db", spec.Name);
        Assert.Equal("database", spec.Hostname);
        Assert.Equal(["db", "postgres", "primary", "database"], spec.Aliases);
        Assert.Equal([5432, 9187], spec.Expose);
        Assert.StartsWith("compose-", spec.VolumeScope, StringComparison.Ordinal);
        Assert.EndsWith("-db", spec.VolumeScope, StringComparison.Ordinal);
        // The same service's image volume keeps its name whatever else changes, so its data survives.
        Assert.Equal(ContainerEngine.ScopedVolumeName(spec.VolumeScope!, "/var/lib/postgresql/data/"), ContainerEngine.ScopedVolumeName(spec.VolumeScope!, "/var/lib/postgresql/data"));
        Assert.Equal(spec.VolumeScope, ComposeImages.ToSpec(project, db with { Environment = new Dictionary<string, string?> { ["A"] = "2" } }, []).VolumeScope);
    }

    [Fact]
    public void Readiness_follows_the_health_check_and_an_image_still_writing_its_log()
    {
        Assert.Equal(TimeSpan.FromSeconds(120), ComposeImages.ReadinessDeadline(null));
        Assert.Equal(TimeSpan.FromSeconds(120), ComposeImages.ReadinessDeadline(new HealthcheckSpec(["CMD", "true"]) { Interval = TimeSpan.FromSeconds(1), Retries = 3, Timeout = TimeSpan.FromSeconds(1) }));
        // MySQL's documented check: a long start period and many retries.
        var mysql = new HealthcheckSpec(["CMD", "mysqladmin", "ping"]) { StartPeriod = TimeSpan.FromSeconds(60), Interval = TimeSpan.FromSeconds(10), Timeout = TimeSpan.FromSeconds(5), Retries = 10 };
        Assert.Equal(TimeSpan.FromSeconds(60 + 15 * 11), ComposeImages.ReadinessDeadline(mysql));
        Assert.Equal(ComposeImages.MaximumReadiness, ComposeImages.ReadinessDeadline(mysql with { Retries = 1000 }));

        Assert.True(ComposeImages.KeepWaiting(TimeSpan.FromSeconds(100), TimeSpan.FromSeconds(120), TimeSpan.FromMinutes(5)));
        Assert.True(ComposeImages.KeepWaiting(TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(3)));
        Assert.False(ComposeImages.KeepWaiting(TimeSpan.FromSeconds(200), TimeSpan.FromSeconds(120), TimeSpan.FromSeconds(45)));
        Assert.False(ComposeImages.KeepWaiting(TimeSpan.FromMinutes(11), TimeSpan.FromSeconds(120), TimeSpan.Zero));

        // MariaDB declares 33060 and never opens it; only what the Compose file publishes must answer.
        PublishedPort[] mapped = [new(3306, "tcp", 31000, "127.0.0.1"), new(33060, "tcp", 31001, "127.0.0.1")];
        Assert.Equal(mapped, ComposeImages.ReadinessPorts(mapped, []));
        Assert.Equal([3306], ComposeImages.ReadinessPorts(mapped, [new PortBinding(3306)]).Select(p => p.ContainerPort));
    }

    [Fact]
    public void A_service_that_builds_its_image_is_still_attempted_and_its_build_is_named()
    {
        using var temp = new TempDirectory();
        var file = temp.Write("compose.yaml", "services:\n  worker:\n    build: ./worker\n    image: example/worker:dev\n");
        var service = ComposeReader.Read([file]).Services["worker"];
        Assert.EndsWith("worker", service.BuildContext, StringComparison.Ordinal);
        Assert.Equal("example/worker:dev", service.Image);
        Assert.Equal("example/worker:dev", ComposeImages.ToSpec(ComposeReader.Read([file]), service, []).Image);
    }
}
