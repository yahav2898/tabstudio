using System.Net;
using TabStudio.Testing;
using TabStudio.Web.Serving;

namespace TabStudio.Web.Tests;

/// <summary>
/// Serving a static web site for the web preview: its files, the live reload script in every page, the events that
/// reload a page or swap a saved style sheet, and nothing outside the folder.
/// </summary>
public sealed class StaticSiteServerTests : IAsyncLifetime, IAsyncDisposable
{
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(10);

    private readonly TempDirectory _site = new();
    private readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(20) };
    private StaticSiteServer _server = null!;

    public Task InitializeAsync()
    {
        _site.Write("index.html", "<!DOCTYPE html>\n<html><head><link rel=\"stylesheet\" href=\"css/site.css\"></head><BODY><h1>Hi</h1></BODY></html>\n");
        _site.Write("css/site.css", "h1 { color: teal; }\n");
        _site.Write("about/index.html", "<p>About</p>");
        _site.Write("my page.html", "<p>Spaced</p>");
        _site.Write(".git/config", "[core]\n");
        _server = StaticSiteServer.Start(_site.Path);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Serves_the_index_with_the_live_reload_script_before_the_end_of_its_body()
    {
        using var response = await _http.GetAsync(_server.Address);
        var page = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains($"<h1>Hi</h1><script src=\"/{StaticSiteServer.ScriptPath}\"></script>\n</BODY>", page, StringComparison.Ordinal);
        Assert.StartsWith("127.0.0.1", _server.Address.Authority, StringComparison.Ordinal);

        var script = await _http.GetStringAsync(new Uri(_server.Address, StaticSiteServer.ScriptPath));
        Assert.Contains("new EventSource('/__tabstudio/live-reload')", script, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Serves_files_with_their_types_and_folders_with_their_index()
    {
        using var css = await _http.GetAsync(new Uri(_server.Address, "css/site.css"));
        using var folder = await _http.GetAsync(new Uri(_server.Address, "about"));
        using var headRequest = new HttpRequestMessage(HttpMethod.Head, new Uri(_server.Address, "css/site.css"));
        using var head = await _http.SendAsync(headRequest);
        using var body = new StringContent("x");
        using var post = await _http.PostAsync(_server.Address, body);

        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
        Assert.Equal("h1 { color: teal; }\n", await css.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.MovedPermanently, folder.StatusCode);
        Assert.Equal("/about/", folder.Headers.Location?.OriginalString);
        Assert.Contains("<p>About</p>", await _http.GetStringAsync(new Uri(_server.Address, "about/")), StringComparison.Ordinal);
        Assert.Contains("Spaced", await _http.GetStringAsync(new Uri(_server.Address, "my%20page.html")), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
    }

    [Theory]
    [InlineData("missing.html")]
    [InlineData(".git/config")]
    [InlineData("..%2F..%2Fetc%2Fpasswd")]
    public async Task Answers_404_for_what_the_site_does_not_have_or_must_not_show(string path)
    {
        using var response = await _http.GetAsync(new Uri(_server.Address + path));
        var page = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Not found", page, StringComparison.Ordinal);
        Assert.DoesNotContain("[core]", page, StringComparison.Ordinal);
        Assert.Contains(StaticSiteServer.ScriptPath, page, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Symlinks_cannot_serve_files_outside_the_site_or_a_hidden_folder()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // Windows developer mode or administrator rights are needed to create symlinks.
        }

        using var outside = new TempDirectory();
        var secret = outside.Write("private.txt", "private content");
        File.CreateSymbolicLink(_site.Resolve("leak.txt"), secret);
        Directory.CreateSymbolicLink(_site.Resolve("linked"), outside.Path);
        Directory.CreateSymbolicLink(_site.Resolve("git"), _site.Resolve(".git"));
        Directory.CreateDirectory(_site.Resolve("folder"));
        File.CreateSymbolicLink(_site.Resolve("folder/index.html"), secret);
        foreach (var path in new[] { "leak.txt", "linked/private.txt", "git/config", "folder/" })
        {
            using var response = await _http.GetAsync(new Uri(_server.Address, path));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task A_saved_style_sheet_is_swapped_and_any_other_file_reloads_the_page()
    {
        using var events = await _http.GetAsync(new Uri(_server.Address, StaticSiteServer.EventsPath), HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync());
        Assert.Equal("text/event-stream", events.Content.Headers.ContentType?.MediaType);
        Assert.Equal("retry: 1000", await ReadLineAsync(reader));
        await UiWait.UntilAsync(() => _server.ListeningPages == 1);

        Assert.True(_server.NotifyChanged(_site.Resolve("css/site.css")));
        Assert.Equal("event: css", await NextEventAsync(reader));
        Assert.Equal("data: /css/site.css", await ReadLineAsync(reader));

        Assert.True(_server.NotifyChanged(_site.Resolve("my page.html")));
        Assert.Equal("event: reload", await NextEventAsync(reader));
        Assert.Equal("data: /my%20page.html", await ReadLineAsync(reader));

        using var elsewhere = new TempDirectory();
        Assert.False(_server.NotifyChanged(elsewhere.Write("index.html")));
        Assert.Null(_server.RouteOf(elsewhere.Path));
    }

    [Fact]
    public async Task Stopping_ends_the_pages_event_streams_and_frees_the_port()
    {
        var address = _server.Address;
        using var events = await _http.GetAsync(new Uri(address, StaticSiteServer.EventsPath), HttpCompletionOption.ResponseHeadersRead);
        using var reader = new StreamReader(await events.Content.ReadAsStreamAsync());
        Assert.Equal("retry: 1000", await ReadLineAsync(reader));

        await _server.DisposeAsync();

        Assert.Equal(0, _server.ListeningPages);
        await Assert.ThrowsAnyAsync<HttpRequestException>(() => _http.GetStringAsync(address));
        _server = StaticSiteServer.Start(_site.Path, address.Port);
        Assert.Equal(address, _server.Address);
    }

    public async Task DisposeAsync()
    {
        await _server.DisposeAsync();
        _http.Dispose();
        _site.Dispose();
    }

    ValueTask IAsyncDisposable.DisposeAsync() => new(DisposeAsync());

    private static async Task<string?> ReadLineAsync(StreamReader reader)
    {
        using var timeout = new CancellationTokenSource(EventTimeout);
        return await reader.ReadLineAsync(timeout.Token);
    }

    /// <summary>The next event's first line, past blank lines and keep-alive comments.</summary>
    private static async Task<string?> NextEventAsync(StreamReader reader)
    {
        while (await ReadLineAsync(reader) is { } line)
        {
            if (line.Length > 0 && !line.StartsWith(':'))
            {
                return line;
            }
        }

        return null;
    }

    private static class UiWait
    {
        public static async Task UntilAsync(Func<bool> condition)
        {
            for (var waited = 0; !condition() && waited < 200; waited++)
            {
                await Task.Delay(25);
            }

            Assert.True(condition());
        }
    }
}
