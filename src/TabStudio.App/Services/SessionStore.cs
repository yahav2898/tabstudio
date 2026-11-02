using System.Text.Json;

namespace TabStudio.App.Services;

/// <summary>An open document in a saved session.</summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="CaretOffset">Where the caret was.</param>
/// <param name="UnsavedText">The editor's text when it had unsaved changes, otherwise <see langword="null"/>.</param>
public sealed record SessionDocument(string Path, int CaretOffset, string? UnsavedText);

/// <summary>A breakpoint in a saved session.</summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="Line">One-based line.</param>
/// <param name="Condition">When it stops, such as <c>count == 3</c>, or <see langword="null"/> to stop every time.</param>
public sealed record SessionBreakpoint(string Path, int Line, string? Condition)
{
    /// <summary>After how many hits it stops, for debug adapters, such as <c>5</c>; <see langword="null"/> for every hit.</summary>
    public string? HitCount { get; init; }

    /// <summary>The message it logs instead of stopping, for debug adapters; <see langword="null"/> to stop.</summary>
    public string? LogMessage { get; init; }
}

/// <summary>Everything needed to reopen the IDE where the user left it.</summary>
/// <param name="WorkspacePath">The solution, project or folder that was open.</param>
/// <param name="Documents">The open documents, in tab order.</param>
/// <param name="ActiveDocument">The path of the selected tab.</param>
/// <param name="StartupProject">The project file of the startup project.</param>
public sealed record SessionState(
    string WorkspacePath,
    IReadOnlyList<SessionDocument> Documents,
    string? ActiveDocument,
    string? StartupProject)
{
    /// <summary>The breakpoints of every file, or <see langword="null"/> in sessions saved before there were any.</summary>
    public IReadOnlyList<SessionBreakpoint>? Breakpoints { get; init; }

    /// <summary>The expressions in the Watch panel.</summary>
    public IReadOnlyList<string>? WatchExpressions { get; init; }

    /// <summary>Size of an opt-in C# recording ring, in MiB. Recording itself is never enabled on restore.</summary>
    public int RecordingBufferMegabytes { get; init; } = 64;

    /// <summary>Whether the Properties panel was open rather than folded into its header; <see langword="null"/> in older sessions.</summary>
    public bool? IsPropertiesExpanded { get; init; }
}

/// <summary>What became of a session save.</summary>
public enum SessionSaveResult
{
    /// <summary>The session is on disk, or a later one was saved already.</summary>
    Saved,

    /// <summary>The storage was full: the space TabStudio keeps aside for this was given up to save it.</summary>
    SavedFromReserve,

    /// <summary>It could not be written; the session saved before it is still there.</summary>
    Failed,
}

/// <summary>
/// Saves and restores the IDE session, so TabStudio reopens exactly where the user left it even after
/// Android closed it in the background.
/// </summary>
public sealed class SessionStore
{
    private readonly string _file;
    private readonly string _restoring;
    private readonly string _recovered;
    private readonly string _reserve;
    private readonly Lock _gate = new();
    private int _reserving;
    private long _sequence;
    private long _saved;

