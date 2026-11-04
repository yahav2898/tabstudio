using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace TabStudio.SourceControl.Tests;

/// <summary>
/// The fake's Git side: GitHub's Git server for file contents by name (protocol version 2, as large repositories are
/// downloaded), Git LFS uploads, an archive that breaks off and resumes with <c>Range</c>, and the API's 100 MB blob limit.
/// </summary>
internal sealed partial class FakeGitHub
{
    private readonly List<string> _archiveRanges = [];
    private long _archiveBytesServed;
    private int _uploadPackFetches;
    private int _wantedObjects;

    /// <summary>When set, blobs larger than this are refused by the API as GitHub refuses blobs past 100 MB.</summary>
    public long? BlobApiLimit { get; set; }

    /// <summary>When set, archives claim this size, as one past GitHub's 1 GB limit does.</summary>
    public long? ArchiveClaimsBytes { get; set; }

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

    /// <summary>How many fetches GitHub's Git server answered.</summary>
    public int UploadPackFetches => Volatile.Read(ref _uploadPackFetches);

    /// <summary>How many objects the fetches asked for in all.</summary>
    public int WantedObjects => Volatile.Read(ref _wantedObjects);

    /// <summary>Moves a submodule's pin in a new commit on the default branch.</summary>
    public string PinSubmodule(string name, string path, string commit) => AddEntry(name, path, "160000", commit);

    /// <summary>The content of a Git LFS object the fake holds; <see langword="null"/> when it has none.</summary>
    public byte[]? LfsObject(string oid) => _lfs.TryGetValue(oid, out var content) ? content : null;

    /// <summary>Adds a file in a new commit on the default branch, with its exact bytes.</summary>
    public string PushBytes(string name, string path, byte[] content)
    {
        var sha = GitObjects.BlobHash(content);
        lock (_gate)
        {
            _repos[$"{Login}/{name}"].Blobs[sha] = content;
        }

        return AddEntry(name, path, "100644", sha);
    }

    private HttpResponseMessage? TooLargeBlob(Repo repo, string sha) =>
        BlobApiLimit is { } limit && repo.Blobs.TryGetValue(sha, out var content) && content.Length > limit
            ? Error(HttpStatusCode.Forbidden, "This API returns blobs up to 100 MB in size. The requested blob is too large to fetch via the API, but you can use the Git Data API to request blobs up to 100 MB in size.")
            : null;

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
        answer.Content.Headers.ContentLength = ArchiveClaimsBytes ?? part.Length;
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

