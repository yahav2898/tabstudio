using System.Net;
using System.Net.Http.Headers;

namespace TabStudio.Toolchains.Tests.Fakes;

/// <summary>
/// A web server in memory: serves files by URL, with range requests, and can break connections, go offline or
/// count requests, so downloads can be tested without a network.
/// </summary>
internal sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _breakAfter = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    private int _activeRequests;

    public int PeakConcurrentRequests { get; private set; }

    /// <summary>Every request, in order: <c>GET url (range)</c>.</summary>
    public List<string> Requests { get; } = [];

    /// <summary>Whether the network is down: every request fails as a name resolution error does.</summary>
    public bool Offline { get; set; }

    /// <summary>A delay before each answer, for tests that cancel in the middle.</summary>
    public TimeSpan Latency { get; set; }

    /// <summary>Serves a file.</summary>
    public void Add(Uri address, byte[] content)
    {
        lock (_gate)
        {
            _files[address.AbsoluteUri] = content;
        }
    }

    /// <summary>Stops serving a file.</summary>
    public void Remove(Uri address)
    {
        lock (_gate)
        {
            _files.Remove(address.AbsoluteUri);
        }
    }

    /// <summary>Makes the next download of a file break after some bytes, as a dropped connection does.</summary>
    public void BreakAfter(Uri address, int bytes)
    {
        lock (_gate)
        {
            _breakAfter[address.AbsoluteUri] = bytes;
        }
    }

    /// <summary>An <see cref="HttpClient"/> over this handler.</summary>
    public HttpClient CreateClient() => new(this, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _activeRequests++;
            PeakConcurrentRequests = Math.Max(PeakConcurrentRequests, _activeRequests);
        }

        try
        {
            if (Latency > TimeSpan.Zero)
            {
                await Task.Delay(Latency, cancellationToken);
            }

            var url = request.RequestUri!.AbsoluteUri;
            var range = request.Headers.Range?.Ranges.FirstOrDefault();
            byte[]? content;
            int? breakAfter;
            lock (_gate)
            {
                Requests.Add($"GET {url}{(range is null ? string.Empty : $" ({range.From}-)")}");
                if (Offline)
                {
                    throw new HttpRequestException(HttpRequestError.NameResolutionError, "No such host is known.");
                }

                _files.TryGetValue(url, out content);
                breakAfter = _breakAfter.Remove(url, out var after) ? after : null;
            }

            if (content is null)
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request };
            }

            var start = (int)(range?.From ?? 0);
            if (start > content.Length)
            {
                return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable) { RequestMessage = request };
            }

            var body = content.AsMemory(start).ToArray();
            var response = new HttpResponseMessage(range is null ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
            {
                RequestMessage = request,
                Content = new StreamContent(new BreakingStream(body, breakAfter)),
            };
            response.Content.Headers.ContentLength = body.Length;
            response.Headers.ETag = new EntityTagHeaderValue($"\"{content.Length}\"");
            if (range is not null)
            {
                response.Content.Headers.ContentRange = new ContentRangeHeaderValue(start, content.Length - 1, content.Length);
            }

            return response;
        }
        finally
        {
            lock (_gate)
            {
                _activeRequests--;
            }
        }
    }

    /// <summary>A body that throws an <see cref="IOException"/> after some bytes, as a dropped connection does.</summary>
    private sealed class BreakingStream(byte[] content, int? breakAfter) : Stream
    {
        private int _position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => content.Length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (breakAfter is { } limit && _position >= limit)
            {
                throw new IOException("The connection was reset.");
            }

            var count = Math.Min(buffer.Length, content.Length - _position);
            if (breakAfter is { } stop)
            {
                count = Math.Min(count, stop - _position);
            }

            content.AsSpan(_position, count).CopyTo(buffer);
            _position += count;
            return count;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromResult(Read(buffer.AsSpan(offset, count)));

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Read(buffer.Span));

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
