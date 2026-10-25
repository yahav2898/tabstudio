using System.Text.Json;
using TabStudio.Extensions.Manifests;
using TabStudio.Extensions.Packages;

namespace TabStudio.Extensions.Installation;

/// <summary>
/// The extensions folder: installs .vsix packages into it, one folder per extension, keeps the list of installed
/// extensions with their versions and whether each is enabled, and uninstalls them.
/// </summary>
/// <remarks>
/// Installing extracts into a staging folder first and only then moves the extension into place, so a failed or
/// cancelled install leaves the previous version working. Leftovers of an install interrupted by Android stopping the
/// app are removed the next time the folder is loaded. Every method may be called from any thread.
/// </remarks>
public sealed class ExtensionStore : IDisposable
{
    private const string RecordsFileName = "extensions.json";
    private const string StagingPrefix = ".installing-";
    private const string RemovedPrefix = ".removing-";
    private const string DownloadsFolderName = ".downloads";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<InstalledRecord> _records = [];
    private IReadOnlyList<InstalledExtension> _installed = [];

    /// <summary>Creates the store over a folder, which is created if it does not exist. Call <see cref="Load"/> next.</summary>
    /// <param name="folder">The extensions folder.</param>
    public ExtensionStore(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        Folder = Path.GetFullPath(folder);
        Directory.CreateDirectory(Folder);
    }

    /// <summary>Raised after the installed extensions changed: installed, updated, uninstalled, enabled or disabled.</summary>
    public event EventHandler? Changed;

    /// <summary>The extensions folder.</summary>
    public string Folder { get; }

    /// <summary>The installed extensions, sorted by display name.</summary>
    public IReadOnlyList<InstalledExtension> Installed => Volatile.Read(ref _installed);

    /// <summary>Extensions that are recorded as installed but could not be read the last time the folder was loaded.</summary>
    public IReadOnlyList<string> Problems { get; private set; } = [];

    /// <summary>Reads a manifest from an extension folder: its <c>package.json</c> and, if present, <c>package.nls.json</c>.</summary>
    /// <param name="folder">The extension's folder.</param>
    /// <exception cref="ExtensionPackageException">The folder has no valid manifest.</exception>
    /// <exception cref="IOException">The manifest cannot be read.</exception>
    public static ExtensionManifest ReadManifest(string folder)
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        var packageJson = Path.Combine(folder, "package.json");
        if (!File.Exists(packageJson))
        {
            throw new ExtensionPackageException($"'{Path.GetFileName(folder)}' has no package.json.");
        }

