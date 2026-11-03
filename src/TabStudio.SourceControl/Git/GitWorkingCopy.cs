using System.Text;
using TabStudio.SourceControl.GitHub;

namespace TabStudio.SourceControl.Git;

/// <summary>
/// A working folder with its own <c>.git</c>, fetched from and pushed to its remote over Git's HTTPS protocol. Every
/// step leaves what it got in the object database, so a download that breaks goes on from there next time.
/// </summary>
internal sealed class GitWorkingCopy
{
    /// <summary>How many file contents one request asks for.</summary>
    internal static int BlobBatch { get; set; } = 500;

    private readonly GitHttpClient _remote;
    private RemoteRefs? _refs;

    /// <summary>Creates the working copy.</summary>
    public GitWorkingCopy(string root, GitDirectory git, GitHttpClient remote, Uri remoteAddress, HttpClient http, GitCredentials? credentials)
    {
        Root = root;
        Git = git;
        _remote = remote;
        RemoteAddress = remoteAddress;
        Http = http;
        Credentials = credentials;
    }

    /// <summary>The working folder.</summary>
    public string Root { get; }

    /// <summary>Its Git directory.</summary>
    public GitDirectory Git { get; }

    /// <summary>The remote's HTTPS address.</summary>
    public Uri RemoteAddress { get; }

    /// <summary>The HTTP client, for Git LFS too.</summary>
    public HttpClient Http { get; }

    /// <summary>The sign-in for the remote, if any.</summary>
    public GitCredentials? Credentials { get; }

    /// <summary>The object database.</summary>
    public ObjectStore Objects => Git.Objects;

    /// <summary>Whether file contents are fetched on demand, as in a partial clone.</summary>
    public bool IsPartial =>
        string.Equals(Git.ConfigValue("remote \"origin\"", "promisor"), "true", StringComparison.OrdinalIgnoreCase)
        || Git.ConfigValue("extensions", "partialclone") is not null;

    /// <summary>Opens the working copy of a folder that holds <c>.git</c>.</summary>
    /// <param name="root">The working folder.</param>
    /// <param name="gitDirectory">Its Git directory.</param>
    /// <param name="http">The HTTP client.</param>
    /// <param name="remote">The remote's address, overriding the configuration's (for GitHub, its API's web host).</param>
    /// <param name="credentials">The sign-in, overriding one written into the address.</param>
    /// <exception cref="SyncException">The clone has no remote TabStudio can reach over HTTPS.</exception>
    public static GitWorkingCopy Open(string root, string gitDirectory, HttpClient http, Uri? remote = null, GitCredentials? credentials = null)
    {
        var git = GitDirectory.Open(gitDirectory);
        if (remote is null)
        {
            var configured = git.RemoteAddress();
            if (!GitRemoteAddress.TryParse(configured, out var address, out _, out var written))
            {
                throw new SyncException($"This clone's remote ('{configured ?? "none"}') is not an address TabStudio can reach over HTTPS.");
            }

            remote = address.Address;
            credentials ??= written;
        }

        return new GitWorkingCopy(root, git, new GitHttpClient(http, remote, credentials), remote, http, credentials);
    }

    /// <summary>Lists the remote's branches and tags, once per working copy unless asked again.</summary>
    public async Task<RemoteRefs> RefsAsync(bool refresh, CancellationToken cancellationToken)
    {
        if (_refs is null || refresh)
        {
            _refs = await Transport(() => _remote.ListRefsAsync(cancellationToken)).ConfigureAwait(false);
        }

        return _refs;
    }

    /// <summary>The commit a remote branch points at; <see langword="null"/> when the remote has no such branch.</summary>
    public async Task<string?> RemoteBranchAsync(string branch, bool refresh, CancellationToken cancellationToken) =>
        (await RefsAsync(refresh, cancellationToken).ConfigureAwait(false)).Refs.GetValueOrDefault("refs/heads/" + branch)?.Id;

