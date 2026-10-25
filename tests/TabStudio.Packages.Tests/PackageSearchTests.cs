using System.Net;
using System.Text;
using TabStudio.Packages.Search;

namespace TabStudio.Packages.Tests;

public sealed class PackageSearchTests
{
    private const string Index = """
        {
          "version": "3.0.0",
          "resources": [
            { "@id": "https://search.test/old", "@type": "SearchQueryService" },
            { "@id": "https://search.test/query", "@type": "SearchQueryService/3.5.0" },
            { "@id": "https://search.test/flat/", "@type": "PackageBaseAddress/3.0.0" }
          ]
        }
        """;

    private const string Results = """
        {
          "totalHits": 2,
          "data": [
            {
              "id": "Newtonsoft.Json",
              "version": "13.0.3",
              "description": "Json.NET is a popular high-performance JSON framework for .NET",
              "authors": ["James Newton-King"],
              "totalDownloads": 5000000000,
              "verified": true,
              "iconUrl": "https://search.test/icon.png",
              "projectUrl": "https://www.newtonsoft.com/json",
              "versions": [ { "version": "12.0.3" }, { "version": "13.0.1" }, { "version": "13.0.3" } ]
            },
            { "id": "Broken" },
            {
              "id": "Humanizer.Core",
              "version": "2.14.1",
              "authors": "Mehdi Khalili, Claire Novotny",
              "totalDownloads": 100
            }
          ]
        }
        """;

    [Fact]
    public async Task Finds_packages_with_their_details_through_the_newest_search_service()
    {
        var server = new Server();
        var search = new PackageSearch(new HttpClient(server), new PackageSource("test", "https://search.test/index.json"));

        var page = await search.SearchAsync(" json ", includePrerelease: true, skip: 25, take: 500);

        Assert.Equal("https://search.test/query?q=json&skip=25&take=100&prerelease=true&semVerLevel=2.0.0", server.Requests[^1]);
        Assert.Equal(2, page.TotalHits);
        Assert.Equal(new[] { "Newtonsoft.Json", "Humanizer.Core" }, page.Results.Select(r => r.Id));
        var json = page.Results[0];
        Assert.Equal("13.0.3", json.Version);
        Assert.Equal(new[] { "13.0.3", "13.0.1", "12.0.3" }, json.Versions);
        Assert.Equal(5_000_000_000, json.TotalDownloads);
        Assert.True(json.IsVerified);
        Assert.Equal(new Uri("https://www.newtonsoft.com/json"), json.ProjectUrl);
        Assert.Equal(new[] { "Mehdi Khalili", "Claire Novotny" }, page.Results[1].Authors);
        Assert.Equal(new[] { "2.14.1" }, page.Results[1].Versions);

        await search.SearchAsync("again");
        Assert.Single(server.Requests, r => r.EndsWith("index.json", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Finds_one_package_by_id()
    {
        var search = new PackageSearch(new HttpClient(new Server()), new PackageSource("test", "https://search.test/index.json"));

        var package = await search.FindAsync("newtonsoft.json");

        Assert.NotNull(package);
        Assert.Equal("Newtonsoft.Json", package.Id);
    }

    [Fact]
    public async Task Failures_are_explained()
    {
        var offline = new PackageSearch(new HttpClient(new Server { Offline = true }), new PackageSource("test", "https://search.test/index.json"));
        var error = await Assert.ThrowsAsync<PackageSearchException>(() => offline.SearchAsync("x"));
        Assert.Contains("Could not reach test", error.Message, StringComparison.Ordinal);

        var noSearch = new PackageSearch(new HttpClient(new Server { IndexWithoutSearch = true }), new PackageSource("test", "https://search.test/index.json"));
        var missing = await Assert.ThrowsAsync<PackageSearchException>(() => noSearch.SearchAsync("x"));
        Assert.Contains("no search service", missing.Message, StringComparison.Ordinal);
    }

    private sealed class Server : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public bool Offline { get; init; }

        public bool IndexWithoutSearch { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            Requests.Add(url);
            if (Offline)
            {
                throw new HttpRequestException("No such host is known.");
            }

            var body = url switch
            {
                "https://search.test/index.json" => IndexWithoutSearch ? """{ "version": "3.0.0", "resources": [] }""" : Index,
                _ when url.StartsWith("https://search.test/query?", StringComparison.Ordinal) => Results,
                _ => null,
            };

            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
