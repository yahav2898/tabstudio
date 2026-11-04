using System.Net;
using System.Net.Http.Headers;

namespace TabStudio.App.Tests;

/// <summary>An archive download that breaks off where a test asks and resumes with <c>Range</c>, as on a dropped connection.</summary>
internal sealed partial class FakeGitHub
{
    private readonly List<string> _archiveRanges = [];
    private long _archiveBytesServed;

    /// <summary>When set, the next archive download breaks after this many bytes, as a dropped connection does.</summary>
    public long? BreakArchiveAfter { get; set; }

    /// <summary>The <c>Range</c> headers archive downloads asked with, empty for a download from the start.</summary>
    public IReadOnlyList<string> ArchiveRanges
    {
        get
        {
            lock (_archiveRanges)
            {
                return [.. _archiveRanges];
            }
        }
    }

    /// <summary>How many archive bytes the fake sent in all.</summary>
    public long ArchiveBytesServed => Interlocked.Read(ref _archiveBytesServed);

    /// <summary>Answers a <c>Range</c> request for an archive and breaks a download off where a test asks.</summary>
    private HttpResponseMessage ShapeArchive(HttpRequestMessage request, string path, HttpResponseMessage response)
    {
        if (!path.Contains("/zipball/", StringComparison.Ordinal) || response.StatusCode != HttpStatusCode.OK)
        {
            return response;
        }

        var body = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
        var etag = response.Headers.ETag;
        var from = 0L;
        var status = HttpStatusCode.OK;
        lock (_archiveRanges)
        {
            _archiveRanges.Add(request.Headers.Range?.ToString() ?? string.Empty);
        }

        if (request.Headers.Range?.Ranges.FirstOrDefault()?.From is { } start
            && (request.Headers.IfRange?.EntityTag is not { } condition || condition.Equals(etag)))
        {
            if (start >= body.Length)
            {
                return new HttpResponseMessage(HttpStatusCode.RequestedRangeNotSatisfiable);
            }

            from = start;
            status = HttpStatusCode.PartialContent;
        }

        var part = body.AsSpan((int)from).ToArray();
        var limit = BreakArchiveAfter;
        BreakArchiveAfter = null;
        var answer = new HttpResponseMessage(status) { Content = new StreamContent(new BreakingStream(part, limit, n => Interlocked.Add(ref _archiveBytesServed, n))) };
        answer.Content.Headers.ContentLength = part.Length;
        if (status == HttpStatusCode.PartialContent)
        {
            answer.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, body.Length - 1, body.Length);
        }

        if (etag is not null)
        {
            answer.Headers.ETag = etag;
        }

        answer.Headers.AcceptRanges.Add("bytes");
        return answer;
    }

    /// <summary>A body that throws once it has given out a set number of bytes, as a connection that drops.</summary>
    private sealed class BreakingStream(byte[] content, long? breakAfter, Action<int> served) : Stream
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

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (breakAfter is { } limit && _position >= limit)
            {
                throw new IOException("The connection was reset.");
            }

            var available = content.Length - _position;
            if (breakAfter is { } stop)
            {
                available = (int)Math.Min(available, stop - _position);
            }

            var read = Math.Min(count, available);
            Array.Copy(content, _position, buffer, offset, read);
            _position += read;
            served(read);
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
