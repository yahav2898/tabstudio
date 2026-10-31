using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using TabStudio.Toolchains.Downloads;
using TabStudio.Toolchains.Installation;
using TabStudio.Toolchains.Linux;

namespace TabStudio.Toolchains.Alpine;

/// <summary>
/// Installs, updates and removes Alpine packages in the Linux environment, downloading them itself so that
/// downloads show real progress, survive a dropped connection and are verified before anything is installed.
/// </summary>
/// <remarks>
/// <para>
/// The repositories' indexes are downloaded with their signatures checked against the keys in the root file
/// system (Alpine's own, which came with the pinned, hash-checked root file system), and kept, so planning works
/// offline from the last index. A plan's packages are downloaded into a local repository inside the root file
/// system, each checked against the signed index, and then <c>apk</c> installs them from there with
/// <c>--no-network</c>: it checks them again, runs their scripts and keeps its database, so <c>apk</c> in the
/// Terminal knows about them. Should <c>apk</c> want a package the plan did not foresee, it installs from the
/// network instead.
/// </para>
/// </remarks>
public sealed class AlpinePackageManager : IDisposable
{
    /// <summary>The local repository's folder inside the environment.</summary>
    public const string LocalRepository = "/var/cache/tabstudio/repo";

    private readonly AlpineEnvironment _environment;
    private readonly HttpClient _http;
    private readonly Downloader _downloader;
    private readonly IApkTool _apk;
    private readonly SemaphoreSlim _indexGate = new(1, 1);
    private ApkIndex? _index;

