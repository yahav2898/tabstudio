using System.Net.Http.Headers;

namespace TabStudio.SourceControl.GitHub;

/// <summary>Files of a commit from GitHub's raw file host, which the API's rate limit does not count.</summary>
public sealed partial class GitHubClient
{
    /// <summary>Where GitHub serves files of a commit as they are: <c>raw.githubusercontent.com</c>.</summary>
    public static readonly Uri DefaultRawAddress = new("https://raw.githubusercontent.com/");

    private readonly Uri? _rawAddress;

    /// <summary>
    /// Whether files can be read from the raw file host. Signed out, GitHub's API answers 60 requests an hour, and a
    /// file the archive leaves out (<c>export-ignore</c>) cost one request each: symfony/console needed 427 and
    /// laravel/framework 1551.
    /// </summary>
    public bool CanReadRawFiles => _rawAddress is not null;

    /// <summary>
    /// Downloads one file of a public repository's commit from the raw file host, without a token and without using
    /// the API's rate limit. The caller checks the content against the file's blob hash.
    /// </summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="commitSha">The commit's full hash.</param>
    /// <param name="path">The file's path in the commit, with <c>/</c> between folders.</param>
    /// <param name="maximumBytes">The largest file to accept.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The file's bytes, or <see langword="null"/> when there is no raw host or it does not serve the file.</returns>
    public async Task<byte[]?> TryGetRawFileAsync(string owner, string repository, string commitSha, string path, long maximumBytes, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(commitSha);
        ArgumentNullException.ThrowIfNull(path);
        if (_rawAddress is not { } raw)
        {
            return null;
        }

        // No token: the raw host is a different server, and it serves public repositories to anyone.
        var address = new Uri(raw, $"{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/{Uri.EscapeDataString(commitSha)}/{EscapePath(path)}");
        using var request = new HttpRequestMessage(HttpMethod.Get, address);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TabStudio", "1.0"));
        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > maximumBytes)
            {
                return null;
            }

            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            return content.LongLength > maximumBytes ? null : content;
        }
        catch (HttpRequestException)
        {
            // The API can still answer: the caller falls back to it.
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
