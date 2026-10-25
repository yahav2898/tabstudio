using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace TabStudio.Testing.Extensions;

/// <summary>
/// An Open VSX registry for tests, answering with responses recorded from open-vsx.org (searches for "perl", the most
/// popular extensions, themes and "monokai", and the details of <c>vscode.perl</c> with its real, signed package), plus
/// the <see cref="LumenExtension"/> fixture, signed with the fake registry's own key.
/// </summary>
public sealed class FakeOpenVsx : HttpMessageHandler
{
    /// <summary>The fake registry's address.</summary>
    public static readonly Uri Address = new("https://open-vsx.test/");

    /// <summary>The identifier of the real extension the recordings describe.</summary>
    public const string PerlId = "vscode.perl";

    /// <summary>The real extension's version.</summary>
    public const string PerlVersion = "1.95.3";

    private const string LumenKeyId = "lumen-test-key";
    private static readonly Lazy<byte[]> PerlPackage = new(() => Recorded("vscode.perl-1.95.3.vsix"));
    private readonly Ed25519TestSigner _signer = Ed25519TestSigner.FromSeed("open-vsx-test");
    private readonly Dictionary<string, byte[]> _lumenPackages = new(StringComparer.Ordinal);

    /// <summary>The Lumen version the registry offers; tests raise it to offer an update.</summary>
    public string LumenVersion { get; set; } = "1.0.0";

    /// <summary>Whether the registry cannot be reached, as without a connection.</summary>
    public bool IsOffline { get; set; }

    /// <summary>Whether the Lumen package is sent damaged, so its hash and signature do not match.</summary>
    public bool CorruptsLumenPackage { get; set; }

    /// <summary>The paths and queries of the requests received, in order.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>A recorded file's bytes.</summary>
    public static byte[] Recorded(string name)
    {
        using var stream = typeof(FakeOpenVsx).Assembly.GetManifestResourceStream("Recorded/" + name)
            ?? throw new InvalidOperationException($"The recording '{name}' is missing.");
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    /// <summary>A recorded text file.</summary>
    public static string RecordedText(string name) => Encoding.UTF8.GetString(Recorded(name));

    /// <summary>The real, signed package of <c>vscode.perl</c>, as Open VSX serves it.</summary>
    public static byte[] PerlVsix => PerlPackage.Value;

    /// <summary>An HTTP client that talks to this registry.</summary>
    public HttpClient CreateClient() => new(this, disposeHandler: false);

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var uri = request.RequestUri!;
        lock (Requests)
        {
            Requests.Add(uri.PathAndQuery);
        }

        if (IsOffline)
        {
            throw new HttpRequestException("No such host is known. (open-vsx.test:443)");
        }

        return Task.FromResult(Answer(uri));
    }

    private HttpResponseMessage Answer(Uri uri)
    {
        var path = Uri.UnescapeDataString(uri.AbsolutePath);

        // A company registry may live under a path, such as https://intranet.example.com/openvsx/api/.
        var api = path.IndexOf("/api/", StringComparison.Ordinal);
        if (api > 0)
        {
            path = path[api..];
        }

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        if (path == "/api/-/search")
        {
            return Json(Search(query["query"] ?? string.Empty, query["category"]));
        }

        if (path == "/api/vscode/perl" || path == $"/api/vscode/perl/{PerlVersion}")
        {
            return Json(RecordedText("extension-vscode-perl.json"));
        }

        if (path == $"/api/{LumenExtension.Publisher}/{LumenExtension.Name}" || path == $"/api/{LumenExtension.Publisher}/{LumenExtension.Name}/{LumenVersion}")
        {
            return Json(LumenDetails().ToJsonString());
        }

        var file = path.EndsWith("/file/README.md", StringComparison.Ordinal) ? "README.md" : Path.GetFileName(path);
        return (path, file) switch
        {
            (_, "vscode.perl-1.95.3.vsix") => Bytes(PerlVsix, "application/octet-stream"),
            (_, "vscode.perl-1.95.3.sha256") => Bytes(Recorded("vscode.perl-1.95.3.sha256"), "text/plain"),
            (_, "vscode.perl-1.95.3.sigzip") => Bytes(Recorded("vscode.perl-1.95.3.sigzip"), "application/zip"),
            (_, "14ccb407-4e79-41ed-be5a-6d608325c45a") => Bytes(Recorded("open-vsx-public-key.pem"), "text/plain"),
            (_, LumenKeyId) => Bytes(Encoding.UTF8.GetBytes(_signer.PublicKeyPem), "text/plain"),
            _ when file.EndsWith(".vsix", StringComparison.Ordinal) && path.Contains(LumenExtension.Name, StringComparison.Ordinal) => Bytes(LumenPackage(sent: true), "application/octet-stream"),
            _ when file.EndsWith(".sha256", StringComparison.Ordinal) && path.Contains(LumenExtension.Name, StringComparison.Ordinal) => Bytes(Encoding.UTF8.GetBytes(Sha256(LumenPackage(sent: false))), "text/plain"),
            _ when file.EndsWith(".sigzip", StringComparison.Ordinal) && path.Contains(LumenExtension.Name, StringComparison.Ordinal) => Bytes(_signer.SignatureArchive(LumenPackage(sent: false)), "application/zip"),
            (_, "README.md") when path.Contains("/vscode/perl/", StringComparison.Ordinal) => Bytes(Encoding.UTF8.GetBytes(PerlReadme()), "text/markdown"),
            (_, "README.md") when path.Contains(LumenExtension.Name, StringComparison.Ordinal) => Bytes(Encoding.UTF8.GetBytes(LumenExtension.Readme), "text/markdown"),
            (_, "CHANGELOG.md") => Bytes(Encoding.UTF8.GetBytes("# Changelog\n\n## 1.1.0\n\n- New snippets\n\n## 1.0.0\n\n- First release\n"), "text/markdown"),
            (_, "package.json" or "package.nls.json") when path.Contains("/vscode/perl/", StringComparison.Ordinal) => Bytes(PerlFile(file), "application/json"),
            (_, "package.json") when path.Contains(LumenExtension.Name, StringComparison.Ordinal) => Bytes(Encoding.UTF8.GetBytes(LumenExtension.PackageJson(LumenVersion)), "application/json"),
            (_, "package.nls.json") when path.Contains(LumenExtension.Name, StringComparison.Ordinal) => Bytes(Encoding.UTF8.GetBytes(LumenExtension.PackageNls), "application/json"),
            _ when file.EndsWith(".png", StringComparison.Ordinal) => Bytes(path.Contains(LumenExtension.Name, StringComparison.Ordinal) ? LumenExtension.IconPng : TestIcons.Create(path), "image/png"),
            _ => NotFound(path),
        };
    }