    /// <summary>
    /// Fetches a commit and the trees under it, and records the edge of a shallow history. File contents are left for
    /// <see cref="EnsureBlobsAsync"/> where the server allows, so they come in batches that each stay once they arrive.
    /// </summary>
    /// <param name="commit">The commit.</param>
    /// <param name="haves">Commits already here, so the server sends only what is new.</param>
    /// <param name="depth">How much history to fetch for a new clone; 0 for what the haves leave out.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the download; what arrived whole is kept.</param>
    public async Task FetchCommitAsync(string commit, IReadOnlyList<string> haves, int depth, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var refs = await RefsAsync(false, cancellationToken).ConfigureAwait(false);
        var filter = IsPartial && refs.CanFilter;
        if (!Objects.Contains(commit))
        {
            await FetchPackAsync(new FetchRequest([commit], [.. haves.Where(Objects.Contains)], depth, filter), "Downloading the commit", progress, cancellationToken).ConfigureAwait(false);
        }

        for (var attempt = 0; attempt < 4; attempt++)
        {
            var missing = MissingTrees(commit);
            if (missing.Count == 0)
            {
                return;
            }

            // A download that broke kept the commit but not every folder: ask for those folders by name.
            var request = refs.CanWantAnyObject
                ? new FetchRequest(missing, [], 0, filter)
                : new FetchRequest([commit], [], depth, filter);
            await FetchPackAsync(request, "Downloading the folders", progress, cancellationToken).ConfigureAwait(false);
        }

        if (MissingTrees(commit).Count > 0)
        {
            throw new SyncException($"{RemoteAddress.Host} did not send every folder of commit {commit[..7]}.");
        }
    }

    /// <summary>Fetches the file contents the database lacks, in batches.</summary>
    /// <param name="ids">The blobs wanted.</param>
    /// <param name="progress">Receives progress.</param>
    /// <param name="cancellationToken">Cancels the download; batches that arrived are kept.</param>
    public async Task EnsureBlobsAsync(IEnumerable<string> ids, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var missing = ids.Distinct(StringComparer.Ordinal).Where(id => !Objects.Contains(id)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var refs = await RefsAsync(false, cancellationToken).ConfigureAwait(false);
        if (!refs.CanWantAnyObject)
        {
            throw new SyncException($"{RemoteAddress.Host} does not send files by name, and {missing.Count} are missing from this clone.");
        }

        var done = 0;
        foreach (var batch in missing.Chunk(BlobBatch))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var wanted = batch.Where(id => !Objects.Contains(id)).ToList();
            if (wanted.Count > 0)
            {
                progress?.Report(new SyncProgress($"Downloading {FileCount(missing.Count)}", done, missing.Count));
                await FetchPackAsync(new FetchRequest(wanted, [], 0, false), $"Downloading {FileCount(missing.Count)}", progress, cancellationToken).ConfigureAwait(false);
            }

            done += batch.Length;
            progress?.Report(new SyncProgress($"Downloading {FileCount(missing.Count)}", done, missing.Count));
        }

        var still = missing.Where(id => !Objects.Contains(id)).ToList();
        if (still.Count > 0)
        {
            throw new SyncException($"{RemoteAddress.Host} did not send {FileCount(still.Count)}, such as {still[0][..7]}.");
        }
    }

    /// <summary>Reads a commit.</summary>
    public CommitData ReadCommit(string id)
    {
        var value = Objects.Read(id);
        return value.Kind == GitObjectKind.Commit ? CommitCodec.Parse(value.Content) : throw new InvalidDataException($"{id} is not a commit.");
    }

    /// <summary>Every file, symbolic link and submodule of a commit, by path.</summary>
    public Dictionary<string, TreeItem> Entries(string commit) => TreeCodec.Flatten(Objects, ReadCommit(commit).Tree);

    /// <summary>The regular files among a commit's entries, as the sync metadata tracks them.</summary>
    public static Dictionary<string, TrackedFile> Files(Dictionary<string, TreeItem> entries) =>
        entries.Where(e => !e.Value.IsSubmodule && !e.Value.IsSymbolicLink)
            .ToDictionary(e => e.Key, e => new TrackedFile(e.Value.Id, e.Value.Mode), StringComparer.Ordinal);

    /// <summary>Reads a blob, fetching it when a partial clone does not have it yet.</summary>
    public async Task<byte[]> ReadBlobAsync(string id, CancellationToken cancellationToken)
    {
        if (Objects.TryRead(id) is { } value)
        {
            return value.Content;
        }

        await EnsureBlobsAsync([id], null, cancellationToken).ConfigureAwait(false);
        return Objects.Read(id).Content;
    }

