using System.Net;
using System.Net.Sockets;
using TabStudio.Core.Diagnostics;

namespace TabStudio.Packages.Tests;

public sealed class HttpFeedTests : IDisposable
{
    private readonly RestoreFixture _fixture = new();
    private readonly FakeNuGetServer _server = new();
    private readonly HttpClient _http;

    public HttpFeedTests()
    {
        _http = new HttpClient(_server);
    }

    [Fact]
    public async Task Restores_through_the_v3_flat_container_with_lower_case_addresses()
    {
        _server.Add(
            new TestPackage("Web.Package", "2.0.0-RC").DependsOn("Web.Dependency", "1.0.0").WithAssembly("lib/net8.0", "Web.Package"),
            new TestPackage("Web.Dependency", "1.0.0").WithAssembly("lib/netstandard2.0", "Web.Dependency"));

        var result = await RestoreAsync(("Web.Package", "2.0.0-RC"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(new[] { "Web.Dependency", "Web.Package" }, result.Packages.Select(p => p.Id));
        Assert.Contains(FakeNuGetServer.IndexAddress, _server.Requests);
        Assert.Contains("https://packages.test/v3-flatcontainer/web.package/index.json", _server.Requests);
        Assert.Contains("https://packages.test/v3-flatcontainer/web.package/2.0.0-rc/web.package.nuspec", _server.Requests);
        Assert.Contains("https://packages.test/v3-flatcontainer/web.package/2.0.0-rc/web.package.2.0.0-rc.nupkg", _server.Requests);
        Assert.Single(_server.Requests, r => r == FakeNuGetServer.IndexAddress);
    }

    [Fact]
    public async Task An_unreachable_source_reports_TS3001_with_a_clear_message()
    {
        _server.Offline = true;

        var result = await RestoreAsync(("Anything", "1.0.0"));

        Assert.False(result.Success);
        var error = Assert.Single(result.Diagnostics);
        Assert.Equal(PackageDiagnosticIds.NetworkUnreachable, error.Id);
        Assert.Contains("Could not reach packages.test", error.Message, StringComparison.Ordinal);
        Assert.Contains("internet connection", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_server_error_reports_NU1301()
    {
        _server.FailWith = HttpStatusCode.ServiceUnavailable;

        var result = await RestoreAsync(("Anything", "1.0.0"));

        Assert.False(result.Success);
        Assert.Equal(PackageDiagnosticIds.SourceFailed, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public async Task A_304_to_a_plain_GET_is_asked_again_past_the_caches_instead_of_failing_with_NU1301()
    {
        // nuget.org's CDN answered 304 Not Modified to a GET that had no If-None-Match, and Moq's restore failed (NU1301).
        const string versions = "https://packages.test/v3-flatcontainer/cdn.package/index.json";
        _server.Add(new TestPackage("Cdn.Package", "1.0.0").WithAssembly("lib/net8.0"));
        _server.FailNext("cdn.package/index.json", _ => new HttpResponseMessage(HttpStatusCode.NotModified));

        var result = await RestoreAsync(("Cdn.Package", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(2, _server.Requests.Count(r => r == versions));
        Assert.Equal(new[] { versions }, _server.NoCacheRequests);
    }

    [Fact]
    public async Task A_connection_reset_once_is_retried_instead_of_failing_with_TS3001()
    {
        // RestSharp's restore failed with TS3001 when one request's connection was reset.
        _server.Add(new TestPackage("Reset.Package", "1.0.0").WithAssembly("lib/net8.0"));
        _server.FailNext(".nupkg", _ => throw new HttpRequestException(
            HttpRequestError.ConnectionError,
            "An error occurred while sending the request.",
            new IOException("Connection reset by peer", new SocketException((int)SocketError.ConnectionReset))));

        var result = await RestoreAsync(("Reset.Package", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(2, _server.Requests.Count(r => r.EndsWith(".nupkg", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Server_errors_rate_limits_and_timeouts_are_retried()
    {
        _server.Add(new TestPackage("Busy.Package", "1.0.0").WithAssembly("lib/net8.0"));
        _server.FailNext(".nuspec", _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
            .FailNext(".nuspec", _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Headers = { RetryAfter = new(TimeSpan.Zero) } })
            .FailNext(".nuspec", _ => new HttpResponseMessage(HttpStatusCode.RequestTimeout));

        var result = await RestoreAsync(("Busy.Package", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.Equal(4, _server.Requests.Count(r => r.EndsWith(".nuspec", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_download_cut_short_is_downloaded_again()
    {
        _server.Add(new TestPackage("Truncated.Package", "1.0.0").WithAssembly("lib/net8.0", "Truncated.Package"));
        _server.FailNext(".nupkg", _ => FakeNuGetServer.Truncated());

        var result = await RestoreAsync(("Truncated.Package", "1.0.0"));

        Assert.True(result.Success, Describe(result));
        Assert.True(File.Exists(Assert.Single(result.CompileAssemblies)));
    }

    [Fact]
    public async Task A_source_whose_name_does_not_resolve_is_not_asked_again()
    {
        // Offline: no retry brings the network back, and an offline restore must not wait for one.
        _server.FailNext("index.json", _ => throw new HttpRequestException(HttpRequestError.NameResolutionError, "Name or service not known (packages.test:443)"));

        var result = await RestoreAsync(("Anything", "1.0.0"));

        Assert.Equal(PackageDiagnosticIds.NetworkUnreachable, Assert.Single(result.Diagnostics).Id);
        Assert.Single(_server.Requests);
    }

    [Fact]
    public async Task A_restore_that_settled_for_installed_versions_while_the_source_failed_is_not_remembered()
    {
        _server.Add(new TestPackage("Floating", "1.0.0").WithAssembly("lib/net8.0", "Floating"));
        var restorer = _fixture.CreateRestorer(_fixture.Options(_server.Source), _http);
        var installed = await restorer.RestoreAsync(_fixture.Project("First", ("Floating", "1.0.0")));
        Assert.True(installed.Success, Describe(installed));

        // The source fails: the installed 1.0.0 is used, with a warning.
        var project = _fixture.Project("Second", ("Floating", "1.*"));
        _server.FailWith = HttpStatusCode.ServiceUnavailable;
        var offline = await restorer.RestoreAsync(project);
        Assert.True(offline.Success, Describe(offline));
        Assert.Contains(offline.Diagnostics, d => d.Id == PackageDiagnosticIds.SourceFailed && d.Severity == DiagnosticSeverityLevel.Warning);
        Assert.False(File.Exists(PackageRestorer.GetCacheFilePath(project)));

        // Back online, the restore asks the source again and gets the version NuGet picks.
        _server.FailWith = null;
        _server.Add(new TestPackage("Floating", "1.1.0").WithAssembly("lib/net8.0", "Floating"));
        var online = await restorer.RestoreAsync(project);
        Assert.False(online.FromCache);
        Assert.Equal("1.1.0", Assert.Single(online.Packages).Version);
    }

    [Fact]
    public async Task A_package_the_feed_does_not_have_reports_NU1101()
    {
        _server.Add(new TestPackage("Other", "1.0.0"));

        var result = await RestoreAsync(("Missing", "1.0.0"));

        Assert.Equal(PackageDiagnosticIds.PackageNotFound, Assert.Single(result.Diagnostics).Id);
    }

    [Fact]
    public async Task A_second_restore_with_the_same_inputs_comes_from_the_cache_without_the_network()
    {
        _server.Add(new TestPackage("Cached", "1.0.0").WithAssembly("lib/net8.0"));
        var project = _fixture.Project(("Cached", "1.0.0"));
        var restorer = _fixture.CreateRestorer(_fixture.Options(_server.Source), _http);

        var first = await restorer.RestoreAsync(project);
        var requests = _server.Requests.Count;
        _server.Offline = true;
        var second = await restorer.RestoreAsync(project);

        Assert.True(first.Success, Describe(first));
        Assert.False(first.FromCache);
        Assert.True(second.FromCache);
        Assert.True(second.Success, Describe(second));
        Assert.Equal(requests, _server.Requests.Count);
        Assert.Equal(first.CompileAssemblies, second.CompileAssemblies);
        Assert.Equal(first.RuntimeAssemblies, second.RuntimeAssemblies);
        Assert.True(File.Exists(PackageRestorer.GetCacheFilePath(project)));
    }

    [Fact]
    public async Task Changing_a_version_restores_again()
    {
        _server.Add(new TestPackage("Changing", "1.0.0").WithAssembly("lib/net8.0"), new TestPackage("Changing", "2.0.0").WithAssembly("lib/net8.0"));
        var restorer = _fixture.CreateRestorer(_fixture.Options(_server.Source), _http);

        await restorer.RestoreAsync(_fixture.Project(("Changing", "1.0.0")));
        var second = await restorer.RestoreAsync(_fixture.Project(("Changing", "2.0.0")));

        Assert.False(second.FromCache);
        Assert.Equal("2.0.0", Assert.Single(second.Packages).Version);
    }

    [Fact]
    public async Task A_deleted_package_folder_is_restored_again_instead_of_served_from_the_cache()
    {
        _server.Add(new TestPackage("Deleted", "1.0.0").WithAssembly("lib/net8.0"));
        var project = _fixture.Project(("Deleted", "1.0.0"));
        var restorer = _fixture.CreateRestorer(_fixture.Options(_server.Source), _http);

        var first = await restorer.RestoreAsync(project);
        Directory.Delete(Assert.Single(first.Packages).DirectoryPath, recursive: true);
        var second = await restorer.RestoreAsync(project);

        Assert.False(second.FromCache);
        Assert.True(second.Success, Describe(second));
        Assert.True(File.Exists(second.CompileAssemblies[0]));
    }

    [Fact]
    public async Task Packages_already_on_the_device_restore_while_offline()
    {
        _server.Add(new TestPackage("Offline", "1.0.0").DependsOn("Offline.Dependency", "1.0.0").WithAssembly("lib/net8.0"),
            new TestPackage("Offline.Dependency", "1.0.0").WithAssembly("lib/net8.0", "Offline.Dependency"));
        var options = _fixture.Options(_server.Source) with { UseRestoreCache = false };
        var restorer = _fixture.CreateRestorer(options, _http);
        var project = _fixture.Project(("Offline", "1.0.0"));

        var online = await restorer.RestoreAsync(project);
        _server.Offline = true;
        var offline = await restorer.RestoreAsync(project);

        Assert.True(online.Success, Describe(online));
        Assert.True(offline.Success, Describe(offline));
        Assert.Equal(online.CompileAssemblies, offline.CompileAssemblies);
    }

    public void Dispose()
    {
        _http.Dispose();
        _server.Dispose();
        _fixture.Dispose();
    }

    private Task<RestoreResult> RestoreAsync(params (string Id, string? Version)[] references) =>
        _fixture.CreateRestorer(_fixture.Options(_server.Source), _http).RestoreAsync(_fixture.Project(references));

    private static string Describe(RestoreResult result) => string.Join('\n', result.Diagnostics);
}
