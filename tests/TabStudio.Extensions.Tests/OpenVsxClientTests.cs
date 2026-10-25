using System.Text;
using TabStudio.Extensions.Contributions;
using TabStudio.Extensions.Installation;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.OpenVsx;
using TabStudio.Extensions.Security;
using TabStudio.Testing;
using TabStudio.Testing.Extensions;

namespace TabStudio.Extensions.Tests;

/// <summary>The Open VSX client against a fake registry that answers with responses recorded from open-vsx.org.</summary>
public sealed class OpenVsxClientTests : IDisposable
{
    private static readonly ExtensionIdentifier Perl = new("vscode", "perl");
    private static readonly ExtensionIdentifier Lumen = new(LumenExtension.Publisher, LumenExtension.Name);
    private readonly TempDirectory _temp = new();
    private readonly FakeOpenVsx _registry = new();
    private readonly HttpClient _http;

    public OpenVsxClientTests()
    {
        _http = _registry.CreateClient();
    }

    [Fact]
    public async Task An_empty_search_lists_the_most_downloaded_extensions()
    {
        var client = CreateClient();

        var result = await client.SearchAsync(new OpenVsxQuery(string.Empty, Sort: OpenVsxSort.Downloads, Size: 6));

        Assert.False(result.IsOffline);
        Assert.Equal(20130, result.TotalSize);
        Assert.Equal(6, result.Extensions.Count);
        var first = result.Extensions[0];
        Assert.Equal(new ExtensionIdentifier("meta", "pyrefly"), first.Id);
        Assert.Equal("Pyrefly - Python Language Tooling", first.DisplayName);
        Assert.Equal(92382675, first.DownloadCount);
        Assert.Equal(4.5, first.AverageRating);
        Assert.NotNull(first.IconUrl);
        Assert.True(first.IsVerified);
        var request = Assert.Single(_registry.Requests);
        Assert.StartsWith("/api/-/search?query=&size=6&offset=0&sortBy=downloadCount&sortOrder=desc", request, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_search_reads_entries_with_and_without_ratings_and_icons()
    {
        var client = CreateClient();

        var result = await client.SearchAsync(new OpenVsxQuery("perl"));

        Assert.Equal(229, result.TotalSize);
        Assert.Equal(new[] { "vscode.perl", "tree-sitter-perl.vscode-perl-lsp", "richterger.perl" }, result.Extensions.Select(e => e.Id.ToString()));
        var perl = result.Extensions[0];
        Assert.Equal("Perl Language Basics (built-in)", perl.DisplayName);
        Assert.Equal("1.95.3", perl.Version);
        Assert.Null(perl.AverageRating);
        Assert.Null(perl.IconUrl);
        Assert.Equal(new DateTimeOffset(2024, 12, 10, 11, 10, 9, TimeSpan.Zero), perl.Published!.Value.AddTicks(-(perl.Published.Value.Ticks % TimeSpan.TicksPerSecond)));
        Assert.NotNull(result.Extensions[1].IconUrl);
    }

    [Fact]
    public async Task Categories_and_sort_orders_go_into_the_query()
    {
        var client = CreateClient();

        var themes = await client.SearchAsync(new OpenVsxQuery(string.Empty, "Themes", OpenVsxSort.Downloads, Size: 4));
        await client.SearchAsync(new OpenVsxQuery("a b", Sort: OpenVsxSort.Rating, Offset: 30));

        Assert.Equal("PKief.material-icon-theme", themes.Extensions[0].Id.ToString());
        Assert.Contains("&category=Themes", _registry.Requests[0], StringComparison.Ordinal);
        Assert.Contains("query=a%20b", _registry.Requests[1], StringComparison.Ordinal);
        Assert.Contains("&offset=30&sortBy=rating", _registry.Requests[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task Details_list_the_files_versions_and_publisher()
    {
        var client = CreateClient();

        var perl = await client.GetExtensionAsync(Perl);

        Assert.Equal(Perl, perl.Id);
        Assert.Equal("1.95.3", perl.Version);
        Assert.Equal("Perl Language Basics (built-in)", perl.DisplayName);
        Assert.Equal("vscode", perl.PublisherDisplayName);
        Assert.Equal("Open VSX", perl.PublishedBy);
        Assert.Equal("*", perl.EngineVersion);
        Assert.Equal("universal", perl.TargetPlatform);
        Assert.Equal(new[] { "Programming Languages" }, perl.Categories);
        Assert.Contains("1.95.3", perl.Versions);
        Assert.Contains("1.88.1", perl.Versions);
        Assert.DoesNotContain("latest", perl.Versions);
        Assert.Equal(new Uri("https://open-vsx.org/api/vscode/perl/1.95.3/file/vscode.perl-1.95.3.vsix"), perl.Files.Download);
        Assert.NotNull(perl.Files.Signature);
        Assert.NotNull(perl.Files.PublicKey);
        Assert.NotNull(perl.Files.Readme);
        Assert.True(perl.IsVerified);
        Assert.False(perl.IsDeprecated);
    }

    [Fact]
    public async Task An_unknown_extension_is_not_found()
    {
        var client = CreateClient();

        var error = await Assert.ThrowsAsync<OpenVsxException>(() => client.GetExtensionAsync(new ExtensionIdentifier("vscode", "does-not-exist")));

        Assert.Equal(OpenVsxFailure.NotFound, error.Failure);
    }

    [Fact]
    public async Task The_real_package_is_downloaded_and_its_hash_and_signature_are_verified()
    {
        var client = CreateClient();
        var perl = await client.GetExtensionAsync(Perl);
        var progress = new List<double>();

        var download = await client.DownloadAsync(perl, _temp.Resolve("perl.vsix"), new SynchronousProgress(progress.Add));

        Assert.True(download.ChecksumVerified);
        Assert.True(download.SignatureVerified);
        Assert.Equal("ca0d6d19fe076a4c16cdaa97360ca1aaf63849115d15fe0e957a0d79ab0075c1", download.Sha256);
        Assert.Equal(1.0, progress[^1]);
        Assert.Equal(FakeOpenVsx.PerlVsix, await File.ReadAllBytesAsync(download.Path));
    }

    [Fact]
    public void The_recorded_open_vsx_signature_of_the_real_package_verifies()
    {
        using var archive = new MemoryStream(FakeOpenVsx.Recorded("vscode.perl-1.95.3.sigzip"));
        var signature = PackageVerifier.ReadSignature(archive);
        var key = PackageVerifier.ReadPublicKey(FakeOpenVsx.RecordedText("open-vsx-public-key.pem"));

        using (var package = new MemoryStream(FakeOpenVsx.PerlVsix))
        {
            Assert.True(PackageVerifier.VerifySignature(package, signature, key));
        }

        var tampered = (byte[])FakeOpenVsx.PerlVsix.Clone();
        tampered[100] ^= 0x01;
        using var changed = new MemoryStream(tampered);
        Assert.False(PackageVerifier.VerifySignature(changed, signature, key));
    }

    [Fact]
    public async Task A_package_signed_with_the_registry_key_installs()
    {
        var client = CreateClient();
        using var store = new ExtensionStore(_temp.Resolve("extensions"));

        var lumen = await client.GetExtensionAsync(Lumen);
        var download = await client.DownloadAsync(lumen, store.CreateDownloadPath());
        var installed = await store.InstallAsync(download.Path, new ExtensionSource(ExtensionOrigin.Registry, client.Registry, Sha256: download.Sha256, Verification: PackageVerification.Signature));

        Assert.True(download.SignatureVerified);
        Assert.Equal(Lumen, installed.Id);
        Assert.Equal(PackageVerification.Signature, installed.Source.Verification);
    }

    [Fact]
    public async Task A_damaged_download_is_refused_and_deleted()
    {
        _registry.CorruptsLumenPackage = true;
        var client = CreateClient();
        var lumen = await client.GetExtensionAsync(Lumen);
        var path = _temp.Resolve("lumen.vsix");

        var error = await Assert.ThrowsAsync<OpenVsxException>(() => client.DownloadAsync(lumen, path));

        Assert.Equal(OpenVsxFailure.IntegrityFailed, error.Failure);
        Assert.Contains("does not match", error.Message, StringComparison.Ordinal);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Offline_the_last_results_are_shown_and_marked()
    {
        var cache = _temp.Resolve("cache");
        var online = CreateClient(cache);
        await online.SearchAsync(new OpenVsxQuery("perl"));
        await online.GetExtensionAsync(Perl);

        _registry.IsOffline = true;
        var restarted = CreateClient(cache);
        var results = await restarted.SearchAsync(new OpenVsxQuery("perl"));
        var details = await restarted.GetExtensionAsync(Perl);
        var error = await Assert.ThrowsAsync<OpenVsxException>(() => restarted.SearchAsync(new OpenVsxQuery("never searched")));

        Assert.True(results.IsOffline);
        Assert.Equal(3, results.Extensions.Count);
        Assert.Equal("1.95.3", details.Version);
        Assert.Equal(OpenVsxFailure.Offline, error.Failure);
        Assert.Contains("cannot be reached", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Files_of_a_version_are_downloaded_once()
    {
        var client = CreateClient();
        var perl = await client.GetExtensionAsync(Perl);

        var first = await client.GetTextAsync(perl.Files.Readme!);
        var second = await client.GetTextAsync(perl.Files.Readme!);

        Assert.Equal(first, second);
        Assert.StartsWith("# Built-in extension: perl", first, StringComparison.Ordinal);
        Assert.Equal(1, _registry.Requests.Count(r => r.EndsWith("README.md", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Cancelling_cancels_rather_than_reporting_offline()
    {
        var client = CreateClient();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SearchAsync(new OpenVsxQuery("perl"), cancelled.Token));
    }

    [Fact]
    public async Task Updates_are_found_by_reading_the_latest_versions()
    {
        _registry.LumenVersion = "1.1.0";
        var client = CreateClient();

        var latest = await client.GetLatestAsync([Perl, Lumen, new ExtensionIdentifier("nobody", "nothing")]);

        Assert.Equal(new[] { "1.1.0", "1.95.3" }, latest.OrderBy(e => e.Id.Name, StringComparer.Ordinal).Select(e => e.Version));
        Assert.True(ExtensionVersions.IsNewer(latest.Single(e => e.Id == Lumen).Version, "1.0.0"));
    }

    [Theory]
    [InlineData("https://open-vsx.org", "https://open-vsx.org/")]
    [InlineData("https://open-vsx.org/api", "https://open-vsx.org/")]
    [InlineData("https://intranet.example.com/openvsx/", "https://intranet.example.com/openvsx/")]
    [InlineData("http://localhost:3000/api/", "http://localhost:3000/")]
    public void Registry_addresses_are_normalized(string address, string expected)
    {
        Assert.Equal(new Uri(expected), OpenVsxClient.NormalizeRegistry(new Uri(address)));
    }

    [Fact]
    public async Task A_company_registry_is_asked_instead_of_open_vsx()
    {
        var client = new OpenVsxClient(_http, new Uri("https://intranet.example.com/openvsx"));

        await client.SearchAsync(new OpenVsxQuery("perl"));

        Assert.Equal(new Uri("https://intranet.example.com/openvsx/"), client.Registry);
        Assert.StartsWith("/openvsx/api/-/search?", _registry.Requests[0], StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => OpenVsxClient.NormalizeRegistry(new Uri("ftp://example.com")));
    }

    [Fact]
    public async Task The_real_package_installs_with_its_languages_and_grammars()
    {
        using var store = new ExtensionStore(_temp.Resolve("extensions"));
        await using var package = new MemoryStream(FakeOpenVsx.PerlVsix);

        var installed = await store.InstallAsync(package, new ExtensionSource(ExtensionOrigin.File));
        var set = ContributionSet.From(store.Installed);

        Assert.Equal("Perl Language Basics (built-in)", installed.Manifest.DisplayName);
        Assert.Equal(new[] { "perl", "raku" }, installed.Manifest.Contributes.Languages.Select(l => l.Id));
        Assert.Equal("perl", set.FindLanguageForFile("script.pl")?.Id);
        Assert.Equal("source.perl", set.FindGrammarForLanguage("perl")?.ScopeName);
        Assert.Equal("#", set.FindConfiguration("perl")?.LineComment);
        Assert.Contains("Perl", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(set.FindGrammar("source.perl")!.Path)), StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _http.Dispose();
        _registry.Dispose();
        _temp.Dispose();
    }

    private OpenVsxClient CreateClient(string? cache = null) => new(_http, FakeOpenVsx.Address, cache);

    /// <summary>Reports progress on the calling thread, so the test sees every report before the download returns.</summary>
    private sealed class SynchronousProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