    /// <summary>
    /// Writes files of a commit into the folder, as a checkout does: their contents are fetched first where missing, a
    /// large file is streamed, and Git LFS pointers are replaced by the files they stand for.
    /// </summary>
    /// <returns>What the person should know, such as Git LFS files that could not be downloaded.</returns>
    public async Task<IReadOnlyList<string>> WriteFilesAsync(IReadOnlyDictionary<string, TrackedFile> files, HashCache hashes, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        await EnsureBlobsAsync(files.Values.Select(f => f.Sha), progress, cancellationToken).ConfigureAwait(false);
        var pointers = new List<(string Path, LfsObject Object, string Sha)>();
        var written = 0;
        foreach (var (path, file) in files.OrderBy(f => f.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var full = WorkingTree.FullPath(Root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            if (File.Exists(full))
            {
                File.SetAttributes(full, FileAttributes.Normal);
            }

            await using (var stream = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                if (!Objects.TryCopyBlob(file.Sha, stream))
                {
                    throw new MissingObjectException(file.Sha);
                }
            }

            if (new FileInfo(full).Length is >= 100 and <= 1024
                && FileFetcher.LfsPointer.TryParse(await File.ReadAllBytesAsync(full, cancellationToken).ConfigureAwait(false)) is { } pointer)
            {
                pointers.Add((path, new LfsObject(pointer.Oid, pointer.Size), file.Sha));
            }

            hashes.Remember(Root, path, file.Sha);
            if (++written % 200 == 0)
            {
                hashes.Save(Root);
                progress?.Report(new SyncProgress("Writing files", written, files.Count));
            }
        }

        hashes.Save(Root);
        return await WriteLfsAsync(pointers, hashes, progress, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates the symbolic links of a commit that stay inside the folder.</summary>
    /// <returns>The links that were not created.</returns>
    public async Task<List<string>> WriteSymbolicLinksAsync(IEnumerable<TreeItem> links, CancellationToken cancellationToken)
    {
        var skipped = new List<string>();
        foreach (var link in links)
        {
            try
            {
                var target = Encoding.UTF8.GetString(await ReadBlobAsync(link.Id, cancellationToken).ConfigureAwait(false)).Trim();
                var full = WorkingTree.FullPath(Root, link.Name);
                var parent = Path.GetDirectoryName(full)!;
                var resolved = Path.GetFullPath(Path.Combine(parent, target.Replace('/', Path.DirectorySeparatorChar)));
                var root = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (Path.IsPathRooted(target) || !resolved.StartsWith(root, StringComparison.Ordinal))
                {
                    skipped.Add(link.Name);
                    continue;
                }

                Directory.CreateDirectory(parent);
                if (!File.Exists(full) && !Directory.Exists(full))
                {
                    File.CreateSymbolicLink(full, target);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException or SyncException or GitTransportException)
            {
                skipped.Add(link.Name);
            }
        }

        return skipped;
    }

    /// <summary>
    /// Creates a commit on top of <paramref name="parent"/> with changes to its entries, as <c>git commit</c> does after
    /// staging them. Files stored with Git LFS are uploaded first and committed as their pointers.
    /// </summary>
    /// <param name="parent">The parent commit.</param>
    /// <param name="changes">For each changed path, its mode and content's blob, or <see langword="null"/> to delete it.</param>
    /// <param name="message">The commit message.</param>
    /// <returns>The new commit.</returns>
    public string CreateCommit(string parent, IReadOnlyDictionary<string, (string Mode, string Id)?> changes, string message)
    {
        var entries = Entries(parent).ToDictionary(e => e.Key, e => (e.Value.Mode, e.Value.Id), StringComparer.Ordinal);
        foreach (var (path, change) in changes)
        {
            if (change is { } value)
            {
                entries[path] = value;
            }
            else
            {
                entries.Remove(path);
            }
        }

        var tree = TreeCodec.WriteTrees(Objects, entries);
        var person = GitIdentity.Committer(Git);
        return Objects.Write(GitObjectKind.Commit, CommitCodec.Write(new CommitData(tree, [parent], person, person, message)));
    }

    /// <summary>
    /// Pushes a branch from <paramref name="remoteOld"/> to <paramref name="commit"/>, sending the objects the remote
    /// lacks: those of the commits after <paramref name="remoteOld"/> that its tree does not hold.
    /// </summary>
    /// <returns>The server's reason when it refused the branch, such as <c>non-fast-forward</c>; <see langword="null"/> when it took it.</returns>
    public async Task<string?> PushAsync(string branch, string remoteOld, string commit, bool remoteHasCommit, CancellationToken cancellationToken)
    {
        var objects = remoteHasCommit ? new List<string>() : ObjectsToPush(commit, remoteOld);
        var name = "refs/heads/" + branch;
        var refused = await Transport(() => _remote.PushAsync([new RefUpdate(name, remoteOld, commit)], stream => PackWriter.Write(stream, Objects, objects), cancellationToken)).ConfigureAwait(false);
        _refs = null;
        return refused.GetValueOrDefault(name);
    }

    /// <summary>Moves a branch, and the remote's tracking branch, to a commit both now have.</summary>
    public void MoveBranch(string? branch, string commit, string message)
    {
        if (branch is null)
        {
            Git.SetHead(null, commit);
            return;
        }

        Git.WriteRef("refs/heads/" + branch, commit, message);
        Git.WriteRef("refs/remotes/origin/" + branch, commit, message);
        if (Git.CurrentBranch != branch)
        {
            Git.SetHead(branch, commit);
        }
    }

    /// <summary>
    /// Rewrites <c>.git/index</c> for a commit, so <c>git status</c> compares the folder with it as Git Changes does:
    /// a file whose content is the commit's gets its size and time, every other one is looked at again by Git.
    /// </summary>
    public void WriteIndex(string commit, HashCache hashes)
    {
        var entries = Entries(commit).ToDictionary(e => e.Key, e => (e.Value.Mode, e.Value.Id), StringComparer.Ordinal);
        IndexWriter.Write(Git.Path, Root, entries, path =>
        {
            var (mode, id) = entries[path];
            var full = Path.Combine(Root, path.Replace('/', Path.DirectorySeparatorChar));
            if (mode == TreeCodec.SymbolicLinkMode)
            {
                return new FileInfo(full).LinkTarget is { } target
                    && Objects.TryRead(id) is { } link
                    && string.Equals(Encoding.UTF8.GetString(link.Content), target, StringComparison.Ordinal);
            }

            return File.Exists(full) && string.Equals(hashes.GetHash(Root, path), id, StringComparison.Ordinal);
        });
        hashes.Save(Root);
    }

    /// <summary>Whether <paramref name="ancestor"/> is in the history of <paramref name="commit"/> that the database holds.</summary>
    public bool IsAncestor(string ancestor, string commit)
    {
        var shallow = Git.Shallow;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<string>();
        pending.Enqueue(commit);
        while (pending.Count > 0 && seen.Count < 200_000)
        {
            var current = pending.Dequeue();
            if (current == ancestor)
            {
                return true;
            }

            if (!seen.Add(current) || shallow.Contains(current) || Objects.TryRead(current) is not { Kind: GitObjectKind.Commit } value)
            {
                continue;
            }

            foreach (var parent in CommitCodec.Parse(value.Content).Parents)
            {
                pending.Enqueue(parent);
            }
        }

        return false;
    }

    /// <summary>How many commits <paramref name="commit"/> has that <paramref name="since"/> does not, as far as the database knows.</summary>
    public int CountSince(string commit, string since)
    {
        var shallow = Git.Shallow;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        pending.Push(commit);
        while (pending.Count > 0 && seen.Count < 100_000)
        {
            var current = pending.Pop();
            if (current == since || !seen.Add(current) || shallow.Contains(current) || Objects.TryRead(current) is not { Kind: GitObjectKind.Commit } value)
            {
                continue;
            }

            foreach (var parent in CommitCodec.Parse(value.Content).Parents)
            {
                pending.Push(parent);
            }
        }

        return seen.Count;
    }

    /// <summary>The latest commits from a commit, newest first, as far as the database holds the history.</summary>
    public List<GitCommit> History(string from, int count)
    {
        var shallow = Git.Shallow;
        var result = new List<GitCommit>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new PriorityQueue<string, long>();
        pending.Enqueue(from, 0);
        while (pending.Count > 0 && result.Count < count)
        {
            var id = pending.Dequeue();
            if (!seen.Add(id) || Objects.TryRead(id) is not { Kind: GitObjectKind.Commit } value)
            {
                continue;
            }

            var data = CommitCodec.Parse(value.Content);
            var author = data.Author is { } a ? new GitSignature(a.Name, a.Email, a.When) : null;
            result.Add(new GitCommit(id, data.Tree, data.Message.TrimEnd('\n'), data.Parents, author));
            if (shallow.Contains(id))
            {
                continue;
            }

            foreach (var parent in data.Parents)
            {
                var when = Objects.TryRead(parent) is { Kind: GitObjectKind.Commit } p ? CommitCodec.Parse(p.Content).Committer?.When.ToUnixTimeSeconds() ?? 0 : 0;
                pending.Enqueue(parent, -when);
            }
        }

        return result;
    }

    /// <summary>
    /// Uploads the files stored with Git LFS among a commit's changes and returns the pointers to commit in their place,
    /// as Git LFS's clean filter does.
    /// </summary>
    /// <param name="paths">The changed files that <c>.gitattributes</c> sends to Git LFS.</param>
    /// <param name="cancellationToken">Cancels the uploads.</param>
    public async Task<Dictionary<string, byte[]>> UploadLfsAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var uploads = paths.Select(p => (Object: LfsObject.Of(WorkingTree.FullPath(Root, p)), Path: WorkingTree.FullPath(Root, p))).ToList();
        if (uploads.Count > 0)
        {
            await Transport(() => new LfsClient(Http, LfsClient.EndpointFor(RemoteAddress), Credentials).UploadAsync(uploads, cancellationToken)).ConfigureAwait(false);
        }

        return paths.Zip(uploads).ToDictionary(p => p.First, p => p.Second.Object.Pointer(), StringComparer.Ordinal);
    }

    /// <summary>Runs a transport step, describing a broken connection as one.</summary>
    internal async Task<T> Transport<T>(Func<Task<T>> step)
    {
        try
        {
            return await step().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidDataException)
        {
            throw new GitTransportException($"The connection to {RemoteAddress.Host} broke: {ex.Message}", ex);
        }
    }

    private async Task Transport(Func<Task> step) => await Transport(async () =>
    {
        await step().ConfigureAwait(false);
        return true;
    }).ConfigureAwait(false);

    private async Task<IReadOnlyList<string>> WriteLfsAsync(List<(string Path, LfsObject Object, string Sha)> pointers, HashCache hashes, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        if (pointers.Count == 0)
        {
            return [];
        }

        var client = new LfsClient(Http, LfsClient.EndpointFor(RemoteAddress), Credentials);
        var failed = new List<string>();
        var done = 0;
        foreach (var (path, lfs, sha) in pointers)
        {
            progress?.Report(new SyncProgress($"Downloading {FileCount(pointers.Count)} stored with Git LFS", done++, pointers.Count));
            var full = WorkingTree.FullPath(Root, path);
            var temporary = Path.Combine(Root, SyncMetadata.FolderName, $"lfs-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.GetDirectoryName(temporary)!);
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write))
                {
                    await client.DownloadAsync(lfs, stream, cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporary, full, overwrite: true);
                hashes.Remember(Root, path, sha);
            }
            catch (Exception ex) when (ex is GitTransportException or IOException)
            {
                failed.Add($"{path} is stored with Git LFS and holds only its pointer: {ex.Message.TrimEnd('.')}.");
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }

        hashes.Save(Root);
        return failed.Count <= 5 ? failed : [.. failed.Take(4), $"{failed.Count - 4} more files stored with Git LFS hold only their pointers."];
    }

    private async Task FetchPackAsync(FetchRequest fetch, string stage, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var shallow = await FetchIntoAsync(_remote, Objects, fetch, IsPartial, stage, progress, cancellationToken).ConfigureAwait(false);
        Git.AddShallow(shallow);
    }

    /// <summary>
    /// Fetches a pack into an object database. A pack that arrives whole is indexed and kept as a pack; of one that
    /// breaks off, the objects that arrived whole are kept as loose objects, so the next attempt asks only for the rest.
    /// </summary>
    /// <returns>The commits the server marked as shallow.</returns>
    internal static async Task<IReadOnlyList<string>> FetchIntoAsync(GitHttpClient remote, ObjectStore objects, FetchRequest fetch, bool promisor, string stage, IProgress<SyncProgress>? progress, CancellationToken cancellationToken)
    {
        var temporary = Path.Combine(objects.Directory, "pack", $"tmp_pack_{Guid.NewGuid():N}");
        IReadOnlyList<string> shallow;
        var bytes = new Progress<long>(received => progress?.Report(new SyncProgress($"{stage} ({received / (1024 * 1024)} MB)")));
        try
        {
            await using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read))
            {
                try
                {
                    shallow = await remote.FetchAsync(fetch, file, bytes, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or HttpRequestException or InvalidDataException)
                {
                    throw new GitTransportException($"The connection broke during the download: {ex.Message}", ex);
                }
            }
        }
        catch (Exception ex) when (ex is GitTransportException or OperationCanceledException or IOException)
        {
            try
            {
                objects.Salvage(temporary, CancellationToken.None);
            }
            catch (Exception salvage) when (salvage is IOException or InvalidDataException or UnauthorizedAccessException)
            {
                // Nothing usable arrived.
            }

            throw;
        }

        if (new FileInfo(temporary).Length == 0)
        {
            File.Delete(temporary);
            return shallow;
        }

        objects.AddPack(temporary, promisor, cancellationToken);
        return shallow;
    }

    private List<string> MissingTrees(string commit)
    {
        var missing = new List<string>();
        var pending = new Stack<string>();
        pending.Push(ReadCommit(commit).Tree);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!seen.Add(id))
            {
                continue;
            }

            if (Objects.TryRead(id) is not { Kind: GitObjectKind.Tree } tree)
            {
                missing.Add(id);
                continue;
            }

            foreach (var item in TreeCodec.Parse(tree.Content).Where(i => i.IsTree))
            {
                pending.Push(item.Id);
            }
        }

        return missing;
    }

    private List<string> ObjectsToPush(string commit, string remoteOld)
    {
        var known = new HashSet<string>(StringComparer.Ordinal);
        if (remoteOld != GitHash.Zero && Objects.TryRead(remoteOld) is { Kind: GitObjectKind.Commit } old)
        {
            known.Add(remoteOld);
            Reach(CommitCodec.Parse(old.Content).Tree, known, null);
        }

        var result = new List<string>();
        var pending = new Stack<string>();
        pending.Push(commit);
        var shallow = Git.Shallow;
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (known.Contains(id) || Objects.TryRead(id) is not { Kind: GitObjectKind.Commit } value)
            {
                continue;
            }

            known.Add(id);
            result.Add(id);
            var data = CommitCodec.Parse(value.Content);
            Reach(data.Tree, known, result);
            if (shallow.Contains(id))
            {
                continue;
            }

            foreach (var parent in data.Parents)
            {
                pending.Push(parent);
            }
        }

        return result;
    }

    /// <summary>Adds a tree and what it holds to <paramref name="known"/>, and to <paramref name="found"/> what was not known.</summary>
    private void Reach(string treeId, HashSet<string> known, List<string>? found)
    {
        var pending = new Stack<string>();
        pending.Push(treeId);
        while (pending.Count > 0)
        {
            var id = pending.Pop();
            if (!known.Add(id))
            {
                continue;
            }

            if (Objects.TryRead(id) is not { Kind: GitObjectKind.Tree } tree)
            {
                continue;
            }

            found?.Add(id);

            foreach (var item in TreeCodec.Parse(tree.Content))
            {
                if (item.IsTree)
                {
                    pending.Push(item.Id);
                }
                else if (!item.IsSubmodule && known.Add(item.Id) && found is not null && Objects.Contains(item.Id))
                {
                    // A blob a partial clone never fetched is one the remote has; only what is here is sent.
                    found.Add(item.Id);
                }
            }
        }
    }

    private static string FileCount(int count) => count == 1 ? "1 file" : $"{count} files";
}
