using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// Conditional requests and GitHub's request limit: answers that are polled, such as a commit's checks, are kept
/// with their ETag, and asked for again with <c>If-None-Match</c>. GitHub answers 304 Not Modified when nothing
/// changed, which does not count against the limit for a signed-in client.
/// </summary>
/// <remarks>
/// The cache belongs to one client, so keep one client for polling rather than creating one per request.
/// </remarks>
public sealed partial class GitHubClient
{
    private readonly ResponseCache _cache = new();
    private GitHubRateLimit? _rateLimit;

    /// <summary>GitHub's request limit as the latest response reported it, or <see langword="null"/> before the first one.</summary>
    public GitHubRateLimit? RateLimit => Volatile.Read(ref _rateLimit);

    /// <summary>Reads a resource, sending the ETag of the answer kept from last time and reusing it on 304 Not Modified.</summary>
    private async Task<T> GetCachedAsync<T>(string path, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path, null);
        var key = request.RequestUri!.AbsoluteUri;
        var cached = _cache.Find(key);
        if (cached is not null)
        {
            request.Headers.IfNoneMatch.Add(cached.ETag);
        }

        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        byte[] body;
        if (response.StatusCode == HttpStatusCode.NotModified && cached is not null)
        {
            body = cached.Body;
        }
        else
        {
            await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            if (response.Headers.ETag is { } etag)
            {
                _cache.Store(key, etag, body);
            }
        }

        try
        {
            return JsonSerializer.Deserialize(body, type) ?? throw new GitHubApiException(response.StatusCode, "GitHub sent an empty answer.");
        }
        catch (JsonException ex)
        {
            throw new GitHubApiException("GitHub sent an answer TabStudio could not read.", ex);
        }
    }

    /// <summary>Sends a request whose answer has no content TabStudio reads, such as a re-run.</summary>
    private async Task SendWithoutAnswerAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, content);
        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Remembers the request limit a response reports. Responses from other hosts, such as log downloads, have none.</summary>
    private void RecordRateLimit(HttpResponseMessage response)
    {
        if (Header(response, "x-ratelimit-limit") is { } limitText
            && int.TryParse(limitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
            && int.TryParse(Header(response, "x-ratelimit-remaining"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var remaining)
            && long.TryParse(Header(response, "x-ratelimit-reset"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var reset))
        {
            Volatile.Write(ref _rateLimit, new GitHubRateLimit(limit, remaining, DateTimeOffset.FromUnixTimeSeconds(reset)));
        }
    }

    /// <summary>
    /// The answers kept for conditional requests, by address: at most <see cref="MaximumEntries"/> answers and
    /// <see cref="MaximumBytes"/> in all, the least recently used going first.
    /// </summary>
    private sealed class ResponseCache
    {
        private const int MaximumEntries = 256;
        private const long MaximumBytes = 16L * 1024 * 1024;

        private readonly Dictionary<string, LinkedListNode<CachedResponse>> _entries = new(StringComparer.Ordinal);
        private readonly LinkedList<CachedResponse> _recent = new();
        private readonly Lock _gate = new();
        private long _bytes;

        public CachedResponse? Find(string key)
        {
            lock (_gate)
            {
                if (!_entries.TryGetValue(key, out var node))
                {
                    return null;
                }

                _recent.Remove(node);
                _recent.AddFirst(node);
                return node.Value;
            }
        }

        public void Store(string key, EntityTagHeaderValue etag, byte[] body)
        {
            if (body.Length > MaximumBytes / 8)
            {
                return;
            }

            lock (_gate)
            {
                if (_entries.Remove(key, out var old))
                {
                    _recent.Remove(old);
                    _bytes -= old.Value.Body.Length;
                }

                _entries[key] = _recent.AddFirst(new CachedResponse(key, etag, body));
                _bytes += body.Length;
                while (_entries.Count > MaximumEntries || _bytes > MaximumBytes)
                {
                    var last = _recent.Last!;
                    _recent.RemoveLast();
                    _entries.Remove(last.Value.Key);
                    _bytes -= last.Value.Body.Length;
                }
            }
        }
    }

    /// <summary>An answer kept for a conditional request.</summary>
    private sealed record CachedResponse(string Key, EntityTagHeaderValue ETag, byte[] Body);
}
