using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace TabStudio.SourceControl.Git;

/// <summary>A branch or tag a Git server has.</summary>
/// <param name="Name">The full name, such as <c>refs/heads/main</c>.</param>
/// <param name="Id">The object it points at.</param>
/// <param name="Peeled">For an annotated tag, the commit it tags.</param>
internal sealed record RemoteRef(string Name, string Id, string? Peeled);

/// <summary>What a Git server advertises.</summary>
/// <param name="Version">The protocol version it speaks: 2, or 0 for the original protocol.</param>
/// <param name="Refs">Its branches and tags, by full name.</param>
/// <param name="Head">The branch its <c>HEAD</c> points at, such as <c>refs/heads/main</c>, when it says.</param>
/// <param name="Capabilities">Its capabilities; for version 2, also the features of <c>fetch</c>.</param>
internal sealed record RemoteRefs(int Version, IReadOnlyDictionary<string, RemoteRef> Refs, string? Head, IReadOnlySet<string> Capabilities)
{
    /// <summary>Whether a fetch can leave out blobs (partial clone).</summary>
    public bool CanFilter => Capabilities.Contains("filter");

    /// <summary>Whether a fetch can be limited to the latest commits.</summary>
    public bool CanDeepen => Capabilities.Contains("shallow");

    /// <summary>Whether a fetch may ask for any object by name, as protocol version 2 allows.</summary>
    public bool CanWantAnyObject => Version == 2 || Capabilities.Contains("allow-reachable-sha1-in-want") || Capabilities.Contains("allow-any-sha1-in-want");
}

/// <summary>What to fetch.</summary>
/// <param name="Wants">The objects wanted: commits, or any object with protocol version 2.</param>
/// <param name="Haves">Commits the client already has, so the server leaves out what they hold.</param>
/// <param name="Depth">How many commits of history to fetch; 0 for all.</param>
/// <param name="WithoutBlobs">Whether to leave out file contents, fetched later by name.</param>
internal sealed record FetchRequest(IReadOnlyList<string> Wants, IReadOnlyList<string> Haves, int Depth, bool WithoutBlobs);

/// <summary>A change to a branch that a push asks for.</summary>
/// <param name="Name">The full name, such as <c>refs/heads/main</c>.</param>
/// <param name="OldId">What the branch points at now, or <see cref="GitHash.Zero"/> to create it.</param>
/// <param name="NewId">What it should point at, or <see cref="GitHash.Zero"/> to delete it.</param>
internal sealed record RefUpdate(string Name, string OldId, string NewId);

/// <summary>
/// Git's smart HTTP protocol, as <c>git fetch</c> and <c>git push</c> speak it to GitHub, GitLab, Bitbucket, Azure DevOps,
/// Gitea and a self-hosted <c>git http-backend</c>: protocol version 2 for fetching when the server offers it, the
/// original protocol otherwise, and the original protocol for pushing, which is all Git has.
/// </summary>
internal sealed class GitHttpClient
{
    private const string Agent = "git/2.0 (TabStudio)";

    private readonly HttpClient _http;
    private readonly GitCredentials? _credentials;
    private Uri _repository;
    private RemoteRefs? _uploadPack;

