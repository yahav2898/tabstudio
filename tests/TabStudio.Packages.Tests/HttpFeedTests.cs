using System.Net;

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
