using Avalonia.Platform.Storage;
using TabStudio.Core.Workspaces;

namespace TabStudio.App.Services;

/// <summary>
/// Brings a folder the user picked from the device's storage into TabStudio.
/// </summary>
/// <remarks>
/// On the desktop a picked folder has a real path and is opened in place. On Android, folders come from the
/// system file picker as content URIs that compilers cannot read directly, so the folder is copied into the
/// projects folder. Build output and tool folders (<c>bin</c>, <c>obj</c>, <c>node_modules</c>, ...) are skipped.
/// </remarks>
public sealed class FolderImporter
{
    /// <summary>Folders with more files than this are refused, to protect the tablet's storage.</summary>
    public const int MaximumFiles = 50_000;

    private readonly AppPaths _paths;

    /// <summary>Creates the importer.</summary>
    /// <param name="paths">Where copied folders are placed.</param>
    public FolderImporter(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _paths = paths;
    }

    /// <summary>Returns a local path for a picked folder, copying it into the projects folder when needed.</summary>
    /// <param name="folder">The folder the user picked.</param>
    /// <param name="progress">Receives the number of files copied.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <exception cref="ImportException">The folder is too large or cannot be read.</exception>
    public async Task<string> ImportAsync(IStorageFolder folder, IProgress<ImportProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folder);

        if (folder.TryGetLocalPath() is { } local && Directory.Exists(local))
        {
            return local;
        }

        var target = _paths.GetAvailableProjectDirectory(folder.Name);
        var copied = 0;
        try
        {
            await CopyAsync(folder, target, () =>
            {
                copied++;
                if (copied > MaximumFiles)
                {
                    throw new ImportException($"The folder has more than {MaximumFiles:N0} files, which is more than TabStudio imports.");
                }

                progress?.Report(new ImportProgress(copied, null, "Copying"));
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new ImportException($"Could not read the folder: {ex.Message}", ex);
        }

        return target;
    }

    private static async Task CopyAsync(IStorageFolder source, string target, Action fileCopied, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(target);
        await foreach (var item in source.GetItemsAsync().WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            switch (item)
            {
                case IStorageFolder child when !PathUtilities.IsIgnoredDirectory(child.Name):
                    await CopyAsync(child, Path.Combine(target, AppPaths.MakeSafeFileName(child.Name)), fileCopied, cancellationToken).ConfigureAwait(false);
                    break;

                case IStorageFile file:
                    await using (var input = await file.OpenReadAsync().ConfigureAwait(false))
                    await using (var output = File.Create(Path.Combine(target, AppPaths.MakeSafeFileName(file.Name))))
                    {
                        await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                    }

                    fileCopied();
                    break;
            }

            item.Dispose();
        }
    }
}
