using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TabStudio.Forms.Runtime;

namespace TabStudio.Forms.Hosting;

/// <summary>
/// Opens the device's storage picker for an app's file dialogs and hands the app paths it can use with File and
/// FileStream as on Windows. A picked file the device does not give a path for, such as one in a cloud folder, is
/// copied to a temporary file; a temporary file the app saves to is copied back to the picked file as it is written.
/// </summary>
internal sealed class StorageBridge : IDisposable
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = "text/plain",
        [".log"] = "text/plain",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".xml"] = "text/xml",
        [".html"] = "text/html",
        [".htm"] = "text/html",
        [".md"] = "text/markdown",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".bmp"] = "image/bmp",
        [".ico"] = "image/x-icon",
        [".pdf"] = "application/pdf",
        [".zip"] = "application/zip",
    };

    private readonly List<WrittenBack> _writtenBack = [];
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "TabStudio", "Forms", Guid.NewGuid().ToString("N"));
    private bool _disposed;

    /// <summary>Asks the user for files to open, a file to save or a folder. UI thread only.</summary>
    /// <param name="topLevel">The window whose storage picker opens.</param>
    /// <param name="request">What the app asks for.</param>
    /// <returns>Local paths, or <see langword="null"/> when the user cancelled.</returns>
    public async Task<string[]?> PickAsync(TopLevel topLevel, FileRequest request)
    {
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentNullException.ThrowIfNull(request);
        var provider = topLevel.StorageProvider;
        var start = request.InitialDirectory.Length > 0 && Directory.Exists(request.InitialDirectory)
            ? await provider.TryGetFolderFromPathAsync(request.InitialDirectory)
            : null;
        var title = request.Title.Length > 0 ? request.Title : request.Folder ? "Select Folder" : request.Save ? "Save As" : "Open";
        if (request.Folder)
        {
            var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, SuggestedStartLocation = start });
            return folders.Count > 0 && folders[0].TryGetLocalPath() is { } folder ? [folder] : null;
        }

        var types = FileTypes(request.Filter);
        var suggested = request.FilterIndex >= 1 && request.FilterIndex <= types.Count ? types[request.FilterIndex - 1] : types.FirstOrDefault();
        if (request.Save)
        {
            var name = Path.GetFileName(request.FileName);
            var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedStartLocation = start,
                SuggestedFileName = name.Length > 0 ? name : null,
                DefaultExtension = DefaultExtension(name, suggested),
                FileTypeChoices = types.Count > 0 ? types : null,
                SuggestedFileType = suggested,
                ShowOverwritePrompt = true,
            });
            return file is null ? null : [file.TryGetLocalPath() ?? WriteBack(file)];
        }

        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            SuggestedStartLocation = start,
            AllowMultiple = request.Multiselect,
            FileTypeFilter = types.Count > 0 ? types : null,
            SuggestedFileType = suggested,
        });

        if (files.Count == 0)
        {
            return null;
        }

        var paths = new string[files.Count];
        for (var i = 0; i < files.Count; i++)
        {
            paths[i] = files[i].TryGetLocalPath() ?? await CopyInAsync(files[i]);
        }

        return paths;
    }

    /// <summary>Copies back what the app wrote, and removes the temporary files.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var written in _writtenBack)
        {
            written.Dispose();
        }

        _writtenBack.Clear();
        try
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, recursive: true);
            }
        }
        catch (IOException)
        {
            // A file is still open; the system cleans its temporary folder.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>
    /// The file types of a Windows Forms filter: <c>Text files (*.txt)|*.txt|All files (*.*)|*.*</c> is two, the
    /// patterns of each separated by semicolons.
    /// </summary>
    /// <param name="filter">The filter.</param>
    internal static List<FilePickerFileType> FileTypes(string filter)
    {
        var types = new List<FilePickerFileType>();
        var parts = filter.Split('|');
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = parts[i + 1].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (patterns.Length == 0)
            {
                continue;
            }

            if (patterns.Any(p => p is "*.*" or "*"))
            {
                types.Add(new FilePickerFileType(parts[i]) { Patterns = ["*.*"], MimeTypes = ["*/*"], AppleUniformTypeIdentifiers = ["public.item"] });
                continue;
            }

            var mimeTypes = patterns
                .Select(p => MimeTypes.GetValueOrDefault(Path.GetExtension(p)))
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();
            types.Add(new FilePickerFileType(parts[i])
            {
                Patterns = patterns,
                MimeTypes = mimeTypes.Count == patterns.Length ? mimeTypes : null,
            });
        }

        return types;
    }

    private static string? DefaultExtension(string fileName, FilePickerFileType? type)
    {
        var extension = Path.GetExtension(fileName);
        if (extension.Length > 1)
        {
            return extension[1..];
        }

        return type?.Patterns is { Count: > 0 } patterns && Path.GetExtension(patterns[0]) is { Length: > 1 } fromPattern && !fromPattern.Contains('*', StringComparison.Ordinal)
            ? fromPattern[1..]
            : null;
    }

    /// <summary>Copies a picked file the device gives no path for to a temporary file the app can read.</summary>
    private async Task<string> CopyInAsync(IStorageFile file)
    {
        var path = TemporaryPath(file.Name);
        await using (var source = await file.OpenReadAsync())
        {
            await using var target = File.Create(path);
            await source.CopyToAsync(target);
        }

        return path;
    }

    /// <summary>A temporary file for the app to save to, copied back to the picked file as it is written.</summary>
    private string WriteBack(IStorageFile file)
    {
        var path = TemporaryPath(file.Name);
        _writtenBack.Add(new WrittenBack(file, path));
        return path;
    }

    private string TemporaryPath(string name)
    {
        var folder = Path.Combine(_folder, Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        var safe = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(folder, safe.Length > 0 ? safe : "file");
    }

    /// <summary>A temporary file watched for writes, each copied back to the file the user picked.</summary>
    private sealed class WrittenBack : IDisposable
    {
        private readonly IStorageFile _file;
        private readonly string _path;
        private readonly FileSystemWatcher _watcher;
        private readonly System.Threading.Timer _debounce;
        private readonly object _gate = new();
        private Task _copies = Task.CompletedTask;

        public WrittenBack(IStorageFile file, string path)
        {
            _file = file;
            _path = path;
            _debounce = new System.Threading.Timer(_ => QueueCopy(), null, Timeout.Infinite, Timeout.Infinite);
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(path)!, Path.GetFileName(path))
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
            };
            _watcher.Changed += (_, _) => Debounce();
            _watcher.Created += (_, _) => Debounce();
            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>Stops watching, and copies back the last of what the app wrote.</summary>
        public void Dispose()
        {
            _watcher.Dispose();
            _debounce.Dispose();
            QueueCopy();
        }

        /// <summary>Copies once the app has stopped writing for a moment.</summary>
        private void Debounce()
        {
            try
            {
                _debounce.Change(300, Timeout.Infinite);
            }
            catch (ObjectDisposedException)
            {
                // The run ended; the last copy is queued already.
            }
        }

        /// <summary>Queues a copy after the ones already running, so they never overlap.</summary>
        private void QueueCopy()
        {
            lock (_gate)
            {
                _copies = _copies.ContinueWith(_ => CopyBackAsync(), TaskScheduler.Default).Unwrap();
            }
        }

        private async Task CopyBackAsync()
        {
            if (!File.Exists(_path))
            {
                return;
            }

            try
            {
                var source = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                await using (source.ConfigureAwait(false))
                {
                    var target = await _file.OpenWriteAsync().ConfigureAwait(false);
                    await using (target.ConfigureAwait(false))
                    {
                        if (target.CanSeek)
                        {
                            target.SetLength(0);
                        }

                        await source.CopyToAsync(target).ConfigureAwait(false);
                    }
                }
            }
            catch (IOException)
            {
                // The app is still writing; its next change copies it.
            }
            catch (UnauthorizedAccessException)
            {
                // The picked file can no longer be written.
            }
        }
    }
}
