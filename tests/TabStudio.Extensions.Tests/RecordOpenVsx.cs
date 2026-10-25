using System.Text;
using TabStudio.Extensions.Security;

namespace TabStudio.Extensions.Tests;

// TEMPORARY: records real Open VSX answers on CI into ci-out, to build the recorded fake registry from. Removed once recorded.
public sealed class RecordOpenVsx
{
    [Fact]
    public async Task Record_real_answers_on_ci()
    {
        if (Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
        {
            return;
        }

        var root = AppContext.BaseDirectory;
        while (root is not null && !File.Exists(Path.Combine(root, "TabStudio.slnx")))
        {
            root = Path.GetDirectoryName(root.TrimEnd(Path.DirectorySeparatorChar));
        }

        Assert.NotNull(root);
        var output = new StringBuilder();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TabStudio/0.4 (+https://github.com/yahav2898/tabstudio)");

        async Task<(byte[] Body, string? ETag)> Get(string url, string? etag = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url));
            if (etag is not null)
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", etag);
            }

            using var response = await http.SendAsync(request);
            var body = await response.Content.ReadAsByteArrayAsync();
            output.AppendLine($"=== GET {url} -> {(int)response.StatusCode} etag={response.Headers.ETag} type={response.Content.Headers.ContentType} cache={response.Headers.CacheControl} length={body.Length} final={response.RequestMessage?.RequestUri}");
            return (body, response.Headers.ETag?.ToString());
        }

        void Text(byte[] body) => output.AppendLine(Encoding.UTF8.GetString(body));

        Text((await Get("https://open-vsx.org/api/-/search?query=perl&size=3&offset=0&sortBy=relevance&sortOrder=desc&includeAllVersions=false")).Body);
        Text((await Get("https://open-vsx.org/api/-/search?query=&size=6&offset=0&sortBy=downloadCount&sortOrder=desc&includeAllVersions=false")).Body);
        Text((await Get("https://open-vsx.org/api/-/search?query=&size=4&offset=0&sortBy=downloadCount&sortOrder=desc&category=Themes&includeAllVersions=false")).Body);
        var (details, etag) = await Get("https://open-vsx.org/api/vscode/perl");
        Text(details);
        await Get("https://open-vsx.org/api/vscode/perl", etag);
        Text((await Get("https://open-vsx.org/api/vscode/does-not-exist-tabstudio")).Body);
        Text((await Get("https://open-vsx.org/api/vscode/perl/versions?size=5")).Body);
        var sha = (await Get("https://open-vsx.org/api/vscode/perl/1.95.3/file/vscode.perl-1.95.3.sha256")).Body;
        Text(sha);
        var vsix = (await Get("https://open-vsx.org/api/vscode/perl/1.95.3/file/vscode.perl-1.95.3.vsix")).Body;
        var sigzip = (await Get("https://open-vsx.org/api/vscode/perl/1.95.3/file/vscode.perl-1.95.3.sigzip")).Body;
        var json = Encoding.UTF8.GetString(details);
        var keyStart = json.IndexOf("\"publicKey\":\"", StringComparison.Ordinal) + 13;
        var keyUrl = json[keyStart..json.IndexOf('"', keyStart)];
        var pem = (await Get(keyUrl)).Body;
        Text(pem);
        using (var stream = new MemoryStream(vsix))
        {
            output.AppendLine($"sha256 computed={PackageVerifier.ComputeSha256(stream)}");
        }

        using (var archive = new MemoryStream(sigzip))
        using (var package = new MemoryStream(vsix))
        {
            var signature = PackageVerifier.ReadSignature(archive);
            var key = PackageVerifier.ReadPublicKey(Encoding.UTF8.GetString(pem));
            output.AppendLine($"signature verified={PackageVerifier.VerifySignature(package, signature, key)}");
        }

        output.AppendLine("vsix base64:");
        output.AppendLine(Convert.ToBase64String(vsix));
        output.AppendLine("sigzip base64:");
        output.AppendLine(Convert.ToBase64String(sigzip));
        Text((await Get("https://open-vsx.org/api/-/search?query=monokai&size=3&sortBy=relevance&sortOrder=desc")).Body);

        Directory.CreateDirectory(Path.Combine(root!, "ci-out"));
        await File.WriteAllTextAsync(Path.Combine(root!, "ci-out", "openvsx-recording.txt"), output.ToString());
    }
}