    /// <summary>Creates the store over the session file in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public SessionStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = paths.SessionFile;
        _restoring = paths.SessionFile + ".restoring";
        _recovered = Path.Combine(paths.State, "Recovered");
        _reserve = Path.Combine(paths.State, "session.reserve");
    }

    /// <summary>
    /// How much space is kept aside, in a file of its own, for the session to be saved into when the storage fills up:
    /// the session is where unsaved text lives until it is saved, and a full storage is exactly when files cannot be.
    /// </summary>
    internal int ReserveBytes { get; init; } = 8 * 1024 * 1024;

    /// <summary>Opens the file a session is written to; replaced by tests to fill the storage.</summary>
    internal Func<string, Stream> CreateFile { get; init; } = File.Create;

    /// <summary>Where unsaved text that has no file left to go back to is kept, one file per document.</summary>
    public string RecoveredFolder => _recovered;

    /// <summary>
    /// The unsaved documents that <see cref="Load"/> found in a session whose workspace is gone, written to
    /// <see cref="RecoveredFolder"/>; empty when there were none.
    /// </summary>
    public IReadOnlyList<string> LastRecovered { get; private set; } = [];

    /// <summary>
    /// The workspace whose restore began and never finished, because the app ended while reopening it (out of memory,
    /// a crash, or Android closing it); <see langword="null"/> when the last restore finished.
    /// </summary>
    /// <remarks>
    /// Reopening it again at startup could end the app again, every time it starts: the start screen offers it
    /// instead, and opening it from there still restores its session.
    /// </remarks>
    public string? InterruptedRestore
    {
        get
        {
            lock (_gate)
            {
                try
                {
                    return File.Exists(_restoring) ? File.ReadAllText(_restoring).Trim() : null;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    return null;
                }
            }
        }
    }

    /// <summary>Notes that a session is being restored, until <see cref="EndRestore"/>.</summary>
    /// <param name="workspacePath">The workspace being reopened.</param>
    public void BeginRestore(string workspacePath)
    {
        ArgumentNullException.ThrowIfNull(workspacePath);
        lock (_gate)
        {
            try
            {
                File.WriteAllText(_restoring, workspacePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Without the note a crash while reopening is retried at the next start, as before.
            }
        }
    }

    /// <summary>Notes that the restore begun by <see cref="BeginRestore"/> finished, or was given up on purpose.</summary>
    public void EndRestore()
    {
        lock (_gate)
        {
            try
            {
                File.Delete(_restoring);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A stale note only means the next start offers the workspace instead of reopening it.
            }
        }
    }

    /// <summary>
    /// Writes unsaved text whose file can no longer be opened to <see cref="RecoveredFolder"/>, so it outlives the
    /// session that held it.
    /// </summary>
    /// <param name="documents">The documents; those without unsaved text are skipped.</param>
    /// <returns>The files written.</returns>
    public IReadOnlyList<string> KeepUnsavedText(IEnumerable<SessionDocument> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        lock (_gate)
        {
            return KeepUnsavedTextCore(documents);
        }
    }

    private List<string> KeepUnsavedTextCore(IEnumerable<SessionDocument> documents)
    {
        var written = new List<string>();
        foreach (var document in documents)
        {
            if (document.UnsavedText is not { } text)
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(_recovered);
                var name = Path.GetFileName(document.Path);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                var target = Path.Combine(_recovered, $"{Path.GetFileNameWithoutExtension(name)}.{stamp}{Path.GetExtension(name)}");
                for (var i = 2; File.Exists(target); i++)
                {
                    target = Path.Combine(_recovered, $"{Path.GetFileNameWithoutExtension(name)}.{stamp}-{i}{Path.GetExtension(name)}");
                }

                File.WriteAllText(target, text);
                written.Add(target);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The session file still holds it until it is replaced.
            }
        }

        return written;
    }

    /// <summary>The last saved session, or <see langword="null"/> when there is none or its workspace is gone.</summary>
    public SessionState? Load()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_file))
                {
                    return null;
                }

                SessionState? state;
                try
                {
                    using var stream = File.OpenRead(_file);
                    state = JsonSerializer.Deserialize(stream, StateJsonContext.Default.SessionState);
                }
                catch (JsonException)
                {
                    // The only copy of unsaved text when Android ended the app: kept aside rather than overwritten by
                    // the next save, which lost it silently.
                    File.Move(_file, Path.Combine(Path.GetDirectoryName(_file)!, "session.unreadable-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".json"), overwrite: true);
                    return null;
                }

                if (state is null || string.IsNullOrEmpty(state.WorkspacePath))
                {
                    return null;
                }

                // Sessions written by older versions, or damaged by hand, may lack the list.
                state = state.Documents is null ? state with { Documents = [] } : state;
                if (File.Exists(state.WorkspacePath) || Directory.Exists(state.WorkspacePath))
                {
                    return state;
                }

                // The workspace was deleted, moved, or is on storage TabStudio may no longer read (a permission revoked,
                // a card removed). Its unsaved text was lost with the next save of another workspace's session.
                if (state.Documents.Any(d => d.UnsavedText is not null))
                {
                    LastRecovered = KeepUnsavedTextCore(state.Documents);
                    if (LastRecovered.Count == state.Documents.Count(d => d.UnsavedText is not null))
                    {
                        File.Delete(_file);
                    }
                }

                return null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>A number for a session about to be saved, larger than any taken before.</summary>
    public long NextSequence() => Interlocked.Increment(ref _sequence);

    /// <summary>Saves a session, replacing the previous one atomically.</summary>
    /// <param name="state">The session.</param>
    /// <returns>Whether it was written, and whether the storage was full.</returns>
    public SessionSaveResult Save(SessionState state) => Save(state, NextSequence());

    /// <summary>
    /// Saves a session taken as <paramref name="sequence"/>, unless a later one was saved already: a save written in
    /// the background may finish after the one written when the app went to the background.
    /// </summary>
    /// <param name="state">The session.</param>
    /// <param name="sequence">From <see cref="NextSequence"/>, taken when the state was.</param>
    /// <returns>Whether it was written, and whether the storage was full.</returns>
    public SessionSaveResult Save(SessionState state, long sequence)
    {
        ArgumentNullException.ThrowIfNull(state);
        SessionSaveResult result;
        lock (_gate)
        {
            if (sequence <= _saved)
            {
                return SessionSaveResult.Saved;
            }

            _saved = sequence;
            var error = TryWrite(state);
            if (error is null)
            {
                result = SessionSaveResult.Saved;
            }
            else if (StorageErrors.IsFull(error) && ReleaseReserve() && TryWrite(state) is null)
            {
                // The storage filled up: the newest unsaved text would have stayed only in memory, and gone with the
                // app when Android ended it. The space kept aside for it holds it instead.
                return SessionSaveResult.SavedFromReserve;
            }
            else
            {
                // The files themselves are saved separately; the previous session is still in place.
                return SessionSaveResult.Failed;
            }
        }

        EnsureReserveInBackground();
        return result;
    }

    private Exception? TryWrite(SessionState state)
    {
        var temporary = _file + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            using (var stream = CreateFile(temporary))
            {
                JsonSerializer.Serialize(stream, state, StateJsonContext.Default.SessionState);
            }

            File.Move(temporary, _file, overwrite: true);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                // A half-written copy only takes space a full storage does not have.
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
                // The save's own error is what counts.
            }

            return ex;
        }
    }

    private bool ReleaseReserve()
    {
        try
        {
            if (!File.Exists(_reserve))
            {
                return false;
            }

            File.Delete(_reserve);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Puts the space for a full storage aside again, once, when there is plenty to spare.</summary>
    private void EnsureReserveInBackground()
    {
        if (ReserveBytes <= 0 || File.Exists(_reserve) || Interlocked.Exchange(ref _reserving, 1) == 1)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            var temporary = _reserve + ".tmp";
            try
            {
                // Never the last of a nearly full storage: that is what the reserve is for.
                if (StorageErrors.AvailableBytes(_reserve) is { } free && free < 32L * ReserveBytes)
                {
                    return;
                }

                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Written, not only sized: a sparse file would hold no blocks to give back.
                    var block = new byte[64 * 1024];
                    for (var written = 0; written < ReserveBytes; written += block.Length)
                    {
                        stream.Write(block, 0, Math.Min(block.Length, ReserveBytes - written));
                    }

                    stream.Flush(flushToDisk: true);
                }

                File.Move(temporary, _reserve, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Without a reserve a full storage fails the save, as it did before there was one.
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                {
                    // Nothing more to give back.
                }
            }
            finally
            {
                Volatile.Write(ref _reserving, 0);
            }
        });
    }

    /// <summary>Forgets the session, after the user closed the workspace on purpose.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            // A save still on its way from the background must not bring the session back.
            _saved = NextSequence();
            try
            {
                File.Delete(_file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Nothing to do: a stale session is ignored once its workspace is gone.
            }
        }
    }
}
