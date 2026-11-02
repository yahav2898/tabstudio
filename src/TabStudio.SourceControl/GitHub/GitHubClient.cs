using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace TabStudio.SourceControl.GitHub;

/// <summary>
/// The parts of GitHub's REST API that TabStudio's source control uses: repositories, branches, Git objects
/// (blobs, trees, commits, refs), comparisons and pull requests.
/// </summary>
/// <remarks>
/// Without a token only public repositories can be read, and GitHub allows 60 requests an hour. The client is
/// cheap to create; share the <see cref="HttpClient"/>.
/// </remarks>
public sealed partial class GitHubClient
{
    /// <summary>The address of GitHub's API.</summary>
    public static readonly Uri DefaultApiAddress = new("https://api.github.com/");

    private const string ApiVersion = "2022-11-28";

    private readonly HttpClient _http;
    private readonly string? _token;
    private readonly Uri _apiAddress;

    /// <summary>Creates a client.</summary>
    /// <param name="http">The HTTP client to send requests with.</param>
    /// <param name="token">A personal access token or OAuth token, or <see langword="null"/> to stay signed out.</param>
    /// <param name="apiAddress">The API's address; GitHub's by default, or a GitHub Enterprise server's.</param>
    /// <param name="rawAddress">
    /// Where the server serves files of a commit as they are, outside the API: <see cref="DefaultRawAddress"/> for
    /// GitHub's API, and none for any other unless given.
    /// </param>
    /// <param name="webAddress">
    /// The server's web address, where Git LFS files are served: <see cref="DefaultWebAddress"/> for GitHub's API, the
    /// server's root for a GitHub Enterprise API at <c>/api/v3/</c>, and none for any other unless given.
    /// </param>
    public GitHubClient(HttpClient http, string? token = null, Uri? apiAddress = null, Uri? rawAddress = null, Uri? webAddress = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
        _apiAddress = apiAddress ?? DefaultApiAddress;
        _rawAddress = rawAddress ?? (_apiAddress == DefaultApiAddress ? DefaultRawAddress : null);
        _webAddress = webAddress ?? (_apiAddress == DefaultApiAddress
            ? DefaultWebAddress
            : _apiAddress.AbsolutePath.EndsWith("/api/v3/", StringComparison.OrdinalIgnoreCase) ? new Uri(_apiAddress, "/") : null);
    }

    /// <summary>Whether requests are made with a token.</summary>
    public bool IsSignedIn => _token is not null;

    /// <summary>Reads the account the token belongs to.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitHubUser> GetUserAsync(CancellationToken cancellationToken = default)
    {
        var user = await SendAsync(HttpMethod.Get, "user", null, GitHubJsonContext.Default.UserDto, cancellationToken).ConfigureAwait(false);
        return new GitHubUser(user.Login, user.Name);
    }

