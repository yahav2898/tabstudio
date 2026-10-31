using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// Job logs. GitHub answers a log request with a redirect to a short-lived download address on another host;
/// that request is made without the token, which must only ever go to GitHub's API.
/// </summary>
public sealed partial class GitHubClient
{
    /// <summary>Downloads a job's log, keeping its start and end when it is larger than the limits.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="jobId">The job, which is also its check run.</param>
    /// <param name="limits">How much to keep; <see cref="GitHubJobLogLimits.Preview"/> when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <remarks>The whole log is streamed, but only the kept parts are held in memory.</remarks>
    /// <exception cref="GitHubApiException">
    /// The log does not exist (yet), has expired, or the account cannot read it. GitHub publishes a job's log
    /// once the job has finished.
    /// </exception>
    public async Task<GitHubJobLog> GetJobLogAsync(string owner, string repository, long jobId, GitHubJobLogLimits? limits = null, CancellationToken cancellationToken = default)
    {
        limits ??= GitHubJobLogLimits.Preview;
        using var request = CreateRequest(HttpMethod.Get, $"{Repo(owner, repository)}/actions/jobs/{jobId.ToString(CultureInfo.InvariantCulture)}/logs", null);
        var response = await SendRawAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRedirect(response.StatusCode) && response.Headers.Location is { } location)
            {
                // The HTTP handler did not follow the redirect: follow it here, without the token.
                var target = location.IsAbsoluteUri ? location : new Uri(request.RequestUri!, location);
                response.Dispose();
                using var download = new HttpRequestMessage(HttpMethod.Get, target);
                download.Headers.UserAgent.Add(new ProductInfoHeaderValue("TabStudio", "1.0"));
                response = await SendRawAsync(download, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            }

            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await ReadLogAsync(stream, limits, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>Reads a log, keeping whole lines from its start and end within the limits.</summary>
    /// <param name="stream">The log.</param>
    /// <param name="limits">How much to keep.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal static async Task<GitHubJobLog> ReadLogAsync(Stream stream, GitHubJobLogLimits limits, CancellationToken cancellationToken)
    {
        var headLimit = (int)Math.Clamp(limits.HeadBytes, 0, int.MaxValue / 2);
        var tail = new TailBuffer((int)Math.Clamp(limits.TailBytes, 0, int.MaxValue / 2));
        using var head = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await ReadBodyAsync(stream, buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            var toHead = (int)Math.Min(read, Math.Max(0, headLimit - head.Length));
            head.Write(buffer, 0, toHead);
            tail.Append(buffer, toHead, read - toHead);
            total += read;
        }

        return Assemble(head, tail, total);
    }

    /// <summary>Puts a read log together: whole when it fit, otherwise its start and end cut to whole lines.</summary>
    private static GitHubJobLog Assemble(MemoryStream head, TailBuffer tail, long total)
    {
        var headBytes = head.GetBuffer().AsSpan(0, (int)head.Length);
        if (tail.Dropped == 0)
        {
            // Everything fit: the tail continues the head exactly. Decoded together, so a character split
            // between the two stays whole.
            byte[] whole = [.. headBytes, .. tail.ToArray()];
            return new GitHubJobLog(Decode(whole), string.Empty, total, 0);
        }

        // Part was left out: cut both ends to whole lines, so no line is shown broken.
        var lastNewline = headBytes.LastIndexOf((byte)'\n');
        var keptHead = lastNewline >= 0 ? headBytes[..(lastNewline + 1)] : headBytes;
        var tailBytes = tail.ToArray().AsSpan();
        var firstNewline = tailBytes.IndexOf((byte)'\n');
        var keptTail = firstNewline >= 0 ? tailBytes[(firstNewline + 1)..] : tailBytes;
        var omitted = total - keptHead.Length - keptTail.Length;
        return new GitHubJobLog(Decode(keptHead), Decode(keptTail), total, omitted);
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.Moved or HttpStatusCode.Found or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    /// <summary>Decodes UTF-8, dropping a byte order mark; bytes that are not UTF-8 become replacement characters.</summary>
    private static string Decode(ReadOnlySpan<byte> bytes)
    {
        var hasByteOrderMark = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        return Encoding.UTF8.GetString(hasByteOrderMark ? bytes[3..] : bytes);
    }

    /// <summary>The last bytes of a stream, in a ring of fixed size.</summary>
    private sealed class TailBuffer
    {
        private readonly byte[] _ring;
        private int _start;
        private int _count;

        public TailBuffer(int capacity) => _ring = new byte[capacity];

        /// <summary>How many bytes fell out of the ring.</summary>
        public long Dropped { get; private set; }

        private int Capacity => _ring.Length;

        public void Append(byte[] buffer, int offset, int count) => Append(buffer.AsSpan(offset, count));

        public void Append(ReadOnlySpan<byte> bytes)
        {
            if (Capacity == 0)
            {
                Dropped += bytes.Length;
                return;
            }

            if (bytes.Length >= Capacity)
            {
                Dropped += _count + bytes.Length - Capacity;
                bytes[^Capacity..].CopyTo(_ring);
                _start = 0;
                _count = Capacity;
                return;
            }

            // Make room by dropping the oldest bytes, then copy in at most two pieces around the ring's end.
            var overflow = Math.Max(0, _count + bytes.Length - Capacity);
            _start = (_start + overflow) % Capacity;
            _count -= overflow;
            Dropped += overflow;

            var end = (_start + _count) % Capacity;
            var first = Math.Min(bytes.Length, Capacity - end);
            bytes[..first].CopyTo(_ring.AsSpan(end));
            bytes[first..].CopyTo(_ring);
            _count += bytes.Length;
        }

        public byte[] ToArray()
        {
            var result = new byte[_count];
            var first = Math.Min(_count, Capacity - _start);
            Array.Copy(_ring, _start, result, 0, first);
            Array.Copy(_ring, 0, result, first, _count - first);
            return result;
        }
    }
}
