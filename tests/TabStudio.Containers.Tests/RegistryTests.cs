using TabStudio.Containers.Images;
using TabStudio.Containers.Registry;
using TabStudio.Containers.Store;
using TabStudio.Testing;

namespace TabStudio.Containers.Tests;

public sealed class RegistryTests
{
    [Theory]
    [InlineData("postgres", "docker.io", "library/postgres", "latest", "postgres:latest")]
    [InlineData("postgres:17-alpine", "docker.io", "library/postgres", "17-alpine", "postgres:17-alpine")]
    [InlineData("bitnami/redis:7", "docker.io", "bitnami/redis", "7", "bitnami/redis:7")]
    [InlineData("ghcr.io/owner/app:1.2", "ghcr.io", "owner/app", "1.2", "ghcr.io/owner/app:1.2")]
    [InlineData("localhost:5000/img", "localhost:5000", "img", "latest", "localhost:5000/img:latest")]
    [InlineData("index.docker.io/library/nginx:alpine", "docker.io", "library/nginx", "alpine", "nginx:alpine")]
    public void Image_names_are_normalized_as_Docker_does(string text, string registry, string repository, string tag, string familiar)
    {
        var reference = ImageReference.Parse(text);
        Assert.Equal(registry, reference.Registry);
        Assert.Equal(repository, reference.Repository);
        Assert.Equal(tag, reference.Tag);
        Assert.Equal(familiar, reference.Familiar);
    }

    [Fact]
    public void Only_actual_loopback_registries_allow_plain_HTTP()
    {
        Assert.True(ImageReference.Parse("localhost:5000/app").IsInsecure);
        Assert.True(ImageReference.Parse("127.0.0.1:5000/app").IsInsecure);
        Assert.False(ImageReference.Parse("localhost.attacker.example/app").IsInsecure);
        Assert.False(ImageReference.Parse("127.attacker.example/app").IsInsecure);
    }

    [Fact]
    public void Docker_Hub_is_served_from_its_registry_host_and_digests_pin()
    {
        var digest = "sha256:" + new string('a', 64);
        var reference = ImageReference.Parse("redis@" + digest);
        Assert.Equal("registry-1.docker.io", reference.ApiHost);
        Assert.Null(reference.Tag);
        Assert.Equal(digest, reference.ManifestReference);
        Assert.False(ImageReference.TryParse("Bad/Name", out _, out var error));
        Assert.Contains("lowercase", error);
    }

    [Fact]
    public void Platforms_prefer_arm64_then_arm_then_amd64()
    {
        var order = OciPlatform.Preference(OciPlatform.Linux("arm64"));
        Assert.Equal(["linux/arm64", "linux/arm/v7", "linux/amd64"], order.Select(p => p.ToString()));
        Assert.True(OciPlatform.Linux("arm64").Matches(OciPlatform.Linux("arm64", "v8")));
        Assert.False(OciPlatform.Linux("arm", "v7").Matches(OciPlatform.Linux("arm", "v6")));
        Assert.True(OciPlatform.Linux("amd64").NeedsEmulation(OciPlatform.Linux("arm64")));
        Assert.False(OciPlatform.Linux("arm", "v7").NeedsEmulation(OciPlatform.Linux("arm64")));
    }

    [Fact]
    public async Task A_pull_authenticates_chooses_the_platform_verifies_and_deduplicates()
    {
        using var registry = new FakeRegistry();
        registry.PublishImage("team/app", "1.0", "amd64", "arm64");
        using var folder = new TempDirectory();
        var store = new ImageStore(folder.Path);
        var client = new RegistryClient(new HttpClient(), new SessionRegistryCredentialStore());
        var reference = ImageReference.Parse($"{registry.Host}/team/app:1.0");
        var progress = new List<PullProgress>();

        var image = await store.PullAsync(client, reference, [OciPlatform.Linux("arm64"), OciPlatform.Linux("amd64")], new InlineProgress(progress.Add));

        Assert.Equal("arm64", image.Platform.Architecture);
        Assert.Equal("ARCH=arm64", File.ReadAllLines(Path.Combine(store.RootfsOf(image.Id), "etc/os-release"))[1]);
        Assert.Equal(["/bin/hello"], image.Config.Config.Cmd);
        Assert.Contains(progress, p => p.Phase == "Done");
        Assert.True(registry.TokenRequests >= 1);
        var downloads = registry.BlobRequests;

        var again = await store.PullAsync(client, reference, [OciPlatform.Linux("arm64")]);
        Assert.Equal(image.Id, again.Id);
        Assert.Equal(downloads + 1, registry.BlobRequests); // Only the configuration: the layer is already stored.
        Assert.Single(await store.ListAsync());
        Assert.Equal(image.Id, (await store.FindAsync($"{registry.Host}/team/app:1.0"))?.Id);
        Assert.Equal(image.Id, (await store.FindAsync(image.ShortId))?.Id);
    }