    /// <summary>GitHub's Git server for file contents by name, and Git LFS uploads.</summary>
    private async Task<HttpResponseMessage?> HandleGitAsync(HttpRequestMessage request, string path, CancellationToken cancellationToken)
    {
        var host = request.RequestUri!.Host;
        if (string.Equals(host, LfsStorageHost, StringComparison.OrdinalIgnoreCase) && request.Method == HttpMethod.Put)
        {
            _lfs[path] = await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        if (!string.Equals(host, WebHost, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (path.EndsWith(".git/info/lfs/objects/batch", StringComparison.Ordinal) && request.Content is not null)
        {
            var text = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var body = JsonNode.Parse(text)!;
            if (body["operation"]?.GetValue<string>() != "upload")
            {
                // Read again by the download handler.
                request.Content = new StringContent(text, Encoding.UTF8, "application/vnd.git-lfs+json");
                return null;
            }

            if (request.Headers.Authorization?.Scheme != "Basic")
            {
                return Error(HttpStatusCode.Unauthorized, "Credentials needed");
            }

            var objects = new JsonArray();
            foreach (var item in body["objects"]!.AsArray())
            {
                var oid = item!["oid"]!.GetValue<string>();
                var answer = new JsonObject { ["oid"] = oid, ["size"] = item["size"]!.GetValue<long>() };
                if (!_lfs.ContainsKey(oid))
                {
                    answer["actions"] = new JsonObject { ["upload"] = new JsonObject { ["href"] = $"https://{LfsStorageHost}/{oid}", ["header"] = new JsonObject { ["X-Signed"] = "yes" } } };
                }

                objects.Add(answer);
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new JsonObject { ["transfer"] = "basic", ["objects"] = objects }.ToJsonString(), Encoding.UTF8, "application/vnd.git-lfs+json") };
        }

        var parts = path.Split('/');
        if (parts.Length < 3 || !parts[1].EndsWith(".git", StringComparison.Ordinal) || !_repos.TryGetValue($"{parts[0]}/{parts[1][..^4]}", out var repo))
        {
            return null;
        }

        var rest = string.Join('/', parts.Skip(2));
        if (rest == "info/refs" && request.Method == HttpMethod.Get)
        {
            using var advert = new MemoryStream();
            foreach (var line in new[] { "version 2", "agent=fake-github", "ls-refs", "fetch=shallow filter", "object-format=sha1" })
            {
                Pkt(advert, line + "\n");
            }

            advert.Write("0000"u8);
            return GitAnswer(advert.ToArray(), "application/x-git-upload-pack-advertisement");
        }

        if (rest != "git-upload-pack" || request.Method != HttpMethod.Post)
        {
            return null;
        }

        var lines = ReadPktLines(await request.Content!.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false));
        using var output = new MemoryStream();
        if (lines.Contains("command=ls-refs"))
        {
            lock (_gate)
            {
                foreach (var (branch, sha) in repo.Branches)
                {
                    Pkt(output, $"{sha} refs/heads/{branch}\n");
                }
            }

            output.Write("0000"u8);
            return GitAnswer(output.ToArray(), "application/x-git-upload-pack-result");
        }

        var wants = lines.Where(l => l.StartsWith("want ", StringComparison.Ordinal)).Select(l => l[5..]).ToList();
        Interlocked.Increment(ref _uploadPackFetches);
        Interlocked.Add(ref _wantedObjects, wants.Count);
        byte[] pack;
        lock (_gate)
        {
            pack = BlobPack([.. wants.Where(repo.Blobs.ContainsKey).Select(w => repo.Blobs[w])]);
        }

        Pkt(output, "packfile\n");
        for (var offset = 0; offset < pack.Length; offset += 65000)
        {
            var chunk = pack.AsSpan(offset, Math.Min(65000, pack.Length - offset));
            Pkt(output, [1, .. chunk]);
        }

        output.Write("0000"u8);
        return GitAnswer(output.ToArray(), "application/x-git-upload-pack-result");
    }

    private static HttpResponseMessage GitAnswer(byte[] body, string type)
    {
        var answer = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };
        answer.Content.Headers.ContentType = new MediaTypeHeaderValue(type);
        return answer;
    }

    /// <summary>A pack of whole blobs, as Git writes one.</summary>
    private static byte[] BlobPack(IReadOnlyList<byte[]> blobs)
    {
        using var pack = new MemoryStream();
        pack.Write("PACK"u8);
        pack.Write([0, 0, 0, 2]);
        pack.Write([(byte)(blobs.Count >> 24), (byte)(blobs.Count >> 16), (byte)(blobs.Count >> 8), (byte)blobs.Count]);
        foreach (var blob in blobs)
        {
            long size = blob.Length;
            var first = (3 << 4) | (int)(size & 0x0F);
            size >>= 4;
            while (size > 0)
            {
                pack.WriteByte((byte)(first | 0x80));
                first = (int)(size & 0x7F);
                size >>= 7;
            }

            pack.WriteByte((byte)first);
            using (var zlib = new ZLibStream(pack, CompressionLevel.Fastest, leaveOpen: true))
            {
                zlib.Write(blob);
            }
        }

        using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA1))
        {
            sha.AppendData(pack.ToArray());
            pack.Write(sha.GetHashAndReset());
        }
        return pack.ToArray();
    }

    private static void Pkt(Stream stream, string text) => Pkt(stream, Encoding.UTF8.GetBytes(text));

    private static void Pkt(Stream stream, ReadOnlySpan<byte> data)
    {
        stream.Write(Encoding.ASCII.GetBytes((data.Length + 4).ToString("x4", System.Globalization.CultureInfo.InvariantCulture)));
        stream.Write(data);
    }

    private static List<string> ReadPktLines(byte[] body)
    {
        var lines = new List<string>();
        var position = 0;
        while (position + 4 <= body.Length)
        {
            var length = Convert.ToInt32(Encoding.ASCII.GetString(body, position, 4), 16);
            if (length < 4)
            {
                position += 4;
                continue;
            }

            lines.Add(Encoding.UTF8.GetString(body, position + 4, length - 4).TrimEnd('\n'));
            position += length;
        }

        return lines;
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
