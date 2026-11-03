using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using NuGet.Versioning;

namespace TabStudio.Packages.Tests;

/// <summary>
/// An in-memory NuGet V3 feed: a service index and a package base address resource, served to an
/// <see cref="HttpClient"/> without a network.
/// </summary>
internal sealed class FakeNuGetServer : HttpMessageHandler
{
    public const string IndexAddress = "https://packages.test/v3/index.json";
    private const string FlatContainer = "https://packages.test/v3-flatcontainer/";

    private readonly ConcurrentDictionary<string, TestPackage> _packages = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _requests = new();
    private readonly ConcurrentQueue<string> _noCacheRequests = new();
    private readonly ConcurrentQueue<(string Suffix, Func<HttpRequestMessage, HttpResponseMessage> Fault)> _faults = new();
    private readonly Lock _faultGate = new();

    /// <summary>When set, every request fails the way it does without a network.</summary>
    public bool Offline { get; set; }

    /// <summary>When set, every request is answered with this status.</summary>
    public HttpStatusCode? FailWith { get; set; }

    public IReadOnlyList<string> Requests => [.. _requests];

    /// <summary>The addresses of the requests that asked the caches on the way for a fresh answer (<c>Cache-Control: no-cache</c>).</summary>
    public IReadOnlyList<string> NoCacheRequests => [.. _noCacheRequests];

    /// <summary>
    /// Answers the next request whose address ends with <paramref name="suffix"/> with <paramref name="fault"/>, which
    /// may also throw, as a network does; later requests are served normally. Faults added again apply in turn.
    /// </summary>
    public FakeNuGetServer FailNext(string suffix, Func<HttpRequestMessage, HttpResponseMessage> fault)
    {
        _faults.Enqueue((suffix, fault));
        return this;
    }

    /// <summary>A body that sends some bytes and then ends before its length, as a connection that broke off does.</summary>
    public static HttpResponseMessage Truncated() => new(HttpStatusCode.OK) { Content = new StreamContent(new BreakingStream()) };

    public PackageSource Source { get; } = new("packages.test", IndexAddress);

    public FakeNuGetServer Add(params TestPackage[] packages)
    {
        foreach (var package in packages)
        {
            _packages[$"{NuGetNames.Lower(package.Id)}/{NuGetNames.Lower(NuGetVersion.Parse(package.Version))}"] = package;
        }

        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var address = request.RequestUri?.AbsoluteUri ?? string.Empty;
        _requests.Enqueue(address);
        if (request.Headers.CacheControl?.NoCache == true)
        {
            _noCacheRequests.Enqueue(address);
        }

        lock (_faultGate)
        {
            if (_faults.TryPeek(out var next) && address.EndsWith(next.Suffix, StringComparison.Ordinal) && _faults.TryDequeue(out var fault))
            {
                return Task.FromResult(fault.Fault(request));
            }
        }

        if (Offline)
        {
            throw new HttpRequestException("No such host is known. (packages.test:443)");
        }

        if (FailWith is { } status)
        {
            return Task.FromResult(new HttpResponseMessage(status));
        }

        if (address == IndexAddress)
        {
            return Json(new
            {
                version = "3.0.0",
                resources = new object[]
                {
                    new Dictionary<string, string> { ["@id"] = "https://packages.test/query", ["@type"] = "SearchQueryService" },
                    new Dictionary<string, string> { ["@id"] = FlatContainer, ["@type"] = "PackageBaseAddress/3.0.0" },
                },
            });
        }

        var parts = address.StartsWith(FlatContainer, StringComparison.Ordinal) ? address[FlatContainer.Length..].Split('/') : [];
        if (parts.Length == 2 && parts[1] == "index.json")
        {
            var versions = _packages.Where(p => p.Key.StartsWith(parts[0] + "/", StringComparison.Ordinal)).Select(p => p.Key[(parts[0].Length + 1)..]).ToList();
            return versions.Count == 0 ? NotFound() : Json(new { versions });
        }

        if (parts.Length == 3 && _packages.TryGetValue($"{parts[0]}/{parts[1]}", out var package))
        {
            if (parts[2] == $"{parts[0]}.nuspec")
            {
                return Content(Encoding.UTF8.GetBytes(package.CreateNuspec()));
            }

            if (parts[2] == $"{parts[0]}.{parts[1]}.nupkg")
            {
                return Content(package.ToNupkg());
            }
        }

        return NotFound();
    }

    private static Task<HttpResponseMessage> Json(object value) =>
        Content(JsonSerializer.SerializeToUtf8Bytes(value));

    private static Task<HttpResponseMessage> Content(byte[] body) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });

    private static Task<HttpResponseMessage> NotFound() =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));

    /// <summary>A stream that gives a few bytes, then fails the way HttpClient does when a body ends before its length.</summary>
    private sealed class BreakingStream : Stream
    {
        private bool _sent;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_sent)
            {
                throw new HttpIOException(HttpRequestError.ResponseEnded, "The response ended prematurely.");
            }

            _sent = true;
            buffer[offset] = (byte)'{';
            return 1;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