    /// <summary>Creates a client for one repository.</summary>
    /// <param name="http">The HTTP client.</param>
    /// <param name="repository">The repository's HTTPS address, such as <c>https://gitlab.com/group/project.git</c>.</param>
    /// <param name="credentials">The sign-in, if the repository needs one.</param>
    public GitHttpClient(HttpClient http, Uri repository, GitCredentials? credentials)
    {
        _http = http;
        _repository = new Uri(repository.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/");
        _credentials = credentials;
    }

    /// <summary>How long a download may go without a byte before it counts as broken.</summary>
    internal static TimeSpan StallTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Lists the server's branches and tags, and learns which protocol it speaks.</summary>
    /// <param name="cancellationToken">Cancels the requests.</param>
    /// <exception cref="GitTransportException">The server could not be reached or refused.</exception>
    public async Task<RemoteRefs> ListRefsAsync(CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, "info/refs?service=git-upload-pack", null);
        request.Headers.Add("Git-Protocol", "version=2");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A server that moved the repository answers from its new address; later requests go there too, as Git does.
        if (response.RequestMessage?.RequestUri is { } final && final.AbsolutePath.EndsWith("/info/refs", StringComparison.Ordinal))
        {
            _repository = new Uri(final.GetLeftPart(UriPartial.Path)[..^"info/refs".Length]);
        }

        if (response.Content.Headers.ContentType?.MediaType != "application/x-git-upload-pack-advertisement")
        {
            throw new GitTransportException($"{_repository.Host} does not answer as a Git server at this address. Check the address: it is usually the one the site's Clone button gives, ending in .git.");
        }

        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var reader = new PktLineReader(new StallGuard(body));
        var kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (kind == PktKind.Data && reader.Text.StartsWith("# service=", StringComparison.Ordinal))
        {
            while (kind != PktKind.Flush && kind != PktKind.End)
            {
                kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }

            kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        RemoteRefs refs;
        if (kind == PktKind.Data && reader.Text == "version 2")
        {
            var capabilities = new HashSet<string>(StringComparer.Ordinal);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == PktKind.Data)
            {
                AddCapability(capabilities, reader.Text);
            }

            refs = await ListRefsV2Async(capabilities, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            refs = await ReadAdvertisementAsync(reader, kind, cancellationToken).ConfigureAwait(false);
        }

        _uploadPack = refs;
        return refs;
    }

    /// <summary>Fetches a pack into a stream.</summary>
    /// <param name="fetch">What to fetch.</param>
    /// <param name="pack">Receives the pack as it arrives.</param>
    /// <param name="progress">Receives how many bytes have arrived.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The commits the server marked as shallow, the edge of the history it sent.</returns>
    /// <exception cref="GitTransportException">The server refused, or the connection broke; what arrived is in the stream.</exception>
    public async Task<IReadOnlyList<string>> FetchAsync(FetchRequest fetch, Stream pack, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        var refs = _uploadPack ?? await ListRefsAsync(cancellationToken).ConfigureAwait(false);
        using var body = new MemoryStream();
        if (refs.Version == 2)
        {
            WriteCommand(body, "fetch", refs.Capabilities);
            PktLine.WriteLine(body, "ofs-delta");
            PktLine.WriteLine(body, "no-progress");
            if (fetch.Depth > 0 && refs.CanDeepen)
            {
                PktLine.WriteLine(body, $"deepen {fetch.Depth}");
            }

            if (fetch.WithoutBlobs && refs.CanFilter)
            {
                PktLine.WriteLine(body, "filter blob:none");
            }

            foreach (var want in fetch.Wants.Distinct(StringComparer.Ordinal))
            {
                PktLine.WriteLine(body, "want " + want);
            }

            foreach (var have in fetch.Haves.Distinct(StringComparer.Ordinal))
            {
                PktLine.WriteLine(body, "have " + have);
            }

            PktLine.WriteLine(body, "done");
            PktLine.WriteFlush(body);
        }
        else
        {
            var capabilities = new List<string> { refs.Capabilities.Contains("side-band-64k") ? "side-band-64k" : "side-band", "ofs-delta", "no-progress" };
            if (fetch.Depth > 0 && refs.CanDeepen)
            {
                capabilities.Add("shallow");
            }

            if (fetch.WithoutBlobs && refs.CanFilter)
            {
                capabilities.Add("filter");
            }

            capabilities.RemoveAll(c => !refs.Capabilities.Contains(c));
            capabilities.Add("agent=" + Agent);
            var first = true;
            foreach (var want in fetch.Wants.Distinct(StringComparer.Ordinal))
            {
                PktLine.WriteLine(body, first ? $"want {want} {string.Join(' ', capabilities)}" : "want " + want);
                first = false;
            }

            if (fetch.Depth > 0 && refs.CanDeepen)
            {
                PktLine.WriteLine(body, $"deepen {fetch.Depth}");
            }

            if (fetch.WithoutBlobs && refs.CanFilter)
            {
                PktLine.WriteLine(body, "filter blob:none");
            }

            PktLine.WriteFlush(body);
            foreach (var have in fetch.Haves.Distinct(StringComparer.Ordinal))
            {
                PktLine.WriteLine(body, "have " + have);
            }

            PktLine.WriteLine(body, "done");
        }

        using var request = Request(HttpMethod.Post, "git-upload-pack", body.ToArray(), "application/x-git-upload-pack-request");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-git-upload-pack-result"));
        if (refs.Version == 2)
        {
            request.Headers.Add("Git-Protocol", "version=2");
        }

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var reader = new PktLineReader(new StallGuard(stream));
        var shallow = new List<string>();
        try
        {
            if (refs.Version == 2)
            {
                await ReadFetchV2Async(reader, pack, shallow, progress, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var banded = refs.Capabilities.Contains("side-band-64k") || refs.Capabilities.Contains("side-band");
                await ReadFetchV0Async(reader, stream, banded, pack, shallow, fetch.Depth > 0 && refs.CanDeepen, progress, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            throw new GitTransportException($"The connection to {_repository.Host} broke during the download. What arrived is kept; try again to go on.", ex);
        }

        return shallow;
    }

    /// <summary>Pushes branch changes, sending a pack of the objects the server lacks.</summary>
    /// <param name="updates">The branch changes.</param>
    /// <param name="writePack">Writes the pack; not called when every update deletes a branch.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    /// <returns>For each branch the server refused, its reason, as <c>git push</c> reports it.</returns>
    /// <exception cref="GitTransportException">The server could not be reached, refused the sign-in or the pack.</exception>
    public async Task<IReadOnlyDictionary<string, string>> PushAsync(IReadOnlyList<RefUpdate> updates, Action<Stream> writePack, CancellationToken cancellationToken)
    {
        var advertised = await ListReceivePackAsync(cancellationToken).ConfigureAwait(false);
        using var body = new MemoryStream();
        var capabilities = new List<string> { "report-status", "side-band-64k" };
        capabilities.RemoveAll(c => !advertised.Capabilities.Contains(c));
        capabilities.Add("agent=" + Agent);
        var first = true;
        foreach (var update in updates)
        {
            var line = $"{update.OldId} {update.NewId} {update.Name}";
            if (first)
            {
                PktLine.Write(body, Encoding.UTF8.GetBytes(line + "\0" + string.Join(' ', capabilities) + "\n"));
                first = false;
            }
            else
            {
                PktLine.WriteLine(body, line);
            }
        }

        PktLine.WriteFlush(body);
        if (updates.Any(u => u.NewId != GitHash.Zero))
        {
            writePack(body);
        }

        using var request = Request(HttpMethod.Post, "git-receive-pack", body.ToArray(), "application/x-git-receive-pack-request");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-git-receive-pack-result"));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var answer = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        Stream status = new MemoryStream(answer);
        if (advertised.Capabilities.Contains("side-band-64k"))
        {
            var unbanded = new MemoryStream();
            var errors = new StringBuilder();
            var reader = new PktLineReader(status);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == PktKind.Data && reader.Data.Length > 0)
            {
                switch (reader.Data[0])
                {
                    case 1:
                        unbanded.Write(reader.Data.AsSpan(1));
                        break;
                    case 3:
                        errors.Append(Encoding.UTF8.GetString(reader.Data.AsSpan(1)).Trim());
                        break;
                }
            }

            if (errors.Length > 0)
            {
                throw new GitTransportException($"{_repository.Host} refused the push: {errors}");
            }

            unbanded.Position = 0;
            status = unbanded;
        }

        var refused = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!advertised.Capabilities.Contains("report-status"))
        {
            return refused;
        }

        var lines = new PktLineReader(status);
        while (await lines.ReadAsync(cancellationToken).ConfigureAwait(false) == PktKind.Data)
        {
            var text = lines.Text;
            if (text.StartsWith("unpack ", StringComparison.Ordinal) && text != "unpack ok")
            {
                throw new GitTransportException($"{_repository.Host} could not store the pushed objects: {text[7..]}");
            }

            if (text.StartsWith("ng ", StringComparison.Ordinal))
            {
                var parts = text[3..].Split(' ', 2);
                refused[parts[0]] = parts.Length > 1 ? parts[1] : "refused";
            }
        }

        return refused;
    }

    /// <summary>The branches the server has, as its push side advertises them.</summary>
    public async Task<RemoteRefs> ListReceivePackAsync(CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, "info/refs?service=git-receive-pack", null);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.Content.Headers.ContentType?.MediaType != "application/x-git-receive-pack-advertisement")
        {
            throw new GitTransportException($"{_repository.Host} does not take pushes over HTTPS at this address.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var reader = new PktLineReader(new StallGuard(stream));
        var kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (kind == PktKind.Data && reader.Text.StartsWith("# service=", StringComparison.Ordinal))
        {
            while (kind != PktKind.Flush && kind != PktKind.End)
            {
                kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            }

            kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        return await ReadAdvertisementAsync(reader, kind, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RemoteRefs> ListRefsV2Async(HashSet<string> capabilities, CancellationToken cancellationToken)
    {
        using var body = new MemoryStream();
        WriteCommand(body, "ls-refs", capabilities);
        PktLine.WriteLine(body, "peel");
        PktLine.WriteLine(body, "symrefs");
        PktLine.WriteLine(body, "ref-prefix HEAD");
        PktLine.WriteLine(body, "ref-prefix refs/heads/");
        PktLine.WriteLine(body, "ref-prefix refs/tags/");
        PktLine.WriteFlush(body);

        using var request = Request(HttpMethod.Post, "git-upload-pack", body.ToArray(), "application/x-git-upload-pack-request");
        request.Headers.Add("Git-Protocol", "version=2");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/x-git-upload-pack-result"));
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var reader = new PktLineReader(new StallGuard(stream));
        var refs = new Dictionary<string, RemoteRef>(StringComparer.Ordinal);
        string? head = null;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false) == PktKind.Data)
        {
            var parts = reader.Text.Split(' ');
            ThrowIfError(reader.Text);
            if (parts.Length < 2 || !GitHash.IsHash(parts[0]))
            {
                continue;
            }

            string? peeled = null;
            foreach (var attribute in parts.Skip(2))
            {
                if (attribute.StartsWith("symref-target:", StringComparison.Ordinal) && parts[1] == "HEAD")
                {
                    head = attribute["symref-target:".Length..];
                }
                else if (attribute.StartsWith("peeled:", StringComparison.Ordinal))
                {
                    peeled = attribute["peeled:".Length..];
                }
            }

            refs[parts[1]] = new RemoteRef(parts[1], parts[0].ToLowerInvariant(), peeled);
        }

        // The features of the fetch command, such as "fetch=shallow filter", count as capabilities.
        var all = new HashSet<string>(capabilities, StringComparer.Ordinal);
        return new RemoteRefs(2, refs, head, all);
    }

    private static async Task<RemoteRefs> ReadAdvertisementAsync(PktLineReader reader, PktKind kind, CancellationToken cancellationToken)
    {
        var refs = new Dictionary<string, RemoteRef>(StringComparer.Ordinal);
        var capabilities = new HashSet<string>(StringComparer.Ordinal);
        string? head = null;
        var first = true;
        while (kind == PktKind.Data)
        {
            var line = Encoding.UTF8.GetString(reader.Data).TrimEnd('\n');
            ThrowIfError(line);
            if (first)
            {
                var nul = line.IndexOf('\0', StringComparison.Ordinal);
                if (nul >= 0)
                {
                    foreach (var capability in line[(nul + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    {
                        capabilities.Add(capability);
                        if (capability.StartsWith("symref=HEAD:", StringComparison.Ordinal))
                        {
                            head = capability["symref=HEAD:".Length..];
                        }
                    }

                    line = line[..nul];
                }

                first = false;
            }

            var parts = line.Split(' ', 2);
            if (parts.Length == 2 && GitHash.IsHash(parts[0]) && parts[1] != "capabilities^{}")
            {
                var name = parts[1];
                if (name.EndsWith("^{}", StringComparison.Ordinal))
                {
                    var tag = name[..^3];
                    if (refs.TryGetValue(tag, out var annotated))
                    {
                        refs[tag] = annotated with { Peeled = parts[0].ToLowerInvariant() };
                    }
                }
                else
                {
                    refs[name] = new RemoteRef(name, parts[0].ToLowerInvariant(), null);
                }
            }

            kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        }

        return new RemoteRefs(0, refs, head, capabilities);
    }

    private static async Task ReadFetchV2Async(PktLineReader reader, Stream pack, List<string> shallow, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        string? section = null;
        long received = 0;
        while (true)
        {
            var kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
            switch (kind)
            {
                case PktKind.End:
                    throw new EndOfStreamException("The server's answer ended before the pack.");
                case PktKind.Flush or PktKind.ResponseEnd:
                    return;
                case PktKind.Delimiter:
                    section = null;
                    continue;
            }

            if (section is null)
            {
                section = reader.Text;
                ThrowIfError(section);
                continue;
            }

            if (section == "shallow-info")
            {
                var line = reader.Text;
                if (line.StartsWith("shallow ", StringComparison.Ordinal))
                {
                    shallow.Add(line[8..]);
                }
            }
            else if (section == "packfile")
            {
                received += await WriteBandAsync(reader.Data, pack, cancellationToken).ConfigureAwait(false);
                progress?.Report(received);
            }
        }
    }

    private static async Task ReadFetchV0Async(PktLineReader reader, Stream raw, bool banded, Stream pack, List<string> shallow, bool deepened, IProgress<long>? progress, CancellationToken cancellationToken)
    {
        PktKind kind;
        if (deepened)
        {
            while ((kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) == PktKind.Data)
            {
                var line = reader.Text;
                ThrowIfError(line);
                if (line.StartsWith("shallow ", StringComparison.Ordinal))
                {
                    shallow.Add(line[8..]);
                }
            }
        }

        // One NAK or ACK, then the pack on band 1.
        while ((kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) == PktKind.Data)
        {
            var line = reader.Text;
            ThrowIfError(line);
            if (line == "NAK" || line.StartsWith("ACK ", StringComparison.Ordinal))
            {
                break;
            }
        }

        long received = 0;
        if (!banded)
        {
            // A server without side-band sends the pack itself after its answer; the reader has read nothing past it.
            var buffer = new byte[64 * 1024];
            int read;
            while ((read = await raw.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await pack.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                received += read;
                progress?.Report(received);
            }

            return;
        }

        while ((kind = await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) == PktKind.Data)
        {
            received += await WriteBandAsync(reader.Data, pack, cancellationToken).ConfigureAwait(false);
            progress?.Report(received);
        }

        if (kind == PktKind.End && received == 0)
        {
            throw new EndOfStreamException("The server's answer ended before the pack.");
        }
    }

    private static async Task<int> WriteBandAsync(byte[] data, Stream pack, CancellationToken cancellationToken)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        switch (data[0])
        {
            case 1:
                await pack.WriteAsync(data.AsMemory(1), cancellationToken).ConfigureAwait(false);
                return data.Length - 1;
            case 3:
                throw new GitTransportException("The Git server stopped: " + Encoding.UTF8.GetString(data.AsSpan(1)).Trim());
            default:
                return 0;
        }
    }

    private static void WriteCommand(Stream body, string command, IReadOnlySet<string> capabilities)
    {
        PktLine.WriteLine(body, "command=" + command);
        if (capabilities.Any(c => c.StartsWith("agent", StringComparison.Ordinal)))
        {
            PktLine.WriteLine(body, "agent=" + Agent);
        }

        if (capabilities.Contains("object-format=sha1"))
        {
            PktLine.WriteLine(body, "object-format=sha1");
        }

        PktLine.WriteDelimiter(body);
    }

    private static void AddCapability(HashSet<string> capabilities, string line)
    {
        capabilities.Add(line);
        var equals = line.IndexOf('=', StringComparison.Ordinal);
        if (equals > 0 && line[..equals] == "fetch")
        {
            foreach (var feature in line[(equals + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                capabilities.Add(feature);
            }
        }
    }

    private static void ThrowIfError(string line)
    {
        if (line.StartsWith("ERR ", StringComparison.Ordinal))
        {
            throw new GitTransportException("The Git server refused: " + line[4..].Trim());
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string path, byte[]? content, string? contentType = null)
    {
        var request = new HttpRequestMessage(method, new Uri(_repository, path));
        request.Headers.UserAgent.ParseAdd(Agent);
        request.Headers.Add("Pragma", "no-cache");
        if (_credentials is { } credentials)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.UserName}:{credentials.Password}")));
        }

        if (content is not null)
        {
            request.Content = new ByteArrayContent(content);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType!);
        }

        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new GitTransportException($"Could not reach {_repository.Host}: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new GitTransportException($"{_repository.Host} did not answer in time.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var status = response.StatusCode;
        response.Dispose();
        throw status switch
        {
            HttpStatusCode.Unauthorized => new GitTransportException(status, _credentials is null
                ? $"{_repository.Host} wants a sign-in for this repository."
                : $"{_repository.Host} did not accept the sign-in for this repository."),
            HttpStatusCode.Forbidden => new GitTransportException(status, $"{_repository.Host} does not let this account do that in this repository."),
            HttpStatusCode.NotFound => new GitTransportException(status, $"{_repository.Host} has no repository at {_repository.AbsolutePath.TrimEnd('/')}, or it is private."),
            _ => new GitTransportException(status, $"{_repository.Host} answered {(int)status} ({status})."),
        };
    }

    /// <summary>Turns a stalled read into a broken connection, so a download does not wait forever.</summary>
    private sealed class StallGuard(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stall.CancelAfter(StallTimeout);
            try
            {
                return await inner.ReadAsync(buffer, stall.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new IOException("The server stopped sending data.", ex);
            }
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