        var nls = Path.Combine(folder, "package.nls.json");
        return ManifestReader.Read(File.ReadAllText(packageJson), File.Exists(nls) ? File.ReadAllText(nls) : null);
    }

    /// <summary>Finds an installed extension.</summary>
    /// <param name="id">Its identifier.</param>
    public InstalledExtension? Find(ExtensionIdentifier id) => Installed.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Reads the installed extensions from the folder and removes what interrupted installs left behind. A damaged
    /// list is rebuilt from the extension folders, which are kept, enabled.
    /// </summary>
    public void Load()
    {
        _gate.Wait();
        try
        {
            var records = ReadRecords() ?? RecoverRecords();
            var installed = new List<InstalledExtension>(records.Count);
            var problems = new List<string>();
            foreach (var record in records)
            {
                try
                {
                    var folder = Path.Combine(Folder, record.Folder);
                    installed.Add(new InstalledExtension(ReadManifest(folder), folder, record.Enabled, ToSource(record), record.InstalledAt));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExtensionPackageException)
                {
                    problems.Add($"{record.Id} {record.Version}: {ex.Message}");
                }
            }

            RemoveLeftovers(records);
            _records = records;
            Problems = problems;
            Publish(installed);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Installs or updates an extension from a .vsix file. An installed version of the same extension is replaced and
    /// keeps whether it was enabled.
    /// </summary>
    /// <param name="vsixPath">The package.</param>
    /// <param name="source">Where it came from, to show and to check for updates.</param>
    /// <param name="cancellationToken">Cancels the install, which then changes nothing.</param>
    /// <returns>The installed extension.</returns>
    /// <exception cref="ExtensionPackageException">The file is not a valid extension package.</exception>
    public async Task<InstalledExtension> InstallAsync(string vsixPath, ExtensionSource source, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(vsixPath);
        ArgumentNullException.ThrowIfNull(source);
        InstalledExtension installed;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            installed = await Task.Run(() => InstallCore(vsixPath, source, cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return installed;
    }

    /// <summary>Installs or updates an extension from a stream, such as a file the user picked.</summary>
    /// <param name="vsix">The package's bytes. It is read to the end; it does not need to be seekable.</param>
    /// <param name="source">Where it came from.</param>
    /// <param name="cancellationToken">Cancels the install, which then changes nothing.</param>
    /// <returns>The installed extension.</returns>
    /// <exception cref="ExtensionPackageException">The stream is not a valid extension package.</exception>
    public async Task<InstalledExtension> InstallAsync(Stream vsix, ExtensionSource source, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vsix);
        var file = CreateDownloadPath();
        try
        {
            await using (var copy = File.Create(file))
            {
                await vsix.CopyToAsync(copy, cancellationToken).ConfigureAwait(false);
            }

            return await InstallAsync(file, source, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            TryDeleteFile(file);
        }
    }

    /// <summary>A path in the store's download folder for a package to be installed; the caller deletes it.</summary>
    public string CreateDownloadPath()
    {
        var downloads = Path.Combine(Folder, DownloadsFolderName);
        Directory.CreateDirectory(downloads);
        return Path.Combine(downloads, Guid.NewGuid().ToString("N") + ".vsix");
    }

    /// <summary>Uninstalls an extension and deletes its folder.</summary>
    /// <param name="id">The extension.</param>
    /// <returns>Whether it was installed.</returns>
    public bool Uninstall(ExtensionIdentifier id)
    {
        _gate.Wait();
        try
        {
            var record = _records.FirstOrDefault(r => Matches(r, id));
            if (record is null)
            {
                return false;
            }

            var records = _records.Where(r => r != record).ToList();
            WriteRecords(records);
            _records = records;
            RemoveFolder(Path.Combine(Folder, record.Folder));
            Publish(Installed.Where(e => e.Id != id));
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Enables or disables an installed extension; a disabled extension contributes nothing.</summary>
    /// <param name="id">The extension.</param>
    /// <param name="enabled">Whether it is enabled.</param>
    /// <returns>The extension as it is now, or <see langword="null"/> when it is not installed.</returns>
    public InstalledExtension? SetEnabled(ExtensionIdentifier id, bool enabled)
    {
        InstalledExtension? updated;
        _gate.Wait();
        try
        {
            var index = _records.FindIndex(r => Matches(r, id));
            if (index < 0)
            {
                return null;
            }

            if (_records[index].Enabled == enabled)
            {
                return Find(id);
            }

            var records = _records.ToList();
            records[index] = records[index] with { Enabled = enabled };
            WriteRecords(records);
            _records = records;
            updated = Find(id) is { } current ? current with { IsEnabled = enabled } : null;
            Publish(Installed.Select(e => e.Id == id && updated is not null ? updated : e));
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return updated;
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private InstalledExtension InstallCore(string vsixPath, ExtensionSource source, CancellationToken cancellationToken)
    {
        using var package = VsixPackage.Open(vsixPath);
        var manifest = package.Manifest;
        var folderName = FolderNameFor(manifest);
        var target = Path.Combine(Folder, folderName);
        var staging = Path.Combine(Folder, StagingPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            package.ExtractTo(staging, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (Directory.Exists(target))
            {
                RemoveFolder(target);
            }

            Directory.Move(staging, target);
        }
        catch
        {
            RemoveFolder(staging);
            throw;
        }

        var previous = _records.FirstOrDefault(r => Matches(r, manifest.Id));
        var record = new InstalledRecord(
            manifest.Id.ToString(),
            manifest.Version,
            folderName,
            previous?.Enabled ?? true,
            source.Origin,
            source.Registry?.AbsoluteUri,
            source.FileName,
            source.Sha256,
            source.Verification,
            DateTimeOffset.UtcNow);
        var records = _records.Where(r => r != previous).Append(record).ToList();
        WriteRecords(records);
        _records = records;
        if (previous is not null && !string.Equals(previous.Folder, folderName, StringComparison.OrdinalIgnoreCase))
        {
            RemoveFolder(Path.Combine(Folder, previous.Folder));
        }

        var installed = new InstalledExtension(manifest, target, record.Enabled, ToSource(record), record.InstalledAt);
        Publish(Installed.Where(e => e.Id != manifest.Id).Append(installed));
        return installed;
    }

    private void Publish(IEnumerable<InstalledExtension> installed) =>
        Volatile.Write(ref _installed, [.. installed.OrderBy(e => e.Manifest.DisplayName, StringComparer.CurrentCultureIgnoreCase)]);

    private static bool Matches(InstalledRecord record, ExtensionIdentifier id) =>
        ExtensionIdentifier.TryParse(record.Id, out var recordId) && recordId == id;

    private static ExtensionSource ToSource(InstalledRecord record) => new(
        record.Origin,
        Uri.TryCreate(record.Registry, UriKind.Absolute, out var registry) ? registry : null,
        record.FileName,
        record.Sha256,
        record.Verification);

    /// <summary>The folder an extension version is extracted to, such as <c>vscode.perl-1.95.3</c>.</summary>
    private static string FolderNameFor(ExtensionManifest manifest)
    {
        var name = $"{manifest.Id}-{manifest.Version}".ToLowerInvariant();
        var invalid = Path.GetInvalidFileNameChars();
        return new string([.. name.Select(c => invalid.Contains(c) || c is '/' or '\\' or ':' ? '_' : c)]);
    }

    private string RecordsPath => Path.Combine(Folder, RecordsFileName);

    private List<InstalledRecord>? ReadRecords()
    {
        if (!File.Exists(RecordsPath))
        {
            return [];
        }

        try
        {
            using var stream = File.OpenRead(RecordsPath);
            return JsonSerializer.Deserialize(stream, ExtensionStoreJsonContext.Default.InstalledRecords)?.Extensions?.ToList();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Rebuilds the list of installed extensions from the extension folders, after the list was damaged.</summary>
    private List<InstalledRecord> RecoverRecords()
    {
        var records = new List<InstalledRecord>();
        foreach (var folder in Directory.EnumerateDirectories(Folder))
        {
            var name = Path.GetFileName(folder);
            if (name.StartsWith('.'))
            {
                continue;
            }

            try
            {
                var manifest = ReadManifest(folder);
                records.Add(new InstalledRecord(manifest.Id.ToString(), manifest.Version, name, true, ExtensionOrigin.File, null, null, null, PackageVerification.None, Directory.GetCreationTimeUtc(folder)));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ExtensionPackageException)
            {
                // Not an extension folder.
            }
        }

        // When an interrupted update left two versions, keep the newest folder.
        records = [.. records.GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(r => r.InstalledAt).First())];
        WriteRecords(records);
        return records;
    }

    private void WriteRecords(List<InstalledRecord> records)
    {
        var temporary = RecordsPath + ".tmp";
        using (var stream = File.Create(temporary))
        {
            JsonSerializer.Serialize(stream, new InstalledRecords(records), ExtensionStoreJsonContext.Default.InstalledRecords);
        }

        File.Move(temporary, RecordsPath, overwrite: true);
    }

    /// <summary>Removes staging folders, folders of removed versions, downloads, and extension folders no record names.</summary>
    private void RemoveLeftovers(List<InstalledRecord> records)
    {
        var kept = records.Select(r => r.Folder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var folder in Directory.EnumerateDirectories(Folder))
        {
            var name = Path.GetFileName(folder);
            if (name.StartsWith(StagingPrefix, StringComparison.Ordinal) || name.StartsWith(RemovedPrefix, StringComparison.Ordinal)
                || name == DownloadsFolderName || (!name.StartsWith('.') && !kept.Contains(name)))
            {
                RemoveFolder(folder);
            }
        }
    }

    /// <summary>Moves a folder out of the way at once, then deletes it; what cannot be deleted now goes on the next load.</summary>
    private void RemoveFolder(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return;
        }

        var removed = Path.Combine(Folder, RemovedPrefix + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.Move(folder, removed);
            Directory.Delete(removed, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A file is in use; the leftover is removed the next time the folder is loaded.
        }
    }

    private static void TryDeleteFile(string file)
    {
        try
        {
            File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The downloads folder is emptied on the next load.
        }
    }
}
