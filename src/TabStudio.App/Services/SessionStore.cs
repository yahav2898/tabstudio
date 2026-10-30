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

/// <summary>
/// Saves and restores the IDE session, so TabStudio reopens exactly where the user left it even after
/// Android closed it in the background.
/// </summary>
public sealed class SessionStore
{
    private readonly string _file;
    private readonly Lock _gate = new();

    /// <summary>Creates the store over the session file in the app's state folder.</summary>
    /// <param name="paths">Where the app keeps its state.</param>
    public SessionStore(AppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _file = paths.SessionFile;
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

                return state is not null && (File.Exists(state.WorkspacePath) || Directory.Exists(state.WorkspacePath)) ? state : null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    /// <summary>Saves a session, replacing the previous one atomically.</summary>
    /// <param name="state">The session.</param>
    public void Save(SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        lock (_gate)
        {
            try
            {
                var temporary = _file + ".tmp";
                using (var stream = File.Create(temporary))
                {
                    JsonSerializer.Serialize(stream, state, StateJsonContext.Default.SessionState);
                }

                File.Move(temporary, _file, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Saving the session is best effort; the files themselves are saved separately.
            }
        }
    }

    /// <summary>Forgets the session, after the user closed the workspace on purpose.</summary>
    public void Clear()
    {
        lock (_gate)
        {
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