    /// <summary>Creates the package manager.</summary>
    /// <param name="environment">The Linux environment.</param>
    /// <param name="http">Sends index requests.</param>
    /// <param name="downloader">Downloads packages.</param>
    /// <param name="apk">Runs <c>apk</c>.</param>
    public AlpinePackageManager(AlpineEnvironment environment, HttpClient http, Downloader downloader, IApkTool apk)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(apk);
        _environment = environment;
        _http = http;
        _downloader = downloader;
        _apk = apk;
    }

    /// <summary>What is installed now.</summary>
    public ApkInstalledDatabase Installed => ApkInstalledDatabase.Read(_environment.Rootfs);

    /// <summary>The index loaded last, if any, without touching the network.</summary>
    public ApkIndex? CachedIndex => _index ??= TryReadCachedIndex();

    /// <summary>
    /// Loads the repositories' index: from the mirror when <paramref name="refresh"/> is set or none was
    /// downloaded yet, and otherwise, or when the device is offline, the last one downloaded.
    /// </summary>
    /// <param name="refresh">Whether to ask the mirror for a newer index.</param>
    /// <param name="cancellationToken">Stops the download.</param>
    /// <exception cref="ToolchainInstallException">No index could be read: offline with none kept, or a bad signature.</exception>
    public async Task<ApkIndex> LoadIndexAsync(bool refresh, CancellationToken cancellationToken)
    {
        await _indexGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!refresh && CachedIndex is { } cached)
            {
                return cached;
            }

            var keys = ApkSignature.LoadKeys(Path.Combine(_environment.Rootfs, "etc", "apk", "keys"));
            try
            {
                var packages = new List<ApkPackage>();
                foreach (var repository in _environment.Distribution.Repositories)
                {
                    var bytes = await FetchIndexAsync(repository, keys, cancellationToken).ConfigureAwait(false);
                    try
                    {
                        packages.AddRange(ApkSignature.ReadIndex(bytes, keys, repository));
                    }
                    catch (InvalidDataException ex)
                    {
                        File.Delete(IndexFile(repository));
                        throw new ToolchainInstallException(InstallFailure.IntegrityFailed, $"The {_environment.Distribution.Name} package index could not be verified: {ex.Message}", innerException: ex);
                    }
                }

                _index = new ApkIndex(packages);
                return _index;
            }
            finally
            {
                foreach (var key in keys.Values)
                {
                    key.Dispose();
                }
            }
        }
        finally
        {
            _indexGate.Release();
        }
    }

    /// <summary>Plans installing packages, with the newest index.</summary>
    /// <param name="names">The packages.</param>
    /// <param name="cancellationToken">Stops reading the index.</param>
    public Task<ApkPlan> PlanInstallAsync(IEnumerable<string> names, CancellationToken cancellationToken) =>
        PlanInstallAsync(names, refresh: true, cancellationToken);

    /// <summary>Plans an install using fresh metadata or the previously verified index of the same batch.</summary>
    /// <param name="names">The packages.</param>
    /// <param name="refresh">Whether to ask the mirror for a newer index first.</param>
    /// <param name="cancellationToken">Stops reading the index.</param>
    public async Task<ApkPlan> PlanInstallAsync(IEnumerable<string> names, bool refresh, CancellationToken cancellationToken) =>
        ApkResolver.PlanInstall(await LoadIndexAsync(refresh, cancellationToken).ConfigureAwait(false), Installed.Packages, names);

    /// <summary>Plans updating packages and what they depend on.</summary>
    /// <param name="names">The packages.</param>
    /// <param name="refresh">Whether to ask the mirror for a newer index first.</param>
    /// <param name="cancellationToken">Stops reading the index.</param>
    public async Task<ApkPlan> PlanUpgradeAsync(IEnumerable<string> names, bool refresh, CancellationToken cancellationToken) =>
        ApkResolver.PlanUpgrade(await LoadIndexAsync(refresh, cancellationToken).ConfigureAwait(false), Installed.Packages, names);

    /// <summary>Downloads a plan's packages into the local repository, checking each against the signed index.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="progress">Receives the bytes downloaded so far across all packages, and the package being downloaded.</param>
    /// <param name="cancellationToken">Stops downloading; what arrived is kept for the next attempt.</param>
    /// <exception cref="DownloadException">A package could not be downloaded or did not verify.</exception>
    public async Task DownloadAsync(ApkPlan plan, Action<long, ApkPackage>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var received = new long[plan.Packages.Count];
        var reporting = new Lock();
        // HTTP waits can overlap; apk still installs serially, after every package has passed verification.
        // Four streams bound memory and connections on a tablet while avoiding one round trip per package.
        await Parallel.ForEachAsync(Enumerable.Range(0, plan.Packages.Count), new ParallelOptions
        {
            MaxDegreeOfParallelism = 4,
            CancellationToken = cancellationToken,
        }, async (index, stop) =>
        {
            var package = plan.Packages[index];
            void Report(long bytes)
            {
                lock (reporting)
                {
                    received[index] = Math.Clamp(bytes, 0, package.Size);
                    progress?.Invoke(received.Sum(), package);
                }
            }

            Report(0);
            await _downloader.DownloadAsync(
                new DownloadRequest
                {
                    Address = new Uri(_environment.Distribution.RepositoryAddress(package.Repository ?? "main", _environment.Architecture!), package.FileName),
                    Destination = LocalPackagePath(package),
                    Name = $"{package.Name} {ApkVersion.Display(package.Version)}",
                    Size = package.Size,
                    Verify = path => ApkSignature.VerifyPackage(path, package),
                },
                new InlineProgress<DownloadProgress>(p => Report(p.Received)),
                stop).ConfigureAwait(false);
            Report(package.Size);
        }).ConfigureAwait(false);

        progress?.Invoke(plan.DownloadSize, plan.Packages.Count > 0 ? plan.Packages[^1] : new ApkPackage { Name = string.Empty, Version = string.Empty });
    }

    /// <summary>
    /// Installs packages with <c>apk</c> from the local repository, without the network; if <c>apk</c> needs a
    /// package that was not downloaded, it installs from the mirror instead. Empties the local repository after.
    /// </summary>
    /// <param name="names">The packages to install, or upgrade.</param>
    /// <param name="upgrade">Whether to upgrade them and what they need.</param>
    /// <param name="line">Receives each line <c>apk</c> prints.</param>
    /// <param name="cancellationToken">Stops <c>apk</c>.</param>
    /// <exception cref="ToolchainInstallException"><c>apk</c> failed.</exception>
    public async Task InstallAsync(IReadOnlyList<string> names, bool upgrade, Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        var repositories = new List<string>();
        foreach (var repository in _environment.Distribution.Repositories)
        {
            var folder = Path.Combine(_environment.Rootfs, LocalRepository.TrimStart('/'), repository, _environment.Architecture!);
            Directory.CreateDirectory(folder);
            if (File.Exists(IndexFile(repository)))
            {
                File.Copy(IndexFile(repository), Path.Combine(folder, "APKINDEX.tar.gz"), overwrite: true);
            }

            repositories.Add($"{LocalRepository}/{repository}");
        }

        var result = await _apk.AddAsync(names, repositories, upgrade, line, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded && result.NeedsNetwork)
        {
            line("Some packages were not downloaded yet; installing them from the mirror.");
            result = await _apk.AddOnlineAsync(names, upgrade, line, cancellationToken).ConfigureAwait(false);
        }

        if (!result.Succeeded)
        {
            throw new ToolchainInstallException(InstallFailure.CommandFailed, $"apk could not install {string.Join(", ", names)}: {ErrorOf(result)}", result.Output);
        }

        CleanLocalRepository();
    }

    /// <summary>Removes packages, and the dependencies nothing else needs.</summary>
    /// <param name="names">The packages.</param>
    /// <param name="line">Receives each line <c>apk</c> prints.</param>
    /// <param name="cancellationToken">Stops <c>apk</c>.</param>
    /// <exception cref="ToolchainInstallException"><c>apk</c> failed.</exception>
    public async Task RemoveAsync(IReadOnlyList<string> names, Action<string> line, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(names);
        if (names.Count == 0)
        {
            return;
        }

        var result = await _apk.DeleteAsync(names, line, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new ToolchainInstallException(InstallFailure.CommandFailed, $"apk could not remove {string.Join(", ", names)}: {ErrorOf(result)}", result.Output);
        }
    }

    /// <summary>Deletes downloaded packages, which <c>apk</c> has installed or which are no longer needed.</summary>
    public void CleanLocalRepository()
    {
        var folder = Path.Combine(_environment.Rootfs, LocalRepository.TrimStart('/'));
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _indexGate.Dispose();

    /// <summary>The bytes downloads take: packages waiting to be installed.</summary>
    public long DownloadsSize => DiskSpace.Size(Path.Combine(_environment.Rootfs, LocalRepository.TrimStart('/')));

    private string LocalPackagePath(ApkPackage package) =>
        Path.Combine(_environment.Rootfs, LocalRepository.TrimStart('/'), package.Repository ?? "main", _environment.Architecture!, package.FileName);

    private string IndexFile(string repository) => Path.Combine(_environment.Downloads, "index", repository, "APKINDEX.tar.gz");

    /// <summary>Downloads a repository's index, or keeps the last one when the mirror says it has not changed or cannot be reached.</summary>
    private async Task<byte[]> FetchIndexAsync(string repository, IReadOnlyDictionary<string, System.Security.Cryptography.RSA> keys, CancellationToken cancellationToken)
    {
        var file = IndexFile(repository);
        var tag = file + ".etag";
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(_environment.Distribution.RepositoryAddress(repository, _environment.Architecture!), "APKINDEX.tar.gz"));
        if (File.Exists(file) && File.Exists(tag) && EntityTagHeaderValue.TryParse(await File.ReadAllTextAsync(tag, cancellationToken).ConfigureAwait(false), out var etag))
        {
            request.Headers.IfNoneMatch.Add(etag);
        }

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotModified && File.Exists(file))
            {
                return await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            }

            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync(timeout.Token).ConfigureAwait(false);

            // Verified before it replaces the last good index: a Wi-Fi sign-in page answers 200 with HTML, which was
            // written over the index, failed its check and was deleted, leaving nothing to work offline with.
            try
            {
                _ = ApkSignature.ReadIndex(bytes, keys, repository);
            }
            catch (InvalidDataException ex)
            {
                if (File.Exists(file))
                {
                    return await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
                }

                throw new ToolchainInstallException(
                    InstallFailure.NoNetwork,
                    $"{request.RequestUri!.Host} did not send a {_environment.Distribution.Name} package index. If this network asks you to sign in, sign in with the browser and try again.",
                    innerException: ex);
            }

            var temporary = file + ".download";
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, file, overwrite: true);
            if (response.Headers.ETag is { } newTag)
            {
                await File.WriteAllTextAsync(tag, newTag.ToString(), cancellationToken).ConfigureAwait(false);
            }

            return bytes;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            if (File.Exists(file))
            {
                // Offline: the last index still describes what the mirror had then.
                return await File.ReadAllBytesAsync(file, cancellationToken).ConfigureAwait(false);
            }

            throw new ToolchainInstallException(
                InstallFailure.NoNetwork,
                $"Could not reach {request.RequestUri!.Host} to read the {_environment.Distribution.Name} package index. Connect to the internet and try again.",
                innerException: ex);
        }
    }

    /// <summary>The index from the files kept by the last download, without the network, or <see langword="null"/>.</summary>
    private ApkIndex? TryReadCachedIndex()
    {
        if (!_environment.Distribution.Repositories.All(r => File.Exists(IndexFile(r))))
        {
            return null;
        }

        var keys = ApkSignature.LoadKeys(Path.Combine(_environment.Rootfs, "etc", "apk", "keys"));
        try
        {
            return new ApkIndex(_environment.Distribution.Repositories.SelectMany(r => ApkSignature.ReadIndex(File.ReadAllBytes(IndexFile(r)), keys, r)).ToList());
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or CryptographicException)
        {
            return null;
        }
        finally
        {
            foreach (var key in keys.Values)
            {
                key.Dispose();
            }
        }
    }

    /// <summary>The lines of apk's output that say what went wrong.</summary>
    private static string ErrorOf(ApkResult result)
    {
        var errors = result.Output.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("ERROR:", StringComparison.Ordinal)).Select(l => l["ERROR:".Length..].Trim()).ToList();
        return errors.Count > 0 ? string.Join(" ", errors.Take(3)) : $"it exited with code {result.ExitCode}.";
    }
}