    [Fact]
    public async Task A_cached_layer_is_reverified_even_when_its_length_is_unchanged()
    {
        using var registry = new FakeRegistry();
        registry.PublishImage("team/app", "1.0", "amd64");
        using var folder = new TempDirectory();
        using var store = new ImageStore(folder.Path);
        using var http = new HttpClient();
        var client = new RegistryClient(http, new SessionRegistryCredentialStore());
        var reference = ImageReference.Parse($"{registry.Host}/team/app:1.0");
        var image = await store.PullAsync(client, reference, [OciPlatform.Linux("amd64")]);
        var path = store.BlobPath(Assert.Single(image.Layers).Digest);
        var bytes = File.ReadAllBytes(path); bytes[0] ^= 0xFF; File.WriteAllBytes(path, bytes);
        var requests = registry.BlobRequests;
        await store.PullAsync(client, reference, [OciPlatform.Linux("amd64")]);
        Assert.Equal(requests + 2, registry.BlobRequests);
        Assert.Equal(image.Layers[0].Digest, RegistryClient.Digest(File.ReadAllBytes(path)));
    }

    [Fact]
    public async Task A_corrupted_layer_is_rejected_and_nothing_is_unpacked()
    {
        using var registry = new FakeRegistry { CorruptBlobs = true };
        registry.PublishImage("team/app", "1.0", "arm64");
        using var folder = new TempDirectory();
        var store = new ImageStore(folder.Path);
        var client = new RegistryClient(new HttpClient(), new SessionRegistryCredentialStore());

        await Assert.ThrowsAnyAsync<Exception>(() => store.PullAsync(client, ImageReference.Parse($"{registry.Host}/team/app:1.0"), [OciPlatform.Linux("arm64")]));

        Assert.Empty(await store.ListAsync());
        Assert.Empty(Directory.EnumerateDirectories(store.ImagesFolder));
    }

    [Fact]
    public async Task Missing_images_and_platforms_get_one_line_reasons()
    {
        using var registry = new FakeRegistry();
        registry.PublishImage("team/app", "1.0", "amd64");
        var client = new RegistryClient(new HttpClient(), new SessionRegistryCredentialStore());

        var missing = await Assert.ThrowsAsync<RegistryException>(() => client.ResolveAsync(ImageReference.Parse($"{registry.Host}/team/none:1"), [OciPlatform.Linux("arm64")]));
        Assert.Contains("was not found", missing.Message);
        var platform = await Assert.ThrowsAsync<RegistryException>(() => client.ResolveAsync(ImageReference.Parse($"{registry.Host}/team/app:1.0"), [OciPlatform.Linux("arm64")]));
        Assert.Contains("offers linux/amd64", platform.Message);
    }

    [Fact]
    public async Task Saved_credentials_are_sent_to_the_token_service()
    {
        using var registry = new FakeRegistry { RequiredBasic = Convert.ToBase64String("me:pat"u8.ToArray()) };
        registry.PublishImage("private/app", "1", "arm64");
        var credentials = new SessionRegistryCredentialStore();
        var client = new RegistryClient(new HttpClient(), credentials);
        var reference = ImageReference.Parse($"{registry.Host}/private/app:1");

        await Assert.ThrowsAsync<RegistryException>(() => client.ResolveAsync(reference, [OciPlatform.Linux("arm64")]));
        await credentials.SetAsync(registry.Host, new RegistryCredential("me", "pat"));
        var resolved = await new RegistryClient(new HttpClient(), credentials).ResolveAsync(reference, [OciPlatform.Linux("arm64")]);
        Assert.NotNull(resolved.Platform);
    }

    [Fact]
    public void Challenges_are_parsed_with_quotes_and_commas()
    {
        var values = RegistryClient.Challenge.ParseParameters("realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:library/a:pull,push\"");
        Assert.Equal("https://auth.docker.io/token", values["realm"]);
        Assert.Equal("repository:library/a:pull,push", values["scope"]);
    }

    private sealed class InlineProgress(Action<PullProgress> report) : IProgress<PullProgress>
    {
        public void Report(PullProgress value) => report(value);
    }
}