    private string Search(string text, string? category)
    {
        if (category == "Themes")
        {
            return RecordedText("search-themes.json");
        }

        return text.Trim().ToLowerInvariant() switch
        {
            "" => RecordedText("search-popular.json"),
            "perl" => RecordedText("search-perl.json"),
            "monokai" => RecordedText("search-monokai.json"),
            "lumen" => new JsonObject
            {
                ["offset"] = 0,
                ["totalSize"] = 1,
                ["extensions"] = new JsonArray(LumenSummary()),
            }.ToJsonString(),
            _ => """{ "offset": 0, "totalSize": 0, "extensions": [] }""",
        };
    }

    private JsonObject LumenSummary() => new()
    {
        ["url"] = Api($"{LumenExtension.Publisher}/{LumenExtension.Name}"),
        ["files"] = LumenFiles(),
        ["name"] = LumenExtension.Name,
        ["namespace"] = LumenExtension.Publisher,
        ["version"] = LumenVersion,
        ["timestamp"] = "2026-10-01T09:30:00.000000Z",
        ["verified"] = true,
        ["averageRating"] = 4.5,
        ["reviewCount"] = 12,
        ["downloadCount"] = 4321,
        ["displayName"] = LumenExtension.DisplayName,
        ["description"] = "Syntax coloring, snippets and themes for Lumen.",
        ["deprecated"] = false,
    };

    private JsonObject LumenDetails()
    {
        var details = LumenSummary();
        details["namespaceDisplayName"] = "TabStudio Tests";
        details["engines"] = new JsonObject { ["vscode"] = "^1.80.0" };
        details["categories"] = new JsonArray("Programming Languages", "Snippets", "Themes");
        details["license"] = "MIT";
        details["repository"] = "https://github.com/tabstudio-tests/lumen";
        details["targetPlatform"] = "universal";
        details["publishedBy"] = new JsonObject { ["loginName"] = "lumen-dev", ["fullName"] = "Lumen Developers" };
        details["allVersions"] = new JsonObject { ["latest"] = Api("x"), [LumenVersion] = Api("x"), ["1.0.0"] = Api("x") };
        details["extensionKind"] = new JsonArray("ui", "workspace", "web");
        return details;
    }

    private JsonObject LumenFiles()
    {
        var root = $"{LumenExtension.Publisher}/{LumenExtension.Name}/{LumenVersion}/file/";
        var package = $"{LumenExtension.Publisher}.{LumenExtension.Name}-{LumenVersion}";
        return new JsonObject
        {
            ["download"] = Api(root + package + ".vsix"),
            ["sha256"] = Api(root + package + ".sha256"),
            ["signature"] = Api(root + package + ".sigzip"),
            ["publicKey"] = Api("-/public-key/" + LumenKeyId),
            ["icon"] = Api(root + "images/icon.png"),
            ["readme"] = Api(root + "README.md"),
            ["changelog"] = Api(root + "CHANGELOG.md"),
            ["manifest"] = Api(root + "package.json"),
        };
    }

    /// <summary>The Lumen package of the offered version, as signed, or as sent when it is to arrive damaged.</summary>
    private byte[] LumenPackage(bool sent)
    {
        if (!_lumenPackages.TryGetValue(LumenVersion, out var package))
        {
            package = LumenExtension.Build(LumenVersion);
            _lumenPackages[LumenVersion] = package;
        }

        if (sent && CorruptsLumenPackage)
        {
            package = (byte[])package.Clone();
            package[^20] ^= 0xFF;
        }

        return package;
    }

    private static string PerlReadme() => Encoding.UTF8.GetString(PerlFile("README.md"));

    /// <summary>A file of the real Perl package, such as its package.json, as the registry's file API serves it.</summary>
    private static byte[] PerlFile(string name)
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(PerlVsix));
        using var stream = zip.GetEntry("extension/" + name)!.Open();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static string Api(string path) => new Uri(Address, "api/" + path).AbsoluteUri;

    private static string Sha256(byte[] bytes) => Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));

    private static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static HttpResponseMessage Bytes(byte[] bytes, string type)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(type);
        content.Headers.ContentLength = bytes.Length;
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static HttpResponseMessage NotFound(string path) => new(HttpStatusCode.NotFound)
    {
        Content = new StringContent(
            string.Create(CultureInfo.InvariantCulture, $$"""{ "error": "Not found: {{path}}" }"""),
            Encoding.UTF8,
            "application/json"),
    };
}
