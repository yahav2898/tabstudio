using System.Collections.Concurrent;
using TabStudio.Live.Files;
using TabStudio.Live.Protocol;

namespace TabStudio.Live.Sessions;

/// <summary>
/// Files that change on the host's disk while the session runs, such as a new file or a save: guests' copies follow,
/// so their IntelliSense and their Solution Explorer see them too.
/// </summary>
public sealed partial class LiveHost
{
    private static readonly TimeSpan WatchDelay = TimeSpan.FromMilliseconds(500);

    private readonly ConcurrentDictionary<string, bool> _changedPaths = new(StringComparer.Ordinal);
    private FileSystemWatcher? _watcher;
    private int _watchScheduled;

    private void StartWatching()
    {
        try
        {
            var watcher = new FileSystemWatcher(WorkspaceRoot!)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            };
            watcher.Changed += OnFileEvent;
            watcher.Created += OnFileEvent;
            watcher.Deleted += OnFileEvent;
            watcher.Renamed += OnFileRenamed;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            // Without a watcher guests still get every shared document's edits and every save; new files wait for the next session.
            _watcher = null;
        }
    }

    private void StopWatching()
    {
        if (_watcher is { } watcher)
        {
            _watcher = null;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        Changed(e.OldFullPath);
        Changed(e.FullPath);
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => Changed(e.FullPath);

    private void Changed(string fullPath)
    {
        if (WorkspaceRoot is not { } root || LiveWorkspacePaths.ToRelativePath(root, fullPath) is not { } path)
        {
            return;
        }

        _changedPaths[path] = true;
        if (Interlocked.Exchange(ref _watchScheduled, 1) == 0)
        {
            _ = RefreshChangedLaterAsync();
        }
    }

    private async Task RefreshChangedLaterAsync()
    {
        try
        {
            await Task.Delay(WatchDelay, _stopping.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
        {
            return;
        }

        Interlocked.Exchange(ref _watchScheduled, 0);
        var paths = _changedPaths.Keys.ToList();
        foreach (var path in paths)
        {
            _changedPaths.TryRemove(path, out _);
        }

        // Hashing reads the files, off the session's thread.
        // A folder renamed, moved or unzipped into the workspace raises one event for itself, none for its files.
        var found = paths.Select(p => (Path: p, Entry: _files.Find(p), Folder: _files.FindFolder(p))).ToList();
        Dispatcher.Post(() => ApplyFileChanges(found));
    }

    private void ApplyFileChanges(List<(string Path, LiveFileEntry? Entry, IReadOnlyList<LiveFileEntry> Folder)> found)
    {
        var changed = new List<LiveFileEntry>();
        var removed = new List<string>();
        foreach (var (path, entry, folder) in found)
        {
            foreach (var file in entry is null ? folder : [entry])
            {
                if (!_shared.TryGetValue(file.Path, out var known) || known != file)
                {
                    _shared[file.Path] = file;
                    changed.Add(file);
                }
            }

            if (entry is not null)
            {
                continue;
            }

            // A file or a whole folder went away.
            var prefix = path + "/";
            foreach (var gone in _shared.Keys.Where(k => k == path || k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            {
                if (_files.Find(gone) is null)
                {
                    _shared.Remove(gone);
                    removed.Add(gone);
                }
            }
        }

        if (changed.Count > 0 || removed.Count > 0)
        {
            Broadcast(LiveMethods.FilesChanged, LiveJson.Body(new LiveFilesNotice(changed, removed), LiveJsonContext.Default.LiveFilesNotice));
        }
    }
}