    /// <summary>Reads a repository.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitHubRepositoryInfo> GetRepositoryAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync(HttpMethod.Get, Repo(owner, repository), null, GitHubJsonContext.Default.RepositoryDto, cancellationToken).ConfigureAwait(false);
        return ToRepository(dto);
    }

    /// <summary>
    /// Creates a repository in the signed-in account, with an initial commit so that Git objects can be added
    /// through the API (GitHub's Git API does not work on an empty repository).
    /// </summary>
    /// <param name="name">The repository name.</param>
    /// <param name="isPrivate">Whether only invited people can see it.</param>
    /// <param name="description">A one-line description, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitHubRepositoryInfo> CreateRepositoryAsync(string name, bool isPrivate, string? description = null, CancellationToken cancellationToken = default)
    {
        var request = new CreateRepositoryRequest(name, description, isPrivate, AutoInit: true);
        var dto = await SendAsync(HttpMethod.Post, "user/repos", Json(request, GitHubJsonContext.Default.CreateRepositoryRequest), GitHubJsonContext.Default.RepositoryDto, cancellationToken).ConfigureAwait(false);
        return ToRepository(dto);
    }

    /// <summary>Lists a repository's branches.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="cancellationToken">Cancels the requests.</param>
    public async Task<IReadOnlyList<GitHubBranch>> GetBranchesAsync(string owner, string repository, CancellationToken cancellationToken = default)
    {
        var branches = new List<GitHubBranch>();
        for (var page = 1; ; page++)
        {
            var batch = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/branches?per_page=100&page={page.ToString(CultureInfo.InvariantCulture)}", null, GitHubJsonContext.Default.IReadOnlyListBranchDto, cancellationToken).ConfigureAwait(false);
            branches.AddRange(batch.Select(b => new GitHubBranch(b.Name, b.Commit.Sha, b.Protected)));
            if (batch.Count < 100)
            {
                return branches;
            }
        }
    }

    /// <summary>Reads the commit at the tip of a branch.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The commit's hash.</returns>
    public async Task<string> GetBranchHeadAsync(string owner, string repository, string branch, CancellationToken cancellationToken = default)
    {
        var reference = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/git/ref/heads/{EscapePath(branch)}", null, GitHubJsonContext.Default.RefDto, cancellationToken).ConfigureAwait(false);
        return reference.Object.Sha;
    }

    /// <summary>Finds the commit a branch, tag or commit hash refers to.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">A branch, a tag or a full or abbreviated commit hash.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitCommit> ResolveCommitAsync(string owner, string repository, string reference, CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/commits/{EscapePath(reference)}", null, GitHubJsonContext.Default.RepositoryCommitDto, cancellationToken).ConfigureAwait(false);
        return ToCommit(dto);
    }

    /// <summary>Reads a commit.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="sha">The commit's hash.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitCommit> GetCommitAsync(string owner, string repository, string sha, CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/git/commits/{sha}", null, GitHubJsonContext.Default.GitCommitDto, cancellationToken).ConfigureAwait(false);
        return ToCommit(dto);
    }

    /// <summary>Lists the latest commits of a branch, newest first.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">The branch, tag or commit to start from.</param>
    /// <param name="count">How many commits to list, at most 100.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<GitCommit>> GetHistoryAsync(string owner, string repository, string reference, int count = 30, CancellationToken cancellationToken = default)
    {
        var path = $"{Repo(owner, repository)}/commits?sha={Uri.EscapeDataString(reference)}&per_page={Math.Clamp(count, 1, 100).ToString(CultureInfo.InvariantCulture)}";
        var commits = await SendAsync(HttpMethod.Get, path, null, GitHubJsonContext.Default.IReadOnlyListRepositoryCommitDto, cancellationToken).ConfigureAwait(false);
        return [.. commits.Select(ToCommit)];
    }

    /// <summary>Reads a tree.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="sha">The tree's hash.</param>
    /// <param name="recursive">Whether to include every entry at any depth instead of only the tree's own.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitTree> GetTreeAsync(string owner, string repository, string sha, bool recursive, CancellationToken cancellationToken = default)
    {
        var path = $"{Repo(owner, repository)}/git/trees/{sha}{(recursive ? "?recursive=1" : string.Empty)}";
        var dto = await SendAsync(HttpMethod.Get, path, null, GitHubJsonContext.Default.TreeDto, cancellationToken).ConfigureAwait(false);
        var entries = dto.Tree.Select(e => new GitTreeEntry(e.Path, e.Mode, ParseKind(e.Type), e.Sha, e.Size)).ToList();
        return new GitTree(dto.Sha, entries, dto.Truncated);
    }

    /// <summary>Downloads a file's content.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="sha">The blob's hash.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<byte[]> GetBlobAsync(string owner, string repository, string sha, CancellationToken cancellationToken = default)
    {
        var blob = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/git/blobs/{sha}", null, GitHubJsonContext.Default.BlobDto, cancellationToken).ConfigureAwait(false);
        return blob.Encoding switch
        {
            "base64" => Convert.FromBase64String(blob.Content?.Replace("\n", string.Empty, StringComparison.Ordinal) ?? string.Empty),
            "utf-8" => System.Text.Encoding.UTF8.GetBytes(blob.Content ?? string.Empty),
            _ => throw new GitHubApiException($"GitHub sent blob {sha} in an unknown encoding '{blob.Encoding}'."),
        };
    }

    /// <summary>Uploads a file's content.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="content">The bytes.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The blob's hash.</returns>
    public async Task<string> CreateBlobAsync(string owner, string repository, ReadOnlyMemory<byte> content, CancellationToken cancellationToken = default)
    {
        var request = new CreateBlobRequest(Convert.ToBase64String(content.Span), "base64");
        var created = await SendAsync(HttpMethod.Post, $"{Repo(owner, repository)}/git/blobs", Json(request, GitHubJsonContext.Default.CreateBlobRequest), GitHubJsonContext.Default.ShaDto, cancellationToken).ConfigureAwait(false);
        return created.Sha;
    }

    /// <summary>Creates a tree from changes to another tree, or from scratch.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="baseTree">The tree the changes apply to, or <see langword="null"/> for a tree holding only <paramref name="changes"/>.</param>
    /// <param name="changes">Files to add, replace or delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new tree's hash.</returns>
    public async Task<string> CreateTreeAsync(string owner, string repository, string? baseTree, IEnumerable<GitTreeChange> changes, CancellationToken cancellationToken = default)
    {
        var entries = changes.Select(c => new CreateTreeEntry(c.Path, c.Mode, "blob", c.Sha)).ToList();
        var request = new CreateTreeRequest(baseTree, entries);
        var created = await SendAsync(HttpMethod.Post, $"{Repo(owner, repository)}/git/trees", Json(request, GitHubJsonContext.Default.CreateTreeRequest), GitHubJsonContext.Default.ShaDto, cancellationToken).ConfigureAwait(false);
        return created.Sha;
    }

    /// <summary>Creates a commit. The signed-in account is its author.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="message">The commit message.</param>
    /// <param name="treeSha">The tree it records.</param>
    /// <param name="parents">Its parents.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitCommit> CreateCommitAsync(string owner, string repository, string message, string treeSha, IReadOnlyList<string> parents, CancellationToken cancellationToken = default)
    {
        var request = new CreateCommitRequest(message, treeSha, parents);
        var dto = await SendAsync(HttpMethod.Post, $"{Repo(owner, repository)}/git/commits", Json(request, GitHubJsonContext.Default.CreateCommitRequest), GitHubJsonContext.Default.GitCommitDto, cancellationToken).ConfigureAwait(false);
        return ToCommit(dto);
    }

    /// <summary>Moves a branch to a commit. Without <paramref name="force"/>, GitHub only accepts a fast-forward.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="branch">The branch.</param>
    /// <param name="sha">The commit.</param>
    /// <param name="force">Whether to move the branch even if commits on it would be lost.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task UpdateBranchAsync(string owner, string repository, string branch, string sha, bool force = false, CancellationToken cancellationToken = default) =>
        SendAsync(new HttpMethod("PATCH"), $"{Repo(owner, repository)}/git/refs/heads/{EscapePath(branch)}", Json(new UpdateRefRequest(sha, force), GitHubJsonContext.Default.UpdateRefRequest), GitHubJsonContext.Default.RefDto, cancellationToken);

    /// <summary>Creates a branch at a commit.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="branch">The new branch's name.</param>
    /// <param name="sha">The commit it starts at.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public Task CreateBranchAsync(string owner, string repository, string branch, string sha, CancellationToken cancellationToken = default) =>
        SendAsync(HttpMethod.Post, $"{Repo(owner, repository)}/git/refs", Json(new CreateRefRequest($"refs/heads/{branch}", sha), GitHubJsonContext.Default.CreateRefRequest), GitHubJsonContext.Default.RefDto, cancellationToken);

    /// <summary>Compares two commits.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="baseSha">The base commit.</param>
    /// <param name="headSha">The head commit.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitComparison> CompareAsync(string owner, string repository, string baseSha, string headSha, CancellationToken cancellationToken = default)
    {
        var dto = await SendAsync(HttpMethod.Get, $"{Repo(owner, repository)}/compare/{EscapePath(baseSha)}...{EscapePath(headSha)}?per_page=1", null, GitHubJsonContext.Default.CompareDto, cancellationToken).ConfigureAwait(false);
        return new GitComparison(dto.Status, dto.AheadBy, dto.BehindBy);
    }

    /// <summary>Opens a pull request.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="title">The title.</param>
    /// <param name="body">The description, in Markdown.</param>
    /// <param name="head">The branch with the changes.</param>
    /// <param name="baseBranch">The branch to merge them into.</param>
    /// <param name="draft">Whether to open it as a draft.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<GitHubPullRequest> CreatePullRequestAsync(string owner, string repository, string title, string? body, string head, string baseBranch, bool draft = false, CancellationToken cancellationToken = default)
    {
        var request = new CreatePullRequestRequest(title, body, head, baseBranch, draft);
        var dto = await SendAsync(HttpMethod.Post, $"{Repo(owner, repository)}/pulls", Json(request, GitHubJsonContext.Default.CreatePullRequestRequest), GitHubJsonContext.Default.PullRequestDto, cancellationToken).ConfigureAwait(false);
        return ToPullRequest(dto);
    }

    /// <summary>Lists the open pull requests from a branch.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="headBranch">The branch with the changes, or <see langword="null"/> for every open pull request.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    public async Task<IReadOnlyList<GitHubPullRequest>> GetOpenPullRequestsAsync(string owner, string repository, string? headBranch = null, CancellationToken cancellationToken = default)
    {
        var path = $"{Repo(owner, repository)}/pulls?state=open&per_page=50";
        if (headBranch is not null)
        {
            path += $"&head={Uri.EscapeDataString($"{owner}:{headBranch}")}";
        }

        var list = await SendAsync(HttpMethod.Get, path, null, GitHubJsonContext.Default.IReadOnlyListPullRequestDto, cancellationToken).ConfigureAwait(false);
        return [.. list.Select(ToPullRequest)];
    }

    /// <summary>Downloads a commit's files as a zip archive into a stream.</summary>
    /// <param name="owner">The owner.</param>
    /// <param name="repository">The repository name.</param>
    /// <param name="reference">The commit, branch or tag.</param>
    /// <param name="destination">Where to write the archive.</param>
    /// <param name="maximumBytes">The largest archive to accept.</param>
    /// <param name="progress">Receives the number of bytes downloaded so far and the total, when known.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    public async Task DownloadArchiveAsync(
        string owner,
        string repository,
        string reference,
        Stream destination,
        long maximumBytes,
        IProgress<(long Received, long? Total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(destination);
        using var request = CreateRequest(HttpMethod.Get, $"{Repo(owner, repository)}/zipball/{EscapePath(reference)}", null);
        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var total = response.Content.Headers.ContentLength;
        if (total > maximumBytes)
        {
            throw new GitHubApiException(HttpStatusCode.RequestEntityTooLarge, "The repository is larger than TabStudio downloads.");
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await ReadBodyAsync(source, buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            received += read;
            if (received > maximumBytes)
            {
                throw new GitHubApiException(HttpStatusCode.RequestEntityTooLarge, "The repository is larger than TabStudio downloads.");
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            progress?.Report((received, total));
        }
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, HttpContent? content, JsonTypeInfo<T> type, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, content);
        using var response = await SendRawAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        try
        {
            return await response.Content.ReadFromJsonAsync(type, cancellationToken).ConfigureAwait(false)
                ?? throw new GitHubApiException(response.StatusCode, "GitHub sent an empty answer.");
        }
        catch (JsonException ex)
        {
            throw new GitHubApiException("GitHub sent an answer TabStudio could not read.", ex);
        }
    }

    /// <summary>How long a download may go without a byte before it is given up on.</summary>
    internal static TimeSpan BodyStallTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>
    /// Reads the next bytes of a response body as a <see cref="GitHubApiException"/> when the connection breaks or
    /// stalls: a dropped connection came out as an <see cref="IOException"/> no caller expected, which ended the app
    /// from the CI log panel, and a stalled one kept its spinner going for good.
    /// </summary>
    internal static async Task<int> ReadBodyAsync(Stream body, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        stall.CancelAfter(BodyStallTimeout);
        try
        {
            return await body.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubApiException("The connection to GitHub stopped sending data. Check the network and try again.", ex);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            throw new GitHubApiException("The connection to GitHub broke during the download. Check the network and try again.", ex);
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, HttpContent? content)
    {
        var request = new HttpRequestMessage(method, new Uri(_apiAddress, path)) { Content = content };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TabStudio", "1.0"));
        request.Headers.Add("X-GitHub-Api-Version", ApiVersion);
        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage request, HttpCompletionOption completion, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _http.SendAsync(request, completion, cancellationToken).ConfigureAwait(false);
            RecordRateLimit(response);
            return response;
        }
        catch (HttpRequestException ex)
        {
            throw new GitHubApiException($"Could not reach GitHub: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitHubApiException("GitHub did not answer in time.", ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? message = null;
        try
        {
            var error = await response.Content.ReadFromJsonAsync(GitHubJsonContext.Default.ErrorDto, cancellationToken).ConfigureAwait(false);
            message = error?.Message;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Not every error comes with a JSON body.
        }

        DateTimeOffset? reset = null;
        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
            && Header(response, "x-ratelimit-remaining") == "0"
            && long.TryParse(Header(response, "x-ratelimit-reset"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
        {
            reset = DateTimeOffset.FromUnixTimeSeconds(seconds);
        }

        throw new GitHubApiException(response.StatusCode, message ?? $"GitHub answered {(int)response.StatusCode} {response.ReasonPhrase}.", reset);
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private static JsonContent Json<T>(T value, JsonTypeInfo<T> type) => JsonContent.Create(value, type);

    private static string Repo(string owner, string repository) => $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}";

    /// <summary>Escapes each segment of a ref name, keeping the slashes of names such as <c>feature/login</c>.</summary>
    private static string EscapePath(string reference) => string.Join('/', reference.Split('/').Select(Uri.EscapeDataString));

    private static GitTreeEntryKind ParseKind(string type) => type switch
    {
        "tree" => GitTreeEntryKind.Tree,
        "commit" => GitTreeEntryKind.Commit,
        _ => GitTreeEntryKind.Blob,
    };

    private static GitHubRepositoryInfo ToRepository(RepositoryDto dto) =>
        new(dto.Owner.Login, dto.Name, dto.DefaultBranch ?? "main", dto.Private, dto.Permissions?.Push ?? false, new Uri(dto.HtmlUrl));

    private static GitCommit ToCommit(GitCommitDto dto) =>
        new(dto.Sha, dto.Tree.Sha, dto.Message, [.. dto.Parents.Select(p => p.Sha)], ToSignature(dto.Author));

    private static GitCommit ToCommit(RepositoryCommitDto dto) =>
        new(dto.Sha, dto.Commit.Tree.Sha, dto.Commit.Message, [.. dto.Parents.Select(p => p.Sha)], ToSignature(dto.Commit.Author));

    private static GitSignature? ToSignature(SignatureDto? dto) =>
        dto is null ? null : new GitSignature(dto.Name ?? string.Empty, dto.Email ?? string.Empty, dto.Date ?? DateTimeOffset.MinValue);

    private static GitHubPullRequest ToPullRequest(PullRequestDto dto) =>
        new(dto.Number, dto.Title, dto.State, dto.Head.Ref, dto.Base.Ref, new Uri(dto.HtmlUrl), dto.Draft);
}
